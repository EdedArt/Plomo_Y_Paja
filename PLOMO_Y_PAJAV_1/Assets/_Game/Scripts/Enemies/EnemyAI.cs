using System;                    // Para Action (eventos de C#)
using System.Collections;        // Para IEnumerator (corrutinas: esperar segundos sin congelar el juego)
using UnityEngine;               // Tipos básicos de Unity
using UnityEngine.AI;            // NavMesh: navegación automática por el nivel

/// <summary>
/// IA de los secuaces de Don Lobo (lobos guardia).
/// DÓNDE VA: GameObject raíz del enemigo.
///
/// MÁQUINA DE ESTADOS (un estado a la vez; cada frame se ejecuta la lógica del actual):
///   Idle ──┐                      (solo Centinela: vuelve a su puesto y vigila hasta ver/oír al jugador)
///   Patrol ┴→ Alert → Chase ⇄ Attack
///                       │
///   (EnemyHealth, vida baja) → Surrender → el jugador puede interrogarlo (DialogueNPC)
///   (EnemyHealth, vida 0)    → Dead
///
/// JERARQUÍA DEL PREFAB:
///   LoboGuardia (EnemyAI + EnemyHealth + NavMeshAgent + CapsuleCollider)
///   └── SpriteHolder (BillboardSprite + RubberHoseAnimator)
///       └── Art (SpriteRenderer)
///
/// NAVMESH: en la escena, Window → AI → Navigation → Bake. Sin NavMesh horneado
/// el enemigo se queda quieto, pero no lanza errores.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]          // Unity agrega un NavMeshAgent si falta
public class EnemyAI : MonoBehaviour
{
    // ===================== Estados =====================

    // Todos los estados posibles. Idle y Dead son de apoyo (centinela y muerte).
    public enum EnemyState { Idle, Patrol, Alert, Chase, Attack, Surrender, Dead }

    [Header("Estado actual (solo lectura — para debug)")]
    [SerializeField] private EnemyState currentState = EnemyState.Patrol; // Visible en el Inspector para ver qué piensa

    // Dos comportamientos de inicio
    public enum EnemyType { Patrullero, Centinela }

    [Header("Tipo de enemigo")]
    [Tooltip("Patrullero: sigue una ruta. Centinela: estático hasta detectar.")]
    [SerializeField] private EnemyType enemyType = EnemyType.Patrullero; // Lo escriben los LevelBuilder: no renombrar

    // ===================== Configuración =====================

    [Header("Patrullaje")]
    [Tooltip("Puntos de patrulla. Crear GameObjects vacíos en la escena y asignarlos aquí.")]
    [SerializeField] private Transform[] patrolPoints;          // Ruta, en orden

    [Tooltip("Tiempo de espera en cada punto de patrulla")]
    [SerializeField] private float waitTimeAtPoint = 2f;        // Segundos quieto en cada punto

    [Header("Detección")]
    [Tooltip("Radio de visión — si el jugador entra a este rango, el enemigo alerta")]
    [SerializeField] private float sightRange = 12f;            // Metros que alcanza a ver

    [Tooltip("Ángulo de visión en grados (180 = ve en semicírculo frontal)")]
    [SerializeField] private float fieldOfView = 120f;          // Ancho del "cono" de visión

    [Tooltip("Radio de escucha — si el jugador corre aquí cerca, lo escucha")]
    [SerializeField] private float hearingRange = 6f;           // Metros a los que oye pasos corriendo

    [Header("Combate")]
    [Tooltip("Distancia a la que puede atacar al jugador")]
    [SerializeField] private float attackRange = 2f;            // Alcance del golpe

    [Tooltip("Daño por ataque")]
    [SerializeField] private float attackDamage = 15f;          // Vida que le quita a Wilbur por golpe

    [Tooltip("Segundos entre ataques")]
    [SerializeField] private float attackCooldown = 1.5f;       // Pausa mínima entre golpes
    // La rendición (canSurrender + umbral) se configura en EnemyHealth

    [Header("Velocidades")]
    [SerializeField] private float patrolSpeed = 2f;            // Caminando tranquilo
    [SerializeField] private float chaseSpeed = 5f;             // Corriendo tras el jugador

    [Header("Referencias")]
    [Tooltip("Transform del jugador — si no se asigna, se busca automáticamente con tag 'Player'")]
    [SerializeField] private Transform playerTransform;         // A quién persigue
    [SerializeField] private BillboardSprite billboardSprite;   // Dibujo del lobo (opcional: sin él solo no cambia de pose)

