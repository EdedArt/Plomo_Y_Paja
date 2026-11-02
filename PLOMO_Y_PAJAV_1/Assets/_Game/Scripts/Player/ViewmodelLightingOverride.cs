using UnityEngine;

/// <summary>
/// OPCIONAL, una por nivel: cambia el tinte de las manos en esa escena (ej: un cuarto muy oscuro o una calle bajo un farol).
/// DÓNDE VA: cualquier GameObject de la escena. Solo hace efecto mientras está activo (OnEnable / OnDisable).
/// </summary>
public class ViewmodelLightingOverride : MonoBehaviour
{
    [SerializeField] private Color tint = new Color(0.85f, 0.80f, 0.75f, 1f);

    public Color Tint => tint;

    /// <summary>El override activo (el último que se activó), o null.</summary>
    public static ViewmodelLightingOverride Current { get; private set; }

    private void OnEnable() { Current = this; }

    private void OnDisable()
    {
        if (Current == this) Current = null;
    }
}
