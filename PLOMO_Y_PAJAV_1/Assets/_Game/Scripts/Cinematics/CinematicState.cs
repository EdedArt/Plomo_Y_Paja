using UnityEngine;

/// <summary>
/// ¿Hay una cinemática en curso dentro de un nivel? (INTRO.2: taxi y llegada). Reutilizable.
/// QUIÉN LO LEE (el mismo camino que el interrogatorio):
/// - PlayerAim.GameplayBlocked() → manos, mira, disparo, recarga y cambio de arma bloqueados.
/// - GameManager: no pausa con Esc (en la cinemática Esc mantenido es "saltar").
/// - EnemyAI: nadie ve, oye, camina ni ataca (I7). PlayerHealth: no recibe daño.
/// Se apaga solo si el dueño se destruye (al cambiar de escena): es un UnityEngine.Object.
/// OJO: no se limpia en sceneLoaded a propósito, porque ese evento llega DESPUÉS de los Awake de la escena nueva.
/// </summary>
public static class CinematicState
{
    private static Object _owner;

    public static bool IsPlaying => _owner != null;

    /// <summary>Empieza (o termina, con playing = false) la cinemática de "owner".</summary>
    public static void Set(Object owner, bool playing)
    {
        if (playing) _owner = owner;
        else if (_owner == owner) _owner = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _owner = null;
}
