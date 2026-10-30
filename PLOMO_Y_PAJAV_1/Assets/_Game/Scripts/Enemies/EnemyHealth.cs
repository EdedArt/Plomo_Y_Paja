using System;                    // Para Action (eventos de C#)
using UnityEngine;               // Tipos básicos de Unity
using UnityEngine.Events;        // Para UnityEvent (eventos que se conectan desde el Inspector)

/// <summary>
/// Vida de los enemigos: recibir golpes, rendirse y morir.
/// DÓNDE VA: en la raíz del enemigo, junto con EnemyAI.
///
/// QUIÉN LO USA:
/// - ShotgunController (David) llama TakeDamage(daño) por cada perdigón que acierta.
/// - EnemyAI escucha OnDamaged (pose hit) y recibe TriggerSurrender() / Die().
/// - DialogueNPC pregunta IsSurrendered para dejar interrogar.
/// - RubberHoseAnimator escucha OnDamaged (aplastamiento por golpe).
///
/// GUARDIAS INTERROGABLES: canSurrender = true y npcId = el id del guardia en
/// DialogueDatabase (además, agregar DialogueNPC al mismo GameObject).
///
/// RENDICIÓN:
/// La primera vez que la vida baja a ≤ surrenderThreshold (y sigue vivo) se rinde:
/// la IA se detiene y no ataca. Si el jugador lo remata, cuenta como muerto (no interrogado).
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    // ===================== Configuración =====================

    [Header("Salud")]
    [Tooltip("Vida máxima del enemigo")]
    [SerializeField] private float maxHealth = 60f;             // Vida con la que nace

    [Tooltip("Vida actual (visible en Inspector para debug)")]
    [SerializeField] private float currentHealth;               // Vida que le queda (se llena en Awake)

    [Header("Rendición")]
    [Tooltip("Si es true, el guardia se rinde al llegar al umbral en vez de pelear hasta morir")]
    [SerializeField] private bool canSurrender = false;         // Lo escriben los LevelBuilder: no renombrar

    [Tooltip("ID del guardia en DialogueDatabase (ej: guardia_porky, guardia_barracon, guardia_registro)")]
    [SerializeField] private string npcId = "";                 // Lo escriben los LevelBuilder: no renombrar

    [Tooltip("Se rinde cuando la vida baja a este % (0 a 1)")]
    [SerializeField, Range(0f, 0.5f)] private float surrenderThreshold = 0.3f; // 0.3 = al 30 % de vida

    [Tooltip("Segundos tras rendirse en que ignora daño, para que los perdigones restantes " +
             "del mismo disparo no lo maten (la escopeta dispara 8 a la vez)")]
    [SerializeField, Min(0f)] private float surrenderGraceTime = 0.4f; // Ventana de "perdón" tras rendirse

    [Header("Eventos (Inspector)")]
    [Tooltip("Se dispara cada vez que recibe daño")]
    public UnityEvent onDamageTaken;                            // Para conectar sonidos/partículas desde el Inspector
    [Tooltip("Se dispara al morir")]
    public UnityEvent onEnemyDied;
    [Tooltip("Se dispara al rendirse")]
    public UnityEvent onSurrender;

    // ===================== Eventos de código =====================

    /// <summary>Recibió daño (cantidad). Lo escuchan EnemyAI y RubberHoseAnimator.</summary>
    public event Action<float> OnDamaged;

    /// <summary>El enemigo se rindió.</summary>
    public event Action OnSurrender;

    /// <summary>El enemigo murió.</summary>
    public event Action OnDeath;

    [Header("Opciones")]
    [Tooltip("Tiempo en segundos antes de destruir el GameObject al morir")]
    [SerializeField] private float destroyDelay = 3f;           // Para que se vea el dibujo de muerte

    // ===================== Estado interno =====================

    private EnemyAI _ai;                     // Su IA (opcional: un objeto rompible podría no tenerla)
    private bool _isSurrendered = false;     // ¿Ya se rindió?
    private bool _isDead = false;            // ¿Ya murió?
    private float _graceUntil = -1f;         // Hasta cuándo ignora daño tras rendirse

    // ===================== Propiedades públicas =====================

    public string NpcId => npcId;                                               // Id para DialogueDatabase
    public bool CanSurrender => canSurrender;                                   // ¿Es interrogable?
    public bool IsSurrendered => _isSurrendered && !_isDead;                    // Rendido y vivo
    public bool IsDead => _isDead;                                              // Muerto
    public float HealthPercentage => maxHealth > 0 ? currentHealth / maxHealth : 0f; // Vida de 0 a 1 (sin dividir entre 0)

    // ===================== Ciclo de vida =====================

    private void Awake()
    {
        _ai = GetComponent<EnemyAI>();                          // Puede ser null: se revisa antes de usarlo
        currentHealth = maxHealth;                              // Empezar con la vida llena
    }

    // ===================== Daño =====================

    /// <summary>
    /// Recibir daño. Retorna true si el enemigo murió con este golpe
    /// (ShotgunController lo usa para saber si mató).
    /// </summary>
    public bool TakeDamage(float damage)
    {
        if (_isDead) return false;                              // Ya está muerto: los golpes no cuentan
        if (Time.time < _graceUntil) return false;              // Recién rendido: el resto del disparo no cuenta
        if (damage <= 0f) return false;                         // Daño cero o negativo: ignorar (no es curación)

        currentHealth = Mathf.Max(0f, currentHealth - damage);  // Restar sin bajar de 0
        onDamageTaken?.Invoke();                                // Avisar al Inspector (UnityEvent es clase normal, no UnityEngine.Object)
        OnDamaged?.Invoke(damage);                              // Avisar al código (pose hit, aplastamiento)

        if (currentHealth <= 0f)                                // ¿Murió? (también si ya estaba rendido: cuenta como muerto)
        {
            Die();
            return true;
        }

        // ¿Se rinde? Solo la primera vez que cruza el umbral
        if (canSurrender && !_isSurrendered && HealthPercentage <= surrenderThreshold)
            Surrender();

        return false;                                           // Sigue vivo
    }

    // ===================== Rendición y muerte =====================

    private void Surrender()
    {
        _isSurrendered = true;                                  // Marcar como rendido
        _graceUntil = Time.time + surrenderGraceTime;           // Abrir la ventana de "perdón"
        if (_ai != null) _ai.TriggerSurrender();                // La IA se detiene y levanta las manos

        OnSurrender?.Invoke();                                  // Avisar al código
        onSurrender?.Invoke();                                  // Avisar al Inspector
    }

    private void Die()
    {
        _isDead = true;                                         // Marcar como muerto (bloquea más daño)

        onEnemyDied?.Invoke();                                  // Avisar al Inspector
        OnDeath?.Invoke();                                      // Avisar al código
        if (_ai != null) _ai.Die();                             // La IA se apaga y muestra el dibujo de muerte

        // Script de David: cuenta la muerte para la historia y los finales
        if (GameStateTracker.Instance != null) GameStateTracker.Instance.RegisterEnemyKilled(gameObject.name);

        // Script de David: el compañero de IA puede comentar la muerte (él decide si habla o calla)
        if (AICompanion.Instance != null) AICompanion.Instance.Speak(AITrigger.EnemyKilled);

        Destroy(gameObject, destroyDelay);                      // Borrar el cuerpo tras unos segundos
    }
}
