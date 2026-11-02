using UnityEngine;

/// <summary>
/// Pistas y objetos recogibles con el mismo trato que los personajes: sin iluminación, contorno negro
/// y un vaivén suave, para que se lean como "interactuables" incluso en un rincón oscuro.
/// DÓNDE VA: en el objeto de la pista (CluePickup la añade sola si falta).
/// Solo mueve los hijos visuales: el trigger de la pista se queda en su sitio.
/// </summary>
public class PickupPresentation : MonoBehaviour
{
    [SerializeField] private float bobHeight = 0.06f;
    [SerializeField] private float bobFrequency = 0.8f;
    [SerializeField] private float spinDegreesPerSecond = 0f;

    private Transform[] _visuals;
    private Vector3[] _basePositions;
    private Quaternion[] _baseRotations;
    private float _phase;
    private bool _ownRendererOnly;
    private Vector3 _rootBase;

    private void Start()
    {
        _phase = Random.value * 10f;

        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer r in renderers)
            CharacterLook.ApplyPlaceholder(r);

        // Si el renderer está en este mismo objeto, no hay hijos que mover: se mueve todo (vaivén mínimo)
        int count = 0;
        foreach (MeshRenderer r in renderers) if (r.transform != transform) count++;
        _ownRendererOnly = count == 0;
        _rootBase = transform.localPosition;

        _visuals = new Transform[count];
        _basePositions = new Vector3[count];
        _baseRotations = new Quaternion[count];
        int i = 0;
        foreach (MeshRenderer r in renderers)
        {
            if (r.transform == transform) continue;
            _visuals[i] = r.transform;
            _basePositions[i] = r.transform.localPosition;
            _baseRotations[i] = r.transform.localRotation;
            i++;
        }
    }

    private void Update()
    {
        float t = Time.time * bobFrequency * Mathf.PI * 2f + _phase;
        Vector3 offset = Vector3.up * (Mathf.Sin(t) * bobHeight);
        Quaternion spin = Quaternion.Euler(0f, Time.time * spinDegreesPerSecond, 0f);

        if (_ownRendererOnly)
        {
            transform.localPosition = _rootBase + offset * 0.5f;
            return;
        }
        for (int i = 0; i < _visuals.Length; i++)
        {
            if (_visuals[i] == null) continue;
            _visuals[i].localPosition = _basePositions[i] + offset;
            _visuals[i].localRotation = spin * _baseRotations[i];
        }
    }
}
