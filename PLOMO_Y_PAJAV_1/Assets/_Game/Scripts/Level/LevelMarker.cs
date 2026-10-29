using UnityEngine;

/// <summary>
/// Tipos de marcador que colocan los LevelBuilder (y que leen otros sistemas).
/// </summary>
public enum MarkerType
{
    PlayerSpawn,
    Clue,
    PatrolWaypoint,
    SentinelPoint,
    InterrogableGuard,
    LevelExit
}

/// <summary>
/// Marcador de posición en el nivel (spawn, pistas, patrullas, salida...).
/// DÓNDE VA: GameObjects vacíos bajo "--- MARKERS ---". Los crea el LevelBuilder.
/// No tiene lógica: solo dibuja un gizmo con etiqueta para ubicarlo en la escena.
/// </summary>
public class LevelMarker : MonoBehaviour
{
    [Header("Marcador")]
    [Tooltip("Qué representa este punto")]
    [SerializeField] private MarkerType markerType = MarkerType.PatrolWaypoint;

    [Tooltip("ID asociado: clueId, npcId, ruta de patrulla, etc.")]
    [SerializeField] private string id = "";

    [Header("Gizmo")]
    [Tooltip("Radio de la esfera del gizmo")]
    [SerializeField] private float gizmoRadius = 0.35f;

    public MarkerType Type => markerType;
    public string Id => id;

    /// <summary>Configurar desde scripts de editor.</summary>
    public void Setup(MarkerType type, string markerId)
    {
        markerType = type;
        id = markerId;
    }

    private void OnDrawGizmos()
    {
        Color color = GetColor(markerType);
        Gizmos.color = color;
        Gizmos.DrawWireSphere(transform.position, gizmoRadius);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * (gizmoRadius * 3f));

        color.a = 0.25f;
        Gizmos.color = color;
        Gizmos.DrawSphere(transform.position, gizmoRadius);

#if UNITY_EDITOR
        string label = string.IsNullOrEmpty(id) ? markerType.ToString() : $"{markerType}: {id}";
        UnityEditor.Handles.color = GetColor(markerType);
        UnityEditor.Handles.Label(transform.position + Vector3.up * (gizmoRadius + 0.4f), label);
#endif
    }

    private static Color GetColor(MarkerType type)
    {
        switch (type)
        {
            case MarkerType.PlayerSpawn:       return Color.green;
            case MarkerType.Clue:              return Color.yellow;
            case MarkerType.PatrolWaypoint:    return Color.cyan;
            case MarkerType.SentinelPoint:     return new Color(1f, 0.5f, 0f);
            case MarkerType.InterrogableGuard: return Color.magenta;
            case MarkerType.LevelExit:         return Color.red;
            default:                           return Color.white;
        }
    }
}
