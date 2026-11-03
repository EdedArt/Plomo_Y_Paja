using UnityEngine;

/// <summary>
/// Objeto del mapa que se usa al instante (fase HUD.3, H13): botiquín o caja de munición.
/// INTERACCIÓN: la misma de WeaponPickup y las pistas: acercarse y apretar E, con el aviso bajo la mira
/// (RubberHoseCrosshair.SetPrompt). No se usa en pausa, diálogo, interrogatorio ni cinemática (PlayerAim.GameplayBlocked).
/// Si Wilbur está lleno, el objeto NO se gasta y un aviso lo dice (HudWilbur.ShowToast).
/// A diferencia de WeaponPickup, pide línea de vista: el cuarto de Centavo tiene objetos y no se toman a través de la pared.
/// Con dos objetos al alcance, manda el más cercano (un solo aviso y una sola tecla por objeto).
/// SE VE: hijo "Visual" (SpriteRenderer + BillboardSprite, o el placeholder con etiqueta si falta el PNG), vaivén de ±3 cm en
/// 1.4 s, y cada 2–3 s un brillo: la lucecita cálida sube y el dibujo crece un poco. Sin colliders: no tapa ni frena nada.
/// Reiniciar tras morir recarga la escena (H17): objetos y drops vuelven solos.
/// </summary>
public abstract class Pickup : MonoBehaviour
{
    [Header("Interacción (H13)")]
    [SerializeField] private string interactPrompt = "[E] Tomar";
    [Tooltip("Distancia horizontal (m) desde el jugador")]
    [SerializeField] private float interactDistance = 2.2f;
    [Tooltip("Diferencia de altura máxima (m) entre el jugador y el objeto")]
    [SerializeField] private float interactHeight = 2.2f;

    [Header("Dibujo")]
    [Tooltip("Hijo con el dibujo (lo arma PickupBuilder)")]
    [SerializeField] private Transform visual;
    [Tooltip("Lado del lienzo del dibujo en el mundo (m)")]
    [SerializeField] private float visualSize = 0.5f;
    [Tooltip("Vaivén vertical (m, ±)")]
    [SerializeField] private float bobAmplitude = 0.03f;
    [Tooltip("Segundos por vaivén")]
    [SerializeField] private float bobPeriod = 1.4f;

    [Header("Brillo")]
    [Tooltip("Lucecita cálida (opcional)")]
    [SerializeField] private bool useLight = true;
    [SerializeField] private Color lightColor = new Color(1f, 0.85f, 0.55f);
    [SerializeField] private float lightRange = 1.6f;
    [SerializeField] private float lightBase = 0.25f;
    [SerializeField] private float lightPeak = 1.5f;
    [Tooltip("Lejos de esto (m) la luz se apaga")]
    [SerializeField] private float lightMaxDistance = 20f;
    [Tooltip("Segundos entre brillos (mínimo, máximo)")]
    [SerializeField] private Vector2 glintEvery = new Vector2(2f, 3f);
    [SerializeField] private float glintDuration = 0.3f;
    [Tooltip("Cuánto crece el dibujo en el brillo (0.08 = 8 %)")]
    [SerializeField] private float glintScale = 0.08f;

    private const float VoiceCooldown = 20f;                // guion §2: máx. una línea cada 20 s

    private static readonly RaycastHit[] GroundHits = new RaycastHit[16];
    private static Pickup _focused;                         // el objeto dueño del aviso bajo la mira
    private static float _focusedDistance;
    private static int _lastUseFrame = -1;                  // una tecla, un objeto
    private static float _nextVoiceTime;

    private Transform _player;
    private Camera _camera;
    private Light _light;
    private Vector3 _baseLocal;
    private Vector3 _baseScale = Vector3.one;
    private float _bobPhase;
    private float _glintStart = -1f;
    private float _nextGlint;
    private bool _used;

    /// <summary>Qué objeto es (para "Guardar posiciones de objetos").</summary>
    public abstract PickupKind Kind { get; }

    /// <summary>Lo aplica al jugador. false = está lleno y el objeto no se gasta. toast = el aviso que corresponda.</summary>
    protected abstract bool TryApply(GameObject player, out string toast);

    /// <summary>Sonido al usarlo.</summary>
    protected abstract string UseSfx { get; }

    /// <summary>Línea opcional de Wilbur al usarlo.</summary>
    protected abstract string UseVoice { get; }

    /// <summary>
    /// El punto del piso bajo position (ignora a guardias y al jugador, y los triggers). Si no hay piso, devuelve position.
    /// Lo usan PickupBuilder (al colocar) y GuardDropSpawner (a los pies del guardia).
    /// </summary>
    public static Vector3 SnapToGround(Vector3 position, float up = 1.2f, float down = 5f)
    {
        int count = Physics.RaycastNonAlloc(position + Vector3.up * up, Vector3.down, GroundHits, up + down,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = GroundHits[i];
            if (hit.distance >= best || IsActor(hit.collider)) continue;
            best = hit.distance;
            position.y = hit.point.y;
        }
        return position;
    }

    private static bool IsActor(Collider collider)
    {
        return collider.GetComponentInParent<EnemyHealth>() != null || collider.GetComponentInParent<PlayerHealth>() != null;
    }

