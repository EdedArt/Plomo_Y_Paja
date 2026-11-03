using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// Estilo de boton para el menu: fondo beige papel viejo, borde negro grueso
// y texto dorado. Al pasar el mouse crece un poco y se inclina, como un
// rotulo de caricatura de los 30.
// Poner en cada boton. Funciona en el editor sin darle Play.
[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class BotonCartoon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    ISelectHandler, IDeselectHandler, IPointerClickHandler, ISubmitHandler
{
    /// <summary>MainMenu lo activa al seleccionar por codigo, para que abrir un panel no suene.</summary>
    public static bool SilenciarHover;

    [Header("Sonido (mudo si el id no existe)")]
    [SerializeField] private string sonidoHover = "sfx_ui_hover";
    [SerializeField] private string sonidoClic = "sfx_ui_click";

    [Header("Colores")]
    [SerializeField] private Color beige = new Color(0.93f, 0.87f, 0.73f);
    [SerializeField] private Color beigeEncima = new Color(1f, 0.95f, 0.80f);
    [SerializeField] private Color dorado = new Color(0.72f, 0.52f, 0.13f);
    [SerializeField] private Color doradoEncima = new Color(0.85f, 0.63f, 0.16f);
    [SerializeField] private Color contorno = new Color(0.08f, 0.06f, 0.05f);

    [Header("Borde")]
    [SerializeField, Range(0f, 16f)] private float grosorBorde = 6f;

    [Header("Animacion")]
    [SerializeField] private float escalaEncima = 1.06f;
    [SerializeField] private float inclinacionEncima = -1.5f;
    [SerializeField, Range(1f, 30f)] private float velocidad = 14f;

    private Image fondo;
    private TMP_Text texto;
    private RectTransform rt;
    private RectTransform borde;
    private bool encima;
    private bool seleccionado;

    /// <summary>Suena el efecto solo si existe en la AudioLibrary (no avisa si falta: son placeholders).</summary>
    public static bool PlayIfPresent(string id, float volumen = 1f)
    {
        if (string.IsNullOrEmpty(id)) return false;
        AudioLibrary biblioteca = AudioLibrary.Active;
        if (biblioteca == null || !biblioteca.ContainsSfx(id)) return false;
        return AudioLibrary.PlaySfx2D(id, volumen);
    }

    private void OnEnable()
    {
        rt = (RectTransform)transform;
        fondo = GetComponent<Image>();
        texto = GetComponentInChildren<TMP_Text>();

        Aplicar();
    }

    private void Aplicar()
    {
        fondo.color = beige;

        if (texto != null)
        {
            texto.color = dorado;
            texto.fontStyle |= FontStyles.Bold;
            texto.alignment = TextAlignmentOptions.Center;

            // Contorno negro en las letras, para que se lean sobre el fondo.
            texto.outlineWidth = 0.22f;
            texto.outlineColor = contorno;
        }

        CrearBorde();
    }

    private void CrearBorde()
    {
        if (grosorBorde <= 0f) return;

        var hijo = transform.Find("Borde");
        if (hijo == null)
        {
            var go = new GameObject("Borde", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            go.transform.SetAsFirstSibling();
            hijo = go.transform;
        }

        borde = (RectTransform)hijo;
        borde.anchorMin = Vector2.zero;
        borde.anchorMax = Vector2.one;
        borde.offsetMin = new Vector2(-grosorBorde, -grosorBorde);
        borde.offsetMax = new Vector2(grosorBorde, grosorBorde);

        var img = borde.GetComponent<Image>();
        img.color = contorno;
        img.raycastTarget = false;
        img.sprite = fondo.sprite;
        img.type = fondo.type;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        encima = true;

        // Raton y teclado comparten seleccion: el boton bajo el raton es el activo.
        EventSystem sistema = EventSystem.current;
        if (sistema != null && sistema.currentSelectedGameObject != gameObject)
            sistema.SetSelectedGameObject(gameObject);
    }

    public void OnPointerExit(PointerEventData e) { encima = false; }

    public void OnSelect(BaseEventData e)
    {
        seleccionado = true;
        if (!SilenciarHover) PlayIfPresent(sonidoHover);
    }

    public void OnDeselect(BaseEventData e) { seleccionado = false; }
    public void OnPointerClick(PointerEventData e) { PlayIfPresent(sonidoClic); }
    public void OnSubmit(BaseEventData e) { PlayIfPresent(sonidoClic); }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            Aplicar();
            return;
        }

        float k = 1f - Mathf.Exp(-velocidad * Time.unscaledDeltaTime);

        bool activo = encima || seleccionado;

        float objetivo = activo ? escalaEncima : 1f;
        rt.localScale = Vector3.Lerp(rt.localScale, Vector3.one * objetivo, k);

        float giro = activo ? inclinacionEncima : 0f;
        var e = rt.localEulerAngles;
        e.z = Mathf.LerpAngle(e.z, giro, k);
        rt.localEulerAngles = e;

        fondo.color = Color.Lerp(fondo.color, activo ? beigeEncima : beige, k);
        if (texto != null)
            texto.color = Color.Lerp(texto.color, activo ? doradoEncima : dorado, k);
    }
}
