using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PISTAS.1 (P2): la HOJA DE PISTA. Papel del interrogatorio + Special Elite en el centro de la pantalla: título, imagen
/// opcional, texto y "E / clic para cerrar". Canvas propio (orden 50: encima del HUD 4 / mira 5 / IA 10, debajo del diálogo
/// 100 y del interrogatorio 300).
///
/// MIENTRAS ESTÁ ABIERTA: Time.timeScale = 0 (todo en tiempo real), el jugador no mira ni camina (SetCanMove) y se usan
/// dos estados que ya existen para no tocar archivos de otros (ver auditoría PISTAS.1):
/// - CinematicState (dueño = la hoja): PlayerAim.GameplayBlocked() → sin disparo, mira ni manos; GameManager no abre la
///   pausa con Esc; los guardias no actúan.
/// - UiHideReasons.Reason.Cinematic: HUD y manos ocultos.
/// Se cierra con E, clic o Esc tras 0.5 s. Esos estados se sueltan UN CUADRO DESPUÉS de cerrar: así la misma pulsación de
/// Esc no abre la pausa y la misma E/clic no dispara ni toma otro objeto.
///
/// DÓNDE VA: prefab Resources/Clues/ClueCard (menú Plomo y Paja → Pistas → Crear hoja de pista). Si una escena no lo tiene,
/// Show() lo instancia de Resources; si tampoco existe, la arma por código con la fuente por defecto (nunca falta).
/// </summary>
public class ClueCard : MonoBehaviour
{
    public const string ResourcePath = "Clues/ClueCard";
    public const int SortingOrder = 50;
    private const float MinOpenSeconds = 0.5f;
    private const string CloseHint = "E / clic para cerrar";

    private static readonly Color Ink = new Color32(0x2A, 0x1F, 0x1A, 0xFF);
    private static readonly Color HintInk = new Color32(0x6E, 0x66, 0x5E, 0xFF);
    private static readonly Color Cream = new Color32(0xF2, 0xE6, 0xC8, 0xFF);

    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Image picture;
    [SerializeField] private TMP_Text hintText;

    public static ClueCard Instance { get; private set; }

    /// <summary>¿Hay una hoja abierta? (o se acaba de cerrar este cuadro)</summary>
    public static bool IsOpen => Instance != null && Instance._open;

