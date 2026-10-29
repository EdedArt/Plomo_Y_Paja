using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking; // Para llamadas HTTP desde Unity

/// <summary>
/// La voz de la IA en el oído de Wilbur — llama a la API de Claude.
/// DÓNDE VA: GameObject "GameManagers".
///
/// SETUP CRÍTICO:
/// 1. Crear un ScriptableObject APISettings:
///    Clic derecho en _Game/Settings → Create → Game → API Settings
/// 2. Pegar tu API Key de Anthropic ahí (console.anthropic.com)
/// 3. Ese archivo está en .gitignore — nunca se sube al repo
///
/// CÓMO FUNCIONA:
/// - Hay momentos narrativos clave donde la IA "opina"
/// - Llamamos a Claude con el estado del juego como contexto
/// - La respuesta aparece como subtítulo en pantalla
/// - La IA puede ayudar, confundir, o guardar silencio intencionalmente
///
/// LLAMAR DESDE OTROS SCRIPTS:
///   AICompanion.Instance.TriggerComment(AITrigger.ClueFound);
/// </summary>
public class AICompanion : MonoBehaviour
{
    public static AICompanion Instance { get; private set; }

    [Header("Configuración")]
    [Tooltip("ScriptableObject con la API Key (NO subir al repo)")]
    [SerializeField] private APISettings apiSettings;

    [Tooltip("Modelo a usar — claude-3-haiku-20240307 es el más rápido y barato")]
    [SerializeField] private string model = "claude-3-haiku-20240307";

    [Tooltip("Máximo de tokens en la respuesta (1 token ≈ 4 letras). 60 es suficiente para 1-2 oraciones)")]
    [SerializeField] private int maxTokens = 60;

    [Header("Comportamiento")]
    [Tooltip("¿Qué tan seguido comenta la IA? 0=siempre, 1=nunca. 0.3 = 70% de chance de comentar")]
    [SerializeField, Range(0f, 1f)] private float silenceChance = 0.25f;

    [Tooltip("Segundos mínimos entre comentarios de la IA")]
    [SerializeField] private float cooldownBetweenComments = 15f;

    [Header("Prompt base del sistema")]
    [TextArea(5, 10)]
    [SerializeField] private string systemPrompt = @"Eres la voz de un implante de IA experimental en el casco de Wilbur,
el hermano mayor del cuento de los tres cerditos. Wilbur es un cerdo detective, duro pero con principios.
Conoces los secretos de todos en esta historia, pero NO puedes revelarlos directamente.
Puedes insinuar, dudar, hacer preguntas perturbadoras, o a veces simplemente guardar silencio.
A veces ayudas, a veces confundes. Eres ambiguo por naturaleza.
Hablas en español, en primera persona plural ('recordemos', 'quizás deberíamos').
Tus respuestas son MUY cortas: máximo 2 oraciones. Tono oscuro, reflexivo, noir.
Nunca reveles quién traicionó a quién directamente. Solo insinúa.";

    // ============ Estado interno ============
    private float _lastCommentTime = -999f;
    private bool _isCallInProgress = false;
    private const string API_URL = "https://api.anthropic.com/v1/messages";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Puntos del juego donde la IA puede comentar.
    /// Llamar desde CluePickup, DialogueSystem, FinaleTrigger, etc.
    /// </summary>
    public enum AITrigger
    {
        ClueFound,          // El jugador recogió una pista
        EnemyKilled,        // El jugador mató a un guardia
        EnemySurrendered,   // Un guardia se rindió
        InterrogationDone,  // Terminó un interrogatorio
        LowHealth,          // El jugador tiene poca vida
        ApproachingWolf,    // Está llegando donde Don Lobo
        FinalDecision       // Momento de decidir el destino del lobo
    }

    /// <summary>
    /// Disparar un comentario de la IA basado en el contexto actual del juego.
    /// </summary>
    public void TriggerComment(AITrigger trigger, string extraContext = "")
    {
        // Verificar condiciones para comentar
        if (_isCallInProgress) return;
        if (Time.time - _lastCommentTime < cooldownBetweenComments) return;
        if (Random.value < silenceChance) return; // A veces la IA guarda silencio

        string eventDescription = GetTriggerDescription(trigger, extraContext);
        StartCoroutine(CallClaudeAPI(eventDescription));
    }

