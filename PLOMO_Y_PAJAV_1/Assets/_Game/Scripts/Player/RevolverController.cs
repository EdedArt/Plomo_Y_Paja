using System.Collections;
using UnityEngine;

/// <summary>
/// Revólver de Centavo, segunda arma de Wilbur (fase 3C.5, D13–D15). Mínimo de VM2: una bala hitscan, semiautomático.
/// 3C.5.2: tambor de 5 (los 5 huecos del dibujo reload_01) + 15 de reserva, y recarga BALA POR BALA (IStagedReloadWeapon):
/// abrir (openSeconds) → una bala por cada una que falta, sin pasar de la reserva (insertSeconds cada una) → cerrar
/// (closeSeconds). Disparar 1 → carga 1; disparar 2 → carga 2. Disparar durante la recarga: termina la bala actual, cierra
/// y dispara. Cambiar de arma corta la recarga y las balas ya metidas se quedan.
/// DÓNDE VA: GameObject "Revolver" hijo de la cámara del Player, junto a la escopeta (lo crea "Crear Prefabs de Prueba").
/// Empieza SIN dueño: WeaponSwitcher.GiveRevolver (WeaponPickup, el revólver que suelta Centavo) lo entrega.
///
/// DAÑO: el mismo camino que ShotgunController.ShootPellet: primero EnemyHealth en los padres del collider y, solo si no
/// hay, IDamageable (Centavo, D3). Sin caída por distancia (D14: arma de lejos). 24 = 40 % de los 60 de un guardia (D15):
/// guardia 60 → 36 → 12 → muere al 3.er tiro; Colmillo (se rinde a ≤ 18) 60 → 36 → 12 = se rinde al 2.º.
/// BLOQUEO: no dispara ni recarga en pausa, diálogo, interrogatorio (InterrogationDirector.IsActive) ni muerto.
/// </summary>
public class RevolverController : MonoBehaviour, IWeapon, IStagedReloadWeapon
{
    [Header("Disparo (D14, D15)")]
    [Tooltip("Daño por bala. 24 = 40 % de la vida de un guardia (60)")]
    [SerializeField] private float damage = 24f;
    [Tooltip("Dispersión desde la cadera, en grados (por eje)")]
    [SerializeField] private float hipSpreadDegrees = 0.5f;
    [Tooltip("Dispersión apuntando, en grados (por eje)")]
    [SerializeField] private float aimSpreadDegrees = 0f;
    [Tooltip("Alcance en metros (sin caída de daño)")]
    [SerializeField] private float range = 80f;
    [Tooltip("Segundos mínimos entre disparos (un disparo por clic)")]
    [SerializeField] private float fireInterval = 0.45f;

    [Header("Munición")]
    [Tooltip("Balas en el tambor: 5, los huecos que tiene el dibujo de la recarga (3C.5.2)")]
    [SerializeField] private int magazineSize = 5;
    [SerializeField] private int loadedRounds = 5;
    [SerializeField] private int reserveRounds = 15;
    [Tooltip("Máximo de reserva")]
    [SerializeField] private int maxReserve = 24;

    [Header("Recarga bala por bala (3C.5.2)")]
    [Tooltip("Segundos para abrir el tambor")]
    [SerializeField] private float openSeconds = 0.35f;
    [Tooltip("Segundos por cada bala que se mete")]
    [SerializeField] private float insertSeconds = 0.3f;
    [Tooltip("Segundos para cerrar el tambor")]
    [SerializeField] private float closeSeconds = 0.35f;

    [Header("Audio (opcional; sin clip se usan los SFX de la escopeta de la librería, no hay de revólver)")]
    [SerializeField] private AudioClip fireSound;
    [SerializeField] private AudioClip emptySound;
    [SerializeField] private AudioClip reloadSound;
    [Tooltip("Sin SFX propio de revólver se usa el disparo de la escopeta más agudo (como Centavo en 3C.2)")]
    [SerializeField] private float libraryFirePitch = 1.4f;

