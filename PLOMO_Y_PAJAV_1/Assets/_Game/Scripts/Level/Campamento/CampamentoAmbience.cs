using UnityEngine;

/// <summary>
/// Ambientes del Nivel 2 (N2.1): amb_bosque (2D) en el sendero, que se apaga al llegar al portón, y amb_campamento (3D, en el
/// gramófono de los barracones). Los clips salen de AudioLibrary por id: si faltan, ese ambiente no suena (AudioLibrary avisa
/// una vez) y basta agregar el archivo y correr "Reconstruir Librería de Audio". La lluvia la pone RainEmitter, como siempre.
/// DÓNDE VA: objeto "Ambiente" de Campamento (lo arma Plomo y Paja → Nivel 2 → Generar Campamento).
/// </summary>
public class CampamentoAmbience : MonoBehaviour
{
    [Header("Fuentes (las pone el builder)")]
    [SerializeField] private AudioSource forestSource;
    [SerializeField] private AudioSource gramophoneSource;

    [Header("Ids de AudioLibrary")]
    [SerializeField] private string forestId = "amb_bosque";
    [SerializeField] private string campId = "amb_campamento";

    [Header("Mezcla")]
    [SerializeField, Range(0f, 1f)] private float forestVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float gramophoneVolume = 0.85f;
    [Tooltip("El bosque suena entero con el jugador al sur de este Z…")]
    [SerializeField] private float forestFullZ = -18f;
    [Tooltip("…y en silencio al norte de este (el portón está en z = -10)")]
    [SerializeField] private float forestSilentZ = -4f;

    private Transform _player;

    private void Start()
    {
        Begin(forestSource, forestId, 0f);
        Begin(gramophoneSource, campId, gramophoneVolume * AudioLibrary.SfxVolume);
    }

    private static void Begin(AudioSource source, string id, float volume)
    {
        if (source == null) return;
        AudioClip clip = AudioLibrary.GetSfx(id);
        if (clip == null) return;
        source.clip = clip;
        source.loop = true;
        source.volume = volume;
        source.Play();
    }

    private void Update()
    {
        if (forestSource == null || !forestSource.isPlaying) return;
        if (_player == null)
        {
            GameObject go = GameObject.FindWithTag("Player");
            if (go == null) return;
            _player = go.transform;
        }
        float inForest = 1f - Mathf.InverseLerp(forestFullZ, forestSilentZ, _player.position.z);
        forestSource.volume = forestVolume * AudioLibrary.SfxVolume * inForest;
    }
}