    // ===================== Estado interno =====================

    private NavMeshAgent _agent;                    // Mueve al enemigo por el NavMesh
    private EnemyHealth _health;                    // Su vida (para reaccionar a los golpes)
    private PlayerController _playerController;     // Para saber si el jugador corre (oído)
    private int _currentPatrolIndex = 0;            // Punto de patrulla al que va
    private bool _waitingAtPoint = false;           // true mientras espera en un punto
    private float _lastAttackTime = -999f;          // Momento del último golpe (muy atrás: el primero sale ya)
    private bool _playerInSightRange = false;       // ¿Lo vio este frame?
    private bool _playerInAttackRange = false;      // ¿Está al alcance del golpe?
    private bool _hasPatrolDestination = false;     // Ya se le dio el destino del punto actual
    private Vector3 _homePosition;                  // Puesto del centinela (donde empezó)
    private Quaternion _homeRotation;               // Hacia dónde mira el centinela en su puesto
    private bool _returningHome = false;            // true mientras el centinela camina de vuelta a su puesto

    // ===================== API pública =====================

    /// <summary>(estado anterior, estado nuevo). Lo usa RubberHoseAnimator para reaccionar.</summary>
    public event Action<EnemyState, EnemyState> OnStateChanged;

    public EnemyState CurrentState => currentState;                         // Estado actual
    public bool IsSurrendered => currentState == EnemyState.Surrender;      // ¿Está rendido?
    /// <summary>Velocidad real en m/s (0 si está quieto o sin NavMesh).</summary>
    public float CurrentSpeed => _agent != null && _agent.enabled ? _agent.velocity.magnitude : 0f;
    /// <summary>Velocidad de persecución configurada (para escalar el rebote al correr).</summary>
    public float ChaseSpeed => chaseSpeed;

    // ===================== Ciclo de vida =====================

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();                  // Siempre existe por el RequireComponent
        TryGetComponent(out _health);                           // Opcional: sin EnemyHealth solo no reacciona a golpes