    [Header("Efectos")]
    [SerializeField] private GameObject bulletHolePrefab;
    [SerializeField] private float bulletHoleDuration = 10f;

    [Header("Referencia a la cámara")]
    [SerializeField] private Camera playerCamera;

    public event System.Action OnFired;
    public event System.Action OnReloadStarted;
    public event System.Action OnReloadFinished;
    public event System.Action OnDryFire;
    public event System.Action OnReloadOpened;
    public event System.Action OnRoundInserting;
    public event System.Action<int> OnRoundLoaded;
    public event System.Action OnReloadClosing;

    private AudioSource _audioSource;
    private PlayerAim _aim;
    private PlayerHealth _health;
    private float _nextFireTime;
    private bool _isReloading;
    private Coroutine _reloadRoutine;
    private bool _fireQueued;
    private float _plannedReload = 1f;

    public bool IsReloading => _isReloading;
    /// <summary>Lo que dura la recarga en curso (o la próxima): abrir + balas que faltan (sin pasar de la reserva) + cerrar.</summary>
    public float ReloadDuration => _isReloading ? _plannedReload : PlanReload();
    public float RoundInsertSeconds => insertSeconds;
    public int LoadedRounds => loadedRounds;
    public int MagazineSize => magazineSize;
    public int ReserveRounds => reserveRounds;

    /// <summary>En las unidades de ShootPellet (offset por eje = s·0.01) para que la mira lo lea igual que la escopeta.</summary>
    public float SpreadAngle => Mathf.Tan(CurrentSpreadDegrees() * Mathf.Deg2Rad) * 100f;

    private void Awake()
    {
        TryGetComponent(out _audioSource);
        _aim = GetComponentInParent<PlayerAim>();
        _health = GetComponentInParent<PlayerHealth>();
        if (playerCamera == null) playerCamera = GetComponentInParent<Camera>();
        if (playerCamera == null) playerCamera = Camera.main;
    }

    private void OnDisable()
    {
        CancelReload();
    }

    /// <summary>Munición al recogerlo (WeaponPickup → WeaponSwitcher.GiveRevolver).</summary>
    public void SetAmmo(int loaded, int reserve)
    {
        loadedRounds = Mathf.Clamp(loaded, 0, magazineSize);
        reserveRounds = Mathf.Clamp(reserve, 0, maxReserve);
    }

    private bool Blocked()
    {
        return PlayerAim.GameplayBlocked() || (_health != null && _health.IsDead) || playerCamera == null;
    }

    private void Update()
    {
        if (Blocked()) return;

        // Disparar durante la recarga: termina la bala actual, cierra y dispara (VM2)
        if (_isReloading && Input.GetButtonDown("Fire1")) _fireQueued = true;

        if (Input.GetButtonDown("Fire1") && !_isReloading && Time.time >= _nextFireTime)
        {
            if (loadedRounds > 0) Fire();
            else if (reserveRounds > 0) StartReload();
            else
            {
                OnDryFire?.Invoke();
                if (emptySound != null) PlaySound(emptySound); else AudioLibrary.PlaySfx2D("escopeta_vacia");
                _nextFireTime = Time.time + 0.2f;
            }
        }

        if (Input.GetKeyDown(KeyCode.R) && !_isReloading && loadedRounds < magazineSize && reserveRounds > 0)
            StartReload();
    }

    private void Fire()
    {
        _nextFireTime = Time.time + fireInterval;
        loadedRounds--;

        if (fireSound != null) PlaySound(fireSound);
        else PlayLibraryShot();

        ShootBullet();
        OnFired?.Invoke();

        // Tambor vacío con reserva: recarga sola tras una pausa corta (como la escopeta)
        if (loadedRounds <= 0 && reserveRounds > 0) StartCoroutine(AutoReload());
    }

    private IEnumerator AutoReload()
    {
        yield return new WaitForSeconds(0.3f);
        if (isActiveAndEnabled && !_isReloading && loadedRounds <= 0 && reserveRounds > 0 && !Blocked()) StartReload();
    }

