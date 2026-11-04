using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Fase N1.1, única excepción de la regla N1: red de seguridad a 10 m o más detrás de los bordes visibles, inalcanzable en
/// juego normal. Si el jugador llega (un bug de colisión), vuelve al último punto seguro.
/// DÓNDE VA: "N1_RedSeguridad" (raíz de NivelPorky), capa Ignore Raycast, con varios BoxCollider trigger. Lo crea Nivel1MapBuilder.
/// </summary>
public class SafetyCatchVolume : MonoBehaviour
{
    [Tooltip("Punto de reaparición mientras todavía no se guardó ninguno")]
    [SerializeField] private Vector3 fallbackPoint;

    [Tooltip("Cada cuántos segundos se guarda la posición del jugador si está parado sobre el NavMesh")]
    [SerializeField] private float sampleInterval = 1f;

    private CharacterController _player;
    private Vector3 _lastSafe;
    private float _nextSample;

    /// <summary>Solo desde el editor.</summary>
    public void Setup(Vector3 fallback)
    {
        fallbackPoint = fallback;
    }

    private void Awake()
    {
        _lastSafe = fallbackPoint;
    }

    private void Start()
    {
        PlayerController controller = FindObjectOfType<PlayerController>();
        if (controller != null) controller.TryGetComponent(out _player);
    }

    private void Update()
    {
        if (_player == null || Time.time < _nextSample) return;
        _nextSample = Time.time + sampleInterval;
        if (!_player.isGrounded) return;
        if (NavMesh.SamplePosition(_player.transform.position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            _lastSafe = hit.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_player == null || other.gameObject != _player.gameObject) return;

        Debug.LogWarning($"[SafetyCatch] El jugador cruzó un borde y llegó a {_player.transform.position}: vuelve a {_lastSafe}. Revisar ese borde.");
        _player.enabled = false;
        _player.transform.position = _lastSafe + Vector3.up * 0.1f;
        _player.enabled = true;
        Physics.SyncTransforms();
    }
}
