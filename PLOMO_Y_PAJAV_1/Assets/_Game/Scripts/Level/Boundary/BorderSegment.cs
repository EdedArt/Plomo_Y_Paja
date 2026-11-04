using UnityEngine;

/// <summary>
/// Fase N1.1: pieza de borde del Nivel 1 (cerca, seto, auto, barricada...). DÓNDE VA: raíz de cada prefab de
/// Prefabs/Level/Boundary/. Una sola caja que abraza la forma visible (regla N1/N3: nada detiene al jugador sin verse);
/// el kit la calcula y marca la pieza estática y "Not Walkable" (no se camina por encima).
/// </summary>
[DisallowMultipleComponent]
public class BorderSegment : MonoBehaviour
{
    [Tooltip("Id de la pieza en el kit (nombre del prefab)")]
    [SerializeField] private string pieceId = "";

    [Tooltip("Largo nominal a lo largo de X local (m). BorderPath lo usa para repartir las piezas")]
    [SerializeField] private float length = 1f;

    public string PieceId => pieceId;
    public float Length => length;

    /// <summary>Solo desde el kit: deja la caja del borde con estos bounds locales.</summary>
    public BoxCollider Configure(string id, float pieceLength, Bounds localBounds)
    {
        pieceId = id;
        length = pieceLength;
        if (!TryGetComponent(out BoxCollider box)) box = gameObject.AddComponent<BoxCollider>();
        box.center = localBounds.center;
        box.size = localBounds.size;
        return box;
    }
}
