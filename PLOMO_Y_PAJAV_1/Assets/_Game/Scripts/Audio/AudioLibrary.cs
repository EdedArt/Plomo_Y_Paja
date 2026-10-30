using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Librería de audio del juego: voces por lineId y efectos con variantes.
/// ASSET: Assets/_Game/Audio/AudioLibrary.asset
/// MENÚ: Plomo y Paja → Reconstruir Librería de Audio (la llena leyendo las carpetas)
///
/// Convención de archivos:
/// - Voces: Assets/_Game/Audio/Voices/&lt;characterId&gt;/&lt;lineId&gt;.wav  → PlayVoice("guardia_ver_01", pos)
/// - SFX:   Assets/_Game/Audio/SFX/&lt;nombre&gt;_&lt;NN&gt;.wav           → PlaySfx("escopeta_disparo", pos)
///   (todas las _NN de un nombre son variantes; se elige una al azar sin repetir la anterior)
///
/// - Música: Assets/_Game/Audio/Music/&lt;nombre&gt;_&lt;NN&gt;.wav       → se agrupa igual que los SFX
///
/// Si falta un clip no suena nada y se avisa una sola vez por id en la consola (Log, no error).
/// VOLÚMENES: todos en este asset (voiceVolume, sfxVolume y el bloque "mix" del jugador y el ambiente).
/// </summary>
[CreateAssetMenu(fileName = "AudioLibrary", menuName = "Plomo y Paja/Audio Library")]
public class AudioLibrary : ScriptableObject
{
    [Serializable]
    public class VoiceEntry
    {
        public string lineId;
        public AudioClip clip;
    }

    [Serializable]
    public class SfxEntry
    {
        public string name;
        public AudioClip[] variants;
    }

    [SerializeField] private VoiceEntry[] voices = new VoiceEntry[0];
    [SerializeField] private SfxEntry[] sfx = new SfxEntry[0];

    [Header("Mezcla")]
    [SerializeField, Range(0f, 1f)] private float voiceVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.9f;
    [Tooltip("Distancia a la que un sonido 3D deja de oírse")]
    [SerializeField] private float maxDistance = 30f;

    /// <summary>Volúmenes y tiempos del jugador y del ambiente (3A6). Se ajustan en el Inspector del asset.</summary>
    [Serializable]
    public class MixSettings
    {
        [Tooltip("Pasos de Wilbur (pasos_madera / pasos_barro)")]
        [Range(0f, 1f)] public float footsteps = 0.45f;
        [Tooltip("Lluvia (lluvia_loop) al aire libre")]
        [Range(0f, 1f)] public float rainOutdoor = 0.4f;
        [Tooltip("Lluvia bajo techo: más baja y ahogada")]
        [Range(0f, 1f)] public float rainIndoor = 0.12f;
        [Tooltip("Corte del filtro paso bajo bajo techo (Hz)")]
        public float rainIndoorCutoff = 900f;
        [Tooltip("Truenos (trueno), solo al aire libre")]
        [Range(0f, 1f)] public float thunder = 0.8f;
        public float thunderMinInterval = 25f;
        public float thunderMaxInterval = 60f;
        [Tooltip("Segundos mínimos entre quejidos de Wilbur (la escopeta no los apila)")]
        public float hurtVoiceCooldown = 0.6f;

        [Header("Voces de enemigos (3A7)")]
        [Tooltip("Segundos mínimos entre frases de enemigos (cualquiera). Una frase de MAYOR prioridad que la anterior se salta este tiempo")]
        public float enemyPhraseCooldown = 4f;
        [Tooltip("Mismo efecto exacto (mismo id) como máximo 1 vez cada tantos segundos: 8 perdigones = 1 impacto")]
        public float sfxDedupeSeconds = 0.08f;
        [Tooltip("Probabilidad de frase al morir (el sonido de muerte no verbal suena SIEMPRE)")]
        [Range(0f, 1f)] public float deathPhraseChance = 0.25f;
        [Tooltip("Segundos mínimos entre dos frases de muerte (de cualquier enemigo)")]
        public float deathPhraseCooldown = 20f;
        [Range(0f, 1f)] public float surrenderPhraseChance = 1f;
        [Range(0f, 1f)] public float hurtPhraseChance = 0.4f;
        [Tooltip("Segundos mínimos entre quejidos del MISMO enemigo")]
        public float hurtPhraseCooldown = 1.5f;
        [Range(0f, 1f)] public float spottedPhraseChance = 0.5f;
        [Range(0f, 1f)] public float patrolPhraseChance = 0.3f;
        [Tooltip("La frase de patrulla solo sale si ningún enemigo habló en los últimos segundos")]
        public float patrolQuietSeconds = 15f;
    }

