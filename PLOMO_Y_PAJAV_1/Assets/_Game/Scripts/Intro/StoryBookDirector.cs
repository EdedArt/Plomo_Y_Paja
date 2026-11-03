using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Dirige la escena del libro (INTRO.1): fundido, tapa, portadilla, 6 páginas con narrador y máquina de escribir,
/// final con la cámara entrando en la última ilustración, y paso al Nivel 1 sin silencio (I11).
/// DÓNDE VA: objeto "IntroDirector" de Intro.unity (lo arma Plomo y Paja → Intro → Construir escena del libro).
///
/// Ritmo (Fase-Intro.md, INTRO.1 paso 3): negro → Cam_Wide 0.8 s, espera 1.2 s → la tapa se abre (1.0 s,
/// sfx_libro_abrir) mientras la cámara va a Cam_Spread → portadilla 2.0 s → por página: pasa la hoja (0.7 s,
/// sfx_pagina al azar) → narrador + texto escrito en 0.85 × la duración del clip → 0.8 s después del clip, la siguiente.
/// - Clic o toque de ESPACIO: completa el texto y pasa la hoja ya (I4). Mantener ESPACIO/Esc 1 s: salta al Nivel 1 (I5).
/// - El Nivel 1 se empieza a cargar (async) al llegar a la página 5; la lluvia sube durante el fundido final y pasa
///   viva a la escena siguiente (CarryOverAudio), donde RainEmitter arranca la suya.
/// - Música baja, con -6 dB mientras habla el narrador. Ambiente del cuarto en bucle. Leve temblor de cámara en mano.
/// - Nunca toca el FOV (lo fija el builder). Todo en tiempo real (unscaled).
/// - Sin audio: el texto usa una duración estimada; sin la escena en Build Settings: usa el índice siguiente y avisa.
/// </summary>
public class StoryBookDirector : MonoBehaviour
{
    [Header("Referencias (las pone el builder)")]
    [SerializeField] private IntroBookData data;
    [SerializeField] private StoryBook book;
    [SerializeField] private SpreadCompositor compositor;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private ScreenFade fade;
    [SerializeField] private HoldToSkip skip;

    [Header("Fuentes de audio (2D)")]
    [SerializeField] private AudioSource narratorSource;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource ambienceSource;
    [SerializeField] private AudioSource rainSource;

    [Header("Tiempos (s)")]
    [SerializeField] private float fadeInSeconds = 0.8f;
    [SerializeField] private float holdWideSeconds = 1.2f;
    [SerializeField] private float coverSeconds = 1.0f;
    [SerializeField] private float dollySeconds = 1.0f;
    [SerializeField] private float titleSeconds = 2.0f;
    [SerializeField] private float turnSeconds = 0.7f;
    [Tooltip("El texto se escribe en esta fracción de la duración del clip (I3)")]
    [SerializeField, Range(0.3f, 1f)] private float typeFraction = 0.85f;
    [Tooltip("Pausa tras el final del clip antes de pasar la hoja (I4)")]
    [SerializeField] private float afterClipSeconds = 0.8f;
    [SerializeField] private float pushSeconds = 1.5f;
    [SerializeField] private float fadeOutSeconds = 1.0f;
    [SerializeField] private float skipFadeSeconds = 0.5f;
    [Tooltip("Sin clip del narrador: segundos por carácter (mínimo 3 s)")]
    [SerializeField] private float fallbackSecondsPerChar = 0.055f;
    [Tooltip("Página (1–6) en la que se empieza a cargar el Nivel 1")]
    [SerializeField] private int preloadAtPage = 5;

