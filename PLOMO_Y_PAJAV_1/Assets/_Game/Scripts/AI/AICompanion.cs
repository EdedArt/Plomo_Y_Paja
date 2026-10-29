using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// La voz en el oído de Wilbur. Llama a la API de OpenAI (Chat Completions)
/// y muestra la respuesta como subtítulo con efecto máquina de escribir.
/// Si no hay API key, conexión o la respuesta falla, usa líneas de respaldo.
///
/// Uso desde cualquier script:
///     AICompanion.Instance?.Speak(AITrigger.ClueFound, "Nota de Porky");
/// </summary>
public enum AITrigger
{
    ClueFound,           // al recoger una pista
    InterrogationEnded,  // al terminar un interrogatorio
    EnemyKilled,         // al matar a un enemigo
    FinalConfrontation,  // al llegar con Don Lobo
    LowHealth            // cuando Wilbur tiene poca vida
}

public class AICompanion : MonoBehaviour
{
    public static AICompanion Instance { get; private set; }

    /// <summary>
    /// Resumen del estado del juego que se envía como contexto.
    /// El GameStateTracker (Día 3) lo asigna así:
    ///     AICompanion.ContextProvider = () => "Pistas: 2/5, ...";
    /// </summary>
    public static Func<string> ContextProvider;

    /// <summary>Se dispara cada vez que la IA dice algo (por si otro sistema quiere escucharlo).</summary>
    public event Action<string> OnLineSpoken;

    [Header("OpenAI")]
    [Tooltip("gpt-5.4-mini recomendado. Si falla, prueben gpt-4.1-mini y desmarquen 'Is Reasoning Model'.")]
    [SerializeField] private string model = "gpt-5.4-mini";
    [Tooltip("Marcado para modelos GPT-5.x / o-series. Desmarcado para gpt-4.1-mini / gpt-4o-mini.")]
    [SerializeField] private bool isReasoningModel = true;
    [Tooltip("Solo para modelos de razonamiento: low = rápido y barato.")]
    [SerializeField] private string reasoningEffort = "low";
    [Tooltip("Los modelos de razonamiento gastan tokens pensando; dejar holgura.")]
    [SerializeField] private int maxCompletionTokens = 400;
    [SerializeField] private float requestTimeoutSeconds = 10f;

    [Header("Comportamiento")]
    [Tooltip("Segundos mínimos entre líneas de baja prioridad (muertes, poca vida).")]
    [SerializeField] private float lowPriorityCooldown = 12f;
    [Range(0f, 1f)]
    [Tooltip("Probabilidad de comentar al matar a un enemigo (para no hablar en cada muerte).")]
    [SerializeField] private float enemyKilledChance = 0.5f;
    [SerializeField] private int historySize = 5;

    [Header("Subtítulos (UI)")]
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private CanvasGroup subtitleGroup;
    [SerializeField] private float charsPerSecond = 35f;
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool logRequests = true;

    private const string Endpoint = "https://api.openai.com/v1/chat/completions";
    private const string KeyFileName = "openai_key.txt";

    private string apiKey;
    private bool requestInFlight;
    private float lastLowPriorityTime = -999f;
    private readonly Queue<string> recentLines = new Queue<string>();
    private Coroutine subtitleRoutine;

    // ------------------------------------------------------------------
    // Prompt del sistema: quién es la IA y qué sabe
    // ------------------------------------------------------------------
    private const string SystemPrompt =
        "Eres una voz misteriosa dentro de la cabeza de Wilbur, un cerdo detective aficionado armado con una escopeta, " +
        "en Porcópolis, una ciudad de caricatura noir de los años 30 que siempre está lloviendo. " +
        "Wilbur cree que el mafioso Don Lobo secuestró a su hermano menor, Porky. " +
        "La verdad, que tú conoces pero NUNCA revelas directamente: Porky le robó dinero a Don Lobo y lo humilló en público; " +
        "Don Lobo lo retiene como garantía de la deuda, no como víctima. Max, el hermano del medio, sabe algo y calla. " +
        "Reglas estrictas: responde SOLO con 1 o 2 oraciones cortas (máximo 30 palabras en total), en español, " +
        "en segunda persona hablándole a Wilbur, con tono de narrador noir de los años 30 (lluvia, humo, plomo, sombras). " +
        "Sé ambiguo: a veces siembras dudas sobre la inocencia de Porky, a veces sobre las intenciones de Wilbur, a veces sobre ti misma. " +
        "Nunca confirmes la verdad completa. Sin comillas, sin emojis, sin acotaciones ni narración de acciones.";

