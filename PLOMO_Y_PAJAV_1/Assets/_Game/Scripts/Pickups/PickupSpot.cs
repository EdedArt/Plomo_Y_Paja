using UnityEngine;

/// <summary>
/// Fase N1.2 (regla N7): dónde descansa un objeto recogible. DÓNDE VA: en cada Pickup_&lt;id&gt; de "--- PICKUPS ---" (lo agrega PickupBuilder).
/// Se ajusta a la superficie de abajo con un rayo y dice si el objeto quedó flotando. containerTag = el mueble o contenedor
/// ("Cocina: mostrador"): lo guarda "Guardar posiciones de objetos" y lo lee la auditoría. Sin lógica en juego.
/// </summary>
public class PickupSpot : MonoBehaviour
{
    /// <summary>Más que esto entre la marca y la superficie = aviso (N7: nada flota).</summary>
    public const float MaxGap = 0.05f;

    private const float CastUp = 0.3f, CastDown = 1.5f;

    [Tooltip("Mueble o contenedor donde está (opcional)")]
    [SerializeField] private string containerTag = "";

    public string ContainerTag => containerTag;

    /// <summary>Solo desde el editor.</summary>
    public void Setup(string container)
    {
        containerTag = container;
    }

    /// <summary>Deja el objeto sobre la superficie de abajo. false = no hay superficie. gap = cuánto se movió en altura.</summary>
    public bool Snap(out Collider surface, out float gap)
    {
        Vector3 position = transform.position;
        surface = null;
        gap = 0f;
        if (!Pickup.TryFindSurface(position, CastUp, CastDown, out RaycastHit hit)) return false;

        surface = hit.collider;
        gap = Mathf.Abs(position.y - hit.point.y);
        position.y = hit.point.y;
        transform.position = position;
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.1f);
    }
}
