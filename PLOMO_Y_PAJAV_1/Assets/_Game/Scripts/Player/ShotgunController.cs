using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controla la escopeta de Wilbur: disparo, recarga, munición y efectos visuales.
/// DÓNDE VA: GameObject hijo de la Main Camera, llamado "Shotgun".
///
/// SETUP EN INSPECTOR:
/// - Main Camera
///   └── Shotgun (GameObject)
///       ├── ShotgunController (este script)
///       ├── Animator (para animaciones de disparo y recarga)
///       └── AudioSource
///
/// CÓMO FUNCIONA:
/// La escopeta usa RAYCAST, no proyectiles físicos.
/// Dispara múltiples "pellets" (perdigones) con dispersión aleatoria.
/// Eso da el efecto scatter característico de la escopeta.
/// </summary>
public class ShotgunController : MonoBehaviour, IWeapon
{
    [Header("Configuración del arma")]
    [Tooltip("Daño por perdigón — la escopeta dispara varios a la vez")]
    [SerializeField] private float damagePerPellet = 15f;

    [Tooltip("Número de perdigones por disparo (escopeta: entre 6 y 12)")]
    [SerializeField] private int pelletsPerShot = 8;

    [Tooltip("Dispersión (unidades de ShootPellet: cada eje se desvía hasta spread·0.01). 3C.5 (D14): 7.5 = ×1.5 del 5 original")]
    [SerializeField] private float spreadAngle = 7.5f;

    [Tooltip("Multiplicador de la dispersión mientras se apunta (clic derecho, PlayerAim)")]
    [SerializeField] private float aimSpreadMultiplier = 0.6f;

    [Tooltip("Alcance máximo del disparo en metros")]
    [SerializeField] private float range = 30f;

    [Header("Caída del daño con la distancia (3C.5, D14: la escopeta es de corta distancia)")]
    [Tooltip("Hasta esta distancia (m) cada perdigón hace el 100 % del daño")]
    [SerializeField] private float falloffStart = 6f;
    [Tooltip("A esta distancia (m) el perdigón hace falloffEndMultiplier del daño (bajada lineal desde falloffStart)")]
    [SerializeField] private float falloffEnd = 20f;
    [SerializeField, Range(0f, 1f)] private float falloffEndMultiplier = 0.25f;
    [Tooltip("Desde falloffEnd el daño sigue bajando en línea hasta 0 a esta distancia (m)")]
    [SerializeField] private float zeroDamageDistance = 30f;

    [Header("Munición")]
    [Tooltip("Balas en el cargador actual")]
    [SerializeField] private int currentAmmo = 6;

    [Tooltip("Capacidad máxima del cargador")]
    [SerializeField] private int maxAmmoInMag = 6;

    [Tooltip("Reserva SOLO si el Player no tiene PlayerAmmo (HUD.1, H4: la reserva vive en PlayerAmmo, que empieza con 24 desde la 3C.6)")]
    [SerializeField] private int reserveAmmo = 24;

    [Header("Tiempos")]
    [Tooltip("Segundos entre disparos")]
    [SerializeField] private float fireRate = 1.2f;

    [Tooltip("Segundos que tarda la recarga")]
    [SerializeField] private float reloadTime = 2.5f;

    [Header("Efectos visuales")]
    [Tooltip("Partículas del fogonazo en el cañón")]
    [SerializeField] private ParticleSystem muzzleFlash;

    [Tooltip("Prefab del agujero de bala en la pared")]
    [SerializeField] private GameObject bulletHolePrefab;

    [Tooltip("Cuántos segundos duran los agujeros en la pared")]
    [SerializeField] private float bulletHoleDuration = 10f;

    [Header("Audio")]
    [SerializeField] private AudioClip fireSound;
    [SerializeField] private AudioClip reloadSound;
    [SerializeField] private AudioClip emptySound; // Clic cuando no hay balas
    private AudioSource _audioSource;

    [Header("Referencia a la cámara")]
    [Tooltip("La Main Camera — para hacer el raycast desde el centro de la pantalla")]
    [SerializeField] private Camera playerCamera;