    private void Awake()
    {
        _bobPhase = Random.value * 10f;
        _nextGlint = Time.time + Random.Range(glintEvery.x, glintEvery.y);
        if (visual == null) return;

        if (visual.TryGetComponent(out SpriteRenderer sprite) && sprite.sprite != null)
        {
            Vector3 size = sprite.sprite.bounds.size;
            float side = Mathf.Max(size.x, size.y);
            if (side > 0.0001f) visual.localScale = Vector3.one * (visualSize / side);
        }
        else
        {
            // Placeholder: el look plano con contorno de las pistas y del revólver
            foreach (MeshRenderer r in visual.GetComponentsInChildren<MeshRenderer>(true))
                if (!r.TryGetComponent(out TextMesh _)) CharacterLook.ApplyPlaceholder(r); // la etiqueta conserva su fuente
        }
        _baseLocal = visual.localPosition + Vector3.up * bobAmplitude; // en lo más bajo del vaivén toca el piso
        _baseScale = visual.localScale;

        if (!useLight) return;
        var lightGo = new GameObject("Brillo");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = Vector3.up * 0.3f;
        _light = lightGo.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = lightColor;
        _light.range = lightRange;
        _light.intensity = lightBase;
        _light.shadows = LightShadows.None;
    }

    private void OnDisable()
    {
        ReleaseFocus();
    }

    private void Update()
    {
        if (_used) return;
        FindPlayer();
        UpdateGlint();
        if (_player == null) return;

        Vector3 delta = transform.position - _player.position;
        float dy = Mathf.Abs(delta.y);
        delta.y = 0f;
        float horizontal = delta.magnitude;
        if (horizontal > interactDistance || dy > interactHeight || PlayerAim.GameplayBlocked() || !HasLineOfSight())
        {
            ReleaseFocus();
            return;
        }

        if (_focused != null && _focused != this && horizontal >= _focusedDistance) return; // otro está más cerca
        _focused = this;
        _focusedDistance = horizontal;
        if (RubberHoseCrosshair.Current != null) RubberHoseCrosshair.Current.SetPrompt(interactPrompt);
        if (Input.GetKeyDown(KeyCode.E) && _lastUseFrame != Time.frameCount) Use();
    }

    /// <summary>Después del billboard (como WeaponPickup): vaivén y aumento del brillo sobre el hijo.</summary>
    private void LateUpdate()
    {
        if (_used || visual == null) return;
        float bob = Mathf.Sin((Time.time / Mathf.Max(0.01f, bobPeriod) + _bobPhase) * Mathf.PI * 2f) * bobAmplitude;
        visual.localPosition = _baseLocal + Vector3.up * bob;
        visual.localScale = _baseScale * (1f + glintScale * GlintAmount());
    }

    private void UpdateGlint()
    {
        if (Time.time >= _nextGlint)
        {
            _glintStart = Time.time;
            _nextGlint = Time.time + Random.Range(glintEvery.x, glintEvery.y);
        }
        if (_light == null) return;
        bool near = _player != null && (transform.position - _player.position).sqrMagnitude <= lightMaxDistance * lightMaxDistance;
        if (_light.enabled != near) _light.enabled = near;
        if (near) _light.intensity = Mathf.Lerp(lightBase, lightPeak, GlintAmount());
    }

    /// <summary>0..1..0 durante el brillo.</summary>
    private float GlintAmount()
    {
        if (_glintStart < 0f) return 0f;
        float t = (Time.time - _glintStart) / Mathf.Max(0.01f, glintDuration);
        return t >= 1f ? 0f : Mathf.Sin(t * Mathf.PI);
    }

    private void FindPlayer()
    {
        if (_player != null) return;
        GameObject go = GameObject.FindWithTag("Player");
        if (go != null) _player = go.transform;
    }

    /// <summary>¿Nada entre el ojo y el objeto? Guardias y jugador no cuentan: el cuerpo de un guardia no tapa su drop.</summary>
    private bool HasLineOfSight()
    {
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return true;
        Vector3 target = transform.position + Vector3.up * 0.25f;
        if (!Physics.Linecast(_camera.transform.position, target, out RaycastHit hit,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return true;
        return IsActor(hit.collider);
    }

    private void Use()
    {
        _lastUseFrame = Time.frameCount;
        if (!TryApply(_player.gameObject, out string toast))
        {
            HudWilbur.ShowToast(toast); // lleno: no se gasta
            return;
        }

        _used = true;
        ReleaseFocus();
        AudioLibrary.PlaySfx2D(UseSfx);
        if (Time.time >= _nextVoiceTime && AudioLibrary.HasVoice(UseVoice) && AudioLibrary.PlayVoice2D(UseVoice))
            _nextVoiceTime = Time.time + VoiceCooldown;
        HudWilbur.ShowToast(toast);
        Destroy(gameObject);
    }

    private void ReleaseFocus()
    {
        if (_focused != this) return;
        _focused = null;
        if (RubberHoseCrosshair.Current != null) RubberHoseCrosshair.Current.SetPrompt(null);
    }
}
