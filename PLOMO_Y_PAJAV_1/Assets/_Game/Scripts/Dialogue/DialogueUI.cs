using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI de interrogatorios. Se crea sola la primera vez que alguien usa DialogueUI.Instance:
/// arma su propio Canvas (Screen Space Overlay), sin prefab.
///
/// - Panel oscuro abajo: nombre del NPC, línea con efecto máquina de escribir
///   (clic o Espacio la completa) y hasta 3 opciones ("1." "2." "3.")
/// - Opciones con clic o con las teclas 1-3
/// - También muestra el prompt "[E] Interrogar"
///
/// sortingOrder por encima de los subtítulos de AICompanion.
/// </summary>
public class DialogueUI : MonoBehaviour
{
    private static DialogueUI _instance;

    /// <summary>Instancia única (se crea si no existe).</summary>
    public static DialogueUI Instance
    {
        get
        {
            if (_instance == null)
                new GameObject("DialogueUI").AddComponent<DialogueUI>();
            return _instance;
        }
    }

    /// <summary>True si ya existe (para no crearla al descargar la escena).</summary>
    public static bool Exists => _instance != null;

    [Header("Estilo")]
    [Tooltip("Color del texto (crema)")]
    [SerializeField] private Color textColor = new Color32(0xE8, 0xE2, 0xD0, 0xFF);

    [Tooltip("Color del panel de fondo (oscuro semitransparente)")]
    [SerializeField] private Color panelColor = new Color(0.04f, 0.04f, 0.05f, 0.85f);

    [Tooltip("Color de fondo de una opción con el mouse encima")]
    [SerializeField] private Color optionHighlightColor = new Color(1f, 0.69f, 0.38f, 0.25f);

    [Tooltip("Orden del canvas (debe quedar sobre los subtítulos de la IA)")]
    [SerializeField] private int sortingOrder = 100;

    [Header("Máquina de escribir")]
    [Tooltip("Letras por segundo")]
    [SerializeField] private float charsPerSecond = 40f;

    private const int MaxOptions = 3;

    private Canvas _canvas;
    private GameObject _panel;
    private TextMeshProUGUI _speakerText;
    private TextMeshProUGUI _lineText;
    private TextMeshProUGUI _promptText;
    private readonly List<Button> _optionButtons = new List<Button>();
    private readonly List<TextMeshProUGUI> _optionTexts = new List<TextMeshProUGUI>();

    private Action<int> _onOptionSelected;
    private int _optionCount;
    private bool _optionsVisible;
    private Coroutine _typeRoutine;

    /// <summary>True mientras el panel de diálogo está abierto.</summary>
    public bool IsOpen => _panel != null && _panel.activeSelf;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        BuildCanvas();
        EnsureEventSystem();
        _panel.SetActive(false);
        _promptText.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        if (!IsOpen) return;

