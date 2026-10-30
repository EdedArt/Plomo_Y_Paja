using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Reacción caricaturesca de un enemigo a los disparos.
/// DÓNDE VA: raíz del enemigo, junto a EnemyHealth (lo añade "Crear Prefabs de Prueba").
///
/// GOLPE (una vez por disparo aunque entren los 8 perdigones):
///   empujón de 1.5 m/s durante 0.15 s, destello blanco 0.08 s, aplastón, chorro de sangre.
/// MUERTE:
///   hit-stop de 50 ms, sale volando hacia atrás girando, aplastón al caer, humo + estrellitas +
///   dientes + charco, y a los 2 s se encoge hasta desaparecer (EnemyHealth lo destruye después).
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
public class EnemyCombatFx : MonoBehaviour
{
    [Header("Golpe")]
    [SerializeField] private float knockbackSpeed = 1.5f;
    [SerializeField] private float knockbackDuration = 0.15f;
    [SerializeField] private float flashDuration = 0.08f;

    [Header("Muerte")]
    [SerializeField] private float hitStopSeconds = 0.05f;
    [SerializeField] private float launchBack = 4f;
    [SerializeField] private float launchUp = 5f;
    [SerializeField] private float launchSpin = 720f;
    [SerializeField] private float fadeOutAfter = 2f;
    [SerializeField] private float fadeOutDuration = 0.35f;

    [Tooltip("Altura del pecho (donde sale la sangre) y de la cabeza")]
    [SerializeField] private float chestHeight = 1.2f;
    [SerializeField] private float headHeight = 1.7f;

    private EnemyHealth _health;
    private NavMeshAgent _agent;
    private RubberHoseMotion _motion;
    private BillboardSprite _billboard;
    private Transform _visual;

    private Renderer[] _renderers;
    private Material[] _originalMaterials;
    private Coroutine _flashRoutine;
    private Coroutine _knockbackRoutine;

    private int _hitFrame = -1;
    private bool _hitPending;
    private bool _dying;

    private void Awake()
    {
        _health = GetComponent<EnemyHealth>();
        TryGetComponent(out _agent);
        TryGetComponent(out _motion);
        _billboard = GetComponentInChildren<BillboardSprite>(true);
        _visual = transform.Find("Visual");
        if (_visual == null) _visual = transform;
    }

