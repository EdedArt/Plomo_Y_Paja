using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Avisos cortos sobre el grupo del HUD (fase HUD.1): tira de papel con texto ("+6 cartuchos"), dura 1.6 s y se desvanece;
/// hasta 3 a la vez, apilados (el más nuevo abajo; si están los 3, se reemplaza el más viejo). Suena sfx_hud_aviso.
/// Lo usan HUD.3 (recogibles) y, si quiere, 3C (HudWilbur.ShowToast). Tiempo real sin escala.
/// </summary>
public class HudToast : MonoBehaviour
{
    private const int Slots = 3;
    private const float Seconds = 1.6f;
    private const float FadeSeconds = 0.3f;
    private const float SlotHeight = 46f;
    public const string SfxId = "sfx_hud_aviso";

    private readonly CanvasGroup[] _groups = new CanvasGroup[Slots];
    private readonly TextMeshProUGUI[] _texts = new TextMeshProUGUI[Slots];
    private readonly float[] _timers = new float[Slots];
    private readonly int[] _order = new int[Slots]; // para saber cuál es el más viejo
    private int _counter;

    public void Init(TMP_FontAsset font)
    {
        var root = (RectTransform)transform;
        for (int i = 0; i < Slots; i++)
        {
            Image paper = HudUi.Image("Aviso_" + (i + 1), root, HudSprites.Panel, new Vector2(300f, 40f));
            paper.type = Image.Type.Sliced;
            paper.color = new Color32(0xEF, 0xE6, 0xCF, 0xF2);
            RectTransform rt = paper.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            _groups[i] = paper.gameObject.AddComponent<CanvasGroup>();
            _groups[i].alpha = 0f;
            _groups[i].blocksRaycasts = false;
            _texts[i] = HudUi.Text("Texto", rt, font, 26f, TextAlignmentOptions.Center, new Color32(0x2A, 0x1E, 0x18, 0xFF));
            HudUi.Stretch(_texts[i].rectTransform, 4f);
        }
    }

    public void Show(string text)
    {
        int slot = -1, oldest = 0;
        for (int i = 0; i < Slots; i++)
        {
            if (_timers[i] <= 0f && slot < 0) slot = i;
            if (_order[i] < _order[oldest]) oldest = i;
        }
        if (slot < 0) slot = oldest;
        _texts[slot].text = text;
        _timers[slot] = Seconds;
        _order[slot] = ++_counter;
        AudioLibrary.PlaySfx2D(SfxId);
        Layout();
    }

    /// <summary>Apila los avisos activos: el más nuevo abajo.</summary>
    private void Layout()
    {
        for (int i = 0; i < Slots; i++)
        {
            if (_timers[i] <= 0f) continue;
            int newer = 0;
            for (int j = 0; j < Slots; j++)
                if (j != i && _timers[j] > 0f && _order[j] > _order[i]) newer++;
            _groups[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, newer * SlotHeight);
        }
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < Slots; i++)
        {
            if (_timers[i] <= 0f)
            {
                if (_groups[i].alpha > 0f) _groups[i].alpha = 0f;
                continue;
            }
            _timers[i] -= dt;
            _groups[i].alpha = Mathf.Clamp01(_timers[i] / FadeSeconds);
            if (_timers[i] <= 0f) Layout();
        }
    }
}
