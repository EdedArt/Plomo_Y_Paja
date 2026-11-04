using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Avisos de pantalla completa del HUD (fase HUD.1, H8). Los crea HudWilbur; tiempo real sin escala (H7).
/// - Daño: destello rojo en los bordes (0.35 s).
/// - Indicador de dirección: cuña roja en un anillo alrededor del centro, apuntando hacia la fuente del daño (solo si se
///   conoce: PlayerHealth.TakeDamage(daño, posición)); se desvanece en 0.8 s y sigue la fuente si Wilbur gira. Hasta 4 a la
///   vez (pool fijo).
/// - Poca vida (≤ 25 %): los bordes laten en rojo (overlay de UI, no un Volume) + loop de corazón (sfx_latido) que sube y
///   baja de volumen.
/// - Curación: destello cálido en los bordes.
/// </summary>
public class HudFeedback : MonoBehaviour
{
    private const int MaxIndicators = 4;
    private const float IndicatorSeconds = 0.8f;
    private const float RingRadius = 170f;
    private const float FlashSeconds = 0.35f;
    private const float HealSeconds = 0.5f;
    private const float HeartFade = 0.6f;

    private static readonly Color Red = new Color32(0xD8, 0x1E, 0x1E, 0xFF);
    private static readonly Color Warm = new Color32(0xFF, 0xD2, 0x5A, 0xFF);

    private Image _redEdge;
    private Image _warmEdge;
    private RectTransform _ring;
    private readonly Image[] _wedges = new Image[MaxIndicators];
    private readonly Vector3[] _sources = new Vector3[MaxIndicators];
    private readonly float[] _timers = new float[MaxIndicators];
    private AudioSource _heart;
    private float _heartVolume = 1f;

    private float _flash;
    private float _heal;
    private bool _low;
    private bool _visible = true;
    private Camera _camera;

    public void Init(AudioSource heartbeat)
    {
        var root = (RectTransform)transform;
        HudUi.Stretch(root, 0f);

        _redEdge = HudUi.Image("BordeRojo", root, HudSprites.Edge, Vector2.zero);
        HudUi.Stretch(_redEdge.rectTransform, 0f);
        _redEdge.color = new Color(Red.r, Red.g, Red.b, 0f);
        _warmEdge = HudUi.Image("BordeCalido", root, HudSprites.Edge, Vector2.zero);
        HudUi.Stretch(_warmEdge.rectTransform, 0f);
        _warmEdge.color = new Color(Warm.r, Warm.g, Warm.b, 0f);

        _ring = HudUi.Rect("Direccion", root, Vector2.zero);
        _ring.anchorMin = _ring.anchorMax = _ring.pivot = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < MaxIndicators; i++)
        {
            _wedges[i] = HudUi.Image("Cuna_" + (i + 1), _ring, HudSprites.Wedge, new Vector2(64f, 48f));
            RectTransform rt = _wedges[i].rectTransform;
            rt.pivot = new Vector2(0.5f, 0f);
            _wedges[i].color = Red;
            _wedges[i].enabled = false;
        }

        _heart = heartbeat;
        if (_heart != null)
        {
            _heartVolume = _heart.volume > 0f ? _heart.volume : 1f;
            _heart.loop = true;
            _heart.playOnAwake = false;
            _heart.spatialBlend = 0f;
            _heart.volume = 0f;
            if (_heart.clip == null) _heart.clip = AudioLibrary.GetSfx("sfx_latido"); // sin clip: no suena (placeholder mudo)
        }
    }

    public void SetVisible(bool visible) => _visible = visible;

    public void SetLowHealth(bool low) => _low = low;

    public void Damaged(Vector3? source)
    {
        _flash = FlashSeconds;
        if (!source.HasValue) return;
        // Un lugar libre o el más viejo
        int slot = 0;
        for (int i = 0; i < MaxIndicators; i++)
        {
            if (_timers[i] <= 0f) { slot = i; break; }
            if (_timers[i] < _timers[slot]) slot = i;
        }
        _sources[slot] = source.Value;
        _timers[slot] = IndicatorSeconds;
        _wedges[slot].enabled = true;
    }

    public void Healed() => _heal = HealSeconds;

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // Bordes: destello de daño + latido de poca vida (el mayor de los dos)
        if (_flash > 0f) _flash = Mathf.Max(0f, _flash - dt);
        float flashAlpha = _flash / FlashSeconds * 0.55f;
        float pulseAlpha = _low ? 0.16f + 0.14f * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI * 1.1f) : 0f;
        SetAlpha(_redEdge, Mathf.Max(flashAlpha, pulseAlpha));

        if (_heal > 0f) _heal = Mathf.Max(0f, _heal - dt);
        SetAlpha(_warmEdge, _heal / HealSeconds * 0.45f);

        UpdateIndicators(dt);
        UpdateHeart(dt);
    }

    private void UpdateIndicators(float dt)
    {
        if (_camera == null) _camera = Camera.main;
        for (int i = 0; i < MaxIndicators; i++)
        {
            if (_timers[i] <= 0f) continue;
            _timers[i] -= dt;
            Image wedge = _wedges[i];
            if (_timers[i] <= 0f || _camera == null)
            {
                wedge.enabled = false;
                continue;
            }
            // Ángulo de la fuente en el plano horizontal de la cámara: 0 = adelante (arriba de la pantalla)
            Vector3 local = _camera.transform.InverseTransformDirection(_sources[i] - _camera.transform.position);
            float angle = Mathf.Atan2(local.x, local.z);
            RectTransform rt = wedge.rectTransform;
            rt.anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * RingRadius;
            rt.localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
            SetAlpha(wedge, Mathf.Clamp01(_timers[i] / IndicatorSeconds));
        }
    }

    private void UpdateHeart(float dt)
    {
        if (_heart == null || _heart.clip == null) return;
        float target = _low && _visible ? _heartVolume : 0f;
        _heart.volume = Mathf.MoveTowards(_heart.volume, target, dt * _heartVolume / HeartFade);
        if (_heart.volume > 0.001f && !_heart.isPlaying) _heart.Play();
        else if (_heart.volume <= 0.001f && _heart.isPlaying) _heart.Stop();
    }

    private static void SetAlpha(Graphic g, float a)
    {
        Color c = g.color;
        if (Mathf.Approximately(c.a, a)) return;
        c.a = a;
        g.color = c;
    }
}