    [Header("Mezcla")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.35f;
    [Tooltip("Bajada de la música mientras habla el narrador (dB)")]
    [SerializeField] private float duckDecibels = -6f;
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.5f;
    [Tooltip("Volumen final de la lluvia (× volumen de efectos); el Nivel 1 la recibe así")]
    [SerializeField, Range(0f, 1f)] private float rainVolume = 0.4f;
    [Tooltip("Segundos en que la lluvia heredada se apaga ya dentro del Nivel 1")]
    [SerializeField] private float rainHandOffSeconds = 2.5f;

    [Header("Cámara en mano")]
    [Tooltip("Desvío máximo (grados), ≤ 0.3")]
    [SerializeField, Range(0f, 0.3f)] private float driftDegrees = 0.25f;
    [SerializeField] private float driftSpeed = 0.15f;

    [Header("Depuración")]
    [SerializeField] private bool debug = false;

    private Vector3 _camPos;
    private Quaternion _camRot = Quaternion.identity;
    private float _driftSeed;
    private float _musicFade = 1f;
    private float _ambienceFade = 1f;
    private AsyncOperation _load;
    private Coroutine _main;
    private bool _leaving;

    private void Start()
    {
        _driftSeed = Random.value * 100f;
        if (fade != null) fade.Alpha = 1f;
        if (book != null && book.CamWide != null) SetCameraPose(book.CamWide);
        if (data == null || book == null || compositor == null)
        {
            Debug.LogWarning("[Intro] Faltan referencias del libro (corre Plomo y Paja → Intro → Construir escena del libro). Se pasa al Nivel 1.");
            StartCoroutine(Leave(skipFadeSeconds));
            return;
        }

        compositor.Initialize(book.SpreadAspect, data.paperTexture);
        book.BindSpreads(compositor);
        compositor.ShowTitle(0, data.titlePageTitle, data.titlePageSubtitle);

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
        yield return Wait(holdWideSeconds, false);

        Log("se abre la tapa");
        AudioLibrary.PlaySfx2D(data.openSfx);
        StartCoroutine(MoveCamera(book.CamSpread, dollySeconds));
        yield return book.OpenCover(coverSeconds);
        yield return Wait(titleSeconds, true);

        int pages = Mathf.Min(data.pages.Length, book.LeafCount - 1);
        for (int i = 1; i <= pages; i++)
        {
            IntroBookData.Page page = data.pages[i - 1];
            if (i >= preloadAtPage) BeginPreload();

            // La doble página nueva se compone (texto oculto) antes de que la hoja empiece a girar
            if (narratorSource != null) narratorSource.Stop();
            compositor.ShowStory(i, page.illustration, page.illustrationLabel, page.text);
            AudioLibrary.PlaySfx2D(data.pageSfx);
            Log("pasa la hoja " + i);
            yield return book.TurnLeaf(i - 1, turnSeconds);
            yield return ReadPage(i, page);
        }

        BeginPreload();
        Log("final: cámara a la ilustración");
        yield return Leave(-1f);
    }

    /// <summary>Narrador + máquina de escribir; espera el fin del clip + la pausa, o un clic/toque.</summary>
    private IEnumerator ReadPage(int spread, IntroBookData.Page page)
    {
        AudioClip clip = AudioLibrary.GetVoice(page.clipId);
        int total = compositor.CharacterCount(spread);
        float length = clip != null ? clip.length : Mathf.Max(3f, total * fallbackSecondsPerChar);
        if (clip != null && narratorSource != null)
        {
            narratorSource.clip = clip;
            narratorSource.volume = AudioLibrary.VoiceVolume;
            narratorSource.Play();
        }
        if (skip != null) skip.ClearTap();

        float typeTime = Mathf.Max(0.1f, length * typeFraction);
        float t = 0f;
        bool advanced = false;
        while (t < typeTime)
        {
            if (AdvancePressed()) { advanced = true; break; }
            compositor.SetVisibleCharacters(spread, Mathf.CeilToInt(total * (t / typeTime)));
            yield return null;
            t += Time.unscaledDeltaTime;
        }
        compositor.SetVisibleCharacters(spread, total);
        if (advanced)
        {
            yield return null; // un cuadro para que el RT tenga el texto completo antes de girar
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

    /// <summary>Final o salto: cámara a Cam_Push (solo en el final), fundido a negro con la lluvia subiendo, y Nivel 1.</summary>
    private IEnumerator Leave(float fadeSeconds)
    {
        if (_leaving) yield break;
        _leaving = true;
        if (skip != null) skip.Armed = false;
        BeginPreload();

        bool ending = fadeSeconds < 0f;
        float total = ending ? Mathf.Max(pushSeconds, fadeOutSeconds) : fadeSeconds;
        if (ending && book != null && book.CamPush != null) StartCoroutine(MoveCamera(book.CamPush, pushSeconds));
        StartCoroutine(RampAudio(total));

        if (ending) yield return Wait(Mathf.Max(0f, pushSeconds - fadeOutSeconds), false);
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
    // Cámara (nunca toca el FOV)
    // ==================================================================

    private void SetCameraPose(Transform anchor)
    {
        _camPos = anchor.position;
        _camRot = anchor.rotation;
        ApplyCamera();
    }

    private IEnumerator MoveCamera(Transform target, float seconds)
    {
        if (target == null) yield break;
        Vector3 fromPos = _camPos;
        Quaternion fromRot = _camRot;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float e = CinematicEase.InOut(t / seconds);
            _camPos = Vector3.Lerp(fromPos, target.position, e);
            _camRot = Quaternion.Slerp(fromRot, target.rotation, e);
            yield return null;
        }
        _camPos = target.position;
        _camRot = target.rotation;
    }

    private void LateUpdate() => ApplyCamera();

    private void ApplyCamera()
    {
        if (viewCamera == null) return;
        float time = Time.unscaledTime * driftSpeed;
        float pitch = (Mathf.PerlinNoise(time, _driftSeed) - 0.5f) * 2f * driftDegrees;
        float yaw = (Mathf.PerlinNoise(_driftSeed, time + 17.3f) - 0.5f) * 2f * driftDegrees;
        viewCamera.transform.SetPositionAndRotation(_camPos, _camRot * Quaternion.Euler(pitch, yaw, 0f));
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

    private IEnumerator Wait(float seconds, bool canAdvance)
    {
        if (canAdvance && skip != null) skip.ClearTap();
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            if (canAdvance && AdvancePressed()) yield break;
            yield return null;
        }
    }

    private void Log(string message)
    {
        if (debug) Debug.Log("[Intro] " + message);
    }
}
