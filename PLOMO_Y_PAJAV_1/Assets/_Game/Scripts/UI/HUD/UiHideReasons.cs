using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Razones para ocultar la interfaz del juego, que se COMPONEN (fase HUD.1, H6; la misma regla que INTRO I12).
/// Reusable: el HUD de Wilbur la usa ya; INTRO.3 (cinemáticas y arma enfundada) debe usar esta misma clase.
///
/// - Cada sistema activa o apaga SU razón con Set(razón, on); la interfaz se ve solo si no queda ninguna activa.
/// - Holstered (arma enfundada) es especial: oculta solo la munición; la vida sigue visible (H6).
/// - Changed avisa cuando cambia el conjunto (sin basura: lo escucha el HUD).
/// - Se limpia sola al cargar una escena (cada nivel trae su propio Player).
///
/// QUIÉN LAS ACTIVA HOY:
/// - Death: PlayerHealth (al morir; se apaga en ResetHealth).
/// - Pause, Dialogue, Interrogation: SyncLegacyStates() las copia una vez por frame de los estados que ya existen
///   (GameManager.IsGamePaused / IsGameOver, DialogueUI.IsOpen, InterrogationDirector.IsActive). Esos sistemas no tienen
///   eventos y son archivos de otras fases; leer tres bool estáticos por frame no genera basura. Si algún día avisan con
///   eventos, que llamen Set y se borra su línea de SyncLegacyStates.
/// - Cinematic: IntroCinematicDirector (taxi y llegada, INTRO.2).
/// - Holstered: PlayerWeaponHolster (arma enfundada o sacándose, INTRO.3); se apaga al llegar a Ready.
/// </summary>
public static class UiHideReasons
{
    [Flags]
    public enum Reason
    {
        None = 0,
        Pause = 1 << 0,
        Dialogue = 1 << 1,
        Interrogation = 1 << 2,
        Cinematic = 1 << 3,
        Death = 1 << 4,
        Holstered = 1 << 5,
    }

    private static Reason _active;
    private static int _syncedFrame = -1;

    /// <summary>Cambió el conjunto de razones activas.</summary>
    public static event Action Changed;

    public static Reason Active => _active;

    /// <summary>¿Se oculta toda la interfaz? (cualquier razón salvo Holstered)</summary>
    public static bool HideAll => (_active & ~Reason.Holstered) != Reason.None;

    /// <summary>¿Se oculta la munición? (cualquier razón, también Holstered)</summary>
    public static bool HideAmmo => _active != Reason.None;

    public static bool IsActive(Reason reason) => (_active & reason) != Reason.None;

    public static void Set(Reason reason, bool on)
    {
        Reason next = on ? _active | reason : _active & ~reason;
        if (next == _active) return;
        _active = next;
        Changed?.Invoke();
    }

    /// <summary>Copia pausa / diálogo / interrogatorio de los sistemas que ya existen. Una vez por frame aunque la llamen varios.</summary>
    public static void SyncLegacyStates()
    {
        if (_syncedFrame == Time.frameCount) return;
        _syncedFrame = Time.frameCount;
        GameManager gm = GameManager.Instance;
        Set(Reason.Pause, gm != null && gm.IsGamePaused);
        Set(Reason.Dialogue, DialogueUI.Exists && DialogueUI.Instance.IsOpen);
        Set(Reason.Interrogation, InterrogationDirector.IsActive);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _active = Reason.None;
        _syncedFrame = -1;
        Changed = null;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        _active = Reason.None;
        Changed?.Invoke();
    }
}
