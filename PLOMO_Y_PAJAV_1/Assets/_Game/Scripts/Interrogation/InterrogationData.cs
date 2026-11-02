using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Quién dice la línea.</summary>
public enum InterrogationLineSpeaker { Target, Player }

/// <summary>Cuándo se puede decir una línea (las respuestas eligen la primera que cumpla).</summary>
public enum InterrogationCondition
{
    Always,
    TrustAtLeastZero,   // confianza ≥ 0 → dice la verdad
    TrustBelowZero,     // confianza < 0 → miente
    ClueNotGiven        // la pista del interrogatorio todavía no se entregó
}

/// <summary>Resultado que fija una línea (el último que se dijo es el del interrogatorio).</summary>
public enum InterrogationLineResult { None, Helpful, Misleading }

/// <summary>Una línea del guion: texto exacto, emoción del retrato, condición y efectos.</summary>
[Serializable]
public class InterrogationLine
{
    [Tooltip("Id del guion = nombre del clip de voz (ej: contador_int_1A_v)")]
    public string id;
    public InterrogationLineSpeaker speaker;
    [TextArea(2, 5)] public string text;
    [Tooltip("Par del retrato (talk, scared, nervous, plead, relief, herido). Vacío = sin cambio")]
    public string emotion;
    public InterrogationCondition condition;

    [Header("Efectos")]
    public int trustDelta;
    public InterrogationLineResult result;
    [Tooltip("Decirla marca que mintió")]
    public bool lie;
    [Tooltip("Al terminar de decirla entrega la pista del interrogatorio (si no se dio ya)")]
    public bool givesClue;
    [Tooltip("La corta un disparo ≈ 0.2 s antes de terminar (contador_muerte_01)")]
    public bool cutByShot;
}

/// <summary>Opción de Wilbur: su línea y las respuestas candidatas (se dice la primera cuya condición se cumpla).</summary>
[Serializable]
public class InterrogationOption
{
    public string playerLineId;
    public string[] responseLineIds = new string[0];
}

/// <summary>Una ronda: línea de apertura opcional del interrogado y 2–3 opciones.</summary>
[Serializable]
public class InterrogationRound
{
    public string openingLineId;
    public InterrogationOption[] options = new InterrogationOption[0];
}

/// <summary>Cuándo vale un pensamiento de respaldo.</summary>
public enum InterrogationThoughtCondition
{
    Always,
    SaidLine,   // solo si el interrogado dijo requiresLineId
    Lied,       // solo si mintió
    ToldTruth   // solo si el resultado fue Helpful y no mintió
}

/// <summary>
/// Pensamiento de respaldo (sin IA). Los condicionados que se cumplen salen con su probabilidad (chance);
/// si no sale ninguno, se elige uno de los generales (Always).
/// </summary>
[Serializable]
public class InterrogationThought
{
    [TextArea(2, 4)] public string text;
    public InterrogationThoughtCondition condition;
    [Tooltip("Para SaidLine")]
    public string requiresLineId;
    [Range(0f, 1f)] public float chance = 1f;
}

/// <summary>
/// Contenido completo de un interrogatorio (fase 3C.4). Asset: Assets/_Game/Data/Interrogation_Contador.asset, lo crea
/// el menú Plomo y Paja → Contador → Crear datos del interrogatorio con los textos EXACTOS del guion.
/// Flujo: rondas (apertura → opción → línea de Wilbur → respuesta) → súplica → pensamiento de la IA → decisión → líneas
/// de la decisión. Los clips salen de la librería de audio con el mismo id.
/// </summary>
[CreateAssetMenu(fileName = "Interrogation", menuName = "Plomo y Paja/Interrogation Data")]
public class InterrogationData : ScriptableObject
{
    [Tooltip("Nombre en el papel (ej: CENTAVO)")]
    public string speakerName = "CENTAVO";
    [Tooltip("Nombre para GameStateTracker y la IA (ej: Centavo)")]
    public string npcName = "Centavo";
    public string initialEmotion = "scared";

    [Header("Pista que puede entregar")]
    public string clueId;
    public string clueName;
    public string clueDescription;
    [TextArea(2, 5)] public string clueFullText;

    [Header("Guion")]
    public InterrogationLine[] lines = new InterrogationLine[0];
    public InterrogationRound[] rounds = new InterrogationRound[0];
    public string pleaLineId;

    [Header("Decisión")]
    public string killLabel = "Jalar el gatillo";
    public string spareLabel = "Dejarlo ir";
    public string[] killLineIds = new string[0];
    public string[] spareLineIds = new string[0];

