using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Música que sigue del menú al libro de la intro sin reiniciarse (M6). Una sola instancia, DontDestroyOnLoad.
/// DÓNDE VA: objeto "MusicPlayer" de MenuPrincipal (lo arma Plomo y Paja → Crear menu de inicio). Al volver al menú, la
/// copia nueva de la escena se destruye si ya hay una sonando.
///
/// - Suena el clip exacto del campo "clip" (no AudioLibrary.GetSfx, que elige variantes al azar); si falta, usa el id de
///   respaldo y, si tampoco está, avisa y se queda callado.
/// - StoryBookDirector no arranca su propia música si esto suena: le manda su bajada bajo el narrador y su fundido final
///   (SetBookMix) y lo apaga al salir (Stop). Si se abre Intro directo en el Editor no hay MusicPlayer y el libro usa la suya.
/// - Al cargar una escena fuera de "scenesWithMusic" se apaga solo: NivelPorky nunca hereda música.
/// - Volumen de Opciones en PlayerPrefs (opt_musica). Todo en tiempo real.
/// </summary>
public class MusicPlayer : MonoBehaviour
{
    private const string VolumeKey = "opt_musica";

    /// <summary>La instancia viva, o null si no hay música de menú.</summary>
    public static MusicPlayer Instance { get; private set; }

    [Header("Pista")]
    [SerializeField] private AudioClip clip;
    [Tooltip("Se pide a la AudioLibrary si el campo clip está vacío")]
    [SerializeField] private string fallbackId = "musica_intro";

    [Header("Mezcla")]
    [Tooltip("Igual al musicVolume del libro, para que no salte el volumen al pasar de escena")]
    [SerializeField, Range(0f, 1f)] private float baseVolume = 0.35f;
    [SerializeField] private float fadeInSeconds = 1.5f;
    [Tooltip("Qué tan rápido sigue el volumen a su objetivo (por segundo); igual al libro")]
    [SerializeField] private float volumeSlew = 0.6f;

    [Header("Escenas")]
    [SerializeField] private string[] scenesWithMusic = { "MenuPrincipal", "Intro" };

    [Header("Empalme del loop")]
    [Tooltip("0 = loop normal. Mayor que 0: cruza el final con el inicio en estos segundos, si musica_intro hace clic al repetir")]
    [SerializeField, Range(0f, 5f)] private float loopCrossfadeSeconds = 0f;

    private static float _userVolume = -1f;

    private AudioSource _main;
    private AudioSource _other;
    private float _entry;
    private float _volume;
    private float _duck = 1f;
    private float _fade = 1f;
    private float _crossfadeTime = -1f;

    /// <summary>Volumen de la opción "Música" (0–1), guardado en PlayerPrefs.</summary>
    public static float UserVolume
    {
        get
        {
            if (_userVolume < 0f) _userVolume = PlayerPrefs.GetFloat(VolumeKey, 1f);
            return _userVolume;
        }
        set
        {
            _userVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, _userVolume);
        }
    }

    public bool IsPlaying => _main != null && _main.isPlaying;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        transform.SetParent(null, false);
        DontDestroyOnLoad(gameObject);
        _main = NewSource();
        _other = NewSource();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        if (Instance != this) return;

        if (clip == null && !string.IsNullOrEmpty(fallbackId)) clip = AudioLibrary.GetSfx(fallbackId);
        if (clip == null)
        {
            Debug.LogWarning("[MusicPlayer] Sin clip de música (" + fallbackId + "): el menú queda sin música.");
            return;
        }

        _main.clip = clip;
        _other.clip = clip;
        _main.loop = loopCrossfadeSeconds <= 0f || clip.length <= loopCrossfadeSeconds * 2f;
        _main.volume = 0f;
        _main.Play();
    }

    private AudioSource NewSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.priority = 16;
        return source;
    }

    private void Update()
    {
        if (Instance != this || clip == null) return;

        float dt = Time.unscaledDeltaTime;
        _entry = Mathf.MoveTowards(_entry, 1f, dt / Mathf.Max(0.01f, fadeInSeconds));
        float target = baseVolume * AudioLibrary.SfxVolume * UserVolume * _duck * _fade * _entry;
        _volume = Mathf.MoveTowards(_volume, target, dt * volumeSlew);

        if (_crossfadeTime >= 0f)
        {
            UpdateCrossfade(dt);
            return;
        }
        _main.volume = _volume;

        bool canCrossfade = loopCrossfadeSeconds > 0f && clip.length > loopCrossfadeSeconds * 2f;
        if (canCrossfade && _main.isPlaying && _main.time >= clip.length - loopCrossfadeSeconds)
        {
            _other.time = 0f;
            _other.volume = 0f;
            _other.Play();
            _crossfadeTime = 0f;
        }
    }

    private void UpdateCrossfade(float dt)
    {
        _crossfadeTime += dt;
        float k = Mathf.Clamp01(_crossfadeTime / loopCrossfadeSeconds);
        _main.volume = _volume * (1f - k);
        _other.volume = _volume * k;
        if (k < 1f) return;

        _main.Stop();
        AudioSource swap = _main;
        _main = _other;
        _other = swap;
        _crossfadeTime = -1f;
    }

    /// <summary>La mezcla que pide el libro cada cuadro: bajada bajo el narrador y fundido final (ambos 0–1).</summary>
    public void SetBookMix(float duck, float fade)
    {
        _duck = duck;
        _fade = fade;
    }

    /// <summary>Apaga la música y quita el reproductor; el próximo menú crea uno nuevo.</summary>
    public void Stop()
    {
        Destroy(gameObject);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        for (int i = 0; i < scenesWithMusic.Length; i++)
        {
            if (scenesWithMusic[i] == scene.name) return;
        }
        Stop();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }
}
