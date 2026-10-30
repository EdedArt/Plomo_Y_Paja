using UnityEngine;               // Tipos básicos de Unity

/// <summary>
/// Animación "de goma" (rubber-hose, caricatura de los años 30) hecha por código:
/// con UN solo dibujo por pose, el personaje rebota, respira, se aplasta y se estira.
///
/// DÓNDE VA: en el mismo "SpriteHolder" que BillboardSprite.
///   LoboGuardia (EnemyAI, EnemyHealth, ...)
///   └── SpriteHolder (BillboardSprite, RubberHoseAnimator)   ← su origen = los pies
///       └── Art (SpriteRenderer)
/// BillboardSprite solo toca la ROTACIÓN; este script solo toca la ESCALA y la POSICIÓN
/// local del SpriteHolder, así que no se pisan. Como el origen está en los pies,
/// toda deformación sale desde los pies: el lobo nunca se despega del piso al aplastarse.
///
/// LAS SEIS REACCIONES:
///   1. Rebote al caminar (más rápido y alto al perseguir)  ← EnemyAI.CurrentSpeed
///   2. Respiración en reposo                              ← cuando está quieto
///   3. Aplastamiento al recibir golpe                     ← EnemyHealth.OnDamaged
///   4. Estiramiento al ver a Wilbur                       ← EnemyAI.OnStateChanged → Alert
///   5. Temblor al rendirse                                ← EnemyAI.OnStateChanged → Surrender
///   6. Aplastarse como tortilla al morir                  ← EnemyAI.OnStateChanged → Dead
///
/// CONSERVAR EL VOLUMEN: cuando se estira a lo alto se adelgaza, y cuando se aplasta
/// se ensancha (ancho = 1 / alto). Es el truco clásico del "squash and stretch".
///
/// SIN EnemyAI / EnemyHealth (por ejemplo, un NPC decorativo): solo respira.
/// No necesita sprite: funciona igual con el placeholder.
/// </summary>
public class RubberHoseAnimator : MonoBehaviour
{
    // ===================== 1. Rebote al caminar =====================

    [Header("1. Rebote al caminar")]
    [Tooltip("Saltitos por segundo caminando (patrulla)")]
    [SerializeField] private float walkHopsPerSecond = 2.2f;
    [Tooltip("Saltitos por segundo corriendo (persecución)")]
    [SerializeField] private float runHopsPerSecond = 3.6f;
    [Tooltip("Altura del saltito caminando (m)")]
    [SerializeField] private float walkHopHeight = 0.06f;
    [Tooltip("Altura del saltito corriendo (m)")]
    [SerializeField] private float runHopHeight = 0.12f;
    [Tooltip("Cuánto se estira arriba y se aplasta al tocar el piso (0.1 = 10 %)")]
    [SerializeField, Range(0f, 0.4f)] private float hopStretch = 0.1f;

    // ===================== 2. Respiración =====================

    [Header("2. Respiración en reposo")]
    [Tooltip("Respiraciones por segundo")]
    [SerializeField] private float breathsPerSecond = 0.6f;
    [Tooltip("Cuánto crece al inhalar (0.03 = 3 %)")]
    [SerializeField, Range(0f, 0.15f)] private float breathAmount = 0.03f;

    // ===================== 3 y 4. Golpe y alerta (resorte) =====================

    [Header("3. Golpe y 4. Alerta (resorte de goma)")]
    [Tooltip("Cuánto se aplasta al recibir un golpe (0.3 = 30 % más bajo)")]
    [SerializeField, Range(0f, 0.6f)] private float hitSquash = 0.3f;
    [Tooltip("Cuánto se estira al ver a Wilbur (0.35 = 35 % más alto)")]
    [SerializeField, Range(0f, 0.8f)] private float alertStretch = 0.35f;
    [Tooltip("Rigidez del resorte: más alto = vuelve más rápido a su forma")]
    [SerializeField] private float springStiffness = 300f;
    [Tooltip("Freno del resorte: más bajo = tiembla más veces antes de quedarse quieto")]
    [SerializeField] private float springDamping = 12f;

    // ===================== 5. Rendición =====================