    private bool _open;
    private float _openedAt;
    private float _savedTimeScale = 1f;
    private PlayerController _frozenPlayer;
    private Action _onClosed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Hide();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        if (_open) ReleaseGameplay(); // cambio de escena con la hoja abierta: no dejar el juego congelado
        Instance = null;
    }

    /// <summary>Abre la hoja. onClosed se llama al cerrarla (ya con el juego en marcha). false si no se pudo abrir.</summary>
    public static bool Show(string title, string body, Sprite image, Action onClosed)
    {
        ClueCard card = FindOrCreate();
        if (card == null) return false;
        card.Open(title, body, image, onClosed);
        return true;
    }

    private static ClueCard FindOrCreate()
    {
        if (Instance != null) return Instance;
        ClueCard prefab = Resources.Load<ClueCard>(ResourcePath);
        if (prefab != null) Instantiate(prefab).name = "ClueCard";
        else Build(null, null, null);   // sin prefab: la misma hoja hecha por código, con la fuente por defecto
        return Instance;                // Awake ya la registró
    }

    private void Open(string title, string body, Sprite image, Action onClosed)
    {
        if (_open) Close(false);
        _onClosed = onClosed;
        if (titleText != null) titleText.text = title;
        if (bodyText != null) bodyText.text = body;
        if (picture != null)
        {
            picture.sprite = image;
            picture.gameObject.SetActive(image != null);
        }
        if (hintText != null) hintText.text = CloseHint;

        _open = true;
        _openedAt = Time.unscaledTime;
        _savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        CinematicState.Set(this, true);
        UiHideReasons.Set(UiHideReasons.Reason.Cinematic, true);
        GameObject player = GameObject.FindWithTag("Player");
        _frozenPlayer = null;
        if (player != null && player.TryGetComponent(out _frozenPlayer)) _frozenPlayer.SetCanMove(false);

        gameObject.SetActive(true);
        if (group != null) group.alpha = 1f;
    }

    private void Update()
    {
        if (!_open || Time.unscaledTime - _openedAt < MinOpenSeconds) return;
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(0))
            Close(true);
    }

    private void Close(bool notify)
    {
        _open = false;
        Hide();
        Time.timeScale = _savedTimeScale;
        if (_frozenPlayer != null) _frozenPlayer.SetCanMove(true);
        _frozenPlayer = null;
        // Esta misma pulsación todavía la leen GameManager (Esc → pausa) y las armas/objetos (E, clic): el bloqueo sigue
        // hasta el cuadro siguiente
        StartCoroutine(ReleaseNextFrame());

        Action callback = _onClosed;
        _onClosed = null;
        if (notify && callback != null) callback();
    }

    private IEnumerator ReleaseNextFrame()
    {
        yield return null;
        if (!_open) ReleaseGameplay();
    }

    private void ReleaseGameplay()
    {
        CinematicState.Set(this, false);
        UiHideReasons.Set(UiHideReasons.Reason.Cinematic, false);
        if (_open)
        {
            Time.timeScale = _savedTimeScale;
            if (_frozenPlayer != null) _frozenPlayer.SetCanMove(true);
        }
    }

    /// <summary>Oculta el contenido sin apagar el objeto (la corrutina del cierre necesita el objeto activo).</summary>
    private void Hide()
    {
        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }
    }

    // ==================================================================
    // Construcción (la usan el menú "Crear hoja de pista" y el respaldo en runtime)
    // ==================================================================

    /// <summary>
    /// Arma la hoja completa. font = Special Elite (null = la de TMP); paper = ui_papel_dialogo en 9-slice (null = panel
    /// crema con borde). Referencia 1920×1080.
    /// </summary>
    public static ClueCard Build(TMP_FontAsset font, Sprite paper, Transform parent)
    {
        var root = new GameObject("ClueCard", typeof(RectTransform));
        if (parent != null) root.transform.SetParent(parent, false);
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        CanvasGroup rootGroup = root.AddComponent<CanvasGroup>();
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;

        // Fondo oscurecido
        RectTransform dim = NewRect("Fondo", root.transform);
        Stretch(dim);
        Image dimImage = dim.gameObject.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.55f);
        dimImage.raycastTarget = false;

        // El papel crece con el texto (ancho fijo, alto preferido)
        RectTransform sheet = NewRect("Papel", root.transform);
        sheet.anchorMin = sheet.anchorMax = new Vector2(0.5f, 0.5f);
        sheet.pivot = new Vector2(0.5f, 0.5f);
        sheet.sizeDelta = new Vector2(1000f, 600f);
        Image sheetImage = sheet.gameObject.AddComponent<Image>();
        sheetImage.raycastTarget = false;
        if (paper != null)
        {
            sheetImage.sprite = paper;
            sheetImage.type = Image.Type.Sliced;
            sheetImage.pixelsPerUnitMultiplier = 1.5f;
        }
        else
        {
            sheetImage.color = Cream;
            Outline border = sheet.gameObject.AddComponent<Outline>();
            border.effectColor = Ink;
            border.effectDistance = new Vector2(4f, -4f);
        }
        VerticalLayoutGroup layout = sheet.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(110, 110, 90, 70);
        layout.spacing = 22f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = sheet.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TMP_Text title = Text("Titulo", sheet, font, 54f, FontStyles.Bold, TextAlignmentOptions.Center, Ink);

        RectTransform pictureRect = NewRect("Imagen", sheet);
        Image pictureImage = pictureRect.gameObject.AddComponent<Image>();
        pictureImage.preserveAspect = true;
        pictureImage.raycastTarget = false;
        LayoutElement pictureLayout = pictureRect.gameObject.AddComponent<LayoutElement>();
        pictureLayout.preferredHeight = 380f;
        pictureLayout.minHeight = 380f;
        pictureRect.gameObject.SetActive(false);

        TMP_Text body = Text("Texto", sheet, font, 32f, FontStyles.Normal, TextAlignmentOptions.TopLeft, Ink);
        body.lineSpacing = 8f;
        TMP_Text hint = Text("Cerrar", sheet, font, 24f, FontStyles.Italic, TextAlignmentOptions.Center, HintInk);
        hint.text = CloseHint;

        ClueCard card = root.AddComponent<ClueCard>();
        card.group = rootGroup;
        card.titleText = title;
        card.bodyText = body;
        card.picture = pictureImage;
        card.hintText = hint;
        card.Hide();
        return card;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, float size, FontStyles style,
        TextAlignmentOptions alignment, Color color)
    {
        RectTransform rect = NewRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }
}
