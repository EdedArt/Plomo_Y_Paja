using UnityEngine;

/// <summary>
/// Reloj único del viewmodel (manos, fogonazo, tinte, apuntar y mira): tiempo REAL (sin escala), recortado a 1/30 s.
/// Por qué sin escala: el hit-stop de CartoonFx pone Time.timeScale en 0 en cada golpe a un enemigo; con tiempo escalado las
/// manos se quedaban congeladas en la pose de culatazo/disparo mientras el mundo estaba parado. Con tiempo real siguen
/// recuperándose. Recortar a 1/30 evita pasos enormes tras un tirón de FPS o al salir de la pausa.
/// </summary>
public static class ViewmodelTime
{
    public const float MaxDelta = 1f / 30f;

    public static float Delta => Mathf.Min(Time.unscaledDeltaTime, MaxDelta);
}
