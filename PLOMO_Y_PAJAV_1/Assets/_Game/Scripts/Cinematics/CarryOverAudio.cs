using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lleva un AudioSource (ej: la lluvia del final del libro) a la escena siguiente y lo apaga suave cuando esa escena ya
/// cargó, para que no haya silencio entre escenas (I11). La escena nueva arranca su propio sonido (RainEmitter sube la
/// lluvia en ~0.3 s) y este se desvanece encima.
/// </summary>
public class CarryOverAudio : MonoBehaviour
{
    private AudioSource _source;
    private float _fadeSeconds = 2f;
    private float _startVolume;
    private bool _loaded;

    /// <summary>Saca la fuente de su escena (DontDestroyOnLoad) y la apaga en fadeSeconds tras la próxima carga.</summary>
    public static void Carry(AudioSource source, float fadeSeconds)
    {
        if (source == null) return;
        var carry = source.gameObject.AddComponent<CarryOverAudio>();
        carry._source = source;
        carry._fadeSeconds = Mathf.Max(0.05f, fadeSeconds);
        source.transform.SetParent(null, true);
        DontDestroyOnLoad(source.gameObject);
        SceneManager.sceneLoaded += carry.HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        _loaded = true;
        _startVolume = _source != null ? _source.volume : 0f;
    }

    private void Update()
    {
        if (!_loaded) return;
        if (_source == null || _startVolume <= 0f)
        {
            Destroy(gameObject);
            return;
        }
        _source.volume = Mathf.MoveTowards(_source.volume, 0f, _startVolume / _fadeSeconds * Time.unscaledDeltaTime);
        if (_source.volume <= 0f) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }
}
