using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Filtro de película vieja SOLO para el libro (A7 §5): una Image a pantalla completa en su propio Canvas Overlay, encima
/// de todo, con el shader procedural PlomoYPaja/Intro/OldFilm (mezcla "multiplicar ×2": puede oscurecer y aclarar lo de
/// abajo sin leer la pantalla). No depende del Volume del juego. Sin texturas.
/// DÓNDE VA: Image "Pelicula" del Canvas UI_Pelicula de Intro.unity (lo arma el builder).
///
/// - Grano animado a ~24 cuadros/s (más fuerte que el Film Grain del juego), motas de polvo y algunas rayas verticales
///   al azar, viñeta oscura y tinte sepia cálido: en el shader.
/// - Temblor de ventanilla (gate weave): mueve "weaveTarget" (la página) 1–2 px a 12 cuadros/s.
/// - Parpadeo de luz cálida (exposición ±3 %, reemplaza a la lámpara): aquí, suavizado.
/// - Todo serializado para afinar en Play. Sin basura por frame (ids cacheados, Random, SetFloat).
/// </summary>
[RequireComponent(typeof(Graphic))]
public class OldFilmFilter : MonoBehaviour
{
    private static readonly int FrameId = Shader.PropertyToID("_FilmFrame");
    private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
    private static readonly int ResolutionId = Shader.PropertyToID("_Resolution");
    private static readonly int GrainStrengthId = Shader.PropertyToID("_GrainStrength");
    private static readonly int GrainSizeId = Shader.PropertyToID("_GrainSize");
    private static readonly int DustChanceId = Shader.PropertyToID("_DustChance");
    private static readonly int DustSizeId = Shader.PropertyToID("_DustSize");
    private static readonly int DustStrengthId = Shader.PropertyToID("_DustStrength");
    private static readonly int ScratchChanceId = Shader.PropertyToID("_ScratchChance");
    private static readonly int ScratchStrengthId = Shader.PropertyToID("_ScratchStrength");
    private static readonly int ScratchWidthId = Shader.PropertyToID("_ScratchWidth");
    private static readonly int ScratchHoldId = Shader.PropertyToID("_ScratchHold");
    private static readonly int VignetteStrengthId = Shader.PropertyToID("_VignetteStrength");
    private static readonly int VignetteRadiusId = Shader.PropertyToID("_VignetteRadius");
    private static readonly int VignetteSoftnessId = Shader.PropertyToID("_VignetteSoftness");
    private static readonly int TintId = Shader.PropertyToID("_Tint");

    [SerializeField] private Graphic graphic;

    [Header("Grano")]
    [Tooltip("Cuadros por segundo del grano, el polvo y las rayas (película a 24)")]
    [SerializeField] private float filmFps = 24f;
    [SerializeField, Range(0f, 0.5f)] private float grainStrength = 0.16f;
    [Tooltip("Tamaño del grano en px de pantalla")]
    [SerializeField, Range(1f, 4f)] private float grainSize = 1.6f;

    [Header("Polvo")]
    [Tooltip("Probabilidad por cuadro de cada una de las 8 motas")]
    [SerializeField, Range(0f, 1f)] private float dustChance = 0.18f;
    [Tooltip("Radio máximo de una mota (fracción del alto)")]
    [SerializeField, Range(0.001f, 0.02f)] private float dustSize = 0.006f;
    [SerializeField, Range(0f, 1f)] private float dustStrength = 0.7f;

    [Header("Rayas verticales")]
    [Tooltip("Probabilidad de que cada una de las 3 rayas aparezca en un tramo")]
    [SerializeField, Range(0f, 1f)] private float scratchChance = 0.22f;
    [SerializeField, Range(0f, 1f)] private float scratchStrength = 0.35f;
    [Tooltip("Ancho de la raya (fracción del ancho)")]
    [SerializeField, Range(0.0002f, 0.005f)] private float scratchWidth = 0.0012f;
    [Tooltip("Cuadros de película que dura cada tramo de una raya")]
    [SerializeField, Range(1f, 48f)] private float scratchHoldFrames = 9f;

    [Header("Temblor de ventanilla (gate weave)")]
    [SerializeField] private RectTransform weaveTarget;
    [SerializeField] private float weaveFps = 12f;
    [Tooltip("Desplazamiento máximo (px de la referencia 1920×1080)")]
    [SerializeField, Range(0f, 4f)] private float weavePixels = 1.5f;

