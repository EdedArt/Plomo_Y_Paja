using UnityEngine;

/// <summary>
/// Fase N1.1: un borde se escribe como una polilínea, no prop por prop. BorderPathFiller (editor) la llena con una mezcla
/// con semilla de piezas del kit (regla N5). Los puntos están en mundo; el objeto queda en el origen y sus hijos son las piezas.
/// </summary>
public class BorderPath : MonoBehaviour
{
    [Tooltip("Polilínea en coordenadas de mundo (y = 0)")]
    [SerializeField] private Vector3[] points = new Vector3[0];

    [Tooltip("Ids de las piezas que se pueden usar")]
    [SerializeField] private string[] pieces = new string[0];

    [Tooltip("Usar las piezas en este orden en lugar de una mezcla al azar")]
    [SerializeField] private bool inOrder;

    [Tooltip("Semilla de la mezcla (el mismo camino da siempre las mismas piezas)")]
    [SerializeField] private int seed;

    public Vector3[] Points => points;
    public string[] Pieces => pieces;
    public bool InOrder => inOrder;
    public int Seed => seed;

    /// <summary>Solo desde el editor.</summary>
    public void Setup(Vector3[] pathPoints, string[] pieceIds, bool useInOrder, int pathSeed)
    {
        points = pathPoints;
        pieces = pieceIds;
        inOrder = useInOrder;
        seed = pathSeed;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        for (int i = 0; i + 1 < points.Length; i++) Gizmos.DrawLine(points[i] + Vector3.up * 0.2f, points[i + 1] + Vector3.up * 0.2f);
    }
}
