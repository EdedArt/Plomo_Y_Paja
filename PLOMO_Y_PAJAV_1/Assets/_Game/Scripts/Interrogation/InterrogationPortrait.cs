using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Retrato grande del interrogado (estilo Mouse P.I., fase 3C.3, D6). Todo en tiempo real (la escena corre con timeScale 0).
/// DÓNDE VA: en la Image "Retrato" del InterrogationCanvas, junto a LipSyncImageAdapter y un CanvasGroup.
///
/// - Entrada: escala 0.92 → 1 y aparece en 0.25 s.
/// - Respira: estira/aplasta ±1.5 % en Y cada 1.6 s (pivote abajo, el piso no se mueve).
/// - scared / nervous: tiembla a saltos ±2 px a 12 Hz ("boil" rubber-hose).
/// - Boca: LipSyncImageAdapter alterna _01/_02 de la emoción actual con la voz.
/// - Parpadeo: blink_01 cada 2.5–5 s durante 0.12 s, solo en "talk" y en silencio.
/// </summary>
[RequireComponent(typeof(Image))]
public class InterrogationPortrait : MonoBehaviour
{
    [SerializeField] private LipSyncImageAdapter mouth;
    [SerializeField] private CanvasGroup group;

    [Header("Entrada")]
    [SerializeField, Min(0.01f)] private float entranceSeconds = 0.25f;
    [SerializeField, Range(0.5f, 1f)] private float entranceScale = 0.92f;

    [Header("Vida")]
    [SerializeField, Range(0f, 0.1f)] private float breathingAmount = 0.015f;
    [SerializeField, Min(0.1f)] private float breathingPeriod = 1.6f;
    [SerializeField, Min(0f)] private float wobblePixels = 2f;
    [SerializeField, Min(1f)] private float wobbleHz = 12f;

    [Header("Parpadeo (solo talk)")]
    [SerializeField] private Vector2 blinkInterval = new Vector2(2.5f, 5f);
    [SerializeField, Min(0.01f)] private float blinkSeconds = 0.12f;

    private RectTransform _rect;
    private Image _image;
    private Vector2 _basePosition;
    private CharacterSpriteSet _set;
    private string _emotion;
    private Sprite _blink;
    private float _shownAt = -1f;
    private float _wobbleNext;
    private Vector2 _wobble;
    private float _nextBlink;
    private float _blinkUntil;
    private bool _visible;

    public string Emotion => _emotion;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _image = GetComponent<Image>();
        _basePosition = _rect.anchoredPosition;
        Apply(false);
    }

    /// <summary>Muestra el retrato con su entrada, en la emoción dada.</summary>
    public void Show(CharacterSpriteSet set, string emotion)
    {
        _set = set;
        _blink = Frame("blink", 0);
        _visible = true;
        _shownAt = Time.unscaledTime;
        _nextBlink = Time.unscaledTime + Random.Range(blinkInterval.x, blinkInterval.y);
        SetEmotion(emotion);
        Apply(true);
    }

    public void Hide()
    {
        _visible = false;
        if (mouth != null) mouth.SetOverride(null);
        Apply(false);
    }

    /// <summary>Cambia el par de la emoción (talk, scared, nervous, plead, relief, herido). Sin cuadros = se queda la anterior.</summary>
    public void SetEmotion(string emotion)
    {
        if (string.IsNullOrEmpty(emotion)) return;
        Sprite closed = Frame(emotion, 0);
        if (closed == null) return;
        _emotion = emotion;
        Sprite open = Frame(emotion, 1);
        if (mouth != null) mouth.SetPair(closed, open != null ? open : closed);
        else _image.sprite = closed;
    }

    private Sprite Frame(string anim, int index)
    {
        if (_set == null || !_set.TryGet(anim, out CharacterSpriteSet.AnimClip clip)) return null;
        return clip.frames[Mathf.Min(index, clip.frames.Length - 1)];
    }

    private void Apply(bool visible)
    {
        if (_image != null) _image.enabled = visible && _image.sprite != null; // sin cuadro no se ve un rectángulo blanco
        if (group != null) group.alpha = visible ? group.alpha : 0f;
    }

    private void LateUpdate()
    {
        if (!_visible) return;
        float now = Time.unscaledTime;
        if (!_image.enabled && _image.sprite != null) _image.enabled = true;

        // Entrada
        float t = Mathf.Clamp01((now - _shownAt) / entranceSeconds);
        float ease = 1f - (1f - t) * (1f - t);
        float scale = Mathf.Lerp(entranceScale, 1f, ease);
        if (group != null) group.alpha = ease;

        // Respiración: estira en Y y adelgaza un poco en X (squash and stretch)
        float breath = Mathf.Sin(now * Mathf.PI * 2f / breathingPeriod) * breathingAmount;
        _rect.localScale = new Vector3(scale * (1f - breath * 0.5f), scale * (1f + breath), 1f);

        // Temblor a saltos para miedo / nervios
        bool shaky = _emotion == "scared" || _emotion == "nervous";
        if (shaky && now >= _wobbleNext)
        {
            _wobbleNext = now + 1f / wobbleHz;
            _wobble = new Vector2(Random.Range(-wobblePixels, wobblePixels), Random.Range(-wobblePixels, wobblePixels));
        }
        else if (!shaky) _wobble = Vector2.zero;
        _rect.anchoredPosition = _basePosition + _wobble;

        // Parpadeo: solo talk y con la boca cerrada
        bool canBlink = _emotion == "talk" && _blink != null && mouth != null && !mouth.IsOpen;
        if (!canBlink)
        {
            if (_blinkUntil > 0f && mouth != null) mouth.SetOverride(null);
            _blinkUntil = -1f;
            return;
        }
        if (_blinkUntil > 0f && now >= _blinkUntil)
        {
            _blinkUntil = -1f;
            mouth.SetOverride(null);
            _nextBlink = now + Random.Range(blinkInterval.x, blinkInterval.y);
        }
        else if (_blinkUntil < 0f && now >= _nextBlink)
        {
            _blinkUntil = now + blinkSeconds;
            mouth.SetOverride(_blink);
        }
    }
}
