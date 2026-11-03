using UnityEngine;

/// <summary>
/// Disparo enemigo que SALE DEL ARMA (fase HUD.2, H9/H11/H12). Reutilizable: guardias (EnemyAI, H18), Centavo
/// (CentavoController) y, cuando exista, Don Lobo. DÓNDE VA: raíz del personaje (los menús lo agregan; si falta, el dueño
/// lo agrega en runtime con el punto de respaldo).
///
/// - MuzzlePoint(): punto del cañón del cuadro que se ve AHORA = punto normalizado del MuzzlePointSet (o el de respaldo)
///   → px del sprite − pivote → unidades del sprite → mundo con el transform del SpriteRenderer (incluye el giro del
///   billboard, la escala del objeto, p. ej. seatedScale / escala 1.15 de Centavo, y flipX).
/// - Fire(target, hitChance, damage, spreadDeg): si acierta (por probabilidad) apunta al pecho con un temblor mínimo; si
///   falla, a un punto 0.6–1.5 m al costado. El RAYO DE DAÑO sale del cañón: lo primero que toca se lleva el tiro (pared,
///   banco, otro personaje); al jugador solo le quita vida si el tiro "acertaba" y lo primero que toca es él →
///   PlayerHealth.TakeDamage(daño, cañón) (el HUD apunta hacia acá). Nunca se pega a sí mismo.
/// - FX en pool (CombatFx): fogonazo + luz, estela, polvo + agujero en el mundo. Sonido 3D en el cañón: sfx_disparo_guardia
///   o, si falta, escopeta_disparo más aguda (como Centavo desde la 3C.2).
/// - Zumbido (H12): tiro fallado cuyo rayo pasa a ≤ 1.5 m de la cámara → sfx_bala_zumbido (máx. uno cada 0.3 s).
/// - Sin basura por disparo: rayos con búfer fijo, efectos en pool.
/// - debug (apagado): gizmo del punto del cañón y del último rayo.
/// </summary>
public class EnemyGunfire : MonoBehaviour
{
    private const float WhizRadius = 1.5f;
    private const float WhizCooldown = 0.3f;
    private static float _nextWhiz;
    private static readonly RaycastHit[] Hits = new RaycastHit[12];