    [Header("Eventos (conectar en Inspector)")]
    [Tooltip("Se llama cuando la munición cambia — conectar a UIManager.UpdateAmmoUI")]
    public UnityEvent<int, int> onAmmoChanged; // (currentAmmo, reserveAmmo)

    [Tooltip("Se llama cuando el jugador mata a un enemigo")]
    public UnityEvent onEnemyKilled;

    // ============ Eventos de código (viewmodel, efectos, audio) ============
    // System.Action con nombre completo: "using System" chocaría con UnityEngine.Random
    public event System.Action OnFired;
    public event System.Action OnReloadStarted;
    public event System.Action OnReloadFinished;
    public event System.Action OnDryFire;
    /// <summary>Cambiaron los cartuchos cargados (HUD.1, IWeapon).</summary>
    public event System.Action OnAmmoChanged;

    /// <summary>Segundos tras el disparo en que suena el bombeo de la corredera.</summary>
    private const float PumpDelay = 0.35f;

    // ============ Estado interno ============
    private bool _canFire = true;
    private bool _isReloading = false;
    private Coroutine _reloadRoutine;
    private Animator _animator;
    private PlayerAim _aim;
    private PlayerAmmo _ammo; // HUD.1 (H4): la reserva vive aquí

    // ============ Hashes del Animator (más eficiente que strings) ============
    private static readonly int FireHash = Animator.StringToHash("Fire");
    private static readonly int ReloadHash = Animator.StringToHash("Reload");

