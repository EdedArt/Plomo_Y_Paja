using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cara de Wilbur + barra de vida del HUD (fase HUD.1, H2/H8). Lo crea HudWilbur; todo en tiempo real sin escala (H7).
/// - Cara: hud_wilbur_01..04 por franja de vida (&gt; 75 %, &gt; 50 %, &gt; 25 %, ≤ 25 %); hud_wilbur_dano 0.25 s en cada golpe
///   (sin ese dibujo, la cara se tiñe de rojo). "Boil" de dibujo animado: ±1.5 px a saltos, 12 veces por segundo.
///   Sin los dibujos: círculo rosado con la etiqueta "WILBUR" (placeholder).
/// - Barra: relleno rojo + segmento "fantasma" que se vacía 0.4 s después del golpe; tiembla al recibir daño; al curarse
///   el relleno sube animado con un brillo cálido. Marco hud_barra_marco si existe; si no, un contorno grueso dibujado.
/// </summary>
public class HudHealthPanel : MonoBehaviour
{
    private const float HurtSeconds = 0.25f;
    private const float ShakeSeconds = 0.25f;
    private const float ShakeAmount = 6f;
    private const float GhostDelay = 0.4f;
    private const float GhostSpeed = 1.2f;   // fracción de barra por segundo
    private const float FillSpeed = 2.5f;    // subida animada al curarse
    private const float BoilStep = 1f / 12f;
    private const float BoilAmount = 1.5f;

    private static readonly Color FillColor = new Color32(0xD8, 0x3A, 0x2E, 0xFF);
    private static readonly Color HealColor = new Color32(0xFF, 0xD2, 0x5A, 0xFF);
    private static readonly Color GhostColor = new Color32(0xF4, 0xE3, 0xB0, 0xFF);

    private Image _face;
    private RectTransform _faceRt;
    private Vector2 _faceBase;
    private Sprite[] _bands;
    private Sprite _hurt;
    private RectTransform _bar;
    private Vector2 _barBase;
    private RectTransform _fill;
    private Image _fillImage;
    private RectTransform _ghost;

    private float _target = 1f;
    private float _shown = 1f;
    private float _ghostValue = 1f;
    private float _ghostWait;
    private float _hurtTimer;
    private float _shakeTimer;
    private float _healGlow;
    private float _boilTimer;
    private int _band = -1;

    public void Init(Sprite[] bands, Sprite hurt, Sprite frame, TMP_FontAsset font, float faceSize, Vector2 barSize)
    {
        var root = (RectTransform)transform;
        _bands = bands;
        _hurt = hurt;

        // Cara
        bool hasFaces = bands != null && bands.Length > 0 && bands[0] != null;
        _face = HudUi.Image("Cara", root, hasFaces ? bands[0] : HudSprites.Circle, new Vector2(faceSize, faceSize));
        _faceRt = _face.rectTransform;
        _faceRt.anchorMin = _faceRt.anchorMax = _faceRt.pivot = Vector2.zero;
        _faceRt.anchoredPosition = _faceBase = Vector2.zero;
        _face.preserveAspect = true;
        if (!hasFaces)
        {
            _face.color = new Color32(0xF2, 0xA6, 0xB4, 0xFF);
            TextMeshProUGUI label = HudUi.Text("Etiqueta", _faceRt, font, 22f, TextAlignmentOptions.Center, new Color32(40, 20, 25, 255));
            label.text = "WILBUR";
            HudUi.Stretch(label.rectTransform, 0f);
        }

        // Barra: contorno (o marco) → fondo → fantasma → relleno
        _bar = HudUi.Rect("Barra", root, barSize);
        _bar.anchorMin = _bar.anchorMax = _bar.pivot = Vector2.zero;
        _bar.anchoredPosition = _barBase = new Vector2(faceSize + 18f, faceSize - barSize.y - 10f); // arriba; debajo, el aviso y la munición
        Image outline = HudUi.Image("Contorno", _bar, HudSprites.Panel, barSize + new Vector2(10f, 10f));
        outline.type = Image.Type.Sliced;
        outline.color = new Color32(20, 16, 18, 255);
        HudUi.Stretch(outline.rectTransform, -5f);
        Image back = HudUi.Image("Fondo", _bar, HudSprites.Panel, barSize);
        back.type = Image.Type.Sliced;
        back.color = new Color32(60, 40, 44, 255);
        HudUi.Stretch(back.rectTransform, 0f);
        Image ghost = HudUi.Image("Fantasma", _bar, HudSprites.Panel, barSize);
        ghost.type = Image.Type.Sliced;
        ghost.color = GhostColor;
        _ghost = ghost.rectTransform;
        HudUi.Stretch(_ghost, 4f);
        _fillImage = HudUi.Image("Relleno", _bar, HudSprites.Panel, barSize);
        _fillImage.type = Image.Type.Sliced;
        _fillImage.color = FillColor;
        _fill = _fillImage.rectTransform;
        HudUi.Stretch(_fill, 4f);
        if (frame != null)
        {
            outline.enabled = false;
            Image frameImage = HudUi.Image("Marco", _bar, frame, barSize + new Vector2(24f, 24f));
            frameImage.type = frame.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            HudUi.Stretch(frameImage.rectTransform, -12f);
        }
        SetBar(_fill, 1f);
        SetBar(_ghost, 1f);
    }

