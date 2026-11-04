using UnityEngine;
using UnityEngine.UI;

// Anima el fondo del menu con varios frames.
// Modo Hervido: corta entre frames, estilo caricatura de los 30. Solo se ve
//   bien si los frames son casi identicos.
// Modo Fundido: mezcla suavemente entre frames. Sirve cuando los dibujos
//   tienen diferencias grandes.
// Dos grupos, cada uno con su modo: "frames" (espera, atardecer) y "framesTormenta" (tras el
//   rayo: el campo "modo"). Ambos con fundido lento y respiracion, porque los cuadros son
//   redibujos, no casi identicos. CutToStorm salta de uno a otro sin fundido.
// Movimiento de camara lento sobre todo el fondo: depende solo del tiempo, no del grupo, asi
//   que el corte del rayo no lo hace saltar.
// La imagen cubre la pantalla sin deformarse (16:9, 16:10, 21:9) y se mueve con
//   el raton (parallax de todo el fondo).
// Usa tiempo sin escala para seguir animando con Time.timeScale en 0.
[RequireComponent(typeof(Image))]
public class MenuFrameAnimator : MonoBehaviour
{
    public enum Modo { Hervido, Fundido }

    [SerializeField] private Sprite[] frames;
    [SerializeField] private Sprite[] framesTormenta;
    [Tooltip("Modo del grupo de tormenta")]
    [SerializeField] private Modo modo = Modo.Fundido;

    [Header("Grupo de espera")]
    [SerializeField] private Modo modoEspera = Modo.Fundido;
    [Tooltip("Segundos que cada cuadro se mantiene antes de mezclarse con el siguiente")]
    [SerializeField, Range(0.2f, 8f)] private float mantenerEspera = 2f;
    [SerializeField, Range(0.1f, 5f)] private float fundidoEspera = 1.2f;
    [SerializeField] private bool respirarEspera = true;

    [Header("Libro animado de la espera (sin usar: solo con modoEspera = Hervido)")]
    [SerializeField, Range(2f, 8f)] private float fpsEspera = 4f;
    [Tooltip("Variacion al azar en lo que dura cada cuadro (0.15 = +-15 %)")]
    [SerializeField, Range(0f, 0.5f)] private float variacionEspera = 0.15f;

    [Header("Hervido")]
    [SerializeField, Range(1f, 24f)] private float fps = 12f;
    [SerializeField, Range(0f, 0.5f)] private float irregularidad = 0.25f;

    [Header("Fundido")]
    [SerializeField, Range(0.2f, 5f)] private float segundosPorFrame = 1.6f;
    [SerializeField, Range(0.1f, 3f)] private float duracionFundido = 0.8f;

    [Header("Respiracion (opcional)")]
    [SerializeField] private bool respirar = true;
    [SerializeField, Range(0f, 0.03f)] private float amplitud = 0.012f;
    [SerializeField, Range(0.1f, 2f)] private float velocidadRespiracion = 0.45f;

    [Header("Cubrir la pantalla y parallax")]
    [SerializeField] private bool cubrirPantalla = true;
    [SerializeField, Range(0f, 40f)] private float parallaxPx = 12f;
    [SerializeField] private float parallaxSuavizado = 4f;
    [Tooltip("Margen extra de la imagen para que la sacudida del rayo no deje ver el borde")]
    [SerializeField] private float margenSacudidaPx = 6f;

    [Header("Movimiento de camara (espera y tormenta)")]
    [SerializeField] private bool moverCamara = true;
    [Tooltip("Zoom en el extremo del recorrido (1.04 = de 1.00 a 1.04)")]
    [SerializeField, Range(1f, 1.2f)] private float zoomCamara = 1.04f;
    [Tooltip("Desplazamiento +- esta fraccion del ancho de pantalla")]
    [SerializeField, Range(0f, 0.1f)] private float recorridoX = 0.015f;
    [Tooltip("Desplazamiento +- esta fraccion del alto de pantalla")]
    [SerializeField, Range(0f, 0.1f)] private float recorridoY = 0.01f;
    [Tooltip("Segundos de la ida y vuelta completa")]
    [SerializeField, Range(2f, 120f)] private float periodoCamara = 20f;

    private Image imagenA;      // frame actual
    private Image imagenB;      // frame siguiente, solo en modo Fundido
    private RectTransform rt;
    private RectTransform rtB;
    private RectTransform padre;
    private Sprite[] activos;
    private Modo modoActivo;
    private bool enEspera = true;
    private int indice;
    private float siguienteCambio;
    private float inicioFundido = -1f;
    private Vector3 escalaBase;
    private Vector2 tamanoPadre;
    private Vector2 parallaxActual;
    private Vector2 desplazamientoExtra;
    private float inicioCamara;
    private float avanceCamara;

