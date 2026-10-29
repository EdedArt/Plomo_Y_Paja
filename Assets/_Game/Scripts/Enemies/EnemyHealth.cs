using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Sistema de salud de los enemigos.
/// DÓNDE VA: misma raíz del enemigo junto con EnemyAI.
///
/// SETUP:
/// - Asignar surrenderThreshold igual al del EnemyAI
/// - Conectar onEnemyDied → GameStateTracker.OnEnemyKilled (para tracking narrativo)
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    [Header("Salud")]
    [SerializeField] private float maxHealth = 60f;
    [SerializeField] private float currentHealth;

    [Header("Umbral de rendición")]
    [Tooltip("Si la vida baja de este % llama a TriggerSurrender en EnemyAI")]
    [SerializeField, Range(0f, 0.5f)] private float surrenderThreshold = 0.3f;
    private bool _surrenderTriggered = false;

    [Header("Eventos")]
    public UnityEvent onDamageTaken;
    public UnityEvent onEnemyDied;

    [Header("Opciones")]
    [Tooltip("Tiempo en segundos antes de destruir el GameObject al morir")]
    [SerializeField] private float destroyDelay = 3f;

    private EnemyAI _ai;

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
        if (currentHealth <= 0) return false; // Ya está muerto

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);
        onDamageTaken?.Invoke();

        // ¿Se rinde?
        if (!_surrenderTriggered && currentHealth / maxHealth <= surrenderThreshold)
        {
            _surrenderTriggered = true;
            _ai?.TriggerSurrender();
        }

        // ¿Murió?
        if (currentHealth <= 0)
        {
            Die();
            return true;
        }

        return false;
    }

    private void Die()
    {
        onEnemyDied?.Invoke();
        _ai?.Die();

        // Notificar al tracker de estado del juego
        GameStateTracker.Instance?.RegisterEnemyKilled(gameObject.name);

        // Destruir el GameObject después de unos segundos (para que el sprite de muerte se vea)
        Destroy(gameObject, destroyDelay);
    }

    // Getter para que EnemyAI pueda leer la vida
    public float HealthPercentage => currentHealth / maxHealth;
}