    /// <summary>Vida nueva (0..1). instant = sin animación (al empezar).</summary>
    public void SetValue(float percent, bool instant)
    {
        _target = Mathf.Clamp01(percent);
        if (instant || _target < _shown) _shown = _target; // al bajar, el relleno baja ya (el fantasma se queda)
        if (instant) _ghostValue = _target;
        int band = _target > 0.75f ? 0 : _target > 0.5f ? 1 : _target > 0.25f ? 2 : 3;
        if (band != _band)
        {
            _band = band;
            if (_hurtTimer <= 0f) ShowBandFace();
        }
    }

    public void Hit()
    {
        _hurtTimer = HurtSeconds;
        _shakeTimer = ShakeSeconds;
        _ghostWait = GhostDelay;
        if (_hurt != null) _face.sprite = _hurt;
        else if (HasBands) _face.color = new Color(1f, 0.55f, 0.55f, 1f);
    }

    public void Healed()
    {
        _healGlow = 1f;
    }

    private bool HasBands => _bands != null && _bands.Length > 0 && _bands[0] != null;

    private void ShowBandFace()
    {
        if (!HasBands) return;
        int i = Mathf.Clamp(_band, 0, _bands.Length - 1);
        while (i > 0 && _bands[i] == null) i--; // falta una franja: la anterior que exista
        _face.sprite = _bands[i];
        _face.color = Color.white;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (_hurtTimer > 0f)
        {
            _hurtTimer -= dt;
            if (_hurtTimer <= 0f) ShowBandFace();
        }

        // Boil: la cara "hierve" a saltos (dibujo animado)
        _boilTimer -= dt;
        if (_boilTimer <= 0f)
        {
            _boilTimer = BoilStep;
            _faceRt.anchoredPosition = _faceBase + new Vector2(Random.Range(-BoilAmount, BoilAmount), Random.Range(-BoilAmount, BoilAmount));
        }

        // Relleno: sube animado al curarse
        if (_shown < _target) _shown = Mathf.MoveTowards(_shown, _target, dt * FillSpeed);
        SetBar(_fill, _shown);

        // Fantasma: espera y después se vacía hasta el valor real
        if (_ghostWait > 0f) _ghostWait -= dt;
        else if (_ghostValue > _shown) _ghostValue = Mathf.MoveTowards(_ghostValue, _shown, dt * GhostSpeed);
        if (_ghostValue < _shown) _ghostValue = _shown;
        SetBar(_ghost, _ghostValue);

        // Temblor
        if (_shakeTimer > 0f)
        {
            _shakeTimer -= dt;
            float k = Mathf.Clamp01(_shakeTimer / ShakeSeconds) * ShakeAmount;
            _bar.anchoredPosition = _barBase + new Vector2(Random.Range(-k, k), Random.Range(-k, k));
        }
        else if (_bar.anchoredPosition != _barBase) _bar.anchoredPosition = _barBase;

        // Brillo cálido al curarse
        if (_healGlow > 0f) _healGlow = Mathf.Max(0f, _healGlow - dt * 2f);
        _fillImage.color = Color.Lerp(FillColor, HealColor, _healGlow);
    }

    private static void SetBar(RectTransform rt, float value)
    {
        Vector2 max = rt.anchorMax;
        if (Mathf.Approximately(max.x, value)) return;
        rt.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
    }
}
