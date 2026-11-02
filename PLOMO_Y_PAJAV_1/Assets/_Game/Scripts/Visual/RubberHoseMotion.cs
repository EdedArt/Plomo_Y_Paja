using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Movimiento "de goma" estilo dibujo animado de los años 30. Funciona con sprites y con el placeholder.
/// DÓNDE VA: raíz del enemigo. Anima el hijo "Visual" (o el que se asigne en target).
///
/// - Reposo: sube y baja ±3 % de la altura a 1.5 Hz
/// - Caminando: estira y aplasta en cada paso
/// - Golpe: aplastón de 0.15 s (PlayHitSquash)
/// - Rendido: tiembla de lado a lado
/// La escala se reparte para que el volumen se mantenga (aplastar = ensanchar).
/// </summary>
public class RubberHoseMotion : MonoBehaviour
{
    [Tooltip("Transform que se deforma. Vacío = hijo 'Visual'")]
    [SerializeField] private Transform target;

    [Header("Reposo")]
    [SerializeField] private float idleAmplitude = 0.03f;
    [SerializeField] private float idleFrequency = 1.5f;

    [Header("Pasos")]
    [Tooltip("Pasos por metro recorrido")]
    [SerializeField] private float stepsPerMeter = 1.6f;
    [SerializeField] private float stepStretch = 0.08f;

    [Header("Golpe")]
    [SerializeField] private float hitSquash = 0.25f;
    [SerializeField] private float hitDuration = 0.15f;

    [Header("Rendición")]
    [SerializeField] private float wobbleAngle = 8f;
    [SerializeField] private float wobbleFrequency = 3f;

    private NavMeshAgent _agent;
    private EnemyHealth _health;
    private Vector3 _baseScale = Vector3.one;
    private Quaternion _baseRotation = Quaternion.identity;
    private Vector3 _lastPosition;
    private float _stepPhase;
    private float _idlePhase;
    private float _hitTimer = -1f;
    private bool _wobble;

    public Transform Target => target;

    private void Awake()
    {
        if (target == null) target = transform.Find("Visual");
        if (target == null) target = transform;

        _baseScale = target.localScale;
        _baseRotation = target.localRotation;
        TryGetComponent(out _agent);
        TryGetComponent(out _health);
        _lastPosition = transform.position;
        _idlePhase = Random.value * 10f; // que no se muevan todos a la vez
    }

    private void OnEnable()
    {
        if (_health != null) _health.OnSurrender += HandleSurrender;
    }

    private void OnDisable()
    {
        if (_health != null) _health.OnSurrender -= HandleSurrender;
        if (target != null)
        {
            target.localScale = _baseScale;
            target.localRotation = _baseRotation;
        }
    }

    private void HandleSurrender() => _wobble = true;

    /// <summary>Aplastón corto al recibir un golpe.</summary>
    public void PlayHitSquash() => _hitTimer = 0f;

    /// <summary>Activa o quita el temblor (rendición).</summary>
    public void SetWobble(bool on) => _wobble = on;

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // Velocidad horizontal: del agente si existe, si no del desplazamiento real
        Vector3 delta = transform.position - _lastPosition;
        _lastPosition = transform.position;
        delta.y = 0f;
        float speed = (_agent != null && _agent.enabled) ? new Vector3(_agent.velocity.x, 0f, _agent.velocity.z).magnitude
                                                         : delta.magnitude / dt;

        float stretch; // + = más alto y delgado, - = aplastado
        if (speed > 0.2f)
        {
            _stepPhase += speed * stepsPerMeter * dt * Mathf.PI;
            stretch = Mathf.Abs(Mathf.Sin(_stepPhase)) * stepStretch * 2f - stepStretch;
        }
        else
        {
            _idlePhase += dt * idleFrequency * Mathf.PI * 2f;
            stretch = Mathf.Sin(_idlePhase) * idleAmplitude;
        }

        if (_hitTimer >= 0f)
        {
            _hitTimer += dt;
            float t = Mathf.Clamp01(_hitTimer / hitDuration);
            stretch -= hitSquash * Mathf.Sin(t * Mathf.PI);
            if (t >= 1f) _hitTimer = -1f;
        }

        float y = 1f + stretch;
        float xz = 1f / Mathf.Sqrt(Mathf.Max(0.2f, y)); // conservar volumen
        target.localScale = new Vector3(_baseScale.x * xz, _baseScale.y * y, _baseScale.z * xz);

        target.localRotation = _wobble
            ? _baseRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * wobbleFrequency * Mathf.PI * 2f) * wobbleAngle)
            : _baseRotation;
    }
}
