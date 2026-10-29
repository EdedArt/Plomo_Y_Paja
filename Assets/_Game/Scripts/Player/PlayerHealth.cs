using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Sistema de salud del jugador Wilbur.
/// DÓNDE VA: en el mismo GameObject "Player".
///
/// SETUP EN INSPECTOR:
/// - Conectar onHealthChanged → UIManager.UpdateHealthUI
/// - Conectar onPlayerDied → GameManager.PlayerDied
/// - Asignar damageVignette (opcional, efecto visual de daño)
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Salud")]
    [Tooltip("Vida máxima del jugador")]
    [SerializeField] private float maxHealth = 100f;

    [Tooltip("Vida actual (visible en Inspector para debug)")]
    [SerializeField] private float currentHealth;

    [Tooltip("Tiempo de invencibilidad después de recibir daño (para no morir instantáneo)")]
    [SerializeField] private float invincibilityTime = 0.3f;

    [Header("Eventos (conectar en Inspector)")]
    [Tooltip("Se dispara cuando la vida cambia → float es el porcentaje (0 a 1)")]
    public UnityEvent<float> onHealthChanged;

    [Tooltip("Se dispara cuando el jugador muere")]
    public UnityEvent onPlayerDied;

    [Tooltip("Se dispara cuando recibe daño (para efectos visuales)")]
    public UnityEvent onDamageTaken;

    [Header("Efecto visual de daño")]
    [Tooltip("Duración del parpadeo rojo de la pantalla al recibir daño")]
    [SerializeField] private float damageFlashDuration = 0.2f;

    private bool _isDead = false;
    private bool _isInvincible = false;

    private void Start()
    {
        currentHealth = maxHealth;
        onHealthChanged?.Invoke(1f); // 100%
    }

    /// <summary>
    /// Recibir daño. Llamado por EnemyAI o trampas.
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (_isDead || _isInvincible) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        // Notificar a la UI con el porcentaje de vida (0 a 1)
        onHealthChanged?.Invoke(currentHealth / maxHealth);
        onDamageTaken?.Invoke();

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // Breve invencibilidad para que los ataques no apilen demasiado daño
            StartCoroutine(InvincibilityCoroutine());
        }
    }

    /// <summary>
    /// Curar al jugador. Útil para pickups de salud en el nivel.
    /// </summary>
    public void Heal(float amount)
    {
        if (_isDead) return;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        onHealthChanged?.Invoke(currentHealth / maxHealth);
    }

    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        // Desactivar el movimiento
        GetComponent<PlayerController>()?.SetCanMove(false);
        GetComponent<ShotgunController>().enabled = false;

        onPlayerDied?.Invoke();
    }

    private IEnumerator InvincibilityCoroutine()
    {
        _isInvincible = true;
        yield return new WaitForSeconds(invincibilityTime);
        _isInvincible = false;
    }

    // Getters
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthPercentage => currentHealth / maxHealth;
    public bool IsDead => _isDead;

    /// <summary>
    /// Reiniciar salud al reiniciar el nivel.
    /// </summary>
    public void ResetHealth()
    {
        _isDead = false;
        _isInvincible = false;
        currentHealth = maxHealth;
        onHealthChanged?.Invoke(1f);
        GetComponent<PlayerController>()?.SetCanMove(true);
        GetComponent<ShotgunController>().enabled = true;
    }
}
