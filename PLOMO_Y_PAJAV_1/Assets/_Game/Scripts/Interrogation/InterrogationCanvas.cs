using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vista del interrogatorio (fase 3C.3, D9): Canvas propio, Screen Space Overlay, por encima de la mira y del HUD.
/// No depende de UIManager ni de DialogueUI. Lo arma el menú Plomo y Paja → Contador → Crear UI de interrogatorio
/// (prefab Prefabs/UI/InterrogationCanvas). Solo muestra cosas; el ritmo (tiempos, teclas, voces) lo lleva
/// InterrogationDirector.
///
/// DISEÑO (3C.4, prueba de David, estilo Mouse P.I.): el personaje es lo grande (retrato ≈ 95 % del alto, detrás del
/// papel) y el diálogo es una tira baja de papel. TODOS los valores de diseño son campos de este componente y se
/// aplican al arrancar y, en Play, cada vez que se cambian en el Inspector (OnValidate): David los ajusta en vivo.
/// En el papel, el texto de la línea y las opciones/decisión comparten el mismo espacio (al elegir, el texto se oculta).
/// </summary>
public class InterrogationCanvas : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private InterrogationPortrait portrait;

    [Header("Papel")]
    [SerializeField] private TMP_Text speakerLabel;
    [SerializeField] private TMP_Text lineLabel;
    [SerializeField] private Button[] optionButtons = new Button[0];
    [SerializeField] private TMP_Text[] optionLabels = new TMP_Text[0];

    [Header("Decisión")]
    [SerializeField] private GameObject decisionRoot;
    [SerializeField] private Button[] decisionButtons = new Button[0];
    [SerializeField] private TMP_Text[] decisionLabels = new TMP_Text[0];

    [Header("Pensamiento de la IA")]
    [SerializeField] private CanvasGroup thoughtGroup;
    [SerializeField] private TMP_Text thoughtLabel;

    [Header("Aviso de pista (opcional)")]
    [SerializeField] private CanvasGroup noticeGroup;
    [SerializeField] private TMP_Text noticeLabel;

    // ============ Diseño (ajustable en Play) ============

    [Header("Diseño: escala")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField, Range(0f, 1f)] private float matchWidthOrHeight = 0.5f;

    [Header("Diseño: papel (tira baja)")]
    [SerializeField] private Vector2 paperAnchorMin = new Vector2(0.12f, 0.02f);
    [SerializeField] private Vector2 paperAnchorMax = new Vector2(0.88f, 0.30f);
    [Tooltip("Margen interior en fracción del papel: x = izquierda/derecha, y = arriba/abajo")]
    [SerializeField] private Vector2 paperPadding = new Vector2(0.06f, 0.12f);
    [Tooltip("Achica los bordes del 9-slice del papel (más alto = bordes más finos)")]
    [SerializeField, Min(0.1f)] private float paperSliceScale = 2f;

    [Header("Diseño: retrato (detrás del papel)")]
    [SerializeField, Range(0.3f, 1f)] private float portraitHeight = 0.95f;
    [SerializeField, Range(0f, 0.5f)] private float portraitBottom = 0.02f;

    [Header("Diseño: letras (px a 1080p)")]
    [SerializeField] private float nameSize = 44f;
    [SerializeField] private float bodySize = 38f;
    [SerializeField] private float optionSize = 34f;
    [SerializeField] private float decisionSize = 44f;
    [SerializeField] private float thoughtSize = 36f;
    [SerializeField] private float noticeSize = 34f;
    [Tooltip("Tamaño mínimo al que se achica el texto largo")]
    [SerializeField] private float minBodySize = 26f;

    [Header("Diseño: colores")]
    [SerializeField] private Color nameColor = new Color32(0x3B, 0x24, 0x14, 0xFF);
    [SerializeField] private Color bodyColor = new Color32(0x1E, 0x12, 0x09, 0xFF);
    [SerializeField] private Color optionColor = new Color32(0x3B, 0x24, 0x14, 0xFF);
    [Tooltip("Opción con el mouse encima: más oscura y subrayada")]
    [SerializeField] private Color optionHoverColor = new Color32(0x0E, 0x07, 0x03, 0xFF);
    [SerializeField] private Color thoughtColor = new Color32(0xF2, 0xE6, 0xC8, 0xFF);
    [SerializeField] private Color thoughtOutlineColor = new Color32(0x14, 0x0C, 0x08, 0xFF);
    [SerializeField, Range(0f, 1f)] private float thoughtOutlineWidth = 0.22f;
    [SerializeField, Range(0f, 1f)] private float thoughtPanelAlpha = 0f;

    [Header("Diseño: pensamiento (arriba)")]
    [SerializeField] private Vector2 thoughtAnchorMin = new Vector2(0.08f, 0.88f);
    [SerializeField] private Vector2 thoughtAnchorMax = new Vector2(0.92f, 0.99f);

    private Action<int> _optionChosen;
    private Action<int> _decisionChosen;
    private bool _wired;

    public InterrogationPortrait Portrait => portrait;
    public int OptionCount => optionButtons.Length;

    private void Awake()
    {
        Wire();
        ApplyLayout();
        SetOpen(false);
        SetAlpha(0f);
        HideOptions();
        HideDecision();
        SetThought("", 0f);
        SetNotice("", 0f);
        ShowLine("", "");
    }

    private void OnValidate()
    {
        if (Application.isPlaying && _wired) ApplyLayout();
    }

    private void Wire()
    {
        if (_wired) return;
        _wired = true;
        for (int i = 0; i < optionButtons.Length; i++)
        {
            int index = i;
            if (optionButtons[i] == null) continue;
            optionButtons[i].onClick.AddListener(() => PickOption(index));
            if (!optionButtons[i].TryGetComponent(out InterrogationOptionHover hover))
                hover = optionButtons[i].gameObject.AddComponent<InterrogationOptionHover>();
            hover.Init(i < optionLabels.Length ? optionLabels[i] : null, this);
        }
        for (int i = 0; i < decisionButtons.Length; i++)
        {
            int index = i;
            if (decisionButtons[i] != null) decisionButtons[i].onClick.AddListener(() => PickDecision(index));
        }
    }

    // ==================================================================
    // Diseño
    // ==================================================================

    /// <summary>Aplica todos los valores de diseño. Lo llaman Awake, OnValidate (en Play) y el menú que arma el prefab.</summary>
    public void ApplyLayout()
    {
        if (TryGetComponent(out CanvasScaler scaler))
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = matchWidthOrHeight;
        }

        // Retrato: abajo al centro, casi todo el alto, cuadrado (AspectRatioFitter), detrás del papel
        if (portrait != null)
        {
            var rect = (RectTransform)portrait.transform;
            Place(rect, new Vector2(0.5f, portraitBottom), new Vector2(0.5f, Mathf.Min(1f, portraitBottom + portraitHeight)),
                new Vector2(0.5f, 0f));
            rect.SetAsFirstSibling();
        }

        // Papel: tira baja, 9-slice
        RectTransform paper = speakerLabel != null ? speakerLabel.transform.parent as RectTransform : null;
        if (paper != null)
        {
            Place(paper, paperAnchorMin, paperAnchorMax, new Vector2(0.5f, 0f));
            if (paper.TryGetComponent(out AspectRatioFitter oldFit)) oldFit.enabled = false; // el 3C.3 la tenía
            if (paper.TryGetComponent(out Image paperImage) && paperImage.sprite != null)
            {
                paperImage.type = paperImage.sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
                paperImage.pixelsPerUnitMultiplier = paperSliceScale;
            }

            float nameHeight = nameSize * 1.35f;
            Vector2 innerMin = new Vector2(paperPadding.x, paperPadding.y);
            Vector2 innerMax = new Vector2(1f - paperPadding.x, 1f - paperPadding.y);

            // Nombre: renglón de arriba
            var nameRect = (RectTransform)speakerLabel.transform;
            Place(nameRect, new Vector2(innerMin.x, innerMax.y), innerMax, new Vector2(0.5f, 1f));
            nameRect.sizeDelta = new Vector2(0f, nameHeight);
            Style(speakerLabel, nameSize, nameColor, FontStyles.Bold);

            // Texto / opciones / decisión: el resto del papel
            if (lineLabel != null)
            {
                FillBelowName((RectTransform)lineLabel.transform, innerMin, innerMax, nameHeight);
                Style(lineLabel, bodySize, bodyColor, FontStyles.Normal);
                lineLabel.enableAutoSizing = true;
                lineLabel.fontSizeMax = bodySize;
                lineLabel.fontSizeMin = Mathf.Min(minBodySize, bodySize);
                lineLabel.alignment = TextAlignmentOptions.TopLeft;
            }
            if (optionButtons.Length > 0 && optionButtons[0] != null)
            {
                var options = (RectTransform)optionButtons[0].transform.parent;
                FillBelowName(options, innerMin, innerMax, nameHeight);
                if (options.TryGetComponent(out VerticalLayoutGroup layout))
                {
                    layout.childAlignment = TextAnchor.UpperLeft;
                    layout.spacing = optionSize * 0.15f;
                }
            }
            if (decisionRoot != null) FillBelowName((RectTransform)decisionRoot.transform, innerMin, innerMax, nameHeight);
        }

        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (optionButtons[i] != null && optionButtons[i].TryGetComponent(out LayoutElement element))
            {
                element.preferredHeight = optionSize * 1.3f;
                element.minHeight = optionSize * 1.1f;
            }
            if (optionButtons[i] != null && optionButtons[i].targetGraphic != null)
            {
                // Sin recuadro: el resaltado es la letra más oscura y subrayada (InterrogationOptionHover)
                ColorBlock colors = optionButtons[i].colors;
                colors.normalColor = colors.highlightedColor = colors.selectedColor = new Color(1f, 1f, 1f, 0f);
                colors.pressedColor = new Color(1f, 1f, 1f, 0.15f);
                optionButtons[i].colors = colors;
            }
            if (i < optionLabels.Length && optionLabels[i] != null)
            {
                Style(optionLabels[i], optionSize, optionColor, FontStyles.Normal);
                optionLabels[i].enableAutoSizing = false;
                optionLabels[i].enableWordWrapping = false;
                optionLabels[i].overflowMode = TextOverflowModes.Ellipsis;
                optionLabels[i].alignment = TextAlignmentOptions.MidlineLeft;
            }
        }
        foreach (TMP_Text label in decisionLabels)
        {
            if (label == null) continue;
            label.fontSize = decisionSize;
            label.enableAutoSizing = false;
        }

        // Pensamiento: crema con contorno oscuro, arriba, sobre el mundo desenfocado
        if (thoughtGroup != null)
        {
            Place((RectTransform)thoughtGroup.transform, thoughtAnchorMin, thoughtAnchorMax, new Vector2(0.5f, 0.5f));
            if (thoughtGroup.TryGetComponent(out Image band))
            {
                Color c = band.color;
                c.a = thoughtPanelAlpha;
                band.color = c;
            }
        }
        if (thoughtLabel != null)
        {
            Style(thoughtLabel, thoughtSize, thoughtColor, FontStyles.Italic);
            thoughtLabel.enableAutoSizing = true;
            thoughtLabel.fontSizeMax = thoughtSize;
            thoughtLabel.fontSizeMin = Mathf.Min(22f, thoughtSize);
            if (Application.isPlaying) // en edición no se crean instancias de material
            {
                thoughtLabel.outlineWidth = thoughtOutlineWidth;
                thoughtLabel.outlineColor = thoughtOutlineColor;
            }
        }
        if (noticeLabel != null) Style(noticeLabel, noticeSize, thoughtColor, FontStyles.Bold);
    }

    private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void FillBelowName(RectTransform rect, Vector2 innerMin, Vector2 innerMax, float nameHeight)
    {
        rect.anchorMin = innerMin;
        rect.anchorMax = innerMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(0f, -nameHeight);
    }

    private static void Style(TMP_Text label, float size, Color color, FontStyles style)
    {
        label.fontSize = size;
        label.color = color;
        label.fontStyle = style;
    }

    internal void SetOptionHover(TMP_Text label, bool hovered)
    {
        if (label == null) return;
        label.color = hovered ? optionHoverColor : optionColor;
        label.fontStyle = hovered ? FontStyles.Underline : FontStyles.Normal;
    }

    // ==================================================================
    // Abrir / cerrar
    // ==================================================================

    /// <summary>Abre/cierra el canvas (sin animación: el director anima con SetAlpha).</summary>
    public void SetOpen(bool open)
    {
        if (canvas != null) canvas.enabled = open;
        if (rootGroup != null)
        {
            rootGroup.interactable = open;
            rootGroup.blocksRaycasts = open;
        }
    }

    public void SetAlpha(float alpha)
    {
        if (rootGroup != null) rootGroup.alpha = alpha;
    }

    // ============ Papel ============

    /// <summary>Pone el nombre y el texto completo, sin letras visibles todavía. Devuelve cuántas letras tiene.</summary>
    public int ShowLine(string speaker, string text)
    {
        if (speakerLabel != null) speakerLabel.text = speaker;
        if (lineLabel == null) return 0;
        lineLabel.gameObject.SetActive(true);
        lineLabel.text = text;
        lineLabel.maxVisibleCharacters = 0;
        lineLabel.ForceMeshUpdate();
        return lineLabel.textInfo.characterCount;
    }

    /// <summary>Máquina de escribir: cuántas letras se ven.</summary>
    public void SetVisibleCharacters(int count)
    {
        if (lineLabel != null) lineLabel.maxVisibleCharacters = Mathf.Max(0, count);
    }

    // ============ Opciones ============

    /// <summary>Opciones en el papel, una por renglón con "1.", "2."… (ocupan el lugar del texto).</summary>
    public void ShowOptions(string speaker, string[] texts, Action<int> chosen)
    {
        _optionChosen = chosen;
        if (speakerLabel != null) speakerLabel.text = speaker;
        if (lineLabel != null) lineLabel.gameObject.SetActive(false);
        for (int i = 0; i < optionButtons.Length; i++)
        {
            bool used = texts != null && i < texts.Length && !string.IsNullOrEmpty(texts[i]);
            if (optionButtons[i] != null) optionButtons[i].gameObject.SetActive(used);
            if (i < optionLabels.Length && optionLabels[i] != null)
            {
                SetOptionHover(optionLabels[i], false);
                if (used) optionLabels[i].text = $"{i + 1}. {texts[i]}";
            }
        }
    }

    public void HideOptions()
    {
        _optionChosen = null;
        foreach (Button b in optionButtons)
            if (b != null) b.gameObject.SetActive(false);
    }

    /// <summary>Elegir por teclado (1/2/3) o por clic.</summary>
    public void PickOption(int index)
    {
        if (_optionChosen == null || index < 0 || index >= optionButtons.Length) return;
        if (optionButtons[index] == null || !optionButtons[index].gameObject.activeSelf) return;
        Action<int> callback = _optionChosen;
        HideOptions();
        callback(index);
    }

    // ============ Decisión ============

    public void ShowDecision(string first, string second, Action<int> chosen)
    {
        _decisionChosen = chosen;
        if (lineLabel != null) lineLabel.gameObject.SetActive(false);
        if (speakerLabel != null) speakerLabel.text = "";
        if (decisionLabels.Length > 0 && decisionLabels[0] != null) decisionLabels[0].text = first;
        if (decisionLabels.Length > 1 && decisionLabels[1] != null) decisionLabels[1].text = second;
        if (decisionRoot != null) decisionRoot.SetActive(true);
    }

    public void HideDecision()
    {
        _decisionChosen = null;
        if (decisionRoot != null) decisionRoot.SetActive(false);
    }

    public void PickDecision(int index)
    {
        if (_decisionChosen == null || index < 0 || index >= decisionButtons.Length) return;
        Action<int> callback = _decisionChosen;
        HideDecision();
        callback(index);
    }

    // ============ Pensamiento y aviso ============

    public void SetThought(string text, float alpha)
    {
        if (thoughtLabel != null) thoughtLabel.text = text;
        if (thoughtGroup != null) thoughtGroup.alpha = alpha;
    }

    /// <summary>Aviso corto (ej: "Pista: Registro de deudas"). Sin aviso en el prefab no hace nada.</summary>
    public void SetNotice(string text, float alpha)
    {
        if (noticeLabel != null) noticeLabel.text = text;
        if (noticeGroup != null) noticeGroup.alpha = alpha;
    }
}
