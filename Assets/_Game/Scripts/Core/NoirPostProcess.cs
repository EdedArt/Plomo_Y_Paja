using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Configura el estilo visual noir inspirado en Mouse P.I. For Hire.
/// DÓNDE VA: GameObject "PostProcessVolume" en la escena.
/// Debe tener también un componente "Volume" (Unity URP).
///
/// SETUP MANUAL (alternativa si el script da problemas):
/// 1. Crear GameObject vacío → "PostProcessVolume"
/// 2. Add Component → Volume → set Global = true
/// 3. New Profile → se crea un archivo .asset de perfil
/// 4. Add Override → Color Adjustments:
///    - Saturation: -70 (casi blanco y negro)
///    - Contrast: +30
///    - Post Exposure: -0.3
/// 5. Add Override → Vignette:
///    - Intensity: 0.4
///    - Smoothness: 0.5
///    - Color: negro
/// 6. Add Override → Film Grain:
///    - Type: Thin
///    - Intensity: 0.4
///    - Response: 0.8
///
/// NOTA: Este script configura todo por código para que no tengan que
/// hacerlo manualmente. Pero si Unity da errores, usar el método manual arriba.
/// </summary>
[RequireComponent(typeof(Volume))]
public class NoirPostProcess : MonoBehaviour
{
    [Header("Intensidad del efecto noir")]
    [Tooltip("0 = color normal, 1 = blanco y negro total")]
    [SerializeField, Range(0f, 1f)] private float noirIntensity = 0.85f;

    [Header("Efecto de pulso al recibir daño")]
    [SerializeField] private float damageVignetteIntensity = 0.7f;
    [SerializeField] private float damageVignetteDuration = 0.3f;
    private float _defaultVignetteIntensity = 0.35f;
    private bool _damagePulseActive = false;

    private Volume _volume;
    private VolumeProfile _profile;

    // Referencias a los efectos que vamos a modificar en runtime
    private ColorAdjustments _colorAdj;
    private Vignette _vignette;
    private FilmGrain _filmGrain;

    private void Awake()
    {
        _volume = GetComponent<Volume>();

        // Crear el perfil de post-proceso en runtime
        _profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _volume.profile = _profile;
        _volume.isGlobal = true;

        SetupNoirEffects();
    }

    private void SetupNoirEffects()
    {
        // ===== Color Adjustments (el efecto más importante para el noir) =====
        _colorAdj = _profile.Add<ColorAdjustments>(true);
        _colorAdj.saturation.value = -70f * noirIntensity; // Desaturar (-100 = B&N total)
        _colorAdj.contrast.value = 25f * noirIntensity;    // Aumentar contraste
        _colorAdj.postExposure.value = -0.2f;              // Ligeramente más oscuro

        // ===== Vignette (sombras en los bordes de la pantalla, clásico noir) =====
        _vignette = _profile.Add<Vignette>(true);
        _vignette.intensity.value = _defaultVignetteIntensity;
        _vignette.smoothness.value = 0.5f;
        _vignette.color.value = Color.black;

        // ===== Film Grain (grano de película, estilo años 30) =====
        _filmGrain = _profile.Add<FilmGrain>(true);
        _filmGrain.type.value = FilmGrainLookup.Thin1;
        _filmGrain.intensity.value = 0.35f;
        _filmGrain.response.value = 0.8f;
    }

    /// <summary>
    /// Efecto de pantalla pulsando al recibir daño (vignette rojo momentáneo).
    /// Conectar a PlayerHealth.onDamageTaken en el Inspector.
    /// </summary>
    public void TriggerDamagePulse()
    {
        if (!_damagePulseActive)
            StartCoroutine(DamagePulseCoroutine());
    }

    private System.Collections.IEnumerator DamagePulseCoroutine()
    {
        _damagePulseActive = true;

        if (_vignette != null)
        {
            _vignette.color.value = new Color(0.8f, 0.1f, 0.1f); // Rojo
            _vignette.intensity.value = damageVignetteIntensity;
        }

        yield return new WaitForSeconds(damageVignetteDuration);

        if (_vignette != null)
        {
            _vignette.color.value = Color.black; // Volver al negro normal
            _vignette.intensity.value = _defaultVignetteIntensity;
        }

        _damagePulseActive = false;
    }

    /// <summary>
    /// Cambiar la intensidad del noir en runtime (ej: durante flashbacks con color).
    /// </summary>
    public void SetNoirIntensity(float intensity)
    {
        if (_colorAdj != null)
            _colorAdj.saturation.value = -70f * Mathf.Clamp01(intensity);
    }
}
