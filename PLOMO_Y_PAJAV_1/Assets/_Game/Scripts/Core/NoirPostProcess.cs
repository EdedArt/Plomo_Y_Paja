using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Post-procesado del juego: look Technicolor de los años 30 (a todo color, cálido)
/// y pulso rojo en los bordes de la pantalla al recibir daño.
/// DÓNDE VA: "Volume_Technicolor" en la escena (lo crea el LevelBuilder), junto al componente Volume.
///
/// En Play sustituye el perfil del Volume por uno propio con los MISMOS valores que
/// Assets/_Game/Settings/TechnicolorVolumeProfile.asset, para poder animar la viñeta sin tocar el asset.
/// El nombre "Noir" se mantiene para no romper referencias.
/// </summary>
[RequireComponent(typeof(Volume))]
public class NoirPostProcess : MonoBehaviour
{
    [Header("Pulso al recibir daño")]
    [SerializeField] private Color damageVignetteColor = new Color32(0xB3, 0x00, 0x1B, 0xFF);
    [SerializeField] private float damageVignetteIntensity = 0.45f;
    [SerializeField] private float damageVignetteDuration = 0.25f;

    // Valores base: deben coincidir con NoirVolumeBuilder.GetOrCreateTechnicolorProfile
    private const float BaseSaturation = 20f;
    private const float BaseVignetteIntensity = 0.2f;

    private Volume _volume;
    private VolumeProfile _profile;
    private PlayerHealth _playerHealth;
    private Coroutine _pulseRoutine;

    private ColorAdjustments _colorAdj;
    private Vignette _vignette;

    private void Awake()
    {
        _volume = GetComponent<Volume>();

        _profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _volume.profile = _profile;
        _volume.isGlobal = true;

        SetupTechnicolorEffects();
    }

    private void Start()
    {
        // Se conecta solo al daño del jugador (antes había que hacerlo a mano en el Inspector)
        _playerHealth = FindObjectOfType<PlayerHealth>();
        if (_playerHealth != null && _playerHealth.onDamageTaken != null)
            _playerHealth.onDamageTaken.AddListener(TriggerDamagePulse);
    }

    private void OnDestroy()
    {
        if (_playerHealth != null && _playerHealth.onDamageTaken != null)
            _playerHealth.onDamageTaken.RemoveListener(TriggerDamagePulse);
        if (_profile != null) Destroy(_profile);
    }

    private void SetupTechnicolorEffects()
    {
        Tonemapping tonemapping = _profile.Add<Tonemapping>();
        tonemapping.mode.Override(TonemappingMode.Neutral);

        _colorAdj = _profile.Add<ColorAdjustments>();
        _colorAdj.postExposure.Override(0.4f);
        _colorAdj.contrast.Override(10f);
        _colorAdj.saturation.Override(BaseSaturation);
        _colorAdj.colorFilter.Override(new Color32(0xFF, 0xF4, 0xE6, 0xFF));

        WhiteBalance whiteBalance = _profile.Add<WhiteBalance>();
        whiteBalance.temperature.Override(8f);

        Bloom bloom = _profile.Add<Bloom>();
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(0.6f);

        _vignette = _profile.Add<Vignette>();
        _vignette.intensity.Override(BaseVignetteIntensity);
        _vignette.color.Override(Color.black);

        FilmGrain grain = _profile.Add<FilmGrain>();
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0.12f);
    }

    /// <summary>
    /// Viñeta roja corta al recibir daño. Se conecta sola a PlayerHealth.onDamageTaken.
    /// Si llega otro golpe durante el pulso, el pulso vuelve a empezar.
    /// </summary>
    public void TriggerDamagePulse()
    {
        if (_vignette == null) return;
        if (_pulseRoutine != null) StopCoroutine(_pulseRoutine);
        _pulseRoutine = StartCoroutine(DamagePulseCoroutine());
    }

    private System.Collections.IEnumerator DamagePulseCoroutine()
    {
        _vignette.color.value = damageVignetteColor;

        // Tiempo real: el pulso termina aunque haya pausa o hit-stop
        float elapsed = 0f;
        while (elapsed < damageVignetteDuration)
        {
            float t = elapsed / damageVignetteDuration;
            _vignette.intensity.value = Mathf.Lerp(damageVignetteIntensity, BaseVignetteIntensity, t * t);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        _vignette.color.value = Color.black;
        _vignette.intensity.value = BaseVignetteIntensity;
        _pulseRoutine = null;
    }

    /// <summary>
    /// Baja la saturación para momentos especiales (ej: flashbacks).
    /// 0 = Technicolor normal, 1 = saturación neutra (0). Nunca aplica saturación negativa.
    /// </summary>
    public void SetNoirIntensity(float intensity)
    {
        if (_colorAdj != null)
            _colorAdj.saturation.value = Mathf.Lerp(BaseSaturation, 0f, Mathf.Clamp01(intensity));
    }
}
