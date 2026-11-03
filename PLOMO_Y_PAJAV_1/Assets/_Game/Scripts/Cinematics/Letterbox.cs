using UnityEngine;

/// <summary>
/// Franjas negras de cine (I10): llevan la imagen a 2.35:1 en 0.4 s. Reutilizable. Tiempo real.
/// DÓNDE VA: un Canvas Overlay propio con dos Image negras ancladas arriba y abajo (top / bottom), a lo ancho.
/// El alto de cada franja se calcula con la proporción real de la pantalla.
/// </summary>
public class Letterbox : MonoBehaviour
{
    [SerializeField] private RectTransform top;
    [SerializeField] private RectTransform bottom;
    [SerializeField] private float targetAspect = 2.35f;
    [SerializeField] private float animateSeconds = 0.4f;

    private float _amount;   // 0 = sin franjas, 1 = 2.35:1
    private float _target;

    public bool IsShown => _target > 0.5f;

    public void Show(bool show, bool instant = false)
    {
        _target = show ? 1f : 0f;
        if (instant) _amount = _target;
        Apply();
    }

    private void Update()
    {
        if (Mathf.Approximately(_amount, _target)) return;
        _amount = Mathf.MoveTowards(_amount, _target, Time.unscaledDeltaTime / Mathf.Max(0.01f, animateSeconds));
        Apply();
    }

    private void Apply()
    {
        // Fracción del alto que cubre cada franja: (1 - pantalla/objetivo) / 2
        float screenAspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
        float bar = Mathf.Max(0f, (1f - screenAspect / Mathf.Max(0.1f, targetAspect)) * 0.5f);
        float h = bar * CinematicEase.InOut(_amount);
        if (top != null)
        {
            top.anchorMin = new Vector2(0f, 1f - h);
            top.anchorMax = Vector2.one;
        }
        if (bottom != null)
        {
            bottom.anchorMin = Vector2.zero;
            bottom.anchorMax = new Vector2(1f, h);
        }
    }
}