    // ------------------------------------------------------------------
    // Líneas de respaldo (sin conexión / sin key / error)
    // ------------------------------------------------------------------
    private static readonly Dictionary<AITrigger, string[]> FallbackLines = new Dictionary<AITrigger, string[]>
    {
        { AITrigger.ClueFound, new[] {
            "El papel no miente, Wilbur. Los que lo escriben, sí.",
            "Guárdalo bien. Algunas pistas pesan más que una escopeta cargada.",
            "Curioso que tu hermanito tuviera esto escondido, ¿no crees?",
            "La tinta se corre con la lluvia, pero la verdad se queda pegada."
        }},
        { AITrigger.InterrogationEnded, new[] {
            "Un lobo asustado dice lo que quieres oír. Uno tranquilo, lo que no.",
            "¿Le crees? Yo tampoco estaría tan segura.",
            "Cada boca en esta ciudad tiene un precio. La pregunta es quién pagó primero.",
            "Escuchaste sus palabras. Ahora escucha lo que no dijo."
        }},
        { AITrigger.EnemyKilled, new[] {
            "Uno menos. ¿Sabía algo que ya nunca vas a saber?",
            "El plomo resuelve problemas, Wilbur. Rara vez los correctos.",
            "Otro que no volverá a casa esta noche.",
            "Fácil apretar el gatillo. Difícil apretar la conciencia."
        }},
        { AITrigger.FinalConfrontation, new[] {
            "Aquí termina el camino. Lo que hagas ahora, Porky lo recordará siempre.",
            "El Lobo te espera. La pregunta es si tú sabes a quién vienes a salvar.",
            "Una bala o una pregunta, Wilbur. Elige bien, que solo tienes una oportunidad."
        }},
        { AITrigger.LowHealth, new[] {
            "Estás sangrando, Wilbur. Muerto no le sirves a nadie, ni siquiera a Porky.",
            "Respira. Los héroes muertos no rescatan a nadie.",
            "La ciudad te está cobrando la cuenta. Busca refugio."
        }},
    };

    // ==================================================================
    // Ciclo de vida
    // ==================================================================
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        apiKey = LoadApiKey();
        if (string.IsNullOrEmpty(apiKey))
            Debug.LogWarning("[AICompanion] No se encontró API key. Se usarán solo líneas de respaldo.");

        if (subtitleGroup != null) subtitleGroup.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ==================================================================
    // API pública
    // ==================================================================
    /// <param name="trigger">Qué pasó.</param>
    /// <param name="detail">Detalle opcional: nombre de la pista, resultado del interrogatorio, etc.</param>
    public void Speak(AITrigger trigger, string detail = "")
    {
        bool highPriority = trigger == AITrigger.ClueFound
                         || trigger == AITrigger.InterrogationEnded
                         || trigger == AITrigger.FinalConfrontation;

        if (!highPriority)
        {
            if (Time.time - lastLowPriorityTime < lowPriorityCooldown) return;
            if (trigger == AITrigger.EnemyKilled && UnityEngine.Random.value > enemyKilledChance) return;
            if (requestInFlight) return; // no interrumpir algo importante
            lastLowPriorityTime = Time.time;
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            ShowLine(PickFallback(trigger));
            return;
        }

        StartCoroutine(RequestLine(trigger, detail));
    }

