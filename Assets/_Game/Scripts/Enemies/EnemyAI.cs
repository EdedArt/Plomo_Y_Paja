using System.Collections;
using UnityEngine;
using UnityEngine.AI; // NavMesh — navegación automática en el nivel

/// <summary>
/// IA de los secuaces de Don Lobo (lobos guardias).
/// DÓNDE VA: GameObject raíz del enemigo.
///
/// MÁQUINA DE ESTADOS:
/// Idle → Patrol → Alert → Chase → Attack
///                            ↓ (si HP < 30%)
///                         Surrender → (jugador puede interrogar)
///
/// SETUP EN INSPECTOR (jerarquía del Prefab):
/// - LoboCacique (EnemyAI + NavMeshAgent + Rigidbody kinematic + CapsuleCollider)
///   ├── SpriteHolder (BillboardSprite — EL SPRITE VA AQUÍ)
///   │   └── SpriteRenderer
///   └── DetectionZone (SphereCollider trigger, separado del cuerpo)
///       └── EnemyDetector.cs (script pequeño que llama OnPlayerEnterRange)
///
/// NAV MESH:
/// - En la escena: Window → AI → Navigation → Bake
/// - El NavMeshAgent mueve al enemigo automáticamente siguiendo el NavMesh
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    // ===== Estados de la IA =====
    public enum EnemyState { Idle, Patrol, Alert, Chase, Attack, Surrender, Dead }

    [Header("Estado actual (solo lectura — para debug)")]
    [SerializeField] private EnemyState currentState = EnemyState.Patrol;

    [Header("Tipo de enemigo")]
    [Tooltip("Patrullero: sigue una ruta. Centinela: estático hasta detectar.")]
    public enum EnemyType { Patrullero, Centinela }
    [SerializeField] private EnemyType enemyType = EnemyType.Patrullero;

    [Header("Patrullaje")]
    [Tooltip("Puntos de patrulla. Crear GameObjects vacíos en la escena y asignarlos aquí.")]
    [SerializeField] private Transform[] patrolPoints;

    [Tooltip("Tiempo de espera en cada punto de patrulla")]
    [SerializeField] private float waitTimeAtPoint = 2f;

    [Header("Detección")]
    [Tooltip("Radio de visión — si el jugador entra a este rango, el enemigo alerta")]
    [SerializeField] private float sightRange = 12f;

    [Tooltip("Ángulo de visión en grados (180 = ve en semicírculo frontal)")]
    [SerializeField] private float fieldOfView = 120f;

    [Tooltip("Radio de escucha — si el jugador corre aquí cerca, lo escucha")]
    [SerializeField] private float hearingRange = 6f;

    [Header("Combate")]
    [Tooltip("Distancia a la que puede atacar al jugador")]
    [SerializeField] private float attackRange = 2f;

    [Tooltip("Daño por ataque")]
    [SerializeField] private float attackDamage = 15f;

    [Tooltip("Segundos entre ataques")]
    [SerializeField] private float attackCooldown = 1.5f;

    [Tooltip("Se rinde cuando le queda este % de vida (0 = nunca se rinde)")]
    [SerializeField, Range(0f, 0.5f)] private float surrenderThreshold = 0.3f;

    [Header("Velocidades")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 5f;

    [Header("Referencias")]
    [Tooltip("Transform del jugador — si no se asigna, se busca automáticamente con tag 'Player'")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private BillboardSprite billboardSprite;

    // ============ Estado interno ============
    private NavMeshAgent _agent;
    private EnemyHealth _health;
    private int _currentPatrolIndex = 0;
    private bool _waitingAtPoint = false;
    private float _lastAttackTime = 0f;
    private bool _playerInSightRange = false;
    private bool _playerInAttackRange = false;

    // ============ Propiedades públicas ============
    public EnemyState CurrentState => currentState;
    public bool IsSurrendered => currentState == EnemyState.Surrender;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _health = GetComponent<EnemyHealth>();

        if (playerTransform == null)
        {
            // Buscar al jugador automáticamente por tag
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }
    }

    private void Start()
    {
        if (enemyType == EnemyType.Centinela)
        {
            ChangeState(EnemyState.Idle); // Centinela empieza quieto
            _agent.isStopped = true;
        }
        else
        {
            ChangeState(EnemyState.Patrol); // Patrullero empieza a patrullar
        }
    }

    private void Update()
    {
        if (currentState == EnemyState.Dead || currentState == EnemyState.Surrender) return;

        // Actualizar detección en cada frame
        CheckPlayerDetection();

        // Ejecutar la lógica del estado actual
        switch (currentState)
        {
            case EnemyState.Idle:    UpdateIdle();    break;
            case EnemyState.Patrol:  UpdatePatrol();  break;
            case EnemyState.Alert:   UpdateAlert();   break;
            case EnemyState.Chase:   UpdateChase();   break;
            case EnemyState.Attack:  UpdateAttack();  break;
        }

        // Actualizar animación del sprite según movimiento
        if (billboardSprite != null && _agent.velocity.magnitude > 0.1f)
            billboardSprite.SetFacingDirection(_agent.velocity);
    }

    /// <summary>
    /// Detectar al jugador por visión y oído.
    /// </summary>
    private void CheckPlayerDetection()
    {
        if (playerTransform == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Detección por visión: dentro del rango Y dentro del ángulo de visión
        if (distanceToPlayer <= sightRange)
        {
            Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, directionToPlayer);

            if (angle < fieldOfView / 2f)
            {
                // Raycast para verificar que no haya paredes entre el enemigo y el jugador
                if (!Physics.Raycast(transform.position + Vector3.up, directionToPlayer, distanceToPlayer,
                    LayerMask.GetMask("Default")))
                {
                    _playerInSightRange = true;

                    if (currentState == EnemyState.Idle || currentState == EnemyState.Patrol)
                        ChangeState(EnemyState.Alert);
                }
            }
        }
        else
        {
            _playerInSightRange = false;
        }

        // Detección por oído: si el jugador está corriendo cerca
        PlayerController playerController = playerTransform.GetComponent<PlayerController>();
        if (distanceToPlayer <= hearingRange && playerController != null && playerController.IsRunning)
        {
            if (currentState == EnemyState.Idle || currentState == EnemyState.Patrol)
                ChangeState(EnemyState.Alert);
        }

        _playerInAttackRange = distanceToPlayer <= attackRange;
    }

    private void UpdateIdle()
    {
        // Centinela: simplemente espera. No hace nada hasta detectar al jugador.
        billboardSprite?.PlayWalk(); // Animación idle (primer frame del walk)
    }

    private void UpdatePatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;
        if (_waitingAtPoint) return;

        // Moverse hacia el punto actual de patrulla
        _agent.SetDestination(patrolPoints[_currentPatrolIndex].position);
        _agent.speed = patrolSpeed;
        billboardSprite?.PlayWalk();

        // ¿Llegó al punto?
        if (_agent.remainingDistance < 0.5f)
        {
            StartCoroutine(WaitAtPatrolPoint());
        }
    }

    private IEnumerator WaitAtPatrolPoint()
    {
        _waitingAtPoint = true;
        _agent.isStopped = true;
        yield return new WaitForSeconds(waitTimeAtPoint);
        _currentPatrolIndex = (_currentPatrolIndex + 1) % patrolPoints.Length;
        _agent.isStopped = false;
        _waitingAtPoint = false;
    }

    private void UpdateAlert()
    {
        // Breve estado de alerta antes de perseguir
        billboardSprite?.PlayAlert();
        _agent.isStopped = true;

        // Después de 0.5 segundos pasa a perseguir
        // (Esto se maneja en ChangeState con una coroutine)
    }

    private void UpdateChase()
    {
        if (playerTransform == null) return;

        _agent.isStopped = false;
        _agent.speed = chaseSpeed;
        _agent.SetDestination(playerTransform.position);
        billboardSprite?.PlayAlert();

        // Atacar si está en rango
        if (_playerInAttackRange)
            ChangeState(EnemyState.Attack);

        // Perdió al jugador
        if (!_playerInSightRange && Vector3.Distance(transform.position, playerTransform.position) > sightRange * 1.5f)
            ChangeState(EnemyState.Patrol);
    }

    private void UpdateAttack()
    {
        // Detenerse para atacar
        _agent.isStopped = true;

        // Mirar al jugador
        if (playerTransform != null)
        {
            Vector3 lookDir = playerTransform.position - transform.position;
            lookDir.y = 0;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
        }

        // Atacar con cooldown
        if (Time.time - _lastAttackTime >= attackCooldown)
        {
            PerformAttack();
        }

        // Si el jugador escapó del rango de ataque, perseguir de nuevo
        if (!_playerInAttackRange)
            ChangeState(EnemyState.Chase);
    }

    private void PerformAttack()
    {
        _lastAttackTime = Time.time;

        // Buscar al jugador y hacerle daño
        PlayerHealth playerHealth = playerTransform?.GetComponent<PlayerHealth>();
        playerHealth?.TakeDamage(attackDamage);
    }

    /// <summary>
    /// Cambiar de estado con efectos secundarios (velocidad, animaciones, etc.)
    /// </summary>
    private void ChangeState(EnemyState newState)
    {
        if (currentState == newState) return;

        currentState = newState;

        switch (newState)
        {
            case EnemyState.Alert:
                StartCoroutine(AlertTransition());
                break;
            case EnemyState.Chase:
                _agent.isStopped = false;
                break;
            case EnemyState.Surrender:
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
                billboardSprite?.PlayWalk(); // Frame de rendición
                break;
            case EnemyState.Dead:
                _agent.isStopped = true;
                _agent.enabled = false;
                billboardSprite?.PlayDeath();
                break;
        }
    }

    private IEnumerator AlertTransition()
    {
        yield return new WaitForSeconds(0.8f); // Pausa dramática de "¡ahí está!"
        if (currentState == EnemyState.Alert)
            ChangeState(EnemyState.Chase);
    }

    /// <summary>
    /// Llamado desde EnemyHealth cuando la vida baja del umbral de rendición.
    /// </summary>
    public void TriggerSurrender()
    {
        if (surrenderThreshold > 0)
            ChangeState(EnemyState.Surrender);
        else
            ChangeState(EnemyState.Chase); // No se rinde, sigue peleando
    }

    /// <summary>
    /// Llamado cuando el enemigo muere.
    /// </summary>
    public void Die()
    {
        ChangeState(EnemyState.Dead);
        // Desactivar el collider del cuerpo para que no bloquee
        GetComponent<Collider>()?.enabled = false;
    }

    // Visualización en el editor: dibujar los rangos de detección
    private void OnDrawGizmosSelected()
    {
        // Radio de visión (amarillo)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);

        // Radio de ataque (rojo)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Radio de oído (cian)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, hearingRange);
    }
}