    [SerializeField] private MixSettings mix = new MixSettings();
    private static readonly MixSettings DefaultMix = new MixSettings();

    private const int PoolSize = 16;

    private static AudioLibrary _active;
    private static readonly HashSet<string> MissingLogged = new HashSet<string>();
    private static readonly Dictionary<string, int> LastVariant = new Dictionary<string, int>();
    private static AudioSource[] _pool;
    private static int _poolCursor;

    private Dictionary<string, AudioClip> _voiceMap;
    private Dictionary<string, AudioClip[]> _sfxMap;

    /// <summary>La librería en uso (la registra AudioLibraryLoader o el propio asset al cargarse).</summary>
    public static AudioLibrary Active => _active;

    /// <summary>Mezcla de la librería activa (valores por defecto si no hay librería).</summary>
    public static MixSettings Mix => _active != null && _active.mix != null ? _active.mix : DefaultMix;

    private void OnEnable()
    {
        if (_active == null) _active = this;
        _voiceMap = null;
        _sfxMap = null;
    }

    /// <summary>Fija la librería activa (AudioLibraryLoader la llama en Awake).</summary>
    public static void SetActive(AudioLibrary library)
    {
        if (library == null) return;
        _active = library;
        library._voiceMap = null;
        library._sfxMap = null;
    }

#if UNITY_EDITOR
    /// <summary>Solo para el builder del Editor.</summary>
    public void EditorSetContent(VoiceEntry[] newVoices, SfxEntry[] newSfx)
    {
        voices = newVoices;
        sfx = newSfx;
        _voiceMap = null;
        _sfxMap = null;
    }
#endif

    // ============ Voz de enemigos: un solo canal ============
    // Un único AudioSource para todos los enemigos: nunca hay dos voces de enemigo a la vez.
    // Prioridad: patrulla < ver < daño < rendición < muerte. Una voz nueva solo interrumpe si tiene MAYOR prioridad
    // (la muerte siempre corta). Entre frases hay un enfriamiento global, salvo que la nueva tenga mayor prioridad.

    public const int PriorityPatrol = 0, PrioritySpotted = 1, PriorityHurt = 2, PrioritySurrender = 3, PriorityDeath = 4;

    private static AudioSource _enemyVoice;
    private static float _voiceBusyUntil;          // fin estimado de la frase en curso (2 s si no hay clip)
    private static int _voicePriority = -1;
    private static float _lastPhraseTime = -999f;  // cuándo empezó la última frase de cualquier enemigo
    private static int _lastPhrasePriority = -1;

    /// <summary>Segundos desde la última frase de cualquier enemigo.</summary>
    public static float SecondsSinceEnemyPhrase => Time.time - _lastPhraseTime;

