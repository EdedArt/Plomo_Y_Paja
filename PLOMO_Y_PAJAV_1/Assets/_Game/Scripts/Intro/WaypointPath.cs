using UnityEngine;

/// <summary>
/// Camino suave (Catmull-Rom) por los hijos de este objeto, en orden (INTRO.2: Taxi_Path, Taxi_ExitPath).
/// Sample(distancia) da la posición y la dirección a lo largo del camino. Las distancias se precalculan una vez
/// (tabla de 64 tramos por segmento): sin basura por frame.
/// DÓNDE VA: un objeto vacío con un hijo por punto (lo arma Plomo y Paja → Intro → Construir taxi y llegada).
/// </summary>
public class WaypointPath : MonoBehaviour
{
    private const int StepsPerSegment = 64;

    private Vector3[] _points;
    private float[] _lengths;   // distancia acumulada en cada muestra
    private Vector3[] _samples;

    public float Length { get; private set; }
    public int PointCount => transform.childCount;

    public Vector3 StartPoint => transform.childCount > 0 ? transform.GetChild(0).position : transform.position;
    public Vector3 EndPoint => transform.childCount > 0 ? transform.GetChild(transform.childCount - 1).position : transform.position;

    private void Awake() => Build();

    /// <summary>Recalcula la tabla (si los puntos se movieron).</summary>
    public void Build()
    {
        int n = transform.childCount;
        _points = new Vector3[Mathf.Max(1, n)];
        for (int i = 0; i < n; i++) _points[i] = transform.GetChild(i).position;
        if (n == 0) _points[0] = transform.position;

        int segments = Mathf.Max(1, _points.Length - 1);
        int count = segments * StepsPerSegment + 1;
        _samples = new Vector3[count];
        _lengths = new float[count];
        float total = 0f;
        for (int s = 0; s < count; s++)
        {
            float u = (float)s / StepsPerSegment;
            _samples[s] = Evaluate(Mathf.Min(u, segments));
            if (s > 0) total += Vector3.Distance(_samples[s - 1], _samples[s]);
            _lengths[s] = total;
        }
        Length = total;
    }

    /// <summary>Posición y dirección a "distance" metros del inicio (se recorta a [0, Length]).</summary>
    public void Sample(float distance, out Vector3 position, out Vector3 forward)
    {
        if (_samples == null) Build();
        distance = Mathf.Clamp(distance, 0f, Length);
        int lo = 0, hi = _lengths.Length - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (_lengths[mid] < distance) lo = mid; else hi = mid;
        }
        float span = _lengths[hi] - _lengths[lo];
        float t = span > 1e-5f ? (distance - _lengths[lo]) / span : 0f;
        position = Vector3.Lerp(_samples[lo], _samples[hi], t);
        Vector3 dir = _samples[hi] - _samples[lo];
        dir.y = 0f;
        forward = dir.sqrMagnitude > 1e-8f ? dir.normalized : transform.forward;
    }

    /// <summary>Catmull-Rom uniforme; u en [0, segmentos].</summary>
    private Vector3 Evaluate(float u)
    {
        int n = _points.Length;
        if (n == 1) return _points[0];
        int i = Mathf.Clamp(Mathf.FloorToInt(u), 0, n - 2);
        float t = u - i;
        Vector3 p0 = _points[Mathf.Max(0, i - 1)];
        Vector3 p1 = _points[i];
        Vector3 p2 = _points[i + 1];
        Vector3 p3 = _points[Mathf.Min(n - 1, i + 2)];
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (transform.childCount < 2) return;
        Gizmos.color = new Color(1f, 0.8f, 0.1f);
        for (int i = 1; i < transform.childCount; i++)
            Gizmos.DrawLine(transform.GetChild(i - 1).position + Vector3.up * 0.2f, transform.GetChild(i).position + Vector3.up * 0.2f);
    }
#endif
}
