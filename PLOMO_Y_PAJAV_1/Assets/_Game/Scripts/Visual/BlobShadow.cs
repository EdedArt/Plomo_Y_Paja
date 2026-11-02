using UnityEngine;

/// <summary>
/// Sombra redonda y suave bajo un personaje (la única "sombra" de los personajes sin iluminación).
/// DÓNDE VA: raíz del personaje (BillboardSprite la añade sola si falta).
/// Busca el piso con un rayo hacia abajo cada frame; se achica y desvanece cuando el personaje
/// sube (vuelo de muerte) y se oculta si el visual desaparece.
/// </summary>
public class BlobShadow : MonoBehaviour
{
    [SerializeField] private float radius = 0.45f;
    [SerializeField] private float maxHeight = 3f;
    [Tooltip("Si este objeto se apaga (ej: 'Visual' al terminar la muerte), la sombra también")]
    [SerializeField] private Transform follow;

    private static readonly int FadeId = Shader.PropertyToID("_Fade");
    private Transform _quad;
    private MeshRenderer _renderer;
    private MaterialPropertyBlock _mpb;

    private void Awake()
    {
        if (follow == null) follow = transform.Find("Visual");
        if (follow == null) follow = transform;

        Material mat = CharacterLook.BlobMaterial;
        if (mat == null)
        {
            enabled = false;
            return;
        }

        var go = new GameObject("BlobShadow_Quad");
        _quad = go.transform;
        _quad.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        _renderer = go.AddComponent<MeshRenderer>();
        _renderer.sharedMaterial = mat;
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _mpb = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        if (_quad == null) return;

        // Desde la altura del visual (en la muerte el root vuela; el visual va con él)
        Vector3 from = follow.position + Vector3.up * 0.5f;
        bool visible = follow.gameObject.activeInHierarchy;
        if (visible && Physics.Raycast(from, Vector3.down, out RaycastHit hit, maxHeight + 0.5f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            float height = Mathf.Max(0f, hit.distance - 0.5f);
            float fade = 1f - Mathf.Clamp01(height / maxHeight);
            float size = radius * 2f * Mathf.Lerp(0.5f, 1f, fade);

            _quad.position = hit.point + hit.normal * 0.02f;
            _quad.rotation = Quaternion.LookRotation(-hit.normal);
            _quad.localScale = Vector3.Scale(new Vector3(size, size * 0.8f, 1f), InverseScale());
            _mpb.SetFloat(FadeId, fade);
            _renderer.SetPropertyBlock(_mpb);
            _renderer.enabled = true;
        }
        else
        {
            _renderer.enabled = false;
        }
    }

    private Vector3 InverseScale()
    {
        Vector3 s = transform.lossyScale;
        return new Vector3(s.x != 0f ? 1f / s.x : 1f, s.y != 0f ? 1f / s.y : 1f, s.z != 0f ? 1f / s.z : 1f);
    }
}