    // ==================================================================
    // Llamada a OpenAI
    // ==================================================================
    private IEnumerator RequestLine(AITrigger trigger, string detail)
    {
        requestInFlight = true;

        string body = BuildRequestJson(BuildUserPrompt(trigger, detail));

        using (var req = new UnityWebRequest(Endpoint, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            req.timeout = Mathf.CeilToInt(requestTimeoutSeconds);

            float t0 = Time.realtimeSinceStartup;
            yield return req.SendWebRequest();
            float elapsed = Time.realtimeSinceStartup - t0;

            string line = null;

            if (req.result == UnityWebRequest.Result.Success)
            {
                line = ParseContent(req.downloadHandler.text);
                if (logRequests)
                    Debug.Log($"[AICompanion] {trigger} ({elapsed:0.0}s): {line}");
            }
            else
            {
                Debug.LogWarning($"[AICompanion] Error {req.responseCode}: {req.error}\n{req.downloadHandler?.text}");
            }

            if (string.IsNullOrWhiteSpace(line))
                line = PickFallback(trigger);

            ShowLine(line);
        }

        requestInFlight = false;
    }

    private string BuildUserPrompt(AITrigger trigger, string detail)
    {
        var sb = new StringBuilder();

        switch (trigger)
        {
            case AITrigger.ClueFound:
                sb.Append("Wilbur acaba de recoger una pista");
                break;
            case AITrigger.InterrogationEnded:
                sb.Append("Wilbur acaba de terminar de interrogar a un guardia lobo");
                break;
            case AITrigger.EnemyKilled:
                sb.Append("Wilbur acaba de matar a un guardia lobo. Hazle dudar si valió la pena");
                break;
            case AITrigger.FinalConfrontation:
                sb.Append("Wilbur está a punto de enfrentarse a Don Lobo en su guarida. Es el momento decisivo");
                break;
            case AITrigger.LowHealth:
                sb.Append("Wilbur está gravemente herido y apenas se sostiene");
                break;
        }

        if (!string.IsNullOrEmpty(detail))
            sb.Append(": ").Append(detail);
        sb.Append(".\n");

        string context = ContextProvider?.Invoke();
        if (!string.IsNullOrEmpty(context))
            sb.Append("Estado actual de la investigación: ").Append(context).Append('\n');

        if (recentLines.Count > 0)
        {
            sb.Append("No repitas ideas ni frases de lo que ya dijiste antes:\n");
            foreach (var l in recentLines) sb.Append("- ").Append(l).Append('\n');
        }

        sb.Append("Responde ahora con tu línea.");
        return sb.ToString();
    }

    // Se arma el JSON a mano porque JsonUtility no permite omitir campos
    // (los modelos de razonamiento rechazan "temperature").
    private string BuildRequestJson(string userPrompt)
    {
        var sb = new StringBuilder();
        sb.Append("{\"model\":\"").Append(Escape(model)).Append("\",");
        sb.Append("\"messages\":[");
        sb.Append("{\"role\":\"system\",\"content\":\"").Append(Escape(SystemPrompt)).Append("\"},");
        sb.Append("{\"role\":\"user\",\"content\":\"").Append(Escape(userPrompt)).Append("\"}");
        sb.Append("],");
        sb.Append("\"max_completion_tokens\":").Append(maxCompletionTokens);

        if (isReasoningModel)
            sb.Append(",\"reasoning_effort\":\"").Append(Escape(reasoningEffort)).Append('"');
        else
            sb.Append(",\"temperature\":0.9");

        sb.Append('}');
        return sb.ToString();
    }

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length + 16);
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.AppendFormat("\\u{0:x4}", (int)c);
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    [Serializable] private class ChatResponse { public Choice[] choices; }
    [Serializable] private class Choice { public ChatMessage message; public string finish_reason; }
    [Serializable] private class ChatMessage { public string content; }

    private string ParseContent(string json)
    {
        try
        {
            var resp = JsonUtility.FromJson<ChatResponse>(json);
            if (resp?.choices == null || resp.choices.Length == 0) return null;

            string content = resp.choices[0].message?.content;
            if (string.IsNullOrWhiteSpace(content))
            {
                // Pasa si el modelo gastó todos los tokens razonando: subir maxCompletionTokens.
                Debug.LogWarning($"[AICompanion] Respuesta vacía (finish_reason: {resp.choices[0].finish_reason}).");
                return null;
            }
            return content.Trim().Trim('"', '“', '”');
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AICompanion] No se pudo parsear la respuesta: " + e.Message);
            return null;
        }
    }

    // ==================================================================
    // API key: variable de entorno o StreamingAssets/openai_key.txt
    // ==================================================================
    private static string LoadApiKey()
    {
        string env = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(env)) return env.Trim();

        string path = Path.Combine(Application.streamingAssetsPath, KeyFileName);
        if (File.Exists(path))
        {
            string key = File.ReadAllText(path).Trim();
            if (!string.IsNullOrEmpty(key)) return key;
        }
        return null;
    }

    // ==================================================================
    // Subtítulos
    // ==================================================================
    private string PickFallback(AITrigger trigger)
    {
        var options = FallbackLines[trigger];
        // Evitar repetir la última línea de respaldo
        for (int i = 0; i < 4; i++)
        {
            string candidate = options[UnityEngine.Random.Range(0, options.Length)];
            if (!recentLines.Contains(candidate)) return candidate;
        }
        return options[UnityEngine.Random.Range(0, options.Length)];
    }

    private void ShowLine(string line)
    {
        recentLines.Enqueue(line);
        while (recentLines.Count > historySize) recentLines.Dequeue();

        OnLineSpoken?.Invoke(line);

        if (subtitleText == null) return;
        if (subtitleRoutine != null) StopCoroutine(subtitleRoutine);
        subtitleRoutine = StartCoroutine(SubtitleRoutine(line));
    }

    private IEnumerator SubtitleRoutine(string line)
    {
        subtitleText.text = line;
        subtitleText.maxVisibleCharacters = 0;
        if (subtitleGroup != null) subtitleGroup.alpha = 1f;

        // Máquina de escribir
        int total = line.Length;
        float shown = 0f;
        while (shown < total)
        {
            shown += charsPerSecond * Time.unscaledDeltaTime;
            subtitleText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
            yield return null;
        }

        // Tiempo de lectura proporcional al largo
        yield return new WaitForSecondsRealtime(Mathf.Clamp(total * 0.06f, 2.5f, 7f));

        // Fade out
        if (subtitleGroup != null)
        {
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                subtitleGroup.alpha = 1f - t / fadeDuration;
                yield return null;
            }
            subtitleGroup.alpha = 0f;
        }
        subtitleRoutine = null;
    }
}
