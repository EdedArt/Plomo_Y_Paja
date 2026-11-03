using UnityEngine;

/// <summary>
/// Menu de pausa. Va en la raiz del prefab Resources/PauseMenu_Canvas
/// (lo genera "Plomo y Paja/Crear menu de pausa" y lo instancia PauseMenuSpawner).
/// Solo muestra/oculta: Time.timeScale, cursor y UiHideReasons los maneja GameManager.
/// El GameObject raiz nunca se desactiva (si no, perderia la suscripcion);
/// se oculta con el CanvasGroup.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class PauseMenu : MonoBehaviour
{
    [Tooltip("Panel central; hace un pequeño 'pop' de escala al abrir.")]
    [SerializeField] private RectTransform panel;

    [Header("Transicion (tiempo unscaled)")]
    [SerializeField, Min(0.01f)] private float duracionFundido = 0.15f;
    [SerializeField] private float escalaInicialPanel = 0.85f;

    private CanvasGroup grupo;
    private bool visible;
    private float progreso; // 0 = oculto, 1 = visible

    private void Awake()
    {
        grupo = GetComponent<CanvasGroup>();
        AplicarVisual(0f);
        AplicarInteraccion(false);
    }

    private void OnEnable()
    {
        GameManager.OnPauseChanged += Mostrar;
    }

    private void OnDisable()
    {
        GameManager.OnPauseChanged -= Mostrar;
    }

    private void Start()
    {
        // Los botones necesitan un EventSystem; las escenas de juego no traen uno.
        DialogueUI.EnsureEventSystem();
    }

    private void Mostrar(bool mostrar)
    {
        visible = mostrar;
        AplicarInteraccion(mostrar);
    }

    private void Update()
    {
        float objetivo = visible ? 1f : 0f;
        if (Mathf.Approximately(progreso, objetivo)) return;

        progreso = Mathf.MoveTowards(progreso, objetivo, Time.unscaledDeltaTime / duracionFundido);
        AplicarVisual(progreso);
    }

    private void AplicarVisual(float t)
    {
        progreso = t;
        if (grupo != null) grupo.alpha = t;
        if (panel != null)
        {
            float e = Mathf.Lerp(escalaInicialPanel, 1f, 1f - (1f - t) * (1f - t));
            panel.localScale = new Vector3(e, e, 1f);
        }
    }

    private void AplicarInteraccion(bool activo)
    {
        if (grupo == null) return;
        grupo.interactable = activo;
        grupo.blocksRaycasts = activo;
    }

    // ============ BOTONES ============

    public void Continuar()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGamePaused)
            GameManager.Instance.TogglePause();
    }

    public void Reiniciar()
    {
        if (GameManager.Instance == null) return;
        // GameManager sobrevive a la recarga: sin esto quedaria IsGamePaused = true en la escena nueva.
        Continuar();
        GameManager.Instance.RetryCurrentLevel();
    }

    public void MenuPrincipal()
    {
        if (GameManager.Instance == null) return;
        Continuar();
        GameManager.Instance.GoToMainMenu();
    }

    public void Salir()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