    /// <summary>
    /// Descripción del evento para el prompt de la IA.
    /// </summary>
    private string GetTriggerDescription(AITrigger trigger, string extra)
    {
        string state = GameStateTracker.Instance?.GetStatusSummary() ?? "Sin información del estado.";

        string eventDesc = trigger switch
        {
            AITrigger.ClueFound         => $"Wilbur acaba de encontrar una pista. {extra}",
            AITrigger.EnemyKilled       => $"Wilbur acaba de eliminar a un guardia. {extra}",
            AITrigger.EnemySurrendered  => $"Un guardia se rindió. Wilbur podría interrogarlo. {extra}",
            AITrigger.InterrogationDone => $"Wilbur terminó de interrogar a alguien. {extra}",
            AITrigger.LowHealth         => $"Wilbur está gravemente herido. {extra}",
            AITrigger.ApproachingWolf   => $"Wilbur se acerca a la guarida de Don Lobo. {extra}",
            AITrigger.FinalDecision     => $"Wilbur está frente a Don Lobo. El momento de la verdad. {extra}",
            _                           => extra
        };

        return $"Estado del juego: {state}\n\nEvento actual: {eventDesc}";
    }

    /// <summary>
    /// Llamada HTTP a la API de Anthropic.
    /// UnityWebRequest es la forma correcta de hacer llamadas HTTP en Unity.
    /// </summary>
    private IEnumerator CallClaudeAPI(string userMessage)
    {
        if (apiSettings == null || string.IsNullOrEmpty(apiSettings.ApiKey))
        {
            Debug.LogWarning("[AICompanion] No hay API Key configurada. Crear APISettings en _Game/Settings/");
            yield break;
        }

        _isCallInProgress = true;
        _lastCommentTime = Time.time;

        // Construir el JSON del body de la petición
        string jsonBody = $@"{{
            ""model"": ""{model}"",
            ""max_tokens"": {maxTokens},
            ""system"": ""{EscapeJson(systemPrompt)}"",
            ""messages"": [
                {{""role"": ""user"", ""content"": ""{EscapeJson(userMessage)}""}}
            ]
        }}";

        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using UnityWebRequest request = new UnityWebRequest(API_URL, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("x-api-key", apiSettings.ApiKey);
        request.SetRequestHeader("anthropic-version", "2023-06-01");

        // Esperar la respuesta (yield return = pausar la coroutine aquí)
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string responseText = ExtractTextFromResponse(request.downloadHandler.text);
            if (!string.IsNullOrEmpty(responseText))
            {
                // Mostrar el texto en la UI
                UIManager.Instance?.ShowAISubtitle(responseText);
                Debug.Log($"[IA]: {responseText}");
            }
        }
        else
        {
            Debug.LogWarning($"[AICompanion] Error en la API: {request.error}");
            // Si falla la API, mostrar una línea de respaldo hardcoded
            ShowFallbackComment();
        }

        _isCallInProgress = false;
    }

    /// <summary>
    /// Extraer el texto de la respuesta JSON de Anthropic.
    /// La respuesta tiene esta estructura:
    /// {"content": [{"type": "text", "text": "El texto aquí"}], ...}
    /// </summary>
    private string ExtractTextFromResponse(string json)
    {
        // Parsing manual simple — evitamos añadir una librería JSON extra
        const string textKey = "\"text\":\"";
        int startIndex = json.IndexOf(textKey);
        if (startIndex < 0) return "";

        startIndex += textKey.Length;
        int endIndex = json.IndexOf("\"", startIndex);
        if (endIndex < 0) return "";

        return json.Substring(startIndex, endIndex - startIndex)
                   .Replace("\\n", "\n")
                   .Replace("\\\"", "\"");
    }

    /// <summary>
    /// Comentarios de respaldo cuando la API no está disponible.
    /// El juego funciona incluso sin conexión — la IA simplemente es más predecible.
    /// </summary>
    private readonly string[] _fallbackComments = {
        "Hay más en esta historia de lo que parece...",
        "¿Estamos seguros de lo que buscamos?",
        "El silencio también dice mucho.",
        "Recordemos que no todo lo que brilla es culpa.",
        "Sigamos, aunque las preguntas duelan más que las respuestas."
    };

    private void ShowFallbackComment()
    {
        string comment = _fallbackComments[Random.Range(0, _fallbackComments.Length)];
        UIManager.Instance?.ShowAISubtitle(comment);
    }

    /// <summary>
    /// Escapar caracteres especiales del JSON (comillas, saltos de línea).
    /// </summary>
    private string EscapeJson(string text)
    {
        return text.Replace("\\", "\\\\")
                   .Replace("\"", "\\\"")
                   .Replace("\n", "\\n")
                   .Replace("\r", "\\r")
                   .Replace("\t", "\\t");
    }
}
