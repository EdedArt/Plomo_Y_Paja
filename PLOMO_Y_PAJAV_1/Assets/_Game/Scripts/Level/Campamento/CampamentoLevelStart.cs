using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Arranque del Nivel 2 (N2.1, decisión L10) y reinicio al morir (H17).
/// DÓNDE VA: objeto "Inicio_Nivel" de Campamento (lo arma Plomo y Paja → Nivel 2 → Generar Campamento).
///
/// SECUENCIA (tiempo real):
///  1. Fundido desde negro. Solo mientras dura, CinematicState bloquea el juego (sin manos, disparo ni guardias).
///  2. wilbur_bosque_01 con subtítulo (sin clip: solo subtítulo, dura según el largo del texto).
///  3. Pensamiento de la API con el contexto de la llegada. Sin key, sin conexión o si tarda más de thoughtTimeout: no sale
///     nada (el guion no da línea de respaldo para la llegada).
/// Los pasos 2 y 3 salen una vez por sesión: reiniciar tras morir solo repite el fundido. ResetSession() los rearma (N2.4).
///
/// AL MORIR (H17): espera deathReloadDelay, funde a negro y recarga esta escena SIN borrar GameStateTracker, así que las
/// pistas y los interrogatorios siguen. En este proyecto nada escucha PlayerHealth.onPlayerDied y no hay pantalla de muerte
/// (UIManager no está en ninguna escena); este componente es el reinicio del Nivel 2.
/// </summary>
public class CampamentoLevelStart : MonoBehaviour
{
    private static bool _playedThisSession;

    [Header("Pantalla (las pone el builder)")]
    [SerializeField] private ScreenFade fade;
    [SerializeField] private CinematicSubtitles subtitles;
    [SerializeField] private AudioSource voice;

    [Header("Línea de llegada (guion §1)")]
    [SerializeField] private string speaker = "Wilbur";
    [SerializeField] private string voiceId = "wilbur_bosque_01";
    [TextArea(1, 3)] [SerializeField] private string lineText = "El bosque de Don Lobo. Hasta los árboles parecen deberle plata.";
    [TextArea(2, 4)] [SerializeField] private string thoughtSituation =
        "Wilbur acaba de entrar al bosque de Don Lobo de noche, siguiendo en el barro las huellas de un lobo y de un cerdo que " +
        "caminan juntos. A lo lejos se oye jazz de un gramófono. Un pensamiento en español, noir y ambiguo, de como mucho 25 palabras.";

    [Header("Tiempos (s, tiempo real)")]
    [SerializeField, Min(0f)] private float fadeInSeconds = 1.2f;
    [SerializeField, Min(0f)] private float beforeLineSeconds = 0.4f;
    [Tooltip("Sin clip: segundos por letra (mínimo 1.5 s en total)")]
    [SerializeField, Min(0.01f)] private float secondsPerCharacter = 0.05f;
    [SerializeField, Min(0f)] private float beforeThoughtSeconds = 0.5f;
    [SerializeField, Min(0.5f)] private float thoughtTimeout = 6f;

    [Header("Reinicio al morir")]
    [SerializeField, Min(0f)] private float deathReloadDelay = 2.5f;
    [SerializeField, Min(0f)] private float deathFadeSeconds = 0.8f;

    private PlayerHealth _health;
    private bool _reloading;

    /// <summary>Nueva partida: la llegada vuelve a salir (la llama el reinicio de sesión, N2.4).</summary>
    public static void ResetSession() => _playedThisSession = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _playedThisSession = false;

    private void Start()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null && player.TryGetComponent(out _health)) _health.OnDeath += HandleDeath;
        else Debug.LogWarning("[Campamento] Sin PlayerHealth: al morir no habrá reinicio.");

        StartCoroutine(Arrival());
    }

    private void OnDestroy()
    {
        if (_health != null) _health.OnDeath -= HandleDeath;
        CinematicState.Set(this, false);
    }

    private IEnumerator Arrival()
    {
        CinematicState.Set(this, true);
        if (fade != null)
        {
            fade.Alpha = 1f;
            yield return fade.FadeTo(0f, fadeInSeconds);
        }
        CinematicState.Set(this, false);

        if (_playedThisSession) yield break;
        _playedThisSession = true;

        yield return new WaitForSecondsRealtime(beforeLineSeconds);
        yield return SayLine();
        yield return new WaitForSecondsRealtime(beforeThoughtSeconds);
        yield return Thought();
    }

    private IEnumerator SayLine()
    {
        AudioClip clip = AudioLibrary.GetVoice(voiceId);
        if (clip != null && voice != null)
        {
            voice.clip = clip;
            voice.volume = AudioLibrary.VoiceVolume;
            voice.Play();
        }
        if (subtitles != null) subtitles.Show(speaker, lineText);
        float seconds = clip != null ? clip.length : Mathf.Max(1.5f, lineText.Length * secondsPerCharacter);
        yield return new WaitForSecondsRealtime(seconds);
        if (subtitles != null) subtitles.Hide();
    }

    private IEnumerator Thought()
    {
        AICompanion ai = AICompanion.Instance;
        if (ai == null) yield break;

        bool done = false;
        string line = null;
        ai.RequestCustomLine(thoughtSituation, result => { done = true; line = result; });

        float limit = Time.realtimeSinceStartup + thoughtTimeout;
        while (!done && Time.realtimeSinceStartup < limit) yield return null;

        if (!done || string.IsNullOrWhiteSpace(line)) yield break;
        if (InterrogationDirector.IsActive || CinematicState.IsPlaying || _reloading) yield break;
        ai.ShowCustomLine(line, false); // RequestCustomLine ya la guardó en el historial
    }

    private void HandleDeath()
    {
        if (_reloading) return;
        _reloading = true;
        StartCoroutine(ReloadAfterDeath());
    }

    private IEnumerator ReloadAfterDeath()
    {
        yield return new WaitForSecondsRealtime(deathReloadDelay);
        if (fade != null) yield return fade.FadeTo(1f, deathFadeSeconds);
        Time.timeScale = 1f; // por si el hit-stop quedó a medias
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
