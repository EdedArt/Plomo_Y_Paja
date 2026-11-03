using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menu de inicio. Los metodos publicos se conectan a los botones (OnClick).
// Estados: Espera ("Pulsa cualquier tecla") -> Rayo -> Menu -> Cargando.
// Si la escena aun no se regenero (sin prompt ni botones asignados) va directo a Menu.
// Teclado: navegacion del EventSystem (flechas/WS, Enter); Esc vuelve al panel principal.
public class MainMenu : MonoBehaviour
{
    private enum Estado { Espera, Rayo, Menu, Cargando }

    [SerializeField] private string escenaJuego = "Intro";
    [SerializeField] private GameObject panelPrincipal;
    [SerializeField] private GameObject panelOpciones;
    [SerializeField] private GameObject panelCreditos;

    [Header("Pantalla de espera")]
    [SerializeField] private CanvasGroup promptEspera;
    [SerializeField] private float pulsoSegundos = 1.6f;

    [Header("Rayo y aparicion de botones")]
    [SerializeField] private LightningFlash rayo;
    [SerializeField] private Button[] botones;
    [Tooltip("Segundos desde el rayo hasta que empiezan a entrar los botones")]
    [SerializeField] private float retrasoBotones = 0.6f;
    [SerializeField] private float escalonBotones = 0.08f;
    [SerializeField] private float duracionBoton = 0.35f;
    [SerializeField] private float deslizamientoBoton = 40f;

    [Header("Logo")]
    [SerializeField] private RectTransform logo;
    [Tooltip("En la espera el logo esta esto mas abajo; con el rayo sube a su posicion del menu")]
    [SerializeField] private float subidaLogo = 45f;
    [SerializeField] private float duracionSubidaLogo = 0.6f;

    [Header("Jugar")]
    [SerializeField] private ScreenFade fundido;
    [SerializeField] private float fundidoSegundos = 0.8f;
    [Tooltip("Se usa el primero que exista en la AudioLibrary")]
    [SerializeField] private string[] sonidosJugar = { "sfx_ui_jugar", "sfx_escopeta_bombeo", "escopeta_bombeo" };

    private Estado estado = Estado.Menu;
    private CanvasGroup[] gruposBotones;
    private Vector2[] posicionesBotones;
    private Vector2 posicionLogo;

    private void Start()
    {
        // Por si se llega desde una partida pausada.
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (fundido != null) fundido.Alpha = 0f;
        if (logo != null) posicionLogo = logo.anchoredPosition;

        if (promptEspera != null && botones != null && botones.Length > 0)
        {
            PrepararEspera();
        }
        else
        {
            if (promptEspera != null) promptEspera.alpha = 0f;
            MostrarPrincipal();
        }
    }

    // ---------- Espera ----------

    private void PrepararEspera()
    {
        estado = Estado.Espera;
        Mostrar(panelPrincipal);

        gruposBotones = new CanvasGroup[botones.Length];
        posicionesBotones = new Vector2[botones.Length];
        for (int i = 0; i < botones.Length; i++)
        {
            if (botones[i] == null) continue;
            var grupo = botones[i].GetComponent<CanvasGroup>();
            if (grupo == null) grupo = botones[i].gameObject.AddComponent<CanvasGroup>();
            gruposBotones[i] = grupo;

            var r = (RectTransform)botones[i].transform;
            posicionesBotones[i] = r.anchoredPosition;
            r.anchoredPosition = posicionesBotones[i] + Vector2.down * deslizamientoBoton;
            ActivarBoton(i, 0f, false);
        }

        if (logo != null) logo.anchoredPosition = posicionLogo + Vector2.down * subidaLogo;
    }

    private void ActivarBoton(int i, float alfa, bool activo)
    {
        CanvasGroup grupo = gruposBotones[i];
        if (grupo == null) return;
        grupo.alpha = alfa;
        grupo.interactable = activo;
        grupo.blocksRaycasts = activo;
    }

    private void Update()
    {
        float t = Time.unscaledTime;

        if (estado == Estado.Espera)
        {
            // Late de 0.35 a 1 con periodo pulsoSegundos.
            float onda = Mathf.Sin(t * Mathf.PI * 2f / Mathf.Max(0.1f, pulsoSegundos)) * 0.5f + 0.5f;
            promptEspera.alpha = Mathf.Lerp(0.35f, 1f, onda);
            if (Input.anyKeyDown) StartCoroutine(Revelar());
        }
        else if (estado == Estado.Menu)
        {
            ActualizarTeclado();
        }
    }

    private IEnumerator Revelar()
    {
        estado = Estado.Rayo; // ignora la entrada durante el rayo
        promptEspera.alpha = 0f;
        if (rayo != null) StartCoroutine(rayo.Strike());
        if (logo != null) StartCoroutine(SubirLogo());

        for (float t = 0f; t < retrasoBotones; t += Time.unscaledDeltaTime) yield return null;

        for (int i = 0; i < botones.Length; i++)
        {
            if (botones[i] != null) StartCoroutine(EntrarBoton(i));
            for (float t = 0f; t < escalonBotones; t += Time.unscaledDeltaTime) yield return null;
        }
        for (float t = 0f; t < duracionBoton; t += Time.unscaledDeltaTime) yield return null;

        for (int i = 0; i < botones.Length; i++) ActivarBoton(i, 1f, true);
        if (rayo != null) rayo.StartAmbient();
        estado = Estado.Menu;
        SeleccionarPrimero(panelPrincipal);
    }