    [Header("5. Temblor al rendirse")]
    [Tooltip("Temblores por segundo")]
    [SerializeField] private float trembleFrequency = 18f;
    [Tooltip("Distancia del temblor a los lados (m)")]
    [SerializeField] private float trembleDistance = 0.025f;

    // ===================== 6. Muerte =====================

    [Header("6. Aplastarse al morir")]
    [Tooltip("Altura final al morir (0.15 = queda al 15 % de su altura)")]
    [SerializeField, Range(0.05f, 1f)] private float deathFlatHeight = 0.15f;
    [Tooltip("Segundos que tarda en quedar aplastado")]
    [SerializeField, Min(0.01f)] private float deathDuration = 0.25f;

    // ===================== Estado interno =====================

    private EnemyAI _ai;                     // Para la velocidad y los cambios de estado (opcional)
    private EnemyHealth _health;             // Para los golpes (opcional)
    private Vector3 _baseScale;              // Escala original del SpriteHolder
    private Vector3 _basePosition;           // Posición local original del SpriteHolder

    private float _phaseOffset;              // Desfase al azar: así no respiran todos a la vez
    private float _hopPhase;                 // Avance del ciclo de saltitos
    private float _moveBlend;                // 0 = quieto (respira), 1 = caminando (rebota); se mezcla suave
    private float _spring;                   // Deformación del resorte (+ estirado, - aplastado)
    private float _springVelocity;           // Velocidad del resorte
    private bool _surrendered;               // ¿Está temblando de miedo?
    private bool _dead;                      // ¿Está muerto?
    private float _deathTimer;               // Tiempo desde que murió

    // ===================== Ciclo de vida =====================

    private void Awake()
    {
        _ai = GetComponentInParent<EnemyAI>();                          // Busca en este objeto y hacia arriba (la raíz)
        _health = GetComponentInParent<EnemyHealth>();
        _baseScale = transform.localScale;                              // Recordar la forma original
        _basePosition = transform.localPosition;
        _phaseOffset = Random.value * 10f;                              // Número al azar entre 0 y 10
    }

    private void OnEnable()
    {
        if (_ai != null) _ai.OnStateChanged += HandleStateChanged;      // Escuchar cambios de estado
        if (_health != null) _health.OnDamaged += HandleDamaged;        // Escuchar golpes
    }

    private void OnDisable()
    {
        if (_ai != null) _ai.OnStateChanged -= HandleStateChanged;      // Dejar de escuchar (evita llamadas a un objeto apagado)
        if (_health != null) _health.OnDamaged -= HandleDamaged;
        transform.localScale = _baseScale;                              // Devolver la forma original
        transform.localPosition = _basePosition;
    }

    // ===================== Reacciones a eventos =====================

    /// <summary>3. Golpe: aplastarse de golpe; el resorte lo devuelve rebotando.</summary>
    private void HandleDamaged(float damage)
    {
        if (_dead) return;                                              // Ya aplastado por la muerte
        _spring = -hitSquash;                                           // Aplastado al instante (cómico)
        _springVelocity = 0f;
    }

    /// <summary>4, 5 y 6: reaccionar al cambio de estado de la IA.</summary>
    private void HandleStateChanged(EnemyAI.EnemyState previous, EnemyAI.EnemyState current)
    {
        switch (current)
        {
            case EnemyAI.EnemyState.Alert:                              // 4. "¡Ahí está!": estirón de sorpresa
                _spring = alertStretch;
                _springVelocity = 0f;
                break;
            case EnemyAI.EnemyState.Surrender:                          // 5. Empieza a temblar
                _surrendered = true;
                break;
            case EnemyAI.EnemyState.Dead:                               // 6. Empieza a aplastarse
                _dead = true;
                _deathTimer = 0f;
                break;
        }
    }

    // ===================== Cada frame =====================

