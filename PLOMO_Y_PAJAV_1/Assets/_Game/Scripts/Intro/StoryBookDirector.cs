using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Dirige el libro 2D de la intro (INTRO.1.1, enmienda A7): apertura con el libro cerrado, 6 páginas a pantalla completa
/// con narrador y máquina de escribir, pase de página con rizo, final entrando en la última ilustración y paso al
/// Nivel 1 sin silencio (I11). DÓNDE VA: objeto "IntroDirector" de Intro.unity (lo arma Plomo y Paja → Intro → Construir
/// escena del libro).
///
/// Ritmo: negro → libro_cerrado en 0.8 s, espera 1.2 s → acercamiento lento 1 → 1.12 en 2 s → el libro se abre
/// (pase con rizo, sfx_libro_abrir) a la página 1 → por página: narrador + texto escrito en 0.85 × la duración del clip
/// → 0.8 s después del clip, pase de página (0.8 s, sfx_pagina al azar) a la siguiente.
/// - Clic o toque de ESPACIO: completa el texto y pasa la página ya (I4). Mantener ESPACIO/Esc 1 s: salta al Nivel 1 (I5).
/// - El Nivel 1 se empieza a cargar (async) al llegar a la página 5; la lluvia sube durante el fundido final y pasa
///   viva a la escena siguiente (CarryOverAudio), donde RainEmitter arranca la suya.
/// - Música baja, con -6 dB mientras habla el narrador. Ambiente en bucle. Todo en tiempo real (unscaled).
/// - Sin audio: el texto usa una duración estimada; sin la escena en Build Settings: usa el índice siguiente y avisa.
/// </summary>
public class StoryBookDirector : MonoBehaviour
{
    [Header("Referencias (las pone el builder)")]
    [SerializeField] private IntroBookData data;
    [SerializeField] private PageCompositor compositor;
    [SerializeField] private PageCurlView view;
    [SerializeField] private ScreenFade fade;
    [SerializeField] private HoldToSkip skip;

    [Header("Fuentes de audio (2D)")]
    [SerializeField] private AudioSource narratorSource;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource ambienceSource;
    [SerializeField] private AudioSource rainSource;

    [Header("Apertura (s)")]
    [SerializeField] private float fadeInSeconds = 0.8f;
    [SerializeField] private float holdCoverSeconds = 1.2f;
    [SerializeField] private float pushInSeconds = 2f;
    [Tooltip("Escala final del acercamiento al libro cerrado")]
    [SerializeField] private float pushInScale = 1.12f;
    [Tooltip("Duración del pase de la apertura a la página 1")]
    [SerializeField] private float openSeconds = 1.0f;

    [Header("Páginas (s)")]
    [SerializeField] private float turnSeconds = 0.8f;
    [Tooltip("El texto se escribe en esta fracción de la duración del clip (I3)")]
    [SerializeField, Range(0.3f, 1f)] private float typeFraction = 0.85f;
    [Tooltip("Pausa tras el final del clip antes de pasar la página (I4)")]
    [SerializeField] private float afterClipSeconds = 0.8f;
    [Tooltip("Sin clip del narrador: segundos por carácter (mínimo 3 s)")]
    [SerializeField] private float fallbackSecondsPerChar = 0.055f;
    [Tooltip("Página (1–6) en la que se empieza a cargar el Nivel 1")]
    [SerializeField] private int preloadAtPage = 5;

    [Header("Final (s)")]
    [SerializeField] private float pushSeconds = 1.5f;
    [Tooltip("Acercamiento final hacia el centro de la ilustración (1 = página entera)")]
    [SerializeField] private float pushZoom = 1.45f;
    [SerializeField] private float fadeOutSeconds = 1.0f;
    [SerializeField] private float skipFadeSeconds = 0.5f;