        if (playerTransform == null)                            // No lo asignaron en el Inspector
        {
            GameObject player = GameObject.FindWithTag("Player"); // Buscar por tag
            if (player != null) playerTransform = player.transform;
        }
        if (playerTransform != null)                            // Guardar el PlayerController una vez (no en cada frame)
            _playerController = playerTransform.GetComponent<PlayerController>();
    }

    private void OnEnable()
    {
        if (_health != null) _health.OnDamaged += HandleDamaged;    // Escuchar los golpes
    }

    private void OnDisable()
    {
        if (_health != null) _health.OnDamaged -= HandleDamaged;    // Dejar de escuchar (evita llamadas a un objeto apagado)
    }

    /// <summary>Recibió un golpe: pose hit un instante y, si estaba tranquilo, alerta.</summary>
    private void HandleDamaged(float damage)
    {
        if (currentState == EnemyState.Dead) return;                // Muerto: se queda el dibujo de muerte
        if (billboardSprite != null) billboardSprite.PlayHit();

        // Le dispararon (aunque sea por la espalda): no puede seguir caminando tranquilo.
        // Si este mismo golpe también lo rinde, EnemyHealth llama TriggerSurrender justo
        // después y Surrender corta la alerta (StopAllCoroutines).
        if (currentState == EnemyState.Idle || currentState == EnemyState.Patrol)
            ChangeState(EnemyState.Alert);                          // Alert → Chase va directo hacia el jugador
    }

    private void Start()
    {
        _homePosition = transform.position;                     // El centinela volverá a este puesto...
        _homeRotation = transform.rotation;                     // ...y a mirar hacia aquí
        if (enemyType == EnemyType.Centinela)
        {
            ChangeState(EnemyState.Idle);                       // Centinela: empieza quieto
            SetAgentStopped(true);                              // Por si ya venía en Idle (ChangeState no repite)
        }
        else
        {
            ChangeState(EnemyState.Patrol);                     // Patrullero: empieza a patrullar
        }
    }

    private void Update()
    {
        // Rendido o muerto: no piensa, no se mueve, no ataca
        if (currentState == EnemyState.Dead || currentState == EnemyState.Surrender) return;

        CheckPlayerDetection();                                 // 1) ¿Ve u oye al jugador?

        switch (currentState)                                   // 2) Lógica del estado actual
        {
            case EnemyState.Idle:   UpdateIdle();   break;
            case EnemyState.Patrol: UpdatePatrol(); break;
            case EnemyState.Alert:  UpdateAlert();  break;
            case EnemyState.Chase:  UpdateChase();  break;
            case EnemyState.Attack: UpdateAttack(); break;
        }
        // El dibujo nunca se voltea (sombra pintada), así que no hace falta
        // avisarle al BillboardSprite hacia dónde camina.
    }

    // ===================== Detección =====================

    /// <summary>Detectar al jugador por visión y oído.</summary>
    private void CheckPlayerDetection()
    {
        _playerInSightRange = false;                            // Se recalcula cada frame
        _playerInAttackRange = false;
        if (playerTransform == null) return;                    // No hay jugador en la escena

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position); // Distancia en metros

        // --- Vista: dentro del radio, dentro del cono y sin paredes en medio ---
        if (distanceToPlayer <= sightRange)
        {
            Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized; // Hacia el jugador
            float angle = Vector3.Angle(transform.forward, directionToPlayer);                      // Cuánto se desvía de su frente

            if (angle < fieldOfView / 2f && HasLineOfSight())  // Mitad del cono a cada lado
            {
                _playerInSightRange = true;                     // ¡Lo vio!
                if (currentState == EnemyState.Idle || currentState == EnemyState.Patrol)
                    ChangeState(EnemyState.Alert);              // Solo se alerta si estaba tranquilo
            }
        }

        // --- Oído: el jugador corre cerca (aunque esté a su espalda) ---
        if (distanceToPlayer <= hearingRange && _playerController != null && _playerController.IsRunning)
        {
            if (currentState == EnemyState.Idle || currentState == EnemyState.Patrol)
                ChangeState(EnemyState.Alert);
        }

        _playerInAttackRange = distanceToPlayer <= attackRange; // ¿Al alcance del golpe?
    }

    /// <summary>
    /// ¿Hay pared entre los ojos del enemigo y el pecho del jugador?
    /// El collider del propio jugador no cuenta como obstáculo (si no, nunca lo vería).
    /// </summary>
    private bool HasLineOfSight()
    {
        Vector3 eye = transform.position + Vector3.up;                          // Ojos: 1 m sobre los pies
        Vector3 toTarget = (playerTransform.position + Vector3.up) - eye;       // Hacia el pecho del jugador

        // OJO: solo la capa "Default" tapa la vista. Si Cristian pone las paredes
        // del nivel en otra capa (p. ej. "Walls"), hay que agregarla a este LayerMask:
        // LayerMask.GetMask("Default", "Walls"). Si no, el lobo verá a través de ellas.
        if (Physics.Raycast(eye, toTarget.normalized, out RaycastHit hit, toTarget.magnitude,
            LayerMask.GetMask("Default")))                                      // Solo choca con la capa Default (paredes)
        {
            return hit.transform.IsChildOf(playerTransform);                    // Si lo primero que toca es el jugador: lo ve
        }
        return true;                                                            // Nada en medio: lo ve
    }

    // ===================== Lógica de cada estado =====================

    private void UpdateIdle()
    {
        // 1) Si viene de perseguir, primero camina de vuelta a su puesto
        if (_returningHome)
        {
            if (billboardSprite != null) billboardSprite.PlayWalk();           // Pose caminando
            if (!AgentReady()) { _returningHome = false; return; }             // Sin NavMesh no puede volver: vigila desde aquí
            if (_agent.pathPending || _agent.remainingDistance > 0.5f) return;  // Aún en camino (el agente gira solo hacia donde va)

            _returningHome = false;                                            // Llegó al puesto
            _agent.isStopped = true;                                           // Plantarse
        }

        // 2) En su puesto: girar suave (Slerp) hasta su rotación de origen y vigilar
        transform.rotation = Quaternion.Slerp(transform.rotation, _homeRotation, Time.deltaTime * 3f);
        if (billboardSprite != null) billboardSprite.PlayIdle();               // Pose quieto (respira gracias a RubberHoseAnimator)
    }

    /// <summary>Al entrar en Idle: si el centinela está lejos de su puesto, mandarlo de vuelta.</summary>
    private void StartReturnHome()
    {
        _returningHome = false;
        if (!AgentReady()) return;                                             // Sin NavMesh no se puede caminar
        if (Vector3.Distance(transform.position, _homePosition) <= 0.5f)        // Ya está en su puesto
        {
            _agent.isStopped = true;
            return;
        }
        _agent.speed = patrolSpeed;                                            // Vuelve caminando, no corriendo
        _agent.isStopped = false;
        _agent.SetDestination(_homePosition);                                  // Ruta al puesto
        _returningHome = true;
    }

    private void UpdatePatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;           // Sin ruta: se queda donde está
        if (_waitingAtPoint) return;                                            // Esperando en un punto
        if (!AgentReady()) return;                                              // Sin NavMesh no se puede caminar

        _agent.speed = patrolSpeed;                                             // Paso tranquilo
        if (billboardSprite != null) billboardSprite.PlayWalk();               // Pose normal

        // El destino se da UNA vez por punto: repetirlo cada frame deja la ruta
        // "pendiente" y remainingDistance nunca sirve para saber si llegó.
        if (!_hasPatrolDestination)
        {
            Transform target = patrolPoints[_currentPatrolIndex];               // Punto actual
            if (target == null) { AdvancePatrolPoint(); return; }               // Hueco vacío en la lista: saltarlo
            _agent.isStopped = false;                                           // Permitir moverse
            _agent.SetDestination(target.position);                             // Calcular la ruta hasta el punto
            _hasPatrolDestination = true;
            return;
        }

        // ¿Llegó? (ruta ya calculada y a menos de medio metro)
        if (!_agent.pathPending && _agent.remainingDistance < 0.5f)
            StartCoroutine(WaitAtPatrolPoint());
    }

    /// <summary>Corrutina: espera en el punto y luego pasa al siguiente.</summary>
    private IEnumerator WaitAtPatrolPoint()
    {
        _waitingAtPoint = true;                                                 // Bloquea UpdatePatrol mientras espera
        SetAgentStopped(true);                                                  // Quieto
        if (billboardSprite != null) billboardSprite.PlayIdle();               // Pose quieto mientras espera
        yield return new WaitForSeconds(waitTimeAtPoint);                       // Esperar sin congelar el juego
        AdvancePatrolPoint();                                                   // Elegir el siguiente punto
        SetAgentStopped(false);                                                 // Puede volver a moverse
        _waitingAtPoint = false;
    }

    private void AdvancePatrolPoint()
    {
        _currentPatrolIndex = (_currentPatrolIndex + 1) % patrolPoints.Length; // Siguiente; % vuelve al primero
        _hasPatrolDestination = false;                                          // Pedir ruta nueva
    }

    private void UpdateAlert()
    {
        // "¡Ahí está!": se detiene un momento (el estirón de sorpresa lo pone RubberHoseAnimator).
        // El paso a Chase lo hace la corrutina AlertTransition (ver ChangeState).
        if (billboardSprite != null) billboardSprite.PlayIdle();
        SetAgentStopped(true);
    }

    private void UpdateChase()
    {
        if (playerTransform == null || !AgentReady()) return;                   // Sin jugador o sin NavMesh

        _agent.isStopped = false;                                               // Correr
        _agent.speed = chaseSpeed;
        _agent.SetDestination(playerTransform.position);                        // Ir a donde está el jugador AHORA
        if (billboardSprite != null) billboardSprite.PlayWalk();               // Pose caminando (el rebote se acelera al correr)

        if (_playerInAttackRange)                                               // Ya lo alcanzó
        {
            ChangeState(EnemyState.Attack);
            return;
        }

        // Lo perdió: fuera de vista y bastante lejos (1.5 veces el radio de visión)
        if (!_playerInSightRange && Vector3.Distance(transform.position, playerTransform.position) > sightRange * 1.5f)
            ChangeState(enemyType == EnemyType.Centinela ? EnemyState.Idle : EnemyState.Patrol);
    }

    private void UpdateAttack()
    {
        SetAgentStopped(true);                                                  // Se planta para golpear
        if (billboardSprite != null) billboardSprite.PlayAttack();             // Pose shoot (golpe cuerpo a cuerpo)

        if (playerTransform != null)                                            // Girar hacia el jugador
        {
            Vector3 lookDir = playerTransform.position - transform.position;
            lookDir.y = 0f;                                                     // Solo en horizontal (no se inclina)
            if (lookDir.sqrMagnitude > 0.001f)                                  // Evitar dirección cero
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
        }

        if (Time.time - _lastAttackTime >= attackCooldown)                      // ¿Pasó la pausa entre golpes?
            PerformAttack();

        if (!_playerInAttackRange)                                              // Se alejó: volver a perseguir
            ChangeState(EnemyState.Chase);
    }

    private void PerformAttack()
    {
        _lastAttackTime = Time.time;                                            // Reiniciar la pausa
        if (playerTransform != null && playerTransform.TryGetComponent(out PlayerHealth playerHealth))
            playerHealth.TakeDamage(attackDamage);                              // Script de David: le quita vida a Wilbur
    }

    // ===================== Cambio de estado =====================

    /// <summary>
    /// Único lugar donde cambia el estado: aplica los efectos de entrar
    /// al estado nuevo (frenar, corrutinas, poses) y avisa por OnStateChanged.
    /// </summary>
    private void ChangeState(EnemyState newState)
    {
        if (currentState == newState) return;                                   // Ya estaba: nada que hacer

        EnemyState previous = currentState;                                     // Guardar para el evento
        currentState = newState;

        switch (newState)
        {
            case EnemyState.Idle:
                StartReturnHome();                                              // Centinela: volver al puesto (o quedarse si ya está)
                break;
            case EnemyState.Patrol:
                _hasPatrolDestination = false;                                  // Pedir de nuevo el destino al volver
                _waitingAtPoint = false;
                break;
            case EnemyState.Alert:
                StartCoroutine(AlertTransition());                              // Pausa dramática y luego Chase
                break;
            case EnemyState.Chase:
                SetAgentStopped(false);                                         // Arrancar
                break;
            case EnemyState.Surrender:
                StopAllCoroutines();                                            // Cortar patrulla/alerta pendientes
                _waitingAtPoint = false;
                if (AgentReady())
                {
                    _agent.isStopped = true;                                    // Frenar
                    _agent.ResetPath();                                         // Olvidar la ruta
                }
                if (_agent.enabled) _agent.velocity = Vector3.zero;             // Sin derrapar
                if (billboardSprite != null) billboardSprite.PlaySurrender();  // Manos arriba
                break;
            case EnemyState.Dead:
                StopAllCoroutines();                                            // La espera de patrulla tocaría un agente apagado
                SetAgentStopped(true);
                _agent.enabled = false;                                         // Ya no ocupa espacio en el NavMesh
                if (billboardSprite != null) billboardSprite.PlayDeath();      // Dibujo de muerte
                break;
        }

        OnStateChanged?.Invoke(previous, newState);                             // Evento de C# (Action, no es un UnityEngine.Object)
    }

    /// <summary>Corrutina: tras la pausa de alerta, empieza a perseguir.</summary>
    private IEnumerator AlertTransition()
    {
        yield return new WaitForSeconds(0.8f);                                  // "¡Ahí está!"
        if (currentState == EnemyState.Alert)                                   // Sigue en alerta (no lo rindieron mientras)
            ChangeState(EnemyState.Chase);
    }

    // ===================== Ayudas del NavMeshAgent =====================

    /// <summary>¿Se le pueden dar órdenes al agente? (activo y parado sobre un NavMesh horneado)</summary>
    private bool AgentReady()
    {
        return _agent != null && _agent.enabled && _agent.isOnNavMesh;
    }

    /// <summary>Frenar o soltar al agente sin errores si no hay NavMesh.</summary>
    private void SetAgentStopped(bool stopped)
    {
        if (AgentReady()) _agent.isStopped = stopped;
    }

    // ===================== Llamadas desde EnemyHealth =====================

    /// <summary>
    /// Llamado desde EnemyHealth cuando se rinde (EnemyHealth decide si puede rendirse).
    /// Se queda quieto y deja de atacar; Update() no corre en estado Surrender.
    /// </summary>
    public void TriggerSurrender()
    {
        if (currentState == EnemyState.Dead) return;                            // Un muerto no se rinde
        ChangeState(EnemyState.Surrender);
    }

    /// <summary>Llamado desde EnemyHealth cuando el enemigo muere.</summary>
    public void Die()
    {
        ChangeState(EnemyState.Dead);
        Collider col = GetComponent<Collider>();                                // Collider del cuerpo
        if (col != null) col.enabled = false;                                   // Que el cadáver no bloquee ni reciba disparos
    }

    // ===================== Ayuda visual en el Editor =====================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;                                            // Radio de visión
        Gizmos.DrawWireSphere(transform.position, sightRange);
        Gizmos.color = Color.red;                                               // Radio de ataque
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.cyan;                                              // Radio de oído
        Gizmos.DrawWireSphere(transform.position, hearingRange);
    }
}
