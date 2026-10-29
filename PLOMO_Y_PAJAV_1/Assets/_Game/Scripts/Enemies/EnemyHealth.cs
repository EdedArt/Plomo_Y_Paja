using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Sistema de salud de los enemigos.
/// DÓNDE VA: misma raíz del enemigo junto con EnemyAI.
///
/// SETUP:
/// - canSurrender = true y npcId (ej: "guardia_porky") para guardias interrogables
///   (agregar también DialogueNPC al mismo GameObject)
/// - Conectar onSurrender → cambio de sprite a "manos arriba" (más adelante)
/// - Conectar onEnemyDied → GameStateTracker.OnEnemyKilled (para tracking narrativo)
///
/// RENDICIÓN:
/// La primera vez que la vida baja a ≤ surrenderThreshold (y sigue vivo) se rinde:
/// la IA se detiene y no ataca. Si el jugador lo remata, cuenta como muerto (no interrogado).
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    [Header("Salud")]
    [Tooltip("Vida máxima del enemigo")]
    [SerializeField] private float maxHealth = 60f;
    [Tooltip("Vida actual (visible en Inspector para debug)")]
    [SerializeField] private float currentHealth;

    [Header("Rendición")]
    [Tooltip("Si es true, el guardia se rinde al llegar al umbral en vez de pelear hasta morir")]
    [SerializeField] private bool canSurrender = false;

    [Tooltip("ID del guardia en DialogueDatabase (ej: guardia_porky, guardia_barracon, guardia_registro)")]
    [SerializeField] private string npcId = "";

    [Tooltip("Se rinde cuando la vida baja a este % (0 a 1)")]
    [SerializeField, Range(0f, 0.5f)] private float surrenderThreshold = 0.3f;

    [Tooltip("Segundos tras rendirse en que ignora daño, para que los perdigones restantes " +
             "del mismo disparo no lo maten (la escopeta dispara 8 a la vez)")]
    [SerializeField, Min(0f)] private float surrenderGraceTime = 0.4f;

    [Header("Eventos")]
    [Tooltip("Se dispara cada vez que recibe daño")]
    public UnityEvent onDamageTaken;
    [Tooltip("Se dispara al morir")]
    public UnityEvent onEnemyDied;
    [Tooltip("Se dispara al rendirse (para cambiar el sprite a manos arriba)")]
    public UnityEvent onSurrender;

    /// <summary>El enemigo se rindió (desde código).</summary>
    public event Action OnSurrender;

    /// <summary>El enemigo murió (desde código).</summary>
    public event Action OnDeath;

    [Header("Opciones")]
    [Tooltip("Tiempo en segundos antes de destruir el GameObject al morir")]
    [SerializeField] private float destroyDelay = 3f;

    private EnemyAI _ai;
    private bool _isSurrendered = false;
    private bool _isDead = false;
    private float _graceUntil = -1f;

    // ============ Propiedades públicas ============
    public string NpcId => npcId;
    public bool CanSurrender => canSurrender;
    public bool IsSurrendered => _isSurrendered && !_isDead;
    public bool IsDead => _isDead;
    public float HealthPercentage => maxHealth > 0 ? currentHealth / maxHealth : 0f;

    private void Awake()
    {
        _ai = GetComponent<EnemyAI>();
        currentHealth = maxHealth;
    }

    /// <summary>
    /// Recibir daño. Retorna true si el enemigo murió con este golpe.
    /// </summary>
    public bool TakeDamage(float damage)
    {
        if (_isDead) return false; // Ya está muerto
        if (Time.time < _graceUntil) return false; // recién rendido: el resto del disparo no cuenta

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);
        onDamageTaken?.Invoke();

        // ¿Murió? (también si ya estaba rendido: cuenta como muerto)
        if (currentHealth <= 0)
        {
            Die();
            return true;
        }

        // ¿Se rinde? Solo la primera vez que cruza el umbral
        if (canSurrender && !_isSurrendered && HealthPercentage <= surrenderThreshold)
        {
            Surrender();
        }

        return false;
    }

    private void Surrender()
    {
        _isSurrendered = true;
        _graceUntil = Time.time + surrenderGraceTime;
        _ai?.TriggerSurrender();

        OnSurrender?.Invoke();
        onSurrender?.Invoke();
    }

    private void Die()
    {
        _isDead = true;

        onEnemyDied?.Invoke();
        OnDeath?.Invoke();
        _ai?.Die();

        // Notificar al tracker de estado del juego
        GameStateTracker.Instance?.RegisterEnemyKilled(gameObject.name);

        // La IA puede comentar la muerte (AICompanion decide si habla o calla)
        AICompanion.Instance?.Speak(AITrigger.EnemyKilled);

        // Destruir el GameObject después de unos segundos (para que el sprite de muerte se vea)
        Destroy(gameObject, destroyDelay);
    }
}