    [Header("Mezcla")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.35f;
    [Tooltip("Bajada de la música mientras habla el narrador (dB)")]
    [SerializeField] private float duckDecibels = -6f;
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.5f;
    [Tooltip("Volumen final de la lluvia (× volumen de efectos); el Nivel 1 la recibe así")]
    [SerializeField, Range(0f, 1f)] private float rainVolume = 0.4f;
    [Tooltip("Segundos en que la lluvia heredada se apaga ya dentro del Nivel 1")]
    [SerializeField] private float rainHandOffSeconds = 2.5f;

    [Header("Depuración")]
    [SerializeField] private bool debug = false;

    private float _musicFade = 1f;
    private float _ambienceFade = 1f;
    private AsyncOperation _load;
    private Coroutine _main;
    private bool _leaving;

    private void Start()
    {
        if (fade != null) fade.Alpha = 1f;
        if (data == null || compositor == null || view == null)
        {
            Debug.LogWarning("[Intro] Faltan referencias del libro (corre Plomo y Paja → Intro → Construir escena del libro). Se pasa al Nivel 1.");
            StartCoroutine(Leave(skipFadeSeconds));
            return;
        }

        compositor.Initialize(data.paperTexture);
        view.SetPaper(data.paperTexture);
        compositor.ShowCover(0, data.closedBookTexture, data.coverTitle);
        view.SetCurrent(compositor.Target(0));

        if (skip != null)
        {
            skip.Skipped += HandleSkip;
            skip.Armed = true;
        }
        StartLoop(musicSource, data.music);
        StartLoop(ambienceSource, data.roomAmbience);
        if (rainSource != null) rainSource.volume = 0f;

        _main = StartCoroutine(Run());
    }

    private void OnDestroy()
    {
        if (skip != null) skip.Skipped -= HandleSkip;
    }

    // ==================================================================
    // Secuencia
    // ==================================================================

    private IEnumerator Run()
    {
        Log("fundido de entrada");
        yield return fade != null ? fade.FadeTo(0f, fadeInSeconds) : null;
        yield return Wait(holdCoverSeconds);

        Log("acercamiento al libro cerrado");
        for (float t = 0f; t < pushInSeconds; t += Time.unscaledDeltaTime)
        {
            compositor.SetCoverZoom(0, Mathf.Lerp(1f, pushInScale, CinematicEase.InOut(t / pushInSeconds)));
            yield return null;
        }
        compositor.SetCoverZoom(0, pushInScale);

        int pages = data.pages != null ? data.pages.Length : 0;
        for (int i = 1; i <= pages; i++)
        {
            IntroBookData.Page page = data.pages[i - 1];
            if (i >= preloadAtPage) BeginPreload();

            // La página nueva se compone (texto oculto) en el otro RT antes de empezar el pase
            if (narratorSource != null) narratorSource.Stop();
            compositor.ShowPage(i, page.illustration, page.illustrationLabel, page.text);
            yield return null; // un cuadro para que su RT ya tenga la página nueva
            AudioLibrary.PlaySfx2D(i == 1 ? data.openSfx : data.pageSfx);
            Log(i == 1 ? "se abre el libro" : "pasa la página " + i);
            yield return Turn(compositor.Target(i), i == 1 ? openSeconds : turnSeconds);
            yield return ReadPage(i, page);
        }

        BeginPreload();
        Log("final: entra en la ilustración");
        yield return Leave(-1f);
    }

    /// <summary>Pase de página con rizo de la actual a "next" (suavizado entra y sale).</summary>
    private IEnumerator Turn(Texture next, float seconds)
    {
        view.BeginTurn(next);
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            view.SetProgress(CinematicEase.InOut(t / seconds));
            yield return null;
        }
        view.EndTurn();
    }

    /// <summary>Narrador + máquina de escribir; espera el fin del clip + la pausa, o un clic/toque.</summary>
    private IEnumerator ReadPage(int sheet, IntroBookData.Page page)
    {
        AudioClip clip = AudioLibrary.GetVoice(page.clipId);
        int total = compositor.CharacterCount(sheet);
        float length = clip != null ? clip.length : Mathf.Max(3f, total * fallbackSecondsPerChar);
        if (clip != null && narratorSource != null)
        {
            narratorSource.clip = clip;
            narratorSource.volume = AudioLibrary.VoiceVolume;
            narratorSource.Play();
        }
        if (skip != null) skip.ClearTap(); // un toque hecho durante el pase no cuenta

        float typeTime = Mathf.Max(0.1f, length * typeFraction);
        float t = 0f;
        bool advanced = false;
        while (t < typeTime)
        {
            if (AdvancePressed()) { advanced = true; break; }
            compositor.SetVisibleCharacters(sheet, Mathf.CeilToInt(total * (t / typeTime)));
            yield return null;
            t += Time.unscaledDeltaTime;
        }
        compositor.SetVisibleCharacters(sheet, total);
        if (advanced)
        {
            yield return null; // un cuadro para que el RT tenga el texto completo antes de pasar
            yield break;
        }

        float end = length + afterClipSeconds;
        while (t < end)
        {
            if (AdvancePressed()) yield break;
            yield return null;
            t += Time.unscaledDeltaTime;
        }
    }

