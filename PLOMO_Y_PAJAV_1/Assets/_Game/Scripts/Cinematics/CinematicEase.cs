using UnityEngine;

/// <summary>Curvas de suavizado para las cinemáticas (libro de la intro, taxi). Entrada y salida en [0, 1].</summary>
public static class CinematicEase
{
    /// <summary>Entra y sale suave (cúbica).</summary>
    public static float InOut(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
    }

    /// <summary>Arranca rápido y frena al final (cúbica).</summary>
    public static float Out(float t)
    {
        t = 1f - Mathf.Clamp01(t);
        return 1f - t * t * t;
    }
}
