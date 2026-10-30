using System.Collections;
using UnityEngine;

/// <summary>
/// Lluvia ligera que sigue al jugador: UN solo sistema de partículas sin colisiones (barato).
/// DÓNDE VA: raíz del Player (lo añade "Crear Prefabs de Prueba" con el material Fx_Lluvia).
///
/// - Una caja de gotas sobre la cámara que se mueve con ella; las gotas viven lo justo para llegar al piso.
/// - Bajo techo se apaga: cada 0.1 s se lanzan 5 rayos hacia arriba y, si alguno topa con un cielorraso o un techo,
///   no se emite lluvia. Los rayos ignoran triggers, los colliders del propio jugador, otros personajes y las mallas
///   sin collider (conos de luz, cielo, fondos, la lluvia misma). El cambio de estado necesita 0.3 s seguidos con el
///   mismo resultado (histéresis) para que la lluvia no parpadee en los umbrales de las puertas.
/// - Sin material asignado no hace nada.
/// - SONIDO (3A6): "lluvia_loop" en bucle 2D; bajo techo baja de volumen y se ahoga con un paso bajo. Al aire libre,
///   "trueno" cada 25–60 s con un destello breve del sol de la escena (si hay). Volúmenes en AudioLibrary.Mix.
/// </summary>
public class RainEmitter : MonoBehaviour
{
    [SerializeField] private Material material;
    [Tooltip("Lado de la caja de lluvia (m)")]
    [SerializeField] private float area = 24f;
    [Tooltip("Altura de la caja sobre la cámara (m)")]
    [SerializeField] private float height = 11f;
    [SerializeField] private float dropsPerSecond = 320f;
    [SerializeField] private float fallSpeed = 11f;
    [SerializeField] private Color dropColor = new Color(0.78f, 0.86f, 1f, 0.5f);

    private ParticleSystem _rain;
    private Transform _camera;
    private float _nextCheck;
    private bool _indoors;          // estado aplicado
    private bool _rawIndoors;       // última lectura de los rayos
    private float _rawSince;        // desde cuándo se mantiene esa lectura

    private const float CheckInterval = 0.1f;
    private const float Hysteresis = 0.3f;
    private const float ProbeLength = 9f;
    // Todas las capas menos "Ignore Raycast" (2) y "UI" (5); el resto se filtra por componente (ver ProbeIndoors)
    private static readonly int RoofMask = ~((1 << 2) | (1 << 5));
    private static readonly RaycastHit[] Hits = new RaycastHit[8];

    private AudioSource _rainAudio;
    private AudioLowPassFilter _rainFilter;
    private bool _audioTried;
    private float _nextThunder;
    private Coroutine _flashRoutine;
    private const float OpenCutoff = 22000f;
    private const float AudioFadeSpeed = 1.5f;      // volumen por segundo al entrar/salir

    /// <summary>¿El jugador está bajo techo? (con histéresis). Lo usan también los pasos.</summary>
    public bool IsIndoors => _indoors;

    private static readonly Vector3[] ProbeOffsets =
    {
        Vector3.zero, new Vector3(1.4f, 0f, 0f), new Vector3(-1.4f, 0f, 0f), new Vector3(0f, 0f, 1.4f), new Vector3(0f, 0f, -1.4f)
    };

    private void Awake()
    {
        if (material == null)
        {
            enabled = false;
            return;
        }

        var go = new GameObject("Lluvia");
        go.transform.SetParent(transform, false);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // la caja emite hacia su +Z: aquí, hacia abajo
        _rain = go.AddComponent<ParticleSystem>();
        _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = _rain.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 800;
        main.startSpeed = fallSpeed;
        main.startLifetime = (height + 1.5f) / fallSpeed;       // llega al piso y desaparece
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.03f);
        main.startColor = dropColor;
        main.gravityModifier = 0f;

        var emission = _rain.emission;
        emission.rateOverTime = dropsPerSecond;

        var shape = _rain.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(area, area, 0.05f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 3f;
        renderer.velocityScale = 0.02f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        _rain.Play();
        ScheduleThunder();
    }

    // ============ Sonido de la lluvia y truenos ============

    /// <summary>Crea la fuente en bucle la primera vez (la AudioLibrary se registra en Awake del Player).</summary>
    private void EnsureRainAudio()
    {
        if (_audioTried) return;
        _audioTried = true;
        AudioClip clip = AudioLibrary.GetSfx("lluvia_loop");
        if (clip == null) return;

        var go = new GameObject("Lluvia_Audio");
        go.transform.SetParent(transform, false);
        _rainAudio = go.AddComponent<AudioSource>();
        _rainAudio.clip = clip;
        _rainAudio.loop = true;
        _rainAudio.spatialBlend = 0f;
        _rainAudio.playOnAwake = false;
        _rainAudio.volume = 0f;
        _rainFilter = go.AddComponent<AudioLowPassFilter>();
        _rainFilter.cutoffFrequency = OpenCutoff;
        _rainAudio.Play();
    }