    [Header("Cañón (H10)")]
    [Tooltip("Puntos por cuadro (Data/MuzzlePoints). Vacío = solo el punto de respaldo de abajo")]
    [SerializeField] private MuzzlePointSet muzzlePoints;
    [Tooltip("Punto normalizado si no hay MuzzlePointSet (a la altura de la mano)")]
    [SerializeField] private Vector2 fallbackPoint = new Vector2(0.46f, 0.49f);
    [Tooltip("SpriteRenderer del dibujo del personaje. Vacío = el del BillboardSprite de los hijos")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Disparo")]
    [SerializeField, Min(1f)] private float maxDistance = 40f;
    [SerializeField] private string shotSfx = "sfx_disparo_guardia";
    [Tooltip("Si no está shotSfx: este disparo con pitch más agudo")]
    [SerializeField] private string fallbackShotSfx = "escopeta_disparo";
    [SerializeField, Range(0.5f, 3f)] private float fallbackPitch = 1.5f;
    [SerializeField, Range(0f, 1f)] private float shotVolume = 0.6f;
    [SerializeField] private float flashSize = 0.45f;

    [Header("Dibujos de efectos (vacío = de código)")]
    [SerializeField] private Sprite flashSprite;
    [SerializeField] private Sprite impactSprite;
    [SerializeField] private Sprite impactSprite2;
    [SerializeField] private Sprite bulletHoleSprite;

    [Header("Depuración (apagada por defecto)")]
    [SerializeField] private bool debug = false;

    private Transform _self;
    private Vector3 _lastFrom, _lastTo;
    private bool _shotSfxChecked;
    private bool _hasShotSfx;

    public MuzzlePointSet MuzzlePoints => muzzlePoints;

    private void Awake()
    {
        _self = transform;
        if (spriteRenderer == null)
        {
            BillboardSprite billboard = GetComponentInChildren<BillboardSprite>(true);
            if (billboard != null) spriteRenderer = billboard.MainRenderer;
        }
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        CombatFx.Configure(flashSprite, impactSprite, impactSprite2, bulletHoleSprite);
    }

    /// <summary>Para el que lo agrega en runtime (sin menú): el punto de respaldo de su dibujo.</summary>
    public void SetFallback(Vector2 point, SpriteRenderer renderer = null)
    {
        fallbackPoint = point;
        if (renderer != null) spriteRenderer = renderer;
    }

    /// <summary>Sonido propio del tirador (ej. Centavo: sin sfx dedicado, su "pop" = escopeta más aguda, 3C.2).</summary>
    public void ConfigureSound(string sfx, string fallbackSfx, float pitch, float volume)
    {
        shotSfx = sfx;
        fallbackShotSfx = fallbackSfx;
        fallbackPitch = pitch;
        shotVolume = volume;
        _shotSfxChecked = false;
    }

    /// <summary>Punto del cañón en el mundo según el cuadro que se ve ahora.</summary>
    public Vector3 MuzzlePoint()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null) return _self.position + Vector3.up * 1.3f;
        Sprite sprite = spriteRenderer.sprite;
        Vector2 n = muzzlePoints != null ? muzzlePoints.Get(sprite) : fallbackPoint;
        Rect rect = sprite.rect;
        Vector2 pivot = sprite.pivot;
        float px = n.x * rect.width, pivotX = pivot.x;
        if (spriteRenderer.flipX) { px = rect.width - px; pivotX = rect.width - pivotX; }
        float ppu = sprite.pixelsPerUnit;
        var local = new Vector3((px - pivotX) / ppu, (n.y * rect.height - pivot.y) / ppu, -0.02f); // un poco hacia la cámara
        return spriteRenderer.transform.TransformPoint(local);
    }

    /// <summary>¿Línea libre desde el cañón hasta el punto? (lo primero que toca es el blanco o nada)</summary>
    public bool ClearFromMuzzle(Transform target, Vector3 point)
    {
        Vector3 from = MuzzlePoint();
        Vector3 delta = point - from;
        float distance = delta.magnitude;
        if (distance < 0.01f) return true;
        return FirstHit(from, delta / distance, distance, out RaycastHit hit) == false || hit.transform.IsChildOf(target);
    }

    /// <summary>
    /// Dispara al blanco. Devuelve true si le quitó vida. hitChance 0..1; spreadDeg = temblor del tiro acertado.
    /// </summary>
    public bool Fire(Transform target, float hitChance, float damage, float spreadDeg)
    {
        Vector3 muzzle = MuzzlePoint();
        CombatFx.MuzzleFlash(muzzle, flashSize);
        PlayShot(muzzle);
        if (target == null) return false;

        Vector3 chest = ChestOf(target);
        bool aimedHit = Random.value < hitChance;
        Vector3 aim;
        if (aimedHit)
        {
            aim = chest + Random.insideUnitSphere * Mathf.Tan(spreadDeg * Mathf.Deg2Rad) * Vector3.Distance(muzzle, chest);
        }
        else
        {
            Vector3 toTarget = chest - muzzle;
            Vector3 side = Vector3.Cross(Vector3.up, toTarget).normalized;
            if (side.sqrMagnitude < 0.01f) side = Vector3.right;
            float sign = Random.value < 0.5f ? -1f : 1f;
            aim = chest + side * (sign * Random.Range(0.6f, 1.5f)) + Vector3.up * Random.Range(-0.4f, 0.5f);
        }

        Vector3 dir = (aim - muzzle).normalized;
        Vector3 end = muzzle + dir * maxDistance;
        bool damaged = false;
        if (FirstHit(muzzle, dir, maxDistance, out RaycastHit hit))
        {
            end = hit.point;
            bool isTarget = hit.transform.IsChildOf(target);
            if (isTarget)
            {
                if (aimedHit && target.TryGetComponent(out PlayerHealth health) && !health.IsDead)
                {
                    health.TakeDamage(damage, muzzle); // HUD.1: el indicador apunta hacia el cañón
                    damaged = true;
                }
            }
            else if (hit.collider.GetComponentInParent<EnemyHealth>() == null && hit.collider.GetComponentInParent<IDamageable>() == null)
            {
                CombatFx.Impact(hit.point, hit.normal); // pared, piso, banco: polvo y agujero
            }
        }
        CombatFx.Tracer(muzzle, end);
        if (!damaged) TryWhiz(muzzle, end);

        _lastFrom = muzzle;
        _lastTo = end;
        return damaged;
    }

    /// <summary>Primer collider sólido en el rayo que no sea del propio tirador (sin triggers, sin Ignore Raycast).</summary>
    private bool FirstHit(Vector3 from, Vector3 dir, float distance, out RaycastHit best)
    {
        best = default;
        int count = Physics.RaycastNonAlloc(from, dir, Hits, distance, ~(1 << 2), QueryTriggerInteraction.Ignore);
        float bestDistance = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = Hits[i];
            if (h.collider == null || h.transform.IsChildOf(_self)) continue;
            if (h.distance < bestDistance) { bestDistance = h.distance; best = h; found = true; }
        }
        return found;
    }

    public static Vector3 ChestOf(Transform target)
    {
        if (target.TryGetComponent(out CharacterController cc)) return cc.bounds.center + Vector3.up * (cc.height * 0.15f);
        if (target.TryGetComponent(out Collider col)) return col.bounds.center;
        return target.position + Vector3.up * 1.2f;
    }

    private void PlayShot(Vector3 muzzle)
    {
        if (!_shotSfxChecked)
        {
            _shotSfxChecked = true;
            _hasShotSfx = !string.IsNullOrEmpty(shotSfx) && AudioLibrary.GetSfx(shotSfx) != null; // avisa una vez si falta
        }
        if (_hasShotSfx)
        {
            AudioLibrary.PlaySfx(shotSfx, muzzle);
            return;
        }
        CombatFx.PlayAt(AudioLibrary.GetSfx(fallbackShotSfx), muzzle, fallbackPitch, shotVolume * AudioLibrary.SfxVolume);
    }

    /// <summary>H12: el rayo fallado pasó cerca de la oreja de Wilbur.</summary>
    private static void TryWhiz(Vector3 from, Vector3 to)
    {
        if (Time.time < _nextWhiz) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 p = cam.transform.position;
        Vector3 seg = to - from;
        float t = Mathf.Clamp01(Vector3.Dot(p - from, seg) / Mathf.Max(0.0001f, seg.sqrMagnitude));
        if ((from + seg * t - p).sqrMagnitude > WhizRadius * WhizRadius) return;
        _nextWhiz = Time.time + WhizCooldown;
        AudioLibrary.PlaySfx2D("sfx_bala_zumbido");
    }

    private void OnDrawGizmos()
    {
        if (!debug) return;
        if (_self == null) _self = transform;
        Gizmos.color = Color.yellow;
        Vector3 m = MuzzlePoint();
        Gizmos.DrawLine(m + Vector3.left * 0.08f, m + Vector3.right * 0.08f);
        Gizmos.DrawLine(m + Vector3.up * 0.08f, m + Vector3.down * 0.08f);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(_lastFrom, _lastTo);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _nextWhiz = 0f;
}