    /// <summary>
    /// Pide hablar a un enemigo. Devuelve true si la frase se acepta (suena el clip si existe; sin clip solo se
    /// registra, para que globos y reglas se comporten igual con o sin audio). False = se omite.
    /// </summary>
    public static bool TryPlayEnemyVoice(string lineId, Vector3 position, int priority)
    {
        float now = Time.time;
        bool busy = now < _voiceBusyUntil;
        if (priority < PriorityDeath)
        {
            if (busy && priority <= _voicePriority) return false;                     // canal ocupado por algo igual o mayor
            if (now - _lastPhraseTime < Mix.enemyPhraseCooldown && priority <= _lastPhrasePriority) return false;
        }

        AudioClip clip = _active != null ? _active.FindVoice(lineId) : null;
        if (clip == null) LogMissingOnce("voz", lineId);

        if (_enemyVoice == null)
        {
            var go = new GameObject("AudioLibrary_EnemyVoice");
            _enemyVoice = go.AddComponent<AudioSource>();
            _enemyVoice.playOnAwake = false;
            _enemyVoice.rolloffMode = AudioRolloffMode.Linear;
            _enemyVoice.minDistance = 2f;
            _enemyVoice.dopplerLevel = 0f;
        }
        _enemyVoice.Stop();                                                           // corta la anterior
        _voicePriority = priority;
        _lastPhraseTime = now;
        _lastPhrasePriority = priority;
        _voiceBusyUntil = now + (clip != null ? clip.length : 2f);
        if (clip != null)
        {
            _enemyVoice.transform.position = position;
            _enemyVoice.spatialBlend = 1f;
            _enemyVoice.maxDistance = _active.maxDistance;
            _enemyVoice.clip = clip;
            _enemyVoice.volume = _active.voiceVolume;
            _enemyVoice.pitch = 1f;
            _enemyVoice.Play();
        }
        return true;
    }

    /// <summary>Corta la voz de enemigo en curso si es de menor prioridad que la dada (ej: al morir un enemigo).</summary>
    public static void CutEnemyVoiceBelow(int priority)
    {
        if (_enemyVoice != null && _voicePriority < priority)
        {
            _enemyVoice.Stop();
            _voiceBusyUntil = 0f;
            _voicePriority = -1;
        }
    }

    // Anti-apilado de efectos idénticos
    private static readonly Dictionary<string, float> LastSfxTime = new Dictionary<string, float>();

    private static bool SfxTooSoon(string name)
    {
        float now = Time.unscaledTime;
        if (LastSfxTime.TryGetValue(name, out float last) && now - last < Mix.sfxDedupeSeconds) return true;
        LastSfxTime[name] = now;
        return false;
    }

    // ============ API estática ============

    /// <summary>Reproduce una línea de voz en una posición del mundo. Devuelve false si no hay clip.</summary>
    public static bool PlayVoice(string lineId, Vector3 position)
    {
        AudioClip clip = _active != null ? _active.FindVoice(lineId) : null;
        if (clip == null)
        {
            LogMissingOnce("voz", lineId);
            return false;
        }
        Play(clip, position, true, _active.voiceVolume);
        return true;
    }

    /// <summary>Reproduce una variante al azar de un efecto en una posición del mundo.</summary>
    public static bool PlaySfx(string name, Vector3 position)
    {
        if (SfxTooSoon(name)) return true;
        AudioClip clip = _active != null ? _active.PickSfx(name) : null;
        if (clip == null)
        {
            LogMissingOnce("sfx", name);
            return false;
        }
        Play(clip, position, true, _active.sfxVolume);
        return true;
    }

    /// <summary>Reproduce un efecto sin posición (sonidos del propio jugador: escopeta, pistas).</summary>
    public static bool PlaySfx2D(string name)
    {
        if (SfxTooSoon(name)) return true;
        AudioClip clip = _active != null ? _active.PickSfx(name) : null;
        if (clip == null)
        {
            LogMissingOnce("sfx", name);
            return false;
        }
        Play(clip, Vector3.zero, false, _active.sfxVolume);
        return true;
    }

    /// <summary>Efecto sin posición con el volumen multiplicado (ej: truenos con Mix.thunder).</summary>
    public static bool PlaySfx2D(string name, float volumeScale)
    {
        if (SfxTooSoon(name)) return true;
        AudioClip clip = _active != null ? _active.PickSfx(name) : null;
        if (clip == null)
        {
            LogMissingOnce("sfx", name);
            return false;
        }
        Play(clip, Vector3.zero, false, _active.sfxVolume * Mathf.Clamp01(volumeScale));
        return true;
    }

    /// <summary>Voz sin posición (la del propio Wilbur).</summary>
    public static bool PlayVoice2D(string lineId)
    {
        AudioClip clip = _active != null ? _active.FindVoice(lineId) : null;
        if (clip == null)
        {
            LogMissingOnce("voz", lineId);
            return false;
        }
        Play(clip, Vector3.zero, false, _active.voiceVolume);
        return true;
    }