    private void ShootBullet()
    {
        Transform cam = playerCamera.transform;
        float offset = Mathf.Tan(CurrentSpreadDegrees() * Mathf.Deg2Rad);
        Vector3 direction = cam.forward
                            + cam.right * Random.Range(-offset, offset)
                            + cam.up * Random.Range(-offset, offset);
        direction.Normalize();

        if (!Physics.Raycast(new Ray(cam.position, direction), out RaycastHit hit, range)) return;

        EnemyHealth enemyHealth = hit.collider.GetComponentInParent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage);
            return;
        }
        if (TryGetDamageable(hit.collider, out IDamageable target))
        {
            target.TakeDamage(damage);
            return;
        }
        if (bulletHolePrefab != null)
        {
            GameObject hole = Instantiate(bulletHolePrefab, hit.point + hit.normal * 0.01f, Quaternion.LookRotation(-hit.normal));
            Destroy(hole, bulletHoleDuration);
        }
    }

    private float CurrentSpreadDegrees()
    {
        return _aim != null && _aim.IsAiming ? aimSpreadDegrees : hipSpreadDegrees;
    }

    /// <summary>Igual que ShotgunController.TryGetDamageable (comparación de Unity si es un componente).</summary>
    private static bool TryGetDamageable(Collider collider, out IDamageable target)
    {
        target = collider.GetComponentInParent<IDamageable>();
        if (target is Object unityObject) return unityObject != null;
        return target != null;
    }

    // ============ Recarga ============

    private void StartReload()
    {
        if (_isReloading || loadedRounds >= magazineSize || reserveRounds <= 0) return;
        _reloadRoutine = StartCoroutine(Reload());
    }

    private float PlanReload()
    {
        int missing = Mathf.Max(0, Mathf.Min(magazineSize - loadedRounds, reserveRounds));
        return openSeconds + missing * insertSeconds + closeSeconds;
    }

    private IEnumerator Reload()
    {
        _isReloading = true;
        _fireQueued = false;
        _plannedReload = PlanReload();
        OnReloadStarted?.Invoke();

        // Abrir
        OnReloadOpened?.Invoke();
        AudioLibrary.PlaySfx2D("escopeta_bombeo");
        yield return new WaitForSeconds(openSeconds);

        // Una bala por cada una que falta, nunca más que la reserva. Cada bala cuenta al entrar (si se corta, se queda)
        while (loadedRounds < magazineSize && reserveRounds > 0)
        {
            OnRoundInserting?.Invoke();
            yield return new WaitForSeconds(insertSeconds);
            loadedRounds++;
            reserveRounds--;
            if (reloadSound != null) PlaySound(reloadSound); else AudioLibrary.PlaySfx2D("escopeta_cargar");
            OnRoundLoaded?.Invoke(loadedRounds);
            if (_fireQueued) break; // termina esta bala, cierra y dispara
        }

        // Cerrar
        OnReloadClosing?.Invoke();
        yield return new WaitForSeconds(closeSeconds);

        _isReloading = false;
        _reloadRoutine = null;
        OnReloadFinished?.Invoke();

        bool fire = _fireQueued;
        _fireQueued = false;
        if (fire && loadedRounds > 0 && !Blocked()) Fire();
    }

    /// <summary>Cambio de arma: corta la recarga; las balas que ya entraron se quedan. No avisa OnReloadFinished.</summary>
    public void CancelReload()
    {
        _fireQueued = false;
        if (!_isReloading) return;
        if (_reloadRoutine != null) StopCoroutine(_reloadRoutine);
        _reloadRoutine = null;
        _isReloading = false;
    }

    // ============ Audio ============

    private void PlayLibraryShot()
    {
        AudioClip clip = AudioLibrary.GetSfx("escopeta_disparo");
        if (clip == null || _audioSource == null) return;
        _audioSource.pitch = libraryFirePitch;
        _audioSource.PlayOneShot(clip, 0.8f);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || _audioSource == null) return;
        _audioSource.pitch = 1f;
        _audioSource.PlayOneShot(clip);
    }
}