    private IEnumerator EntrarBoton(int i)
    {
        var r = (RectTransform)botones[i].transform;
        Vector2 destino = posicionesBotones[i];
        Vector2 origen = destino + Vector2.down * deslizamientoBoton;
        for (float t = 0f; t < duracionBoton; t += Time.unscaledDeltaTime)
        {
            float k = CinematicEase.InOut(t / duracionBoton);
            r.anchoredPosition = Vector2.Lerp(origen, destino, k);
            ActivarBoton(i, k, false);
            yield return null;
        }
        r.anchoredPosition = destino;
        ActivarBoton(i, 1f, false);
    }

    private IEnumerator SubirLogo()
    {
        Vector2 origen = logo.anchoredPosition;
        for (float t = 0f; t < duracionSubidaLogo; t += Time.unscaledDeltaTime)
        {
            logo.anchoredPosition = Vector2.Lerp(origen, posicionLogo, CinematicEase.InOut(t / duracionSubidaLogo));
            yield return null;
        }
        logo.anchoredPosition = posicionLogo;
    }

    // ---------- Teclado ----------

    private void ActualizarTeclado()
    {
        bool enSubpanel = (panelOpciones != null && panelOpciones.activeSelf)
                          || (panelCreditos != null && panelCreditos.activeSelf);
        if (enSubpanel && Input.GetKeyDown(KeyCode.Escape))
        {
            BotonCartoon.PlayIfPresent("sfx_ui_click");
            MostrarPrincipal();
            return;
        }

        // Tras un clic en vacio el EventSystem queda sin seleccion: las flechas la recuperan.
        EventSystem sistema = EventSystem.current;
        if (sistema == null || sistema.currentSelectedGameObject != null) return;
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)
            || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.S))
        {
            GameObject panel = enSubpanel
                ? (panelOpciones != null && panelOpciones.activeSelf ? panelOpciones : panelCreditos)
                : panelPrincipal;
            SeleccionarPrimero(panel);
        }
    }

    private static void SeleccionarPrimero(GameObject panel)
    {
        EventSystem sistema = EventSystem.current;
        if (sistema == null || panel == null) return;
        Selectable primero = panel.GetComponentInChildren<Selectable>();
        if (primero == null) return;

        // Al abrir un panel no suena el tic de hover: solo cuando el jugador se mueve.
        BotonCartoon.SilenciarHover = true;
        sistema.SetSelectedGameObject(primero.gameObject);
        BotonCartoon.SilenciarHover = false;
    }

    // ---------- Botones ----------

    public void Jugar()
    {
        if (estado != Estado.Menu) return;
        StartCoroutine(CargarJuego());
    }

    private IEnumerator CargarJuego()
    {
        estado = Estado.Cargando; // ignora la entrada mientras carga
        if (botones != null)
        {
            for (int i = 0; i < botones.Length; i++)
            {
                if (botones[i] != null) botones[i].interactable = false;
            }
        }
        if (rayo != null) rayo.StopAmbient();

        if (sonidosJugar != null)
        {
            for (int i = 0; i < sonidosJugar.Length; i++)
            {
                if (BotonCartoon.PlayIfPresent(sonidosJugar[i])) break;
            }
        }

        // Carga por detras y activa la escena recien cuando la pantalla esta negra.
        AsyncOperation carga = null;
        if (Application.CanStreamedLevelBeLoaded(escenaJuego))
        {
            carga = SceneManager.LoadSceneAsync(escenaJuego);
        }
        else
        {
            int siguiente = SceneManager.GetActiveScene().buildIndex + 1;
            Debug.LogWarning("[Menu] La escena '" + escenaJuego + "' no esta en Build Settings; se usa el indice " + siguiente + ".");
            if (siguiente > 0 && siguiente < SceneManager.sceneCountInBuildSettings)
                carga = SceneManager.LoadSceneAsync(siguiente);
        }
        if (carga != null) carga.allowSceneActivation = false;

        if (fundido != null) yield return fundido.FadeTo(1f, fundidoSegundos);

        if (carga != null) carga.allowSceneActivation = true;
        else Debug.LogError("[Menu] No hay escena que cargar (revisa Build Settings).");
    }

    public void AbrirOpciones()
    {
        Mostrar(panelOpciones);
        SeleccionarPrimero(panelOpciones);
    }

    public void AbrirCreditos()
    {
        Mostrar(panelCreditos);
        SeleccionarPrimero(panelCreditos);
    }

    public void MostrarPrincipal()
    {
        Mostrar(panelPrincipal);
        SeleccionarPrimero(panelPrincipal);
    }

    public void Salir()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Mostrar(GameObject panel)
    {
        if (panelPrincipal != null) panelPrincipal.SetActive(panel == panelPrincipal);
        if (panelOpciones != null) panelOpciones.SetActive(panel == panelOpciones);
        if (panelCreditos != null) panelCreditos.SetActive(panel == panelCreditos);
    }
}