    /// <summary>
    /// Una variante del efecto (sin repetir la anterior) para un AudioSource propio: bucles de ambiente o pasos.
    /// Devuelve null (y avisa una vez) si no hay clip.
    /// </summary>
    public static AudioClip GetSfx(string name)
    {
        AudioClip clip = _active != null ? _active.PickSfx(name) : null;
        if (clip == null) LogMissingOnce("sfx", name);
        return clip;
    }

    /// <summary>Volumen general de efectos (para multiplicar en fuentes propias).</summary>
    public static float SfxVolume => _active != null ? _active.sfxVolume : 0.9f;

    // ============ Búsqueda ============

    private AudioClip FindVoice(string lineId)
    {
        if (string.IsNullOrEmpty(lineId)) return null;
        if (_voiceMap == null)
        {
            _voiceMap = new Dictionary<string, AudioClip>();
            foreach (VoiceEntry entry in voices)
                if (entry != null && entry.clip != null && !string.IsNullOrEmpty(entry.lineId))
                    _voiceMap[entry.lineId] = entry.clip;
        }
        return _voiceMap.TryGetValue(lineId, out AudioClip clip) ? clip : null;
    }

    private AudioClip PickSfx(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (_sfxMap == null)
        {
            _sfxMap = new Dictionary<string, AudioClip[]>();
            foreach (SfxEntry entry in sfx)
            {
                if (entry == null || string.IsNullOrEmpty(entry.name) || entry.variants == null) continue;
                var valid = new List<AudioClip>();
                foreach (AudioClip c in entry.variants)
                    if (c != null) valid.Add(c);
                if (valid.Count > 0) _sfxMap[entry.name] = valid.ToArray();
            }
        }
        if (!_sfxMap.TryGetValue(name, out AudioClip[] variants)) return null;
        if (variants.Length == 1) return variants[0];

        // Nunca la misma variante dos veces seguidas
        int index;
        if (LastVariant.TryGetValue(name, out int last) && last < variants.Length)
        {
            index = UnityEngine.Random.Range(0, variants.Length - 1);
            if (index >= last) index++;
        }
        else
        {
            index = UnityEngine.Random.Range(0, variants.Length);
        }
        LastVariant[name] = index;
        return variants[index];
    }

    // ============ Reproducción con un pool de AudioSources ============

    private static void Play(AudioClip clip, Vector3 position, bool spatial, float volume)
    {
        AudioSource source = NextSource();
        source.transform.position = position;
        source.spatialBlend = spatial ? 1f : 0f;
        source.maxDistance = _active != null ? _active.maxDistance : 30f;
        source.clip = clip;
        source.volume = volume;
        source.pitch = 1f;
        source.Play();
    }

    private static AudioSource NextSource()
    {
        // El pool vive en la escena; si se cambió de escena se vuelve a crear
        if (_pool == null || _pool[0] == null)
        {
            var root = new GameObject("AudioLibrary_Pool");
            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Source_" + i);
                go.transform.SetParent(root.transform, false);
                AudioSource s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 2f;
                s.dopplerLevel = 0f;
                _pool[i] = s;
            }
            _poolCursor = 0;
        }

        // Preferir una fuente libre; si todas suenan, se recicla la más antigua
        for (int i = 0; i < PoolSize; i++)
        {
            AudioSource candidate = _pool[(_poolCursor + i) % PoolSize];
            if (!candidate.isPlaying)
            {
                _poolCursor = (_poolCursor + i + 1) % PoolSize;
                return candidate;
            }
        }
        AudioSource oldest = _pool[_poolCursor];
        _poolCursor = (_poolCursor + 1) % PoolSize;
        return oldest;
    }

    private static void LogMissingOnce(string kind, string id)
    {
        string key = kind + ":" + id;
        if (MissingLogged.Add(key))
            Debug.Log($"[AudioLibrary] Sin audio para {kind} '{id}' (se usa silencio).");
    }
}
