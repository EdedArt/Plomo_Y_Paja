using UnityEngine;

/// <summary>
/// Lluvia ligera que sigue al jugador: UN solo sistema de partículas sin colisiones (barato).
/// DÓNDE VA: raíz del Player (lo añade "Crear Prefabs de Prueba" con el material Fx_Lluvia).
///
/// - Una caja de gotas sobre la cámara que se mueve con ella; las gotas viven lo justo para llegar al piso.
/// - Bajo techo se apaga: cada 0.3 s se lanzan 5 rayos hacia arriba y, si alguno topa con un cielorraso o un techo,
///   no se emite lluvia. Bajo un agujero del techo se queda apagada también (las goteras las pone el nivel).
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
    private bool _indoors;

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
            _nextCheck = Time.unscaledTime + 0.3f;
            bool indoors = false;
            foreach (Vector3 offset in ProbeOffsets)
            {
                if (Physics.Raycast(_camera.position + offset, Vector3.up, 9f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    indoors = true;
                    break;
                }
            }
            if (indoors != _indoors)
            {
                _indoors = indoors;
                var emission = _rain.emission;
                emission.enabled = !_indoors;
            }
        }
    }
}