        // Completar la línea si se está escribiendo
        if (_typeRoutine != null)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
                FinishTyping();
            return;
        }

        if (!_optionsVisible) return;

        for (int i = 0; i < _optionCount; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
            {
                Select(i);
                return;
            }
        }
    }

    // ============ API pública ============

    /// <summary>
    /// Mostrar una línea del NPC con sus opciones (máximo 3).
    /// onOptionSelected recibe el índice elegido (0, 1 o 2).
    /// </summary>
    public void ShowLine(string speaker, string line, IList<string> options, Action<int> onOptionSelected)
    {
        HidePrompt();
        _panel.SetActive(true);
        _speakerText.text = speaker;
        _onOptionSelected = onOptionSelected;
        _optionCount = Mathf.Min(options?.Count ?? 0, MaxOptions);

        for (int i = 0; i < MaxOptions; i++)
        {
            if (i < _optionCount)
                _optionTexts[i].text = $"{i + 1}. {options[i]}";
            _optionButtons[i].gameObject.SetActive(false);
        }
        _optionsVisible = false;

        if (_typeRoutine != null) StopCoroutine(_typeRoutine);
        _typeRoutine = StartCoroutine(TypeLine(line));
    }

    /// <summary>Cerrar el panel de diálogo.</summary>
    public void Hide()
    {
        if (_typeRoutine != null) StopCoroutine(_typeRoutine);
        _typeRoutine = null;
        _onOptionSelected = null;
        _optionsVisible = false;
        _panel.SetActive(false);
    }

    public void ShowPrompt(string text)
    {
        _promptText.text = text;
        _promptText.gameObject.SetActive(true);
    }

    public void HidePrompt()
    {
        if (_promptText != null) _promptText.gameObject.SetActive(false);
    }

    // ============ Lógica interna ============

    private IEnumerator TypeLine(string line)
    {
        _lineText.text = line;
        _lineText.maxVisibleCharacters = 0;

        float shown = 0f;
        while (shown < line.Length)
        {
            shown += charsPerSecond * Time.unscaledDeltaTime;
            _lineText.maxVisibleCharacters = Mathf.Min(line.Length, Mathf.FloorToInt(shown));
            yield return null;
        }

        _typeRoutine = null;
        ShowOptions();
    }

    private void FinishTyping()
    {
        if (_typeRoutine != null) StopCoroutine(_typeRoutine);
        _typeRoutine = null;
        _lineText.maxVisibleCharacters = _lineText.text.Length;
        ShowOptions();
    }

    private void ShowOptions()
    {
        for (int i = 0; i < _optionCount; i++)
            _optionButtons[i].gameObject.SetActive(true);
        _optionsVisible = true;
    }

    private void Select(int index)
    {
        if (!_optionsVisible || index < 0 || index >= _optionCount) return;

        // Ocultar antes de avisar, para que un doble clic no elija dos veces
        _optionsVisible = false;
        for (int i = 0; i < MaxOptions; i++)
            _optionButtons[i].gameObject.SetActive(false);
        // Que Espacio (Submit) no vuelva a "pulsar" el último botón clicado
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        Action<int> callback = _onOptionSelected;
        _onOptionSelected = null;
        callback?.Invoke(index);
    }

    // ============ Construcción del Canvas ============

    private void BuildCanvas()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        // Panel inferior
        _panel = CreateRect("Panel", transform, new Vector2(0.12f, 0f), new Vector2(0.88f, 0f), new Vector2(0.5f, 0f));
        RectTransform panelRect = (RectTransform)_panel.transform;
        panelRect.anchoredPosition = new Vector2(0f, 40f);
        panelRect.sizeDelta = new Vector2(0f, 380f);
        _panel.AddComponent<Image>().color = panelColor;

        // Nombre del NPC
        _speakerText = CreateText("Speaker", _panel.transform, 36f, FontStyles.Bold | FontStyles.UpperCase);
        SetStretchTop((RectTransform)_speakerText.transform, 20f, 50f);

        // Línea del NPC
        _lineText = CreateText("Line", _panel.transform, 32f, FontStyles.Normal);
        SetStretchTop((RectTransform)_lineText.transform, 75f, 110f);

        // Opciones
        for (int i = 0; i < MaxOptions; i++)
        {
            int index = i; // capturar para el closure
            GameObject optionGo = CreateRect($"Option_{i + 1}", _panel.transform, Vector2.zero, Vector2.zero, Vector2.zero);
            SetStretchTop((RectTransform)optionGo.transform, 195f + i * 56f, 50f);

            Image background = optionGo.AddComponent<Image>();
            background.color = Color.white;

            Button button = optionGo.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = optionHighlightColor;
            colors.selectedColor = new Color(1f, 1f, 1f, 0f);
            colors.pressedColor = optionHighlightColor * 1.4f;
            button.colors = colors;
            button.onClick.AddListener(() => Select(index));

            TextMeshProUGUI label = CreateText("Label", optionGo.transform, 30f, FontStyles.Normal);
            RectTransform labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(16f, 0f);
            labelRect.offsetMax = new Vector2(-16f, 0f);
            label.alignment = TextAlignmentOptions.MidlineLeft;

            _optionButtons.Add(button);
            _optionTexts.Add(label);
        }

        // Prompt de interacción (debajo del centro de la pantalla)
        _promptText = CreateText("Prompt", transform, 34f, FontStyles.Bold);
        RectTransform promptRect = (RectTransform)_promptText.transform;
        promptRect.anchorMin = promptRect.anchorMax = new Vector2(0.5f, 0.38f);
        promptRect.pivot = new Vector2(0.5f, 0.5f);
        promptRect.sizeDelta = new Vector2(600f, 60f);
        _promptText.alignment = TextAlignmentOptions.Center;
    }

    private static GameObject CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        return go;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, float size, FontStyles style)
    {
        GameObject go = CreateRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.color = textColor;
        text.enableWordWrapping = true;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>Estirar a lo ancho del padre, a "top" píxeles del borde superior.</summary>
    private static void SetStretchTop(RectTransform rect, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(30f, 0f);
        rect.offsetMax = new Vector2(-30f, 0f);
        rect.anchoredPosition = new Vector2(0f, -top);
        rect.sizeDelta = new Vector2(-60f, height);
    }

    /// <summary>Los botones necesitan un EventSystem (input clásico) en la escena.</summary>
    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
