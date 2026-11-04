using System.Collections;
using UnityEngine;

/// <summary>
/// Arranque del Nivel 2 (N2.1, decisión L10). El reinicio al morir lo hace LevelDeathRestart, para todos los niveles (N2.1.1).
/// DÓNDE VA: objeto "Inicio_Nivel" de Campamento (lo arma Plomo y Paja → Nivel 2 → Generar Campamento).
///
/// SECUENCIA (tiempo real):
///  1. Fundido desde negro. Solo mientras dura, CinematicState bloquea el juego (sin manos, disparo ni guardias).
///  2. wilbur_bosque_01 con subtítulo (sin clip: solo subtítulo, dura según el largo del texto).
///  3. Pensamiento de la API con el contexto de la llegada. Sin key, sin conexión o si tarda más de thoughtTimeout: una de las
///     líneas de respaldo (guion §1, N2.1.1).
/// Los pasos 2 y 3 salen una vez por sesión: reiniciar tras morir solo repite el fundido. ResetSession() los rearma.
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

    [Tooltip("Guion §1: si la API falla o tarda más de thoughtTimeout")]
    [TextArea(1, 3)] [SerializeField] private string[] fallbackThoughts =
    {
        "Jazz en medio del bosque. O el Lobo tiene buen gusto… o alguien está celebrando algo.",
        "Las huellas siguen juntas. Si lo arrastraban, se cansaron muy pronto.",
        "Un campamento entero para cobrar una deuda. Porky debía más que plata."
    };

    [Header("Tiempos (s, tiempo real)")]
    [SerializeField, Min(0f)] private float fadeInSeconds = 1.2f;
    [SerializeField, Min(0f)] private float beforeLineSeconds = 0.4f;
    [Tooltip("Sin clip: segundos por letra (mínimo 1.5 s en total)")]
    [SerializeField, Min(0.01f)] private float secondsPerCharacter = 0.05f;
    [SerializeField, Min(0f)] private float beforeThoughtSeconds = 0.5f;
    [SerializeField, Min(0.5f)] private float thoughtTimeout = 6f;

    /// <summary>Partida nueva: la llegada vuelve a salir (la llama GameStateTracker.ResetForNewGame).</summary>
    public static void ResetSession() => _playedThisSession = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _playedThisSession = false;

    private void Start()
    {
        StartCoroutine(Arrival());
    }

    private void OnDestroy()
    {
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
        yield return SaySubtitled(lineText, AudioLibrary.GetVoice(voiceId));
        yield return new WaitForSecondsRealtime(beforeThoughtSeconds);
        yield return Thought();
    }

    private IEnumerator SaySubtitled(string text, AudioClip clip)
    {
        if (clip != null && voice != null)
        {
            voice.clip = clip;
            voice.volume = AudioLibrary.VoiceVolume;
            voice.Play();
        }
        if (subtitles != null) subtitles.Show(speaker, text);
        float seconds = clip != null ? clip.length : Mathf.Max(1.5f, text.Length * secondsPerCharacter);
        yield return new WaitForSecondsRealtime(seconds);
        if (subtitles != null) subtitles.Hide();
    }

    private IEnumerator Thought()
    {
        AICompanion ai = AICompanion.Instance;
        bool done = false;
        string line = null;
        if (ai != null)
        {
            ai.RequestCustomLine(thoughtSituation, result => { done = true; line = result; });

            float limit = Time.realtimeSinceStartup + thoughtTimeout;
            while (!done && Time.realtimeSinceStartup < limit) yield return null;
        }

        // Una respuesta que llegue después del límite no se muestra: ya salió el respaldo
        bool fromApi = done && !string.IsNullOrWhiteSpace(line);
        string shown = fromApi ? line : PickFallback();
        if (string.IsNullOrWhiteSpace(shown)) yield break;
        if (InterrogationDirector.IsActive || CinematicState.IsPlaying) yield break;

        if (ai != null) ai.ShowCustomLine(shown, !fromApi); // la de la API ya la guardó RequestCustomLine en el historial
        else yield return SaySubtitled(shown, null);        // sin AICompanion en la escena, igual se lee
    }

    private string PickFallback()
    {
        if (fallbackThoughts == null || fallbackThoughts.Length == 0) return null;
        return fallbackThoughts[Random.Range(0, fallbackThoughts.Length)];
    }
}