    private void Awake()
    {
        imagenA = GetComponent<Image>();
        rt = GetComponent<RectTransform>();
        padre = rt.parent as RectTransform;
        escalaBase = rt.localScale;

        frames = SinNulos(frames);
        framesTormenta = SinNulos(framesTormenta);
        activos = frames;
        modoActivo = modoEspera;
        inicioCamara = Time.unscaledTime;

        if (modo == Modo.Fundido || modoEspera == Modo.Fundido) CrearCapaB();
        if (activos.Length > 0)
        {
            imagenA.sprite = activos[0];
        }
        else if (imagenA.sprite == null)
        {
            // Escena sin regenerar: fondo oscuro en vez de un rectangulo blanco.
            imagenA.color = new Color(0.08f, 0.06f, 0.05f);
            Debug.LogWarning("[Menu] El fondo no tiene cuadros. Corre 'Plomo y Paja/Crear menu de inicio'.");
        }

        if (cubrirPantalla)
        {
            var centro = new Vector2(0.5f, 0.5f);
            rt.anchorMin = centro;
            rt.anchorMax = centro;
            rt.pivot = centro;
            rt.anchoredPosition = Vector2.zero;
            AjustarCobertura();
        }

        siguienteCambio = Time.unscaledTime + Intervalo();
    }

    private static Sprite[] SinNulos(Sprite[] origen)
    {
        if (origen == null) return new Sprite[0];
        int n = 0;
        for (int i = 0; i < origen.Length; i++) if (origen[i] != null) n++;
        var limpio = new Sprite[n];
        n = 0;
        for (int i = 0; i < origen.Length; i++) if (origen[i] != null) limpio[n++] = origen[i];
        return limpio;
    }

    private void CrearCapaB()
    {
        // Capa encima que entra con alfa de 0 a 1 para el fundido.
        var go = new GameObject("FrameFundido", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);

        rtB = (RectTransform)go.transform;
        if (cubrirPantalla)
        {
            var centro = new Vector2(0.5f, 0.5f);
            rtB.anchorMin = centro;
            rtB.anchorMax = centro;
            rtB.pivot = centro;
            rtB.anchoredPosition = Vector2.zero;
        }
        else
        {
            rtB.anchorMin = Vector2.zero;
            rtB.anchorMax = Vector2.one;
            rtB.offsetMin = Vector2.zero;
            rtB.offsetMax = Vector2.zero;
        }

        imagenB = go.GetComponent<Image>();
        imagenB.raycastTarget = false;
        imagenB.color = new Color(1f, 1f, 1f, 0f);
    }

    private float Intervalo()
    {
        // En la espera el intervalo cuenta lo que se mantiene mas lo que dura la mezcla.
        if (modoActivo == Modo.Fundido) return enEspera ? mantenerEspera + fundidoEspera : segundosPorFrame;

        // Pequena variacion para que no suene a metronomo.
        float baseT = 1f / Mathf.Max(enEspera ? fpsEspera : fps, 0.01f);
        float variacion = enEspera ? variacionEspera : irregularidad;
        return baseT * (1f + Random.Range(-variacion, variacion));
    }

    /// <summary>
    /// Corte seco (sin fundido) al primer cuadro de tormenta; desde ahi sigue el
    /// mismo ciclo con ese grupo. Devuelve false si no hay cuadros de tormenta.
    /// </summary>
    public bool CutToStorm()
    {
        if (framesTormenta == null || framesTormenta.Length == 0) return false;

        activos = framesTormenta;
        enEspera = false;
        modoActivo = modo;
        indice = 0;
        inicioFundido = -1f;
        imagenA.sprite = activos[0];
        if (imagenB != null) imagenB.color = new Color(1f, 1f, 1f, 0f);
        siguienteCambio = Time.unscaledTime + Intervalo();
        if (cubrirPantalla) AjustarCobertura();
        return true;
    }

    /// <summary>Desplazamiento adicional en px (la sacudida del rayo).</summary>
    public void SetExtraOffset(Vector2 px)
    {
        desplazamientoExtra = px;
    }

    private void AjustarCobertura()
    {
        if (padre == null) return;
        tamanoPadre = padre.rect.size;
        if (imagenA.sprite != null) rt.sizeDelta = TamanoCubierta(imagenA.sprite);
        if (rtB != null && imagenB.sprite != null) rtB.sizeDelta = TamanoCubierta(imagenB.sprite);
    }

