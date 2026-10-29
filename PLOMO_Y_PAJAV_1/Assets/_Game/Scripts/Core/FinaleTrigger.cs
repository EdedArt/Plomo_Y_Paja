using System.Collections;
using UnityEngine;

/// <summary>
/// Trigger de la confrontación final con Don Lobo.
/// DÓNDE VA: Zone de la guarida final (GameObject con Collider trigger).
/// Se activa cuando el jugador entra a la zona final.
///
/// FLUJO DE LA ESCENA FINAL:
/// 1. Jugador entra al trigger
/// 2. Don Lobo (jefe) aparece o estaba ahí
/// 3. Se activa la secuencia de diálogo final
/// 4. Si el jugador no dispara en X segundos → "perdonó al lobo"
/// 5. Si el jugador dispara y mata al lobo → "mató al lobo"
/// 6. Se llama a GameManager.TriggerEnding()
///
/// NOTA SOBRE EL DISEÑO NARRATIVO:
/// La elección de PERDONAR se hace simplemente NO disparando.
/// El lobo se queda quieto, vulnerable. El jugador tiene que decidir.
/// Eso crea tensión genuina sin menú de "¿perdonar? sí/no".
/// </summary>
public class FinaleTrigger : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Segundos que tiene el jugador para decidir matar o perdonar al lobo")]
    [SerializeField] private float decisionTime = 8f;

    [Tooltip("¿El lobo ya está en la escena o aparece al entrar?")]
    [SerializeField] private GameObject donLoboGameObject;

    [Tooltip("Punto donde el jugador se detiene para la confrontación")]
    [SerializeField] private Transform confrontationPoint;

    [Header("Diálogo final")]
    [TextArea(2, 5)]
    [SerializeField] private string wolfFinalSpeech =
        "Sé lo que buscas, Wilbur. Tu hermano... no es lo que crees. " +
        "Antes de disparar, mira esto.";

    [Tooltip("Pista especial que aparece en esta escena (la prueba definitiva de la traición de Porky)")]
    [SerializeField] private GameObject finalClueObject;

    [Header("Comentarios de la IA según estado")]
    [TextArea(2, 4)]
    [SerializeField] private string aiCommentManyClues =
        "Recordemos todo lo que encontramos. Las pistas no mienten.";
    [TextArea(2, 4)]
    [SerializeField] private string aiCommentFewClues =
        "Hay cosas que aún no sabemos. ¿Estamos seguros?";

    private bool _finaleStarted = false;
    private bool _decisionMade = false;
    private float _decisionTimer = 0f;

    private void OnTriggerEnter(Collider other)
    {
        // Verificar que es el jugador (debe tener el tag "Player")
        if (!other.CompareTag("Player")) return;
        if (_finaleStarted) return;

        _finaleStarted = true;
        StartCoroutine(RunFinaleSequence(other.transform));
    }

    private IEnumerator RunFinaleSequence(Transform player)
    {
        // 1. Detener al jugador momentáneamente
        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null) playerController.SetCanMove(false);

        yield return new WaitForSeconds(0.5f);
        if (playerController != null) playerController.SetCanMove(true);

        // 2. Don Lobo se activa / aparece
        if (donLoboGameObject != null)
            donLoboGameObject.SetActive(true);

        // 3. La IA comenta según cuántas pistas tiene el jugador
        int cluesFound = GameStateTracker.Instance != null ? GameStateTracker.Instance.CluesFound : 0;
        string aiComment = cluesFound >= 3 ? aiCommentManyClues : aiCommentFewClues;
        if (AICompanion.Instance != null) AICompanion.Instance.Speak(AITrigger.FinalConfrontation, aiComment);

        yield return new WaitForSeconds(2f);

        // 4. Don Lobo habla
        if (UIManager.Instance != null) UIManager.Instance.ShowAISubtitle($"Don Lobo: \"{wolfFinalSpeech}\"");

        // 5. Mostrar la pista final (evidencia de la traición de Porky)
        if (finalClueObject != null)
        {
            yield return new WaitForSeconds(3f);
            finalClueObject.SetActive(true);
        }

        yield return new WaitForSeconds(2f);

        // 6. Iniciar el contador de decisión
        if (UIManager.Instance != null) UIManager.Instance.ShowAISubtitle("¿Qué decidimos, Wilbur?");
        if (AICompanion.Instance != null) AICompanion.Instance.Speak(AITrigger.FinalConfrontation, "Wilbur debe decidir si dispara a Don Lobo o lo perdona");

        // 7. Esperar la decisión del jugador
        StartCoroutine(WaitForDecision());
    }

    private IEnumerator WaitForDecision()
    {
        _decisionTimer = decisionTime;

        while (_decisionTimer > 0 && !_decisionMade)
        {
            _decisionTimer -= Time.deltaTime;
            // TODO: Mostrar contador en UI (opcional, crea más tensión)
            yield return null;
        }

        // Si el tiempo se acabó sin disparar → perdonó al lobo
        if (!_decisionMade)
        {
            SparedWolf();
        }
    }

    /// <summary>
    /// Llamar cuando el jugador mata a Don Lobo (conectar a EnemyHealth.onEnemyDied del lobo).
    /// </summary>
    public void OnWolfKilled()
    {
        if (_decisionMade) return;
        _decisionMade = true;
        if (GameStateTracker.Instance != null) GameStateTracker.Instance.RegisterWolfDecision(true);
        StartCoroutine(TransitionToEnding(1.5f));
    }

    private void SparedWolf()
    {
        _decisionMade = true;
        if (GameStateTracker.Instance != null) GameStateTracker.Instance.RegisterWolfDecision(false);

        // Mostrar escena corta de reconciliación
        if (UIManager.Instance != null) UIManager.Instance.ShowAISubtitle("Don Lobo: \"Gracias, Wilbur. Hay mucho que contarte.\"");
        StartCoroutine(TransitionToEnding(3f));
    }

    private IEnumerator TransitionToEnding(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (GameManager.Instance != null) GameManager.Instance.TriggerEnding();
    }
}
