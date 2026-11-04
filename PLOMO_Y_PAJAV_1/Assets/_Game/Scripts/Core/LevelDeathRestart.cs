using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Reinicio al morir en cualquier nivel (N2.1.1, H17): espera deathReloadDelay, funde a negro y llama
/// GameManager.RetryCurrentLevel, que restaura la foto de entrada al nivel y recarga la escena. Después funde desde negro.
/// DÓNDE VA: objeto "GameManagers" (lo agrega GameManagersBootstrap). No hay pantalla de muerte en el juego.
/// Se engancha al PlayerHealth de cada escena al cargarla; en una escena sin jugador (menú, libro) no hace nada.
/// El fundido es propio porque este objeto sobrevive a la recarga y funciona igual en cualquier nivel.
/// </summary>
public class LevelDeathRestart : MonoBehaviour
{
    private const int FadeSortingOrder = 90; // sobre el HUD y la pausa

    [SerializeField, Min(0f)] private float deathReloadDelay = 2.5f;
    [SerializeField, Min(0f)] private float deathFadeSeconds = 0.8f;
    [SerializeField, Min(0f)] private float fadeInSeconds = 0.6f;

    private PlayerHealth _health;
    private Canvas _fadeCanvas;
    private ScreenFade _fade;
    private bool _reloading;

    private void Awake()
    {
        BuildFade();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        // Por si la primera escena cargó antes de suscribirse (idempotente)
        BindPlayer();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Unbind();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        BindPlayer();

        bool cameFromDeath = _reloading;
        _reloading = false;
        StopAllCoroutines();
        if (cameFromDeath) StartCoroutine(FadeIn());
        else SetFade(0f, false);
    }

    private void BindPlayer()
    {
        Unbind();
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return; // menú o libro

        if (player.TryGetComponent(out _health)) _health.OnDeath += HandleDeath;
        else Debug.LogWarning("[Reinicio] El Player no tiene PlayerHealth: al morir no habrá reinicio.");
    }

    private void Unbind()
    {
        if (_health != null) _health.OnDeath -= HandleDeath;
        _health = null;
    }

    private void HandleDeath()
    {
        if (_reloading) return;
        _reloading = true;
        if (GameManager.Instance != null) GameManager.Instance.MarkGameOverForRestart();
        StartCoroutine(ReloadAfterDeath());
    }

    private IEnumerator ReloadAfterDeath()
    {
        yield return new WaitForSecondsRealtime(deathReloadDelay);
        SetFade(0f, true);
        yield return _fade.FadeTo(1f, deathFadeSeconds);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RetryCurrentLevel();
            yield break;
        }
        Time.timeScale = 1f; // por si el hit-stop quedó a medias
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private IEnumerator FadeIn()
    {
        SetFade(1f, true);
        yield return _fade.FadeTo(0f, fadeInSeconds);
        SetFade(0f, false);
    }

    private void SetFade(float alpha, bool visible)
    {
        _fade.Alpha = alpha;
        _fadeCanvas.enabled = visible;
    }

    private void BuildFade()
    {
        var canvasGo = new GameObject("UI_Fundido_Reinicio");
        canvasGo.transform.SetParent(transform, false);
        _fadeCanvas = canvasGo.AddComponent<Canvas>();
        _fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _fadeCanvas.sortingOrder = FadeSortingOrder;

        var blackGo = new GameObject("Fundido");
        blackGo.transform.SetParent(canvasGo.transform, false);
        RectTransform rect = blackGo.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = blackGo.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        _fade = blackGo.AddComponent<ScreenFade>();
        SetFade(0f, false);
    }
}