    // Tamano que cubre al padre (mas el margen de parallax, sacudida y recorrido de camara) sin
    // deformar el sprite. Con ese margen el fondo cubre aun con zoom 1.00 en el punto mas desplazado.
    private Vector2 TamanoCubierta(Sprite s)
    {
        float margen = 2f * (parallaxPx + margenSacudidaPx) + 2f;
        float ancho = tamanoPadre.x + margen + (moverCamara ? 2f * recorridoX * tamanoPadre.x : 0f);
        float alto = tamanoPadre.y + margen + (moverCamara ? 2f * recorridoY * tamanoPadre.y : 0f);
        float aspecto = s.rect.width / Mathf.Max(1f, s.rect.height);
        return ancho / Mathf.Max(1f, alto) > aspecto
            ? new Vector2(ancho, ancho / aspecto)
            : new Vector2(alto * aspecto, alto);
    }

    private void Update()
    {
        float t = Time.unscaledTime;

        if (cubrirPantalla && padre != null && padre.rect.size != tamanoPadre) AjustarCobertura();
        if (activos != null && activos.Length >= 2) ActualizarCuadros(t);

        bool camara = moverCamara && cubrirPantalla;
        if (camara)
        {
            // Ida y vuelta 0 -> 1 -> 0, con ease in-out.
            float ciclo = Mathf.Repeat((t - inicioCamara) / periodoCamara, 1f);
            float tramo = 1f - Mathf.Abs(ciclo * 2f - 1f);
            avanceCamara = tramo * tramo * (3f - 2f * tramo);
        }

        float s = 1f;
        if (enEspera ? respirarEspera : respirar)
        {
            float onda = Mathf.Sin(t * velocidadRespiracion * Mathf.PI * 2f) * amplitud;
            // Cubriendo, la escala nunca baja de 1: el borde no se asoma al "exhalar".
            s += onda + (cubrirPantalla ? amplitud : 0f);
        }
        if (camara) s *= Mathf.Lerp(1f, zoomCamara, avanceCamara);
        rt.localScale = escalaBase * s;

        if (cubrirPantalla) ActualizarPosicion();
    }

    // Segundos de mezcla del grupo activo; 0 = corte seco.
    private float DuracionMezcla()
    {
        if (imagenB == null) return 0f;
        if (modoActivo == Modo.Fundido) return enEspera ? fundidoEspera : duracionFundido;
        return 0f;
    }

    private void ActualizarCuadros(float t)
    {
        float mezcla = DuracionMezcla();

        if (t >= siguienteCambio)
        {
            siguienteCambio = t + Intervalo();
            int sig = (indice + 1) % activos.Length;

            if (mezcla <= 0f)
            {
                indice = sig;
                imagenA.sprite = activos[indice];
                if (cubrirPantalla) AjustarCobertura();
            }
            else if (inicioFundido < 0f)
            {
                imagenB.sprite = activos[sig];
                if (cubrirPantalla) rtB.sizeDelta = TamanoCubierta(imagenB.sprite);
                inicioFundido = t;
            }
        }

        if (mezcla > 0f && inicioFundido >= 0f)
        {
            float k = Mathf.Clamp01((t - inicioFundido) / mezcla);
            k = k * k * (3f - 2f * k);                     // suavizado
            imagenB.color = new Color(1f, 1f, 1f, k);

            if (k >= 1f)
            {
                indice = (indice + 1) % activos.Length;
                imagenA.sprite = activos[indice];
                imagenB.color = new Color(1f, 1f, 1f, 0f);
                inicioFundido = -1f;
                if (cubrirPantalla) AjustarCobertura();
            }
        }
    }

    private void ActualizarPosicion()
    {
        Vector2 objetivo = Vector2.zero;
        if (parallaxPx > 0f && Screen.width > 0 && Screen.height > 0)
        {
            Vector3 m = Input.mousePosition;
            objetivo.x = -Mathf.Clamp(m.x / Screen.width * 2f - 1f, -1f, 1f) * parallaxPx;
            objetivo.y = -Mathf.Clamp(m.y / Screen.height * 2f - 1f, -1f, 1f) * parallaxPx;
        }
        float k = 1f - Mathf.Exp(-parallaxSuavizado * Time.unscaledDeltaTime);
        parallaxActual = Vector2.Lerp(parallaxActual, objetivo, k);
        Vector2 camara = Vector2.zero;
        if (moverCamara)
        {
            camara.x = Mathf.Lerp(-recorridoX, recorridoX, avanceCamara) * tamanoPadre.x;
            camara.y = Mathf.Lerp(-recorridoY, recorridoY, avanceCamara) * tamanoPadre.y;
        }
        rt.anchoredPosition = parallaxActual + desplazamientoExtra + camara;
    }
}
