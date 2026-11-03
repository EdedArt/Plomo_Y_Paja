using System.Collections;
using UnityEngine;

/// <summary>
/// Fundido a/desde negro para las cinemáticas (INTRO.1 libro, INTRO.2 taxi). Tiempo real (no le afecta el hit-stop ni
/// la pausa). DÓNDE VA: una Image negra a pantalla completa con CanvasGroup, en un Canvas Overlay propio (no el HUD).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFade : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;

    private CanvasGroup Group
    {
        get
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            return group;
        }
    }

    /// <summary>0 = transparente, 1 = negro.</summary>
    public float Alpha
    {
        get => Group.alpha;
        set
        {
            Group.alpha = Mathf.Clamp01(value);
            Group.blocksRaycasts = false;
            Group.interactable = false;
        }
    }

    /// <summary>Va al alfa pedido en "duration" segundos reales (suave). Con duration ≤ 0 salta directo.</summary>
    public IEnumerator FadeTo(float target, float duration)
    {
        float from = Alpha;
        if (duration <= 0f)
        {
            Alpha = target;
            yield break;
        }
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            Alpha = Mathf.Lerp(from, target, CinematicEase.InOut(t / duration));
            yield return null;
        }
        Alpha = target;
    }
}
