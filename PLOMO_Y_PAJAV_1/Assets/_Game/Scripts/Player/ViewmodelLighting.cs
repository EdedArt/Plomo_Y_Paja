using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Integra las manos con la escena sin un sistema de luces: las tiñe con un color base (gris cálido) para que no
/// brillen a todo volumen en un cuarto oscuro, y al disparar las pone en blanco puro un instante (el fogonazo las ilumina).
/// DÓNDE VA: lo crea FirstPersonViewmodel en "ViewmodelCanvas" y tiñe HandsImage.color (el fogonazo no se tiñe).
///
/// Un nivel puede cambiar el tinte con un ViewmodelLightingOverride en la escena (ver abajo).
/// </summary>
public class ViewmodelLighting : MonoBehaviour
{
    [Tooltip("Tinte de las manos (se multiplica con el dibujo). Blanco = sin teñir")]
    [SerializeField] private Color baseTint = new Color(0.95f, 0.92f, 0.88f, 1f);
    [Tooltip("Segundos que quedan en blanco puro al disparar")]
    [SerializeField] private float flashHold = 0.06f;
    [Tooltip("Segundos que tarda en volver al tinte base después del blanco")]
    [SerializeField] private float flashEase = 0.25f;

    private Image _hands;
    private float _flashTimer = -1f;

    public void Init(Image hands)
    {
        _hands = hands;
        Apply(CurrentBase());
    }

    /// <summary>Tinte base actual: el del nivel si hay un override activo, si no el propio.</summary>
    public Color CurrentBase()
    {
        ViewmodelLightingOverride o = ViewmodelLightingOverride.Current;
        return o != null ? o.Tint : baseTint;
    }

    /// <summary>Disparo: blanco puro y de vuelta al tinte base.</summary>
    public void Flash()
    {
        _flashTimer = 0f;
    }

    private void Update()
    {
        if (_hands == null) return;
        Color tint = CurrentBase();
        if (_flashTimer >= 0f)
        {
            _flashTimer += Mathf.Min(Time.deltaTime, 1f / 30f); // misma fuente de tiempo que ViewmodelMotion
            if (_flashTimer < flashHold) tint = Color.white;
            else
            {
                float t = Mathf.Clamp01((_flashTimer - flashHold) / Mathf.Max(0.01f, flashEase));
                tint = Color.Lerp(Color.white, tint, t * t * (3f - 2f * t));
                if (t >= 1f) _flashTimer = -1f;
            }
        }
        Apply(tint);
    }

    private void Apply(Color tint)
    {
        tint.a = 1f;
        if (_hands != null && _hands.color != tint) _hands.color = tint;
    }
}