    private void UpdateRainAudio()
    {
        EnsureRainAudio();
        if (_rainAudio == null) return;

        AudioLibrary.MixSettings mix = AudioLibrary.Mix;
        float sfx = AudioLibrary.SfxVolume;
        float targetVolume = (_indoors ? mix.rainIndoor : mix.rainOutdoor) * sfx;
        float targetCutoff = _indoors ? mix.rainIndoorCutoff : OpenCutoff;
        float dt = Time.unscaledDeltaTime;
        _rainAudio.volume = Mathf.MoveTowards(_rainAudio.volume, targetVolume, AudioFadeSpeed * dt);
        // El corte se mueve en escala logarítmica para que el cambio suene parejo
        float logCut = Mathf.MoveTowards(Mathf.Log(_rainFilter.cutoffFrequency), Mathf.Log(Mathf.Max(100f, targetCutoff)), 6f * dt);
        _rainFilter.cutoffFrequency = Mathf.Exp(logCut);

        // Truenos: solo al aire libre y con el juego corriendo (Time.time se detiene en pausa)
        if (Time.time >= _nextThunder)
        {
            if (!_indoors && Time.timeScale > 0f)
            {
                AudioLibrary.PlaySfx2D("trueno", mix.thunder);
                if (_flashRoutine != null) StopCoroutine(_flashRoutine);
                _flashRoutine = StartCoroutine(LightningFlash());
            }
            ScheduleThunder();
        }
    }

    private void ScheduleThunder()
    {
        AudioLibrary.MixSettings mix = AudioLibrary.Mix;
        float min = Mathf.Max(1f, mix.thunderMinInterval);
        _nextThunder = Time.time + Random.Range(min, Mathf.Max(min, mix.thunderMaxInterval));
    }

    /// <summary>Dos parpadeos rápidos del sol de la escena (RenderSettings.sun). Sin sol, nada.</summary>
    private IEnumerator LightningFlash()
    {
        Light sun = RenderSettings.sun;
        if (sun == null) yield break;
        float baseIntensity = sun.intensity;
        sun.intensity = baseIntensity * 3f + 0.5f;
        yield return new WaitForSeconds(0.07f);
        sun.intensity = baseIntensity;
        yield return new WaitForSeconds(0.08f);
        sun.intensity = baseIntensity * 2f + 0.3f;
        yield return new WaitForSeconds(0.1f);
        sun.intensity = baseIntensity;
        _flashRoutine = null;
    }

    private void OnDisable()
    {
        if (_rainAudio != null) _rainAudio.volume = 0f;
        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
        }
    }

    private void LateUpdate()
    {
        if (_rain == null) return;
        if (_camera == null)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            _camera = cam.transform;
        }

        _rain.transform.position = _camera.position + Vector3.up * height;

        if (Time.unscaledTime >= _nextCheck)
        {
            _nextCheck = Time.unscaledTime + CheckInterval;
            bool raw = ProbeIndoors();
            if (raw != _rawIndoors)
            {
                _rawIndoors = raw;
                _rawSince = Time.unscaledTime;
            }
            if (_rawIndoors != _indoors && Time.unscaledTime - _rawSince >= Hysteresis)
            {
                _indoors = _rawIndoors;
                var emission = _rain.emission;
                emission.enabled = !_indoors;
            }
        }

        UpdateRainAudio();
    }

    private bool ProbeIndoors()
    {
        return ProbeUnderRoof(_camera.position, transform);
    }

    /// <summary>
    /// ¿Hay techo sobre este punto? 5 rayos hacia arriba que ignoran triggers, los colliders de "self" y personajes.
    /// Sin histéresis: para el estado estable usar IsIndoors.
    /// </summary>
    public static bool ProbeUnderRoof(Vector3 origin, Transform self)
    {
        foreach (Vector3 offset in ProbeOffsets)
        {
            int count = Physics.RaycastNonAlloc(origin + offset, Vector3.up, Hits, ProbeLength, RoofMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = Hits[i].collider;
                if (c == null || c.isTrigger || (self != null && c.transform.IsChildOf(self))) continue; // el propio jugador
                if (c.GetComponentInParent<EnemyHealth>() != null) continue;                           // otros personajes
                return true;                                                                           // cielorraso, techo, alero...
            }
        }
        return false;
    }
}
