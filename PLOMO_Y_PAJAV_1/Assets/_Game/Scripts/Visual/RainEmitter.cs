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
    }

    private bool ProbeIndoors()
    {
        foreach (Vector3 offset in ProbeOffsets)
        {
            int count = Physics.RaycastNonAlloc(_camera.position + offset, Vector3.up, Hits, ProbeLength, RoofMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = Hits[i].collider;
                if (c == null || c.isTrigger || c.transform.IsChildOf(transform)) continue;           // el propio jugador
                if (c.GetComponentInParent<EnemyHealth>() != null) continue;                           // otros personajes
                return true;                                                                           // cielorraso, techo, alero...
            }
        }
        return false;
    }
}
