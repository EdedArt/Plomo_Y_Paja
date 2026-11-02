using UnityEngine;

/// <summary>
/// Personaje que se puede interrogar con InterrogationDirector (fase 3C.3). Primero: CentavoController.
/// Los guardias podrían implementarlo más adelante sin tocar el director.
/// </summary>
public interface IInterrogationTarget
{
    /// <summary>Nombre en el papel del diálogo (ej: "CENTAVO").</summary>
    string SpeakerName { get; }

    /// <summary>Dónde se pone la cámara al "agacharse" (posición y rotación). null = la cámara no se mueve.</summary>
    Transform CameraAnchor { get; }

    /// <summary>Cuadros del retrato: cada emoción es un par (cuadro 0 = boca cerrada, cuadro 1 = abierta).</summary>
    CharacterSpriteSet PortraitSet { get; }

    /// <summary>Oculta/muestra su dibujo del mundo (D6: durante la escena se ve el retrato).</summary>
    void SetWorldVisible(bool visible);
}

/// <summary>Cómo terminó una escena de interrogatorio.</summary>
public enum InterrogationOutcome
{
    Killed,     // "Jalar el gatillo"
    Pardoned,   // "Dejarlo ir"
    Cancelled   // se cerró sin decisión (error o salida forzada)
}
