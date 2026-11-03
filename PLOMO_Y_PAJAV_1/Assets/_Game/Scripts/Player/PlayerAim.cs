using System;
using UnityEngine;

/// <summary>
/// Apuntar con la mira (mantener clic derecho).
/// DÓNDE VA: GameObject "Player", junto a PlayerController (lo añade "Crear Prefabs de Prueba").
///
/// - IsAiming / OnAimChanged: lo leen el viewmodel, la escopeta (menos dispersión), PlayerController
///   (más lento, sin correr) y la UI (oculta la mira).
/// - AimBlend: 0 = cadera, 1 = apuntando; avanza en aimInTime / aimOutTime (los pone el perfil del arma).
/// - El FOV de la cámara baja fovDelta grados con el mismo tiempo (FOV base leído en Start).
/// - No se apunta mientras recarga, mientras corre (si ya corría), en pausa, en diálogo ni muerto.
///   La recarga cancela el apuntado.
/// </summary>
public class PlayerAim : MonoBehaviour
{
    [Tooltip("Vacío = Camera.main")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float aimInTime = 0.15f;
    [SerializeField] private float aimOutTime = 0.12f;
    [SerializeField] private float fovDelta = 15f;

    public bool IsAiming { get; private set; }
    public float AimBlend { get; private set; }
    public event Action<bool> OnAimChanged;

    private PlayerController _player;
    private IWeapon _weapon;
    private WeaponSwitcher _switcher;
    private float _baseFov = -1f;
    private float _suppressUntil = -1f;
    private bool _dead;

    private void Awake()
    {
        TryGetComponent(out _player);
        TryGetComponent(out _switcher);
        _weapon = GetComponentInChildren<IWeapon>(true);
        if (TryGetComponent(out PlayerHealth health)) health.OnDeath += HandleDeath;
        OnAimChanged += UpdateCrosshair;
    }

    /// <summary>La mira del HUD se oculta mientras se apunta (si existe).</summary>
    private static void UpdateCrosshair(bool aiming)
    {
        if (UIManager.Instance != null) UIManager.Instance.SetCrosshairHiddenByAim(aiming);
    }

    private void Start()
    {
        if (playerCamera == null)
        {
            // La cámara del mundo, no la ViewmodelCamera (FOV fijo): el zoom de apuntar solo toca la principal
            foreach (Camera cam in GetComponentsInChildren<Camera>(true))
                if (cam.name != ViewmodelCameraRig.CameraName) { playerCamera = cam; break; }
        }
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera != null) _baseFov = playerCamera.fieldOfView;
    }

    private void OnDisable()
    {
        SetAiming(false);
        AimBlend = 0f;
        ApplyFov();
    }

    /// <summary>Arma activa (fase 2: cambio de arma). La recarga de esta arma cancela el apuntado.</summary>
    public void SetWeapon(IWeapon weapon)
    {
        _weapon = weapon;
    }

    /// <summary>Tiempos y zoom del arma activa (ViewmodelProfile).</summary>
    public void SetTiming(float inTime, float outTime, float fovDeltaDegrees)
    {
        aimInTime = Mathf.Max(0.01f, inTime);
        aimOutTime = Mathf.Max(0.01f, outTime);
        fovDelta = fovDeltaDegrees;
    }

    /// <summary>Sale del apuntado durante unos segundos (ej: bombear sin dibujo de "bombear apuntando").
    /// Si el botón sigue apretado, vuelve a apuntar solo.</summary>
    public void SuppressFor(float seconds)
    {
        _suppressUntil = Mathf.Max(_suppressUntil, Time.unscaledTime + seconds);
    }

    /// <summary>Pausa, diálogo/interrogatorio o muerte (estados que ya existen en el juego).</summary>
    public static bool GameplayBlocked()
    {
        GameManager gm = GameManager.Instance;
        if (gm != null && (gm.IsGamePaused || gm.IsGameOver)) return true;
        if (InterrogationDirector.IsActive) return true;    // escena de interrogatorio (fase 3C.3)
        if (CinematicState.IsPlaying) return true;          // taxi y llegada (INTRO.2)
        // Sin Time.timeScale: CartoonFx lo pone en 0 un instante en cada golpe (hitstop)
        return DialogueUI.Exists && DialogueUI.Instance.IsOpen;
    }

    /// <summary>
    /// ¿Las armas no responden? GameplayBlocked() + arma enfundada o sacándose (INTRO.3). Lo leen ShotgunController,
    /// RevolverController, WeaponSwitcher y el apuntado. Los guardias NO lo usan (pueden dispararle a un Wilbur desarmado):
    /// EnemyAI sigue con GameplayBlocked().
    /// </summary>
    public static bool WeaponInputBlocked() => GameplayBlocked() || PlayerWeaponHolster.BlocksWeapons;

    private void HandleDeath() => _dead = true;

    private void Update()
    {
        bool reloading = (_weapon != null && _weapon.IsReloading) || (_switcher != null && _switcher.IsSwitching);
        bool runningStart = !IsAiming && _player != null && _player.IsRunning && _player.IsMoving;
        bool want = Input.GetMouseButton(1)
                    && !reloading && !runningStart && !_dead
                    && Time.unscaledTime >= _suppressUntil
                    && !WeaponInputBlocked(); // INTRO.3: enfundada no se apunta
        SetAiming(want);

        float duration = IsAiming ? aimInTime : aimOutTime;
        AimBlend = Mathf.MoveTowards(AimBlend, IsAiming ? 1f : 0f, ViewmodelTime.Delta / duration);
        ApplyFov();
    }

    private void SetAiming(bool aiming)
    {
        if (aiming == IsAiming) return;
        IsAiming = aiming;
        OnAimChanged?.Invoke(aiming);
    }

    private void ApplyFov()
    {
        if (playerCamera == null || _baseFov <= 0f) return;
        float t = Mathf.SmoothStep(0f, 1f, AimBlend);
        playerCamera.fieldOfView = Mathf.Lerp(_baseFov, _baseFov - fovDelta, t);
    }
}
