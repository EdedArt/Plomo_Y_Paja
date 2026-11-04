using UnityEngine;

/// <summary>
/// Pasos de Wilbur: "pasos_madera" bajo techo y "pasos_barro" al aire libre (AudioLibrary, variantes _01.._03).
/// DÓNDE VA: raíz del Player, junto a PlayerController y RainEmitter (lo añade "Crear Prefabs de Prueba").
///
/// Cada archivo de Cristian es una tira de 3 a 7 pasos (2.5–3 s), no un paso suelto: mientras Wilbur camina en el
/// piso suena una tira tras otra (variante al azar, nunca la misma dos veces seguidas) y al detenerse se apaga en
/// ~0.12 s. El ritmo sigue a la velocidad: el pitch sube al correr, así los pasos se oyen más seguidos.
/// Techo o no: el estado de RainEmitter (con histéresis); sin RainEmitter, el mismo chequeo de techo cada 0.2 s.
/// Volumen: AudioLibrary.Mix.footsteps. Sin clips no suena nada.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerFootsteps : MonoBehaviour
{
    private const string IndoorSfx = "pasos_madera";
    private const string OutdoorSfx = "pasos_barro";

    [Tooltip("Velocidad horizontal mínima para que suenen pasos (m/s)")]
    [SerializeField] private float minSpeed = 0.6f;
    [Tooltip("Velocidad a la que la tira suena a su ritmo original (caminar)")]
    [SerializeField] private float referenceSpeed = 4f;
    [SerializeField] private float minPitch = 0.85f;
    [SerializeField] private float maxPitch = 1.3f;
    [Tooltip("Segundos para apagar los pasos al detenerse")]
    [SerializeField] private float fadeOutTime = 0.12f;

    private CharacterController _controller;
    private RainEmitter _rain;
    private AudioSource _source;
    private string _surface;
    private bool _probedIndoors;
    private float _nextProbe;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        TryGetComponent(out _rain);

        var go = new GameObject("Pasos_Audio");
        go.transform.SetParent(transform, false);
        _source = go.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false;
        _source.spatialBlend = 0f;
        _source.volume = 0f;
    }

    private void OnDisable()
    {
        if (_source != null) _source.Stop();
    }

    private void Update()
    {
        Vector3 v = _controller.velocity;
        float speed = new Vector3(v.x, 0f, v.z).magnitude;
        bool walking = Time.timeScale > 0f && _controller.isGrounded && speed > minSpeed;

        if (!walking)
        {
            if (_source.isPlaying)
            {
                _source.volume = Mathf.MoveTowards(_source.volume, 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeOutTime));
                if (_source.volume <= 0f) _source.Stop();
            }
            return;
        }

        string surface = IsIndoors() ? IndoorSfx : OutdoorSfx;
        if (!_source.isPlaying || surface != _surface)
        {
            AudioClip clip = AudioLibrary.GetSfx(surface);
            if (clip == null) return;
            _surface = surface;
            _source.clip = clip;
            _source.Play();
        }

        _source.volume = AudioLibrary.Mix.footsteps * AudioLibrary.SfxVolume;
        _source.pitch = Mathf.Clamp(speed / Mathf.Max(0.1f, referenceSpeed), minPitch, maxPitch);
    }

    private bool IsIndoors()
    {
        if (_rain != null && _rain.enabled) return _rain.IsIndoors;
        if (Time.time >= _nextProbe)
        {
            _nextProbe = Time.time + 0.2f;
            _probedIndoors = RainEmitter.ProbeUnderRoof(transform.position + Vector3.up * 1.6f, transform);
        }
        return _probedIndoors;
    }
}