    [Header("Parpadeo de luz")]
    [Tooltip("Exposición ± esta fracción (0.03 = ±3 %)")]
    [SerializeField, Range(0f, 0.15f)] private float flickerAmount = 0.03f;
    [Tooltip("Qué tan rápido sigue el brillo a su objetivo (1/s)")]
    [SerializeField] private float flickerSmoothing = 18f;

    [Header("Viñeta y tinte")]
    [SerializeField, Range(0f, 1f)] private float vignetteStrength = 0.55f;
    [SerializeField, Range(0.2f, 1.2f)] private float vignetteRadius = 0.85f;
    [SerializeField, Range(0.05f, 1f)] private float vignetteSoftness = 0.55f;
    [Tooltip("Multiplica la imagen: sepia cálido suave")]
    [SerializeField] private Color tint = new Color(1.04f, 0.97f, 0.84f, 1f);

    private Material _material;
    private float _filmClock;
    private float _weaveClock;
    private int _frame;
    private float _exposure = 1f;
    private float _exposureTarget = 1f;
    private Vector2 _weaveBase;

    private void Awake()
    {
        if (graphic == null) graphic = GetComponent<Graphic>();
        Material source = graphic.material;
        if (source == null || source.shader == null || !source.shader.isSupported || !source.HasProperty(FrameId))
        {
            Debug.LogWarning("[Intro] Sin el shader de película (PlomoYPaja/Intro/OldFilm): el libro se ve sin filtro.");
            graphic.enabled = false;
            enabled = false;
            return;
        }
        _material = new Material(source) { name = source.name + " (Play)" };
        graphic.material = _material;
        graphic.raycastTarget = false;
        if (weaveTarget != null)
        {
            _weaveBase = weaveTarget.anchoredPosition;
            // Un poco más grande para que el temblor no deje ver el borde
            float s = 1f + (weavePixels * 2f + 1f) / 1080f;
            weaveTarget.localScale = new Vector3(s, s, 1f);
        }
        Push();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        _filmClock += dt;
        float filmStep = 1f / Mathf.Max(1f, filmFps);
        if (_filmClock >= filmStep)
        {
            _filmClock %= filmStep;
            _frame = (_frame + 1) % 4096;
            _exposureTarget = 1f + Random.Range(-flickerAmount, flickerAmount);
        }
        _exposure = Mathf.Lerp(_exposure, _exposureTarget, 1f - Mathf.Exp(-flickerSmoothing * dt));

        if (weaveTarget != null)
        {
            _weaveClock += dt;
            float weaveStep = 1f / Mathf.Max(1f, weaveFps);
            if (_weaveClock >= weaveStep)
            {
                _weaveClock %= weaveStep;
                float x = Mathf.Round(Random.Range(-weavePixels, weavePixels));
                float y = Mathf.Round(Random.Range(-weavePixels, weavePixels) * 0.6f);
                weaveTarget.anchoredPosition = _weaveBase + new Vector2(x, y);
            }
        }

        Push();
    }

    /// <summary>Todos los valores al material cada cuadro: se pueden afinar en el Inspector durante el Play.</summary>
    private void Push()
    {
        if (_material == null) return;
        _material.SetFloat(FrameId, _frame);
        _material.SetFloat(ExposureId, _exposure);
        _material.SetVector(ResolutionId, new Vector4(Screen.width, Screen.height, 0f, 0f));
        _material.SetFloat(GrainStrengthId, grainStrength);
        _material.SetFloat(GrainSizeId, grainSize);
        _material.SetFloat(DustChanceId, dustChance);
        _material.SetFloat(DustSizeId, dustSize);
        _material.SetFloat(DustStrengthId, dustStrength);
        _material.SetFloat(ScratchChanceId, scratchChance);
        _material.SetFloat(ScratchStrengthId, scratchStrength);
        _material.SetFloat(ScratchWidthId, scratchWidth);
        _material.SetFloat(ScratchHoldId, scratchHoldFrames);
        _material.SetFloat(VignetteStrengthId, vignetteStrength);
        _material.SetFloat(VignetteRadiusId, vignetteRadius);
        _material.SetFloat(VignetteSoftnessId, vignetteSoftness);
        _material.SetColor(TintId, tint);
    }

    private void OnDestroy()
    {
        if (_material != null) Destroy(_material);
    }
}