    /// <summary>Final o salto: acercamiento a la ilustración (solo en el final), fundido a negro con la lluvia subiendo, y Nivel 1.</summary>
    private IEnumerator Leave(float fadeSeconds)
    {
        if (_leaving) yield break;
        _leaving = true;
        if (skip != null) skip.Armed = false;
        BeginPreload();

        bool ending = fadeSeconds < 0f;
        float total = ending ? Mathf.Max(pushSeconds, fadeOutSeconds) : fadeSeconds;
        if (ending && view != null && compositor != null) StartCoroutine(PushIntoIllustration(pushSeconds));
        StartCoroutine(RampAudio(total));

        if (ending) yield return Wait(Mathf.Max(0f, pushSeconds - fadeOutSeconds));
        if (fade != null) yield return fade.FadeTo(1f, ending ? fadeOutSeconds : fadeSeconds);
        if (narratorSource != null) narratorSource.Stop();

        if (_load == null)
        {
            Debug.LogError("[Intro] No hay Nivel 1 que cargar (revisa Build Settings). La escena se queda en negro.");
            yield break;
        }
        if (rainSource != null && rainSource.isPlaying) CarryOverAudio.Carry(rainSource, rainHandOffSeconds);
        Log("activa el Nivel 1");
        _load.allowSceneActivation = true;
    }

    private IEnumerator PushIntoIllustration(float seconds)
    {
        Vector2 center = compositor.IllustrationCenter;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float e = CinematicEase.InOut(t / seconds);
            view.SetZoom(Vector2.Lerp(new Vector2(0.5f, 0.5f), center, e), Mathf.Lerp(1f, pushZoom, e));
            yield return null;
        }
        view.SetZoom(center, pushZoom);
    }

    private void HandleSkip()
    {
        if (_leaving) return;
        Log("saltar (mantenido)");
        if (_main != null) StopCoroutine(_main);
        StartCoroutine(Leave(skipFadeSeconds));
    }

    // ==================================================================
    // Carga del Nivel 1
    // ==================================================================

    private void BeginPreload()
    {
        if (_load != null) return;
        string scene = data != null ? data.nextScene : "";
        if (!string.IsNullOrEmpty(scene) && Application.CanStreamedLevelBeLoaded(scene))
        {
            _load = SceneManager.LoadSceneAsync(scene);
        }
        else
        {
            int next = SceneManager.GetActiveScene().buildIndex + 1;
            Debug.LogWarning($"[Intro] La escena '{scene}' no está en Build Settings; se usa el índice {next}.");
            if (next > 0 && next < SceneManager.sceneCountInBuildSettings) _load = SceneManager.LoadSceneAsync(next);
        }
        if (_load != null)
        {
            _load.allowSceneActivation = false;
            Log("cargando el Nivel 1 en segundo plano");
        }
    }

    // ==================================================================
    // Audio
    // ==================================================================

    private void StartLoop(AudioSource source, string id)
    {
        if (source == null) return;
        AudioClip clip = AudioLibrary.GetSfx(id);
        if (clip == null) return;
        source.clip = clip;
        source.loop = true;
        source.volume = 0f;
        source.Play();
    }

    /// <summary>La música y el ambiente bajan a 0 y la lluvia sube a su volumen final en "seconds".</summary>
    private IEnumerator RampAudio(float seconds)
    {
        if (rainSource != null && !rainSource.isPlaying)
        {
            AudioClip rain = data != null ? AudioLibrary.GetSfx(data.rain) : null;
            if (rain != null)
            {
                rainSource.clip = rain;
                rainSource.loop = true;
                rainSource.volume = 0f;
                rainSource.Play();
            }
        }
        float rainTarget = rainVolume * AudioLibrary.SfxVolume;
        float startRain = rainSource != null ? rainSource.volume : 0f;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float u = t / seconds;
            _musicFade = 1f - u;
            _ambienceFade = 1f - u;
            if (rainSource != null) rainSource.volume = Mathf.Lerp(startRain, rainTarget, u);
            yield return null;
        }
        _musicFade = 0f;
        _ambienceFade = 0f;
        if (rainSource != null) rainSource.volume = rainTarget;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (musicSource != null)
        {
            bool talking = narratorSource != null && narratorSource.isPlaying;
            float duck = talking ? Mathf.Pow(10f, duckDecibels / 20f) : 1f;
            float target = musicVolume * AudioLibrary.SfxVolume * duck * _musicFade;
            musicSource.volume = Mathf.MoveTowards(musicSource.volume, target, dt * 0.6f);
        }
        if (ambienceSource != null)
        {
            float target = ambienceVolume * AudioLibrary.SfxVolume * _ambienceFade;
            ambienceSource.volume = Mathf.MoveTowards(ambienceSource.volume, target, dt * 0.6f);
        }
    }

    // ==================================================================
    // Utilidades
    // ==================================================================

    /// <summary>Clic izquierdo o toque corto de ESPACIO.</summary>
    private bool AdvancePressed()
    {
        bool tap = skip != null && skip.ConsumeTap();
        return Input.GetMouseButtonDown(0) || tap;
    }

    private IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }

    private void Log(string message)
    {
        if (debug) Debug.Log("[Intro] " + message);
    }
}