    [Header("Pensamiento de la IA")]
    [Tooltip("Quién es el interrogado, para el pedido a la IA")]
    [TextArea(2, 4)] public string thoughtWho;
    [Min(0.5f)] public float thoughtTimeoutSeconds = 5f;
    public InterrogationThought[] fallbackThoughts = new InterrogationThought[0];

    [Header("Pensamiento después de la decisión (3C.4.1)")]
    [Tooltip("Se pide a la IA al elegir; espera máxima contada desde ese momento (tiempo real)")]
    [Min(0.5f)] public float afterThoughtTimeoutSeconds = 4f;
    public InterrogationThought[] killThoughts = new InterrogationThought[0];
    public InterrogationThought[] spareThoughts = new InterrogationThought[0];

    /// <summary>La línea con ese id, o null.</summary>
    public InterrogationLine GetLine(string id)
    {
        if (string.IsNullOrEmpty(id) || lines == null) return null;
        foreach (InterrogationLine line in lines)
            if (line != null && line.id == id) return line;
        return null;
    }
}

/// <summary>Estado de una partida de interrogatorio: confianza, mentiras, resultado, pista y lo que se dijo.</summary>
public class InterrogationRun
{
    public readonly InterrogationData Data;
    public int Trust { get; private set; }
    public bool Lied { get; private set; }
    public InterrogationLineResult Result { get; private set; }
    public bool ClueGiven { get; private set; }
    public readonly List<string> TargetSaid = new List<string>();
    private readonly HashSet<string> _said = new HashSet<string>();

    public InterrogationRun(InterrogationData data, bool clueAlreadyGiven)
    {
        Data = data;
        ClueGiven = clueAlreadyGiven;
    }

    public bool Allowed(InterrogationLine line)
    {
        if (line == null) return false;
        switch (line.condition)
        {
            case InterrogationCondition.TrustAtLeastZero: return Trust >= 0;
            case InterrogationCondition.TrustBelowZero: return Trust < 0;
            case InterrogationCondition.ClueNotGiven: return !ClueGiven;
            default: return true;
        }
    }

    /// <summary>Aplica los efectos de una línea dicha (salvo la pista: ver MarkClueGiven).</summary>
    public void Apply(InterrogationLine line)
    {
        Trust += line.trustDelta;
        if (line.result != InterrogationLineResult.None) Result = line.result;
        if (line.lie) Lied = true;
        _said.Add(line.id);
        if (line.speaker == InterrogationLineSpeaker.Target) TargetSaid.Add(line.text);
    }

    /// <summary>true si la pista se entrega ahora (no se había dado).</summary>
    public bool MarkClueGiven()
    {
        if (ClueGiven) return false;
        ClueGiven = true;
        return true;
    }

    public bool HasSaid(string lineId) => _said.Contains(lineId);

    /// <summary>La primera respuesta cuya condición se cumple, o null.</summary>
    public InterrogationLine FirstAllowed(string[] ids)
    {
        if (ids == null) return null;
        foreach (string id in ids)
        {
            InterrogationLine line = Data.GetLine(id);
            if (Allowed(line)) return line;
        }
        return null;
    }

    /// <summary>Dijo la verdad: resultado Helpful y ninguna mentira.</summary>
    public bool ToldTruth => Result == InterrogationLineResult.Helpful && !Lied;

    /// <summary>
    /// Pensamiento de respaldo de una lista: los condicionados que se cumplen salen con su probabilidad; si no, uno
    /// general al azar.
    /// </summary>
    public string PickFallbackThought(InterrogationThought[] pool)
    {
        var general = new List<string>();
        if (pool == null) return "";
        foreach (InterrogationThought t in pool)
        {
            if (t == null || string.IsNullOrEmpty(t.text)) continue;
            InterrogationThoughtCondition condition = t.condition;
            if (condition == InterrogationThoughtCondition.Always && !string.IsNullOrEmpty(t.requiresLineId))
                condition = InterrogationThoughtCondition.SaidLine; // datos de 3C.4 (antes del campo condition)
            if (condition == InterrogationThoughtCondition.Always)
            {
                general.Add(t.text);
                continue;
            }
            bool applies = condition == InterrogationThoughtCondition.SaidLine ? HasSaid(t.requiresLineId)
                         : condition == InterrogationThoughtCondition.Lied ? Lied
                         : ToldTruth;
            if (applies && UnityEngine.Random.value < t.chance) return t.text;
        }
        return general.Count > 0 ? general[UnityEngine.Random.Range(0, general.Count)] : "";
    }
}
