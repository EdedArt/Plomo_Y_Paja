using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton que lleva el registro de todas las decisiones del jugador.
/// Determina qué final se desbloquea al final del juego.
/// DÓNDE VA: GameObject vacío llamado "GameManagers" en la escena.
///
/// LÓGICA DE FINALES:
/// - Final A "La bala fácil":       cluesFound < 2  AND wolfKilled
/// - Final B "Confianza equivocada": cluesFound >= 2 AND interrogations < 2 AND wolfKilled
/// - Final C "La verdad pesa más":  cluesFound >= 3 AND interrogations >= 2 AND wolfSpared
///
/// IMPORTANTE: Este script usa el patrón Singleton.
/// Singleton = una sola instancia en toda la partida. Se accede desde cualquier script
/// con GameStateTracker.Instance.
/// </summary>
public class GameStateTracker : MonoBehaviour
{
    // ===== Singleton =====
    public static GameStateTracker Instance { get; private set; }

    // ===== Registro de pistas =====
    [Header("Estado actual (para debug en Inspector)")]
    [SerializeField] private List<string> cluesCollected = new List<string>();

    // ===== Registro de interrogaciones =====
    [SerializeField] private List<string> npcsInterrogated = new List<string>();

    // ===== Decisiones clave =====
    [SerializeField] private int enemiesKilled = 0;
    [SerializeField] private bool wolfKilled = false;
    [SerializeField] private bool wolfSpared = false;

    // ===== IA companion - para que recuerde el contexto =====
    [SerializeField] private List<string> aiContextLog = new List<string>(); // Últimas 5 acciones

    // ============ Getters públicos ============
    public int CluesFound => cluesCollected.Count;
    public int InterrogationsCompleted => npcsInterrogated.Count;
    public bool WolfKilled => wolfKilled;
    public bool WolfSpared => wolfSpared;
    public string AIContext => string.Join(". ", aiContextLog);

    private void Awake()
    {
        // Patrón Singleton: si ya existe una instancia, destruir esta
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Sobrevive al cambio de escenas
    }

    // ============ Registro de acciones ============

    /// <summary>
    /// Registrar que el jugador recogió una pista.
    /// Llamar desde CluePickup.cs
    /// </summary>
    public void RegisterClueCollected(string clueId, string clueDescription)
    {
        if (!cluesCollected.Contains(clueId))
        {
            cluesCollected.Add(clueId);
            LogAction($"Encontró pista: {clueDescription}");
            Debug.Log($"[GameState] Pista recolectada: {clueId}. Total: {cluesCollected.Count}");
        }
    }

    /// <summary>
    /// Registrar que el jugador interrogó a un NPC.
    /// Llamar desde DialogueSystem.cs al terminar un interrogatorio.
    /// </summary>
    public void RegisterInterrogation(string npcName, string outcome)
    {
        if (!npcsInterrogated.Contains(npcName))
        {
            npcsInterrogated.Add(npcName);
            LogAction($"Interrogó a {npcName}: {outcome}");
            Debug.Log($"[GameState] Interrogatorio completado: {npcName}");
        }
    }

    /// <summary>
    /// Registrar que el jugador mató a un enemigo.
    /// Llamar desde EnemyHealth.cs
    /// </summary>
    public void RegisterEnemyKilled(string enemyName)
    {
        enemiesKilled++;
        LogAction($"Mató a {enemyName}");
    }

    /// <summary>
    /// Registrar la decisión final sobre Don Lobo.
    /// Llamar desde FinaleTrigger.cs
    /// </summary>
    public void RegisterWolfDecision(bool killed)
    {
        wolfKilled = killed;
        wolfSpared = !killed;
        LogAction(killed ? "Mató a Don Lobo" : "Perdonó a Don Lobo");
        Debug.Log($"[GameState] Decisión sobre Don Lobo: {(killed ? "MUERTO" : "PERDONADO")}");
    }

    /// <summary>
    /// Determinar qué final corresponde según el estado actual.
    /// Llamar desde GameManager cuando se llega a la escena final.
    /// </summary>
    public GameEnding DetermineEnding()
    {
        // Final C: descubrió la verdad y perdonó al lobo
        if (cluesCollected.Count >= 3 && npcsInterrogated.Count >= 2 && wolfSpared)
            return GameEnding.TruthAndForgiveness;

        // Final B: siguió algunas pistas pero no suficientes, o no perdonó
        if (cluesCollected.Count >= 2 && wolfKilled)
            return GameEnding.PartialTruth;

        // Final A: pocas o ninguna pista, mató al lobo sin saber nada
        return GameEnding.BlindJustice;
    }

    /// <summary>
    /// Mantener un log de las últimas 5 acciones para darle contexto a la IA.
    /// </summary>
    private void LogAction(string action)
    {
        aiContextLog.Add(action);
        if (aiContextLog.Count > 5)
            aiContextLog.RemoveAt(0); // Mantener solo las últimas 5
    }

    /// <summary>
    /// Resetear todo el estado (para "Nuevo juego" o "Reintentar").
    /// </summary>
    public void ResetState()
    {
        cluesCollected.Clear();
        npcsInterrogated.Clear();
        aiContextLog.Clear();
        enemiesKilled = 0;
        wolfKilled = false;
        wolfSpared = false;
    }

    /// <summary>
    /// Resumen del estado actual (para mostrar en debug o en la IA).
    /// </summary>
    public string GetStatusSummary()
    {
        return $"Pistas: {cluesCollected.Count}. Interrogados: {npcsInterrogated.Count}. " +
               $"Enemigos eliminados: {enemiesKilled}. Acciones recientes: {AIContext}";
    }
}

/// <summary>
/// Enum de los 3 finales posibles.
/// Definido fuera de la clase para que otros scripts lo usen sin necesitar GameStateTracker.
/// </summary>
public enum GameEnding
{
    BlindJustice,        // Final A: mató al lobo sin saber nada
    PartialTruth,        // Final B: sabía algo pero igual mató al lobo
    TruthAndForgiveness  // Final C: descubrió todo, perdonó al lobo
}