    private void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);                 // Limitar el paso: un tirón de FPS no hace explotar el resorte

        if (_dead)                                                      // Muerto: solo la animación de aplastarse
        {
            UpdateDeath(dt);
            return;
        }

        // ¿Se mueve? Velocidad real del NavMeshAgent (0 si no hay EnemyAI)
        float speed = _ai != null ? _ai.CurrentSpeed : 0f;
        float maxSpeed = _ai != null ? Mathf.Max(0.01f, _ai.ChaseSpeed) : 1f; // Evitar dividir entre 0
        float run01 = Mathf.Clamp01(speed / maxSpeed);                  // 0 = quieto, 1 = a toda velocidad
        bool moving = speed > 0.1f && !_surrendered;                    // Rendido no rebota aunque derrape un poco

        // Mezcla suave entre "respirar" (0) y "rebotar" (1), en unos 0.2 s
        _moveBlend = Mathf.MoveTowards(_moveBlend, moving ? 1f : 0f, dt * 5f);

        // --- 1. Rebote ---
        float hopsPerSecond = Mathf.Lerp(walkHopsPerSecond, runHopsPerSecond, run01); // Más rápido al correr
        _hopPhase += dt * hopsPerSecond;                                // Avanzar el ciclo
        float hop = Mathf.Abs(Mathf.Sin(_hopPhase * Mathf.PI));         // 0 = tocando el piso, 1 = arriba del saltito
        float hopHeight = Mathf.Lerp(walkHopHeight, runHopHeight, run01) * hop * _moveBlend; // Altura de este frame
        float hopStretchNow = (hop - 0.5f) * 2f * hopStretch * _moveBlend; // Arriba estirado (+), abajo aplastado (-)

        // --- 2. Respiración (solo si está quieto) ---
        float breath = Mathf.Sin((Time.time + _phaseOffset) * breathsPerSecond * 2f * Mathf.PI) // Onda suave -1..1
                       * breathAmount * (1f - _moveBlend);

        // --- 3 y 4. Resorte (golpe y alerta) ---
        UpdateSpring(dt);

        // --- 5. Temblor a los lados (en la dirección "derecha" del dibujo, que siempre mira a la cámara) ---
        Vector3 tremble = Vector3.zero;
        if (_surrendered)
        {
            float shake = Mathf.Sin((Time.time + _phaseOffset) * trembleFrequency * 2f * Mathf.PI) * trembleDistance;
            tremble = transform.localRotation * Vector3.right * shake;  // Hacia los lados de la pantalla, no hacia la cámara
        }

        // --- Juntar todo ---
        float height = 1f + hopStretchNow + breath + _spring;           // Factor de alto (1 = normal)
        ApplyShape(height);                                             // Escala con volumen conservado
        transform.localPosition = _basePosition + Vector3.up * hopHeight + tremble;
    }

    /// <summary>
    /// Resorte amortiguado: empuja la deformación de vuelta a 0 y la frena.
    /// Fórmula de física: aceleración = -rigidez * posición - freno * velocidad.
    /// </summary>
    private void UpdateSpring(float dt)
    {
        float acceleration = -springStiffness * _spring - springDamping * _springVelocity;
        _springVelocity += acceleration * dt;                           // Primero la velocidad...
        _spring += _springVelocity * dt;                                // ...luego la posición (estable con pasos chicos)
    }

    /// <summary>6. Aplastarse hasta quedar como tortilla, con un pequeño rebote al final.</summary>
    private void UpdateDeath(float dt)
    {
        _deathTimer += dt;
        float t = Mathf.Clamp01(_deathTimer / deathDuration);           // 0 → 1 durante deathDuration
        float eased = EaseOutBack(t);                                   // Se pasa un poquito y vuelve: efecto goma
        ApplyShape(Mathf.LerpUnclamped(1f, deathFlatHeight, eased));    // De altura normal a aplastado
        transform.localPosition = _basePosition;                        // Pegado al piso, sin saltito ni temblor
    }

    /// <summary>Aplica el alto y calcula el ancho para conservar el volumen (ancho = 1 / alto).</summary>
    private void ApplyShape(float height)
    {
        height = Mathf.Max(0.05f, height);                              // Nunca 0 ni negativo (se daría vuelta)
        float width = 1f / height;                                      // Más bajo → más ancho, más alto → más flaco
        transform.localScale = new Vector3(_baseScale.x * width,        // Ancho de pantalla
                                           _baseScale.y * height,       // Alto
                                           _baseScale.z);               // Profundidad sin cambios (es un dibujo plano)
    }

    /// <summary>Curva que llega a 1 pasándose un poco antes (como un rebote de goma).</summary>
    private static float EaseOutBack(float t)
    {
        const float overshoot = 1.70158f;                               // Valor estándar de esta curva
        float u = t - 1f;
        return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
    }
}
