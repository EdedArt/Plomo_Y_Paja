using System.Collections;
using UnityEngine;

/// <summary>
/// Revólver de Centavo, segunda arma de Wilbur (fase 3C.5, D13–D15). Mínimo de VM2: una bala hitscan, semiautomático,
/// tambor de 6 y recarga completa (~2.2 s) con los 3 cuadros de VM_Revolver (abrir, meter, cerrar).
/// DÓNDE VA: GameObject "Revolver" hijo de la cámara del Player, junto a la escopeta (lo crea "Crear Prefabs de Prueba").
/// Empieza SIN dueño: WeaponSwitcher.GiveRevolver (WeaponPickup, el revólver que suelta Centavo) lo entrega.
///
/// DAÑO: el mismo camino que ShotgunController.ShootPellet: primero EnemyHealth en los padres del collider y, solo si no
/// hay, IDamageable (Centavo, D3). Sin caída por distancia (D14: arma de lejos). 24 = 40 % de los 60 de un guardia (D15):
/// guardia 60 → 36 → 12 → muere al 3.er tiro; Colmillo (se rinde a ≤ 18) 60 → 36 → 12 = se rinde al 2.º.
/// BLOQUEO: no dispara ni recarga en pausa, diálogo, interrogatorio (InterrogationDirector.IsActive) ni muerto.
/// </summary>
public class RevolverController : MonoBehaviour, IWeapon
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
    [Tooltip("Balas en el tambor (Centavo \"sabe contar hasta seis\")")]
    [SerializeField] private int magazineSize = 6;
    [SerializeField] private int loadedRounds = 6;
    [SerializeField] private int reserveRounds = 12;
    [Tooltip("Máximo de reserva")]
    [SerializeField] private int maxReserve = 24;

    [Header("Recarga")]
    [Tooltip("Segundos de la recarga completa (abrir, meter balas, cerrar)")]
    [SerializeField] private float reloadTime = 2.2f;

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

    private AudioSource _audioSource;
    private PlayerAim _aim;
    private PlayerHealth _health;
    private float _nextFireTime;
    private bool _isReloading;
    private Coroutine _reloadRoutine;

    public bool IsReloading => _isReloading;
    public float ReloadDuration => reloadTime;
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
        if (_isReloading) return;
        _reloadRoutine = StartCoroutine(Reload());
    }

    private IEnumerator Reload()
    {
        _isReloading = true;
        OnReloadStarted?.Invoke();
        if (reloadSound != null) PlaySound(reloadSound); else AudioLibrary.PlaySfx2D("escopeta_cargar");

        yield return new WaitForSeconds(reloadTime);

        int load = Mathf.Min(magazineSize - loadedRounds, reserveRounds);
        loadedRounds += load;
        reserveRounds -= load;
        _isReloading = false;
        _reloadRoutine = null;
        OnReloadFinished?.Invoke();
    }

    /// <summary>Cambio de arma: corta la recarga sin cargar balas. No avisa OnReloadFinished.</summary>
    public void CancelReload()
    {
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