    private void Awake()
    {
        // TryGetComponent: si falta el componente, GetComponent devuelve un "null falso" de Unity
        // con el que "?." no funciona
        TryGetComponent(out _animator);
        TryGetComponent(out _audioSource);
        _aim = GetComponentInParent<PlayerAim>();
        _ammo = GetComponentInParent<PlayerAmmo>();

        // Si no se asignó la cámara en el Inspector, buscarla automáticamente
        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    private void Start()
    {
        // Notificar la UI del estado inicial
        NotifyAmmo();
    }

    private void Update()
    {
        HandleInput();
    }

    private void HandleInput()
    {
        // Disparo: clic izquierdo
        if (Input.GetButtonDown("Fire1") && _canFire && !_isReloading)
        {
            if (currentAmmo > 0)
            {
                StartCoroutine(Fire());
            }
            else if (Reserve > 0)
            {
                // Vacía con reserva (p. ej. se cambió de arma antes de la recarga automática): recargar
                StartReload();
            }
            else
            {
                // Clic de gatillo vacío
                OnDryFire?.Invoke();
                if (!AudioLibrary.PlaySfx2D("escopeta_vacia")) PlaySound(emptySound);
            }
        }

        // Recarga manual: tecla R
        if (Input.GetKeyDown(KeyCode.R) && !_isReloading && currentAmmo < maxAmmoInMag && Reserve > 0)
        {
            StartReload();
        }
    }

    /// <summary>
    /// Coroutine del disparo: efecto visual → raycast → daño → cooldown.
    /// Una coroutine es una función que puede "pausarse" y continuar después.
    /// </summary>
    private IEnumerator Fire()
    {
        _canFire = false;

        // 1. Efectos inmediatos
        if (muzzleFlash != null) muzzleFlash.Play(); // ?. no sirve con campos de Unity sin asignar
        if (!AudioLibrary.PlaySfx2D("escopeta_disparo")) PlaySound(fireSound);
        if (_animator != null) _animator.SetTrigger(FireHash);
        StartCoroutine(PlayPumpSound());

        // 2. Disparar cada perdigón como un raycast
        for (int i = 0; i < pelletsPerShot; i++)
        {
            ShootPellet();
        }

        // 3. Restar munición y actualizar UI
        currentAmmo--;
        NotifyAmmo();
        OnFired?.Invoke();

        // 4. Auto-recarga si el cargador quedó vacío y hay reserva
        if (currentAmmo <= 0 && Reserve > 0)
        {
            yield return new WaitForSeconds(0.3f); // Pequeña pausa antes de recargar
            // Si mientras tanto se cambió de arma (componente apagado) no recarga a escondidas: lo hace al volver
            if (isActiveAndEnabled && !_isReloading) StartReload();
        }

        // 5. Esperar antes de poder disparar de nuevo (fire rate)
        yield return new WaitForSeconds(fireRate);
        _canFire = true;
    }

    /// <summary>
    /// Dispara un solo perdigón con dispersión aleatoria.
    /// Usa raycast desde el centro de la pantalla hacia donde mira la cámara.
    /// </summary>
    private void ShootPellet()
    {
        // Calcular una dirección con dispersión aleatoria dentro del spreadAngle
        Vector3 shootDirection = playerCamera.transform.forward;
        float spread = spreadAngle * (_aim != null && _aim.IsAiming ? aimSpreadMultiplier : 1f);
        // La dispersión va en los ejes de la CÁMARA (derecha y arriba), no en los del mundo: el cono queda centrado
        // en la mira mirando hacia donde sea. Misma magnitud que antes (spread · 0.01 por eje).
        shootDirection += playerCamera.transform.right * (Random.Range(-spread, spread) * 0.01f)
                        + playerCamera.transform.up * (Random.Range(-spread, spread) * 0.01f);
        shootDirection.Normalize();

        Ray ray = new Ray(playerCamera.transform.position, shootDirection);

        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            // ¿Impactó a un enemigo?
            float damage = damagePerPellet * DamageMultiplierAt(hit.distance);
            EnemyHealth enemyHealth = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null)
            {
                bool killed = damage > 0f && enemyHealth.TakeDamage(damage);
                if (killed) onEnemyKilled?.Invoke();
            }

            // ¿Otro blanco que no es guardia? (Centavo, fase 3C.2). Solo se mira si no hubo EnemyHealth.
            else if (TryGetDamageable(hit.collider, out IDamageable target))
            {
                if (damage > 0f && target.TakeDamage(damage)) onEnemyKilled?.Invoke();
            }

            // ¿Impactó en una superficie (pared, suelo)?
            else if (bulletHolePrefab != null)
            {
                SpawnBulletHole(hit);
            }
        }
    }

    /// <summary>
    /// Multiplicador del daño por distancia (3C.5): 1 hasta falloffStart, baja en línea hasta falloffEndMultiplier en falloffEnd
    /// y de ahí en línea hasta 0 en zeroDamageDistance.
    /// </summary>
    public float DamageMultiplierAt(float distance)
    {
        if (distance <= falloffStart) return 1f;
        if (distance <= falloffEnd)
            return Mathf.Lerp(1f, falloffEndMultiplier, (distance - falloffStart) / Mathf.Max(0.01f, falloffEnd - falloffStart));
        if (distance >= zeroDamageDistance) return 0f;
        return Mathf.Lerp(falloffEndMultiplier, 0f, (distance - falloffEnd) / Mathf.Max(0.01f, zeroDamageDistance - falloffEnd));
    }

    /// <summary>Busca un IDamageable en el collider o sus padres (comparación de Unity si es un componente).</summary>
    private static bool TryGetDamageable(Collider collider, out IDamageable target)
    {
        target = collider.GetComponentInParent<IDamageable>();
        if (target is Object unityObject) return unityObject != null;
        return target != null;
    }

    /// <summary>
    /// Crear un agujero de bala en la superficie donde impactó.
    /// El agujero se destruye automáticamente después de bulletHoleDuration segundos.
    /// </summary>
    private void SpawnBulletHole(RaycastHit hit)
    {
        // Posicionar el decal ligeramente por encima de la superficie para evitar z-fighting
        Vector3 position = hit.point + hit.normal * 0.01f;
        Quaternion rotation = Quaternion.LookRotation(-hit.normal); // Apuntar hacia afuera de la superficie

        GameObject hole = Instantiate(bulletHolePrefab, position, rotation);
        Destroy(hole, bulletHoleDuration);
    }

    private void StartReload()
    {
        _reloadRoutine = StartCoroutine(Reload());
    }

    /// <summary>Cambio de arma (WeaponSwitcher): corta la recarga sin cargar cartuchos. No avisa OnReloadFinished.</summary>
    public void CancelReload()
    {
        if (!_isReloading) return;
        if (_reloadRoutine != null) StopCoroutine(_reloadRoutine);
        _reloadRoutine = null;
        _isReloading = false;
    }

    /// <summary>
    /// Recarga la escopeta. Toma reloadTime segundos.
    /// </summary>
    private IEnumerator Reload()
    {
        _isReloading = true;
        if (_animator != null) _animator.SetTrigger(ReloadHash);
        OnReloadStarted?.Invoke();

        // Un "clac" por cartucho repartido en el tiempo de recarga
        int shells = Mathf.Max(1, Mathf.Min(maxAmmoInMag - currentAmmo, Reserve));
        float interval = reloadTime / shells;
        for (int i = 0; i < shells; i++)
        {
            yield return new WaitForSeconds(interval);
            if (!AudioLibrary.PlaySfx2D("escopeta_cargar") && i == 0) PlaySound(reloadSound);
        }

        // Calcular cuántas balas se necesitan y si hay suficientes en reserva
        int ammoNeeded = maxAmmoInMag - currentAmmo;
        int ammoToLoad = TakeReserve(ammoNeeded); // de PlayerAmmo (o de reserveAmmo si no hay)

        currentAmmo += ammoToLoad;

        NotifyAmmo();
        _isReloading = false;
        _reloadRoutine = null;
        OnReloadFinished?.Invoke();
    }

    private IEnumerator PlayPumpSound()
    {
        yield return new WaitForSeconds(PumpDelay);
        AudioLibrary.PlaySfx2D("escopeta_bombeo");
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
            _audioSource.PlayOneShot(clip);
    }

    // ============ Getters para la UI ============
    public int CurrentAmmo => currentAmmo;
    public int ReserveAmmo => Reserve;
    public int LoadedRounds => currentAmmo;
    public int MagazineSize => maxAmmoInMag;
    public int ReserveRounds => Reserve;
    public AmmoType AmmoType => AmmoType.Shotgun;
    public AmmoDisplayStyle DisplayStyle => AmmoDisplayStyle.Shells;

    /// <summary>Reserva actual: la de PlayerAmmo (H4) o, sin PlayerAmmo, la propia.</summary>
    private int Reserve => _ammo != null ? _ammo.GetReserve(AmmoType.Shotgun) : reserveAmmo;

    private int TakeReserve(int amount)
    {
        if (_ammo != null) return _ammo.Take(AmmoType.Shotgun, amount);
        int taken = Mathf.Clamp(amount, 0, reserveAmmo);
        reserveAmmo -= taken;
        return taken;
    }

    private void NotifyAmmo()
    {
        onAmmoChanged?.Invoke(currentAmmo, Reserve);
        OnAmmoChanged?.Invoke();
    }
    public bool IsReloading => _isReloading;
    /// <summary>Dispersión actual (con el multiplicador de apuntar). Solo lectura, para la mira del HUD; ShootPellet no cambió.</summary>
    public float SpreadAngle => spreadAngle * (_aim != null && _aim.IsAiming ? aimSpreadMultiplier : 1f);
    /// <summary>Segundos que dura la recarga (el viewmodel sincroniza la bajada de manos con esto).</summary>
    public float ReloadDuration => reloadTime;

    /// <summary>
    /// Añadir balas a la reserva (cuando el jugador recoge munición).
    /// </summary>
    public void AddAmmo(int amount)
    {
        // HUD.1: el tope (30) lo pone PlayerAmmo; sin PlayerAmmo, el de siempre
        if (_ammo != null) _ammo.TryAdd(AmmoType.Shotgun, amount, out _);
        else reserveAmmo = Mathf.Min(reserveAmmo + amount, 30); // Máximo 30 en reserva
        NotifyAmmo();
    }
}
