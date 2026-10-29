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
/// Si falta un clip no suena nada y se avisa una sola vez por id en la consola (Log, no error).
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
        AudioClip clip = _active != null ? _active.PickSfx(name) : null;
        if (clip == null)
        {
            LogMissingOnce("sfx", name);
            return false;
        }
        Play(clip, Vector3.zero, false, _active.sfxVolume);
        return true;
    }

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
