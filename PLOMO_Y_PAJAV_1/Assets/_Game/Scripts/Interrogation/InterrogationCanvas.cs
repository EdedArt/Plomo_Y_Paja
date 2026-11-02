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
/// 3C.4.1: la decisión también va escrita en el papel (sin botones de Unity ni cajas): "1. Jalar el gatillo" | "2. Dejarlo ir"
/// con una raya de tinta entre las dos; opciones y decisión usan el mismo resaltado (tinta roja, subrayado, escala y
/// temblor a saltos, InterrogationOptionHover). Modo "solo pensamiento" para el epílogo (SetThoughtOnly).
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
    [SerializeField] private float decisionSize = 52f;
    [SerializeField] private float thoughtSize = 36f;
    [SerializeField] private float noticeSize = 34f;
    [Tooltip("Tamaño mínimo al que se achica el texto largo")]
    [SerializeField] private float minBodySize = 26f;

    [Header("Diseño: colores")]
    [SerializeField] private Color nameColor = new Color32(0x3B, 0x24, 0x14, 0xFF);
    [SerializeField] private Color bodyColor = new Color32(0x1E, 0x12, 0x09, 0xFF);
    [SerializeField] private Color thoughtColor = new Color32(0xF2, 0xE6, 0xC8, 0xFF);
    [SerializeField] private Color thoughtOutlineColor = new Color32(0x14, 0x0C, 0x08, 0xFF);
    [SerializeField, Range(0f, 1f)] private float thoughtOutlineWidth = 0.22f;
    [SerializeField, Range(0f, 1f)] private float thoughtPanelAlpha = 0f;

    [Header("Diseño: opciones y decisión (la misma tinta)")]
    [SerializeField] private Color choiceColor = new Color32(0x1E, 0x12, 0x09, 0xFF);
    [Tooltip("Con el mouse encima: tinta roja + subrayado + escala + temblor a saltos")]
    [SerializeField] private Color choiceHoverColor = new Color32(0x8B, 0x1A, 0x1A, 0xFF);
    [SerializeField, Range(1f, 1.3f)] private float choiceHoverScale = 1.06f;
    [SerializeField, Min(0f)] private float choiceWobblePixels = 1.5f;
    [SerializeField, Min(1f)] private float choiceWobbleHz = 12f;
    [Tooltip("Raya de tinta entre las dos decisiones")]
    [SerializeField] private Color dividerColor = new Color32(0x1E, 0x12, 0x09, 0xB0);
    [SerializeField, Min(0f)] private float dividerWidth = 3f;
    [SerializeField, Range(0.1f, 1f)] private float dividerHeight = 0.75f;

    [Header("Diseño: pensamiento (arriba)")]
    [SerializeField] private Vector2 thoughtAnchorMin = new Vector2(0.08f, 0.88f);
    [SerializeField] private Vector2 thoughtAnchorMax = new Vector2(0.92f, 0.99f);

    private Action<int> _optionChosen;
    private Action<int> _decisionChosen;
    private bool _wired;

    public InterrogationPortrait Portrait => portrait;
    public int OptionCount => optionButtons.Length;

    // Estilo del resaltado (lo lee InterrogationOptionHover)
    public Color ChoiceColor => choiceColor;
    public Color ChoiceHoverColor => choiceHoverColor;
    public float ChoiceHoverScale => choiceHoverScale;
    public float ChoiceWobblePixels => choiceWobblePixels;
    public float ChoiceWobbleHz => choiceWobbleHz;

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
            if (decisionButtons[i] == null) continue;
            decisionButtons[i].onClick.AddListener(() => PickDecision(index));
            if (!decisionButtons[i].TryGetComponent(out InterrogationOptionHover hover))
                hover = decisionButtons[i].gameObject.AddComponent<InterrogationOptionHover>();
            hover.Init(i < decisionLabels.Length ? decisionLabels[i] : null, this);
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
            if (optionButtons[i] != null) MakeInkChoice(optionButtons[i]);
            if (i < optionLabels.Length && optionLabels[i] != null)
            {
                Style(optionLabels[i], optionSize, choiceColor, FontStyles.Normal);
                optionLabels[i].enableAutoSizing = false;
                optionLabels[i].enableWordWrapping = false;
                optionLabels[i].overflowMode = TextOverflowModes.Ellipsis;
                optionLabels[i].alignment = TextAlignmentOptions.MidlineLeft;
                ((RectTransform)optionLabels[i].transform).pivot = new Vector2(0f, 0.5f);
            }
        }

        // Decisión: escrita en el papel, cada una centrada en su mitad, raya de tinta en el medio
        if (decisionRoot != null)
        {
            if (decisionRoot.TryGetComponent(out HorizontalLayoutGroup row))
            {
                row.spacing = 0f;
                row.padding = new RectOffset(0, 0, 0, 0);
                row.childAlignment = TextAnchor.MiddleCenter;
                row.childControlWidth = row.childControlHeight = true;
                row.childForceExpandWidth = row.childForceExpandHeight = true;
            }
            EnsureDivider((RectTransform)decisionRoot.transform);
        }
        foreach (Button b in decisionButtons)
            if (b != null) MakeInkChoice(b);
        foreach (TMP_Text label in decisionLabels)
        {
            if (label == null) continue;
            Style(label, decisionSize, choiceColor, FontStyles.Normal);
            label.enableAutoSizing = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.alignment = TextAlignmentOptions.Center;
            var rect = (RectTransform)label.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
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

    /// <summary>Un Button sin aspecto de botón: sin transición de color, fondo invisible (sigue recibiendo el clic), sin bordes.</summary>
    private static void MakeInkChoice(Button button)
    {
        button.transition = Selectable.Transition.None;
        var nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
        if (button.targetGraphic is Image image)
        {
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
        }
        foreach (Shadow edge in button.GetComponents<Shadow>()) edge.enabled = false; // Outline hereda de Shadow
    }

    private void EnsureDivider(RectTransform parent)
    {
        Transform existing = parent.Find("Divisor");
        Image line;
        if (existing != null) line = existing.GetComponent<Image>();
        else
        {
            var go = new GameObject("Divisor", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            line = go.GetComponent<Image>();
        }
        if (line == null) return;
        line.color = dividerColor;
        line.raycastTarget = false;
        var rect = (RectTransform)line.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f - dividerHeight * 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f + dividerHeight * 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(dividerWidth, 0f);
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
            if (used && i < optionLabels.Length && optionLabels[i] != null) optionLabels[i].text = $"{i + 1}. {texts[i]}";
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

    /// <summary>La decisión escrita en el papel: "1. primera" | "2. segunda", con el nombre de quien decide (WILBUR).</summary>
    public void ShowDecision(string speaker, string first, string second, Action<int> chosen)
    {
        _decisionChosen = chosen;
        if (lineLabel != null) lineLabel.gameObject.SetActive(false);
        if (speakerLabel != null) speakerLabel.text = speaker;
        if (decisionLabels.Length > 0 && decisionLabels[0] != null) decisionLabels[0].text = "1. " + first;
        if (decisionLabels.Length > 1 && decisionLabels[1] != null) decisionLabels[1].text = "2. " + second;
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

    /// <summary>Epílogo: solo la banda del pensamiento (sin papel, retrato ni aviso).</summary>
    public void SetThoughtOnly(bool thoughtOnly)
    {
        GameObject paper = speakerLabel != null && speakerLabel.transform.parent != null ? speakerLabel.transform.parent.gameObject : null;
        if (paper != null) paper.SetActive(!thoughtOnly);
        if (thoughtOnly)
        {
            SetNotice("", 0f);
            HideOptions();
            HideDecision();
        }
    }

    /// <summary>Aviso corto (ej: "Pista: Registro de deudas"). Sin aviso en el prefab no hace nada.</summary>
    public void SetNotice(string text, float alpha)
    {
        if (noticeLabel != null) noticeLabel.text = text;
        if (noticeGroup != null) noticeGroup.alpha = alpha;
    }
}
