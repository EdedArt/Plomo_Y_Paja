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
public class ShotgunController : MonoBehaviour
{
    [Header("Configuración del arma")]
    [Tooltip("Daño por perdigón — la escopeta dispara varios a la vez")]
    [SerializeField] private float damagePerPellet = 15f;

    [Tooltip("Número de perdigones por disparo (escopeta: entre 6 y 12)")]
    [SerializeField] private int pelletsPerShot = 8;

    [Tooltip("Ángulo de dispersión en grados (más alto = más imprecisa)")]
    [SerializeField] private float spreadAngle = 5f;

    [Tooltip("Alcance máximo del disparo en metros")]
    [SerializeField] private float range = 30f;

    [Header("Munición")]
    [Tooltip("Balas en el cargador actual")]
    [SerializeField] private int currentAmmo = 6;

    [Tooltip("Capacidad máxima del cargador")]
    [SerializeField] private int maxAmmoInMag = 6;

    [Tooltip("Balas de reserva totales")]
    [SerializeField] private int reserveAmmo = 18;

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

    /// <summary>Segundos tras el disparo en que suena el bombeo de la corredera.</summary>
    private const float PumpDelay = 0.35f;

    // ============ Estado interno ============
    private bool _canFire = true;
    private bool _isReloading = false;
    private Animator _animator;

    // ============ Hashes del Animator (más eficiente que strings) ============
    private static readonly int FireHash = Animator.StringToHash("Fire");
    private static readonly int ReloadHash = Animator.StringToHash("Reload");

    private void Awake()
    {
        // TryGetComponent: si falta el componente, GetComponent devuelve un "null falso" de Unity
        // con el que "?." no funciona
        TryGetComponent(out _animator);
        TryGetComponent(out _audioSource);

        // Si no se asignó la cámara en el Inspector, buscarla automáticamente
        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    private void Start()
    {
        // Notificar la UI del estado inicial
        onAmmoChanged?.Invoke(currentAmmo, reserveAmmo);
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
            else
            {
                // Clic de gatillo vacío
                OnDryFire?.Invoke();
                if (!AudioLibrary.PlaySfx2D("escopeta_vacia")) PlaySound(emptySound);
            }
        }

        // Recarga manual: tecla R
        if (Input.GetKeyDown(KeyCode.R) && !_isReloading && currentAmmo < maxAmmoInMag && reserveAmmo > 0)
        {
            StartCoroutine(Reload());
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
        onAmmoChanged?.Invoke(currentAmmo, reserveAmmo);
        OnFired?.Invoke();

        // 4. Auto-recarga si el cargador quedó vacío y hay reserva
        if (currentAmmo <= 0 && reserveAmmo > 0)
        {
            yield return new WaitForSeconds(0.3f); // Pequeña pausa antes de recargar
            StartCoroutine(Reload());
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
        shootDirection += new Vector3(
            Random.Range(-spreadAngle, spreadAngle) * 0.01f,
            Random.Range(-spreadAngle, spreadAngle) * 0.01f,
            0f
        );
        shootDirection.Normalize();

        Ray ray = new Ray(playerCamera.transform.position, shootDirection);

        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            // ¿Impactó a un enemigo?
            EnemyHealth enemyHealth = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null)
            {
                bool killed = enemyHealth.TakeDamage(damagePerPellet);
                if (killed) onEnemyKilled?.Invoke();
            }

            // ¿Impactó en una superficie (pared, suelo)?
            else if (bulletHolePrefab != null)
            {
                SpawnBulletHole(hit);
            }
        }
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

    /// <summary>
    /// Recarga la escopeta. Toma reloadTime segundos.
    /// </summary>
    private IEnumerator Reload()
    {
        _isReloading = true;
        if (_animator != null) _animator.SetTrigger(ReloadHash);
        OnReloadStarted?.Invoke();

        // Un "clac" por cartucho repartido en el tiempo de recarga
        int shells = Mathf.Max(1, Mathf.Min(maxAmmoInMag - currentAmmo, reserveAmmo));
        float interval = reloadTime / shells;
        for (int i = 0; i < shells; i++)
        {
            yield return new WaitForSeconds(interval);
            if (!AudioLibrary.PlaySfx2D("escopeta_cargar") && i == 0) PlaySound(reloadSound);
        }

        // Calcular cuántas balas se necesitan y si hay suficientes en reserva
        int ammoNeeded = maxAmmoInMag - currentAmmo;
        int ammoToLoad = Mathf.Min(ammoNeeded, reserveAmmo);

        currentAmmo += ammoToLoad;
        reserveAmmo -= ammoToLoad;

        onAmmoChanged?.Invoke(currentAmmo, reserveAmmo);
        _isReloading = false;
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
    public int ReserveAmmo => reserveAmmo;
    public bool IsReloading => _isReloading;

    /// <summary>
    /// Añadir balas a la reserva (cuando el jugador recoge munición).
    /// </summary>
    public void AddAmmo(int amount)
    {
        reserveAmmo = Mathf.Min(reserveAmmo + amount, 30); // Máximo 30 en reserva
        onAmmoChanged?.Invoke(currentAmmo, reserveAmmo);
    }
}
