using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Inventario mínimo de armas de Wilbur y cambio entre ellas (fase 3C.5, mínimo de VM2).
/// DÓNDE VA: GameObject "Player", junto a PlayerAim (lo añade "Crear Prefabs de Prueba").
///
/// - Tecla 1 = escopeta, 2 = revólver, rueda del mouse = la otra. Sin revólver (D13: solo se consigue recogiendo el que
///   suelta Centavo), la 2 y la rueda no hacen nada.
/// - Cambio = bajar las manos, cambiar perfil/arma a la mitad y subirlas (switchTime en total, tiempo real; reusa la bajada
///   de ViewmodelMotion). Durante el cambio las dos armas están apagadas: no se dispara ni se apunta. Cancela la recarga.
/// - No se cambia en pausa, diálogo, interrogatorio y su epílogo (PlayerAim.GameplayBlocked) ni muerto.
/// - Solo el arma activa tiene el componente encendido. Fuera de esos bloqueos se vuelve a imponer cada frame (por si el
///   diálogo o PlayerHealth.ResetHealth encendieron la escopeta estando el revólver en la mano).
/// - Al recoger el revólver por primera vez se cambia solo a él (WeaponPickup → GiveRevolver).
/// </summary>
public class WeaponSwitcher : MonoBehaviour
{
    private const int Shotgun = 0;
    private const int Revolver = 1;

    [SerializeField] private ShotgunController shotgun;
    [SerializeField] private RevolverController revolver;
    [Tooltip("Vacío = el perfil con el que arranca FirstPersonViewmodel")]
    [SerializeField] private ViewmodelProfile shotgunProfile;
    [SerializeField] private ViewmodelProfile revolverProfile;
    [Tooltip("Segundos del cambio completo (bajar + subir)")]
    [SerializeField] private float switchTime = 0.35f;

    [Header("Revólver al recogerlo (D13)")]
    [SerializeField] private int revolverLoaded = 5;
    [SerializeField] private int revolverReserve = 15;

    [Header("Depuración (apagada por defecto)")]
    [Tooltip("Empieza con el revólver (solo para probar sin pasar por Centavo)")]
    [SerializeField] private bool debug = false;

    private FirstPersonViewmodel _viewmodel;
    private RubberHoseCrosshair _crosshair;
    private PlayerHealth _health;
    private int _active = Shotgun;
    private bool _hasRevolver;
    private bool _switching;

    /// <summary>Cambió el arma activa (mitad del cambio).</summary>
    public event System.Action<IWeapon> OnWeaponChanged;

    public bool HasRevolver => _hasRevolver;
    public bool IsSwitching => _switching;
    public IWeapon ActiveWeapon => Weapon(_active);

    private void Awake()
    {
        if (shotgun == null) shotgun = GetComponentInChildren<ShotgunController>(true);
        if (revolver == null) revolver = GetComponentInChildren<RevolverController>(true);
        _viewmodel = GetComponentInChildren<FirstPersonViewmodel>(true);
        TryGetComponent(out _crosshair);
        TryGetComponent(out _health);
        if (shotgunProfile == null && _viewmodel != null) shotgunProfile = _viewmodel.Profile;
        if (revolver != null) revolver.enabled = false; // empieza sin revólver
    }

    private void Start()
    {
        GameStateTracker tracker = GameStateTracker.Instance;
        bool carried = tracker != null && tracker.HasRevolverFromEarlierScene(SceneManager.GetActiveScene().name);
        if ((carried || debug) && revolver != null)
        {
            _hasRevolver = true;
            revolver.SetAmmo(revolverLoaded, revolverReserve); // cada nivel empieza con la munición llena, como la escopeta
        }
        Activate(Shotgun);
    }

    private bool Blocked() => PlayerAim.GameplayBlocked() || (_health != null && _health.IsDead);

    private void Update()
    {
        if (_switching || Blocked()) return;
        EnforceEnabled();

        int wanted = -1;
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) wanted = Shotgun;
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) wanted = Revolver;
        else if (Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f) wanted = _active == Shotgun ? Revolver : Shotgun;

        if (wanted >= 0) RequestSwitch(wanted);
    }

    /// <summary>Entrega el revólver de Centavo. La primera vez cambia a él. Devuelve false si no hay revólver en el Player.</summary>
    public bool GiveRevolver()
    {
        if (revolver == null) return false;
        bool first = !_hasRevolver;
        _hasRevolver = true;
        if (first) revolver.SetAmmo(revolverLoaded, revolverReserve);
        if (GameStateTracker.Instance != null)
            GameStateTracker.Instance.RegisterRevolverObtained(SceneManager.GetActiveScene().name);
        if (first) RequestSwitch(Revolver);
        return true;
    }

    private void RequestSwitch(int index)
    {
        if (index == _active || _switching || Weapon(index) == null) return;
        if (index == Revolver && !_hasRevolver) return;
        StartCoroutine(Switch(index));
    }

    private IEnumerator Switch(int index)
    {
        _switching = true;
        IWeapon current = Weapon(_active);
        if (current != null) current.CancelReload();
        SetEnabled(Shotgun, false);
        SetEnabled(Revolver, false);
        if (_viewmodel != null) _viewmodel.PlaySwitchDip(switchTime);

        yield return new WaitForSecondsRealtime(switchTime * 0.5f); // manos abajo: cambiar el dibujo
        ApplyActive(index);
        yield return new WaitForSecondsRealtime(switchTime * 0.5f); // manos arriba

        _switching = false;
        if (!Blocked()) EnforceEnabled(); // si se bloqueó mientras tanto, se enciende al terminar el bloqueo
    }

    /// <summary>Sin animación (al empezar el nivel).</summary>
    private void Activate(int index)
    {
        ApplyActive(index);
        // INTRO.2.1: si el nivel arranca bloqueado (taxi), no se vuelve a encender la escopeta que la cinemática apagó en
        // su Start (el orden entre los dos Start no está garantizado). Update lo impone al terminar el bloqueo.
        if (!Blocked()) EnforceEnabled();
    }

    private void ApplyActive(int index)
    {
        _active = index;
        IWeapon weapon = Weapon(index);
        if (_viewmodel != null)
        {
            _viewmodel.SetWeapon(weapon); // también PlayerAim.SetWeapon
            ViewmodelProfile profile = index == Revolver ? revolverProfile : shotgunProfile;
            if (profile != null) _viewmodel.SetProfile(profile);
        }
        else if (TryGetComponent(out PlayerAim aim)) aim.SetWeapon(weapon);
        if (_crosshair != null) _crosshair.SetWeapon(weapon);
        OnWeaponChanged?.Invoke(weapon);
    }

    private void EnforceEnabled()
    {
        SetEnabled(Shotgun, _active == Shotgun);
        SetEnabled(Revolver, _active == Revolver && _hasRevolver);
    }

    private void SetEnabled(int index, bool on)
    {
        MonoBehaviour behaviour = index == Shotgun ? (MonoBehaviour)shotgun : revolver;
        if (behaviour != null && behaviour.enabled != on) behaviour.enabled = on;
    }

    private IWeapon Weapon(int index)
    {
        if (index == Shotgun) return shotgun != null ? shotgun : null;
        return revolver != null ? revolver : null;
    }
}
