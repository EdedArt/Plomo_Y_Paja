using UnityEngine;

/// <summary>
/// Cuarto de herramientas del patio trasero de Porky, donde se esconde Centavo (fase 3C).
/// DÓNDE VA: raíz del prefab Prefabs/Level1/ToolShed_Contador. Lo crea y lo coloca el menú
/// Plomo y Paja → Contador → Construir patio y cuarto (no armarlo a mano).
///
/// No tiene lógica de juego: solo guarda las anclas que usan Centavo (3C.2) y el interrogatorio (3C.3)
/// y dibuja sus gizmos (zona de activación, camino de huida, encuadre de la cámara).
/// </summary>
public class ContadorShed : MonoBehaviour
{
    [Header("Anclas (hijas del prefab)")]
    [Tooltip("Detrás del banco, mirando a la puerta: donde está Centavo antes de que lo hieran")]
    [SerializeField] private Transform standAnchor;

    [Tooltip("En el piso, delante del banco y apoyado en él: donde queda herido")]
    [SerializeField] private Transform seatedAnchor;

    [Tooltip("Donde se pone la cámara al agacharse en el interrogatorio (mira a Centavo sentado)")]
    [SerializeField] private Transform interrogationCamAnchor;

    [Tooltip("Puntos en orden desde el cuarto, por el portón del patio, hasta un lugar fuera de la vista")]
    [SerializeField] private Transform[] exitPath = new Transform[0];

    [Header("Activación")]
    [Tooltip("Esfera (trigger, capa Ignore Raycast) centrada en la puerta del cuarto")]
    [SerializeField] private SphereCollider activationTrigger;

    [Header("Escenografía")]
    [Tooltip("Collider sólido del banco de trabajo: la cobertura de Centavo")]
    [SerializeField] private Collider benchCover;

    [Tooltip("Luz de la lámpara de aceite del banco")]
    [SerializeField] private Light oilLamp;

    public Transform StandAnchor => standAnchor;
    public Transform SeatedAnchor => seatedAnchor;
    public Transform InterrogationCamAnchor => interrogationCamAnchor;
    public Transform[] ExitPath => exitPath;
    public Collider BenchCover => benchCover;
    public Light OilLamp => oilLamp;

    /// <summary>Centro de la zona de activación en mundo (la puerta del cuarto).</summary>
    public Vector3 ActivationCenter =>
        activationTrigger != null ? activationTrigger.transform.TransformPoint(activationTrigger.center) : transform.position;

    /// <summary>Radio de la zona de activación en metros de mundo.</summary>
    public float ActivationRadius
    {
        get
        {
            if (activationTrigger == null) return 0f;
            Vector3 s = activationTrigger.transform.lossyScale;
            return activationTrigger.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
        }
    }

    /// <summary>¿El punto está dentro de la zona de activación?</summary>
    public bool IsInsideActivation(Vector3 worldPoint)
    {
        float r = ActivationRadius;
        return (worldPoint - ActivationCenter).sqrMagnitude <= r * r;
    }

    private void OnDrawGizmos()
    {
        // Zona de activación
        if (activationTrigger != null)
        {
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.6f);
            Gizmos.DrawWireSphere(ActivationCenter, ActivationRadius);
        }

        // Camino de huida
        if (exitPath != null)
        {
            Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.9f);
            Vector3 previous = seatedAnchor != null ? seatedAnchor.position : transform.position;
            foreach (Transform point in exitPath)
            {
                if (point == null) continue;
                Gizmos.DrawLine(previous, point.position);
                Gizmos.DrawWireSphere(point.position, 0.2f);
                previous = point.position;
            }
        }

        // Encuadre de la cámara del interrogatorio
        if (interrogationCamAnchor != null)
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Matrix4x4 saved = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(interrogationCamAnchor.position, interrogationCamAnchor.rotation, Vector3.one);
            Gizmos.DrawFrustum(Vector3.zero, 60f, 1.6f, 0.05f, 16f / 9f);
            Gizmos.matrix = saved;
            if (seatedAnchor != null)
                Gizmos.DrawLine(interrogationCamAnchor.position, seatedAnchor.position + Vector3.up * 0.55f);
        }
    }
}