    private void Start()
    {
        // En Start: BillboardSprite ya creó su renderer de accesorio en Awake
        _renderers = _visual.GetComponentsInChildren<Renderer>(true);
        _originalMaterials = new Material[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            _originalMaterials[i] = _renderers[i].sharedMaterial;
    }

    private void OnEnable()
    {
        if (_health.onDamageTaken != null) _health.onDamageTaken.AddListener(HandleDamage);
        _health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (_health.onDamageTaken != null) _health.onDamageTaken.RemoveListener(HandleDamage);
        _health.OnDeath -= HandleDeath;
        RestoreMaterials();
    }

    private void HandleDamage()
    {
        // Los 8 perdigones llegan en el mismo frame: se reacciona una sola vez en LateUpdate
        if (_hitFrame == Time.frameCount) return;
        _hitFrame = Time.frameCount;
        _hitPending = true;
    }

    private void LateUpdate()
    {
        if (!_hitPending) return;
        _hitPending = false;

        Vector3 away = AwayFromCamera();
        Vector3 chest = transform.position + Vector3.up * chestHeight - away * 0.3f;
        CartoonFx.BloodBurst(chest, away);
        AudioLibrary.PlaySfx("impacto_carne", chest);
        AudioLibrary.PlaySfx("sangre_chorro", chest);

        if (_dying) return; // la muerte ya se encarga del resto

        Flash();
        if (_motion != null) _motion.PlayHitSquash();
        if (_billboard != null) _billboard.PlayHit();
        if (_knockbackRoutine != null) StopCoroutine(_knockbackRoutine);
        _knockbackRoutine = StartCoroutine(Knockback(away));
    }

    /// <summary>Dirección horizontal desde la cámara del jugador hacia el enemigo.</summary>
    private Vector3 AwayFromCamera()
    {
        Camera cam = Camera.main;
        Vector3 dir = cam != null ? transform.position - cam.transform.position : -transform.forward;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.001f ? dir.normalized : -transform.forward;
    }

    private IEnumerator Knockback(Vector3 direction)
    {
        float t = 0f;
        while (t < knockbackDuration)
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) break;
            _agent.Move(direction * knockbackSpeed * Time.deltaTime);
            t += Time.deltaTime;
            yield return null;
        }
        _knockbackRoutine = null;
    }

    // ============ Destello blanco ============

    private void Flash()
    {
        if (_renderers == null) return;
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        // Shaders de personaje (fase 3A2): _FlashAmount por renderer. Otros materiales: cambio de
        // material por uno blanco (truco de 3A), si CartoonFx los tiene.
        Material meshFlash = CartoonFx.Instance != null ? CartoonFx.Instance.FlashMeshMaterial : null;
        Material spriteFlash = CartoonFx.Instance != null ? CartoonFx.Instance.FlashSpriteMaterial : null;
        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            if (r == null) continue;
            if (CharacterLook.SupportsFlash(r))
            {
                CharacterLook.SetFlash(r, 1f);
                continue;
            }
            Material flash = r is SpriteRenderer ? spriteFlash : meshFlash;
            if (flash != null) r.sharedMaterial = flash;
        }

        yield return new WaitForSecondsRealtime(flashDuration);
        RestoreMaterials();
        _flashRoutine = null;
    }

    private void RestoreMaterials()
    {
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            if (r == null) continue;
            r.sharedMaterial = _originalMaterials[i];
            CharacterLook.SetFlash(r, 0f);
        }
    }

    // ============ Muerte ============

    private void HandleDeath()
    {
        if (_dying) return;
        _dying = true;
        CartoonFx.HitStop(hitStopSeconds);
        if (_knockbackRoutine != null) StopCoroutine(_knockbackRoutine);
        StartCoroutine(DeathRoutine(AwayFromCamera()));
    }

    private IEnumerator DeathRoutine(Vector3 away)
    {
        float start = Time.time;
        AudioLibrary.PlaySfx("caida_silbato", transform.position);

        // La deformación de RubberHoseMotion se apaga: desde aquí manda la muerte
        if (_motion != null) _motion.enabled = false;
        yield return null; // EnemyAI.Die ya apagó el NavMeshAgent

        // 1. Sale volando hacia atrás y arriba girando
        Vector3 axis = Vector3.Cross(Vector3.up, away);
        Quaternion baseRot = _visual.localRotation;
        Vector3 velocity = away * launchBack + Vector3.up * launchUp;
        float groundY = transform.position.y;
        float spun = 0f;
        float flight = 0f;
        while (flight < 2f)
        {
            float dt = Time.deltaTime;
            flight += dt;
            velocity.y += -18f * dt;
            transform.position += velocity * dt;
            spun += launchSpin * dt;
            _visual.rotation = Quaternion.AngleAxis(spun, axis) * transform.rotation * baseRot;
            if (transform.position.y <= groundY && velocity.y < 0f) break;
            yield return null;
        }
        Vector3 landed = transform.position;
        landed.y = groundY;
        transform.position = landed;

        // 2. Queda tumbado boca arriba y se aplasta contra el piso
        _visual.rotation = Quaternion.AngleAxis(-90f, axis) * transform.rotation * baseRot;
        AudioLibrary.PlaySfx("golpe_cartoon", landed);
        AudioLibrary.PlaySfx("humo_puf", landed);
        AudioLibrary.PlaySfx("estrellas", landed);
        Vector3 head = landed - away * (headHeight - 0.2f) + Vector3.up * 0.3f;
        CartoonFx.DeathBurst(head, landed);

        Vector3 baseScale = _visual.localScale;
        float squash = 0f;
        while (squash < 0.15f)
        {
            squash += Time.deltaTime;
            float s = Mathf.Sin(Mathf.Clamp01(squash / 0.15f) * Mathf.PI);
            _visual.localScale = new Vector3(baseScale.x * (1f + 0.35f * s), baseScale.y * (1f - 0.3f * s), baseScale.z * (1f + 0.35f * s));
            yield return null;
        }
        _visual.localScale = baseScale;

        // 3. A los 2 s se encoge y desaparece
        while (Time.time - start < fadeOutAfter) yield return null;
        float fade = 0f;
        while (fade < fadeOutDuration)
        {
            fade += Time.deltaTime;
            _visual.localScale = baseScale * (1f - Mathf.Clamp01(fade / fadeOutDuration));
            yield return null;
        }
        _visual.gameObject.SetActive(false);
    }
}
