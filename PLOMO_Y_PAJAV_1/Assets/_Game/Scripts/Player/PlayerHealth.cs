using System;
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
///
/// DESDE CÓDIGO:
/// - OnHealthChanged (current, max) y OnDeath para sistemas que no usan el Inspector
/// - HUD.1: OnDamaged (daño, posición de la fuente si se conoce), OnHealed (lo que curó de verdad). TakeDamage(daño, fuente)
///   manda la posición para el indicador de dirección del HUD; TakeDamage(daño) sigue igual (fuente desconocida).
///   Al morir se activa la razón Death de UiHideReasons (el HUD se oculta).
/// - Al bajar del 30% la IA comenta (AITrigger.LowHealth) una sola vez;
///   se rearma cuando la vida vuelve a superar el 40%
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

    [Header("Aviso de poca vida (IA)")]
    [Tooltip("Porcentaje (0 a 1) por debajo del cual la IA comenta que Wilbur está herido")]
    [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.3f;

    [Tooltip("Porcentaje (0 a 1) que hay que superar para que el aviso pueda volver a sonar")]
    [SerializeField, Range(0f, 1f)] private float lowHealthRearmThreshold = 0.4f;

    [Header("Eventos (conectar en Inspector)")]
    [Tooltip("Se dispara cuando la vida cambia → float es el porcentaje (0 a 1)")]
    public UnityEvent<float> onHealthChanged;

    [Tooltip("Se dispara cuando el jugador muere")]
    public UnityEvent onPlayerDied;

    [Tooltip("Se dispara cuando recibe daño (para efectos visuales)")]
    public UnityEvent onDamageTaken;

    /// <summary>Vida cambió: (vida actual, vida máxima).</summary>
    public event Action<float, float> OnHealthChanged;

    /// <summary>El jugador murió.</summary>
    public event Action OnDeath;

    /// <summary>Recibió daño (HUD.1): (cantidad, posición de la fuente o null si no se conoce).</summary>
    public event Action<float, Vector3?> OnDamaged;

    /// <summary>Se curó (HUD.1): lo que subió de verdad (0 no se avisa).</summary>
    public event Action<float> OnHealed;

    private bool _isDead = false;
    private bool _isInvincible = false;
    private bool _lowHealthArmed = true;

    // Voces de Wilbur (AudioLibrary; sin clip no suena nada)
    private static readonly string[] HurtLines = { "wilbur_dano_01", "wilbur_dano_02", "wilbur_dano_03" };
    private const string LowHealthLine = "wilbur_pocavida_01";
    private float _nextHurtVoice;
    private int _lastHurtLine = -1;

    private void Start()
    {
        currentHealth = maxHealth;
        NotifyHealthChanged();
    }

    /// <summary>
    /// Recibir daño. Llamado por EnemyAI o trampas.
    /// </summary>
    public void TakeDamage(float damage)
    {
        ApplyDamage(damage, null);
    }

    /// <summary>Recibir daño desde una posición conocida (HUD.1: el indicador de dirección del HUD apunta hacia ahí).</summary>
    public void TakeDamage(float damage, Vector3 sourcePosition)
    {
        ApplyDamage(damage, sourcePosition);
    }

    private void ApplyDamage(float damage, Vector3? source)
    {
        if (_isDead || _isInvincible) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        NotifyHealthChanged();
        onDamageTaken?.Invoke();
        OnDamaged?.Invoke(damage, source);

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // Si este golpe lo deja con poca vida, suena el aviso en lugar del quejido
            if (!CheckLowHealth()) PlayHurtVoice();
            // Breve invencibilidad para que los ataques no apilen demasiado daño
            StartCoroutine(InvincibilityCoroutine());
        }
    }

    /// <summary>
    /// Curar al jugador. Útil para pickups de salud en el nivel.
    /// </summary>
    public void Heal(float amount)
    {
        if (_isDead || amount <= 0f) return;
        float before = currentHealth;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        float healed = currentHealth - before;
        if (healed <= 0f) return; // ya estaba lleno: no se avisa (HUD.3 no gasta el botiquín)
        NotifyHealthChanged();
        OnHealed?.Invoke(healed);
        CheckLowHealth();
    }

    /// <summary>
    /// Avisar a la IA al cruzar hacia abajo el umbral de poca vida (una vez por cruce).
    /// </summary>
    private bool CheckLowHealth()
    {
        float percent = HealthPercentage;

        if (percent > lowHealthRearmThreshold)
        {
            _lowHealthArmed = true;
        }
        else if (_lowHealthArmed && percent <= lowHealthThreshold && currentHealth > 0)
        {
            _lowHealthArmed = false;
            if (AICompanion.Instance != null) AICompanion.Instance.Speak(AITrigger.LowHealth);
            AudioLibrary.PlayVoice2D(LowHealthLine);
            _nextHurtVoice = Time.time + AudioLibrary.Mix.hurtVoiceCooldown;
            return true;
        }
        return false;
    }

    /// <summary>Quejido al azar (sin repetir el anterior) con enfriamiento para que los golpes seguidos no se apilen.</summary>
    private void PlayHurtVoice()
    {
        if (Time.time < _nextHurtVoice) return;
        _nextHurtVoice = Time.time + AudioLibrary.Mix.hurtVoiceCooldown;

        int index = UnityEngine.Random.Range(0, HurtLines.Length);
        if (index == _lastHurtLine) index = (index + 1) % HurtLines.Length;
        _lastHurtLine = index;
        AudioLibrary.PlayVoice2D(HurtLines[index]);
    }

    private void NotifyHealthChanged()
    {
        onHealthChanged?.Invoke(HealthPercentage);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        // Desactivar el movimiento y el arma (la escopeta es hija de la cámara)
        if (TryGetComponent(out PlayerController controller)) controller.SetCanMove(false);
        ShotgunController shotgun = GetComponentInChildren<ShotgunController>();
        if (shotgun != null) shotgun.enabled = false;

        onPlayerDied?.Invoke();
        UiHideReasons.Set(UiHideReasons.Reason.Death, true); // HUD.1 (H6): el HUD se oculta al morir
        OnDeath?.Invoke();
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
    public float HealthPercentage => maxHealth > 0 ? currentHealth / maxHealth : 0f;
    public bool IsDead => _isDead;
    /// <summary>¿Vida al máximo? (HUD.3: el botiquín no se gasta).</summary>
    public bool IsFullHealth => currentHealth >= maxHealth;

    /// <summary>
    /// Reiniciar salud al reiniciar el nivel.
    /// </summary>
    public void ResetHealth()
    {
        _isDead = false;
        _isInvincible = false;
        _lowHealthArmed = true;
        currentHealth = maxHealth;
        UiHideReasons.Set(UiHideReasons.Reason.Death, false);
        NotifyHealthChanged();
        if (TryGetComponent(out PlayerController controller)) controller.SetCanMove(true);
        ShotgunController shotgun = GetComponentInChildren<ShotgunController>();
        if (shotgun != null) shotgun.enabled = true;
    }
}
