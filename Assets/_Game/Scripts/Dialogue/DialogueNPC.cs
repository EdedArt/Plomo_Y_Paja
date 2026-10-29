using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// NPC que puede ser interrogado por el jugador.
/// DÓNDE VA: cualquier enemigo rendido o NPC neutral que Wilbur pueda interrogar.
///
/// SISTEMA DE DIÁLOGO:
/// - Cada NPC tiene un árbol de diálogo (lista de DialogueNodes)
/// - Cada node tiene texto del NPC + opciones de respuesta del jugador
/// - Las opciones llevan a otros nodes o terminan el diálogo
/// - Resultado: "helpful", "misleading" o "refused" — se registra en GameStateTracker
///
/// SETUP EN INSPECTOR:
/// - Agregar a un NPC rendido (EnemyAI.currentState == Surrender)
/// - Configurar npcName y los nodes de diálogo
/// - La interacción solo funciona si el NPC está rendido o es neutro
/// </summary>
public class DialogueNPC : MonoBehaviour
{
    [System.Serializable]
    public class DialogueOption
    {
        [Tooltip("Texto que dice el JUGADOR")]
        public string playerText;

        [Tooltip("ID del siguiente node (dejar en -1 para terminar el diálogo)")]
        public int nextNodeId = -1;

        [Tooltip("Resultado narrativo de esta opción")]
        public DialogueOutcome outcome = DialogueOutcome.Neutral;
    }

    [System.Serializable]
    public class DialogueNode
    {
        [Tooltip("ID único de este node (0, 1, 2, 3...)")]
        public int nodeId;

        [Tooltip("Texto que dice el NPC")]
        [TextArea(2, 5)]
        public string npcText;

        [Tooltip("Opciones de respuesta del jugador")]
        public List<DialogueOption> options = new List<DialogueOption>();

        [Tooltip("Resultado de este node si el jugador llega aquí (para tracking)")]
        public string outcomeNote = "";
    }

    public enum DialogueOutcome
    {
        Neutral,      // No revela nada importante
        Helpful,      // Revela info útil sobre la verdad
        Misleading,   // Miente o confunde al jugador
        Refused       // Se niega a hablar
    }

    [Header("NPC")]
    [SerializeField] private string npcName = "Lobo Guardaespaldas";
    [SerializeField] private float interactDistance = 3f;

    [Header("¿Requiere rendición?")]
    [Tooltip("Si true, solo se puede hablar con él cuando está rendido (EnemyAI.IsSurrendered)")]
    [SerializeField] private bool requiresSurrender = true;

    [Header("Árbol de diálogo")]
    [Tooltip("El primer node siempre debe ser el de ID 0")]
    [SerializeField] private List<DialogueNode> dialogueNodes = new List<DialogueNode>();

    [Header("Eventos")]
    public UnityEvent<string, DialogueOutcome> onDialogueCompleted; // (npcName, outcome final)

    // ============ Estado ============
    private bool _dialogueActive = false;
    private bool _dialogueCompleted = false;
    private DialogueNode _currentNode;
    private Transform _playerTransform;
    private EnemyAI _enemyAI; // Para verificar si está rendido

    private void Awake()
    {
        _playerTransform = GameObject.FindWithTag("Player")?.transform;
        _enemyAI = GetComponent<EnemyAI>();
    }

    private void Update()
    {
        if (_dialogueCompleted || _dialogueActive) return;

        // Verificar condiciones para interactuar
        if (!CanInteract()) return;

        float distance = Vector3.Distance(transform.position, _playerTransform.position);
        if (distance <= interactDistance)
        {
            UIManager.Instance?.ShowInteractPrompt($"[E] Interrogar a {npcName}");

            if (Input.GetKeyDown(KeyCode.E))
                StartDialogue();
        }
        else
        {
            UIManager.Instance?.HideInteractPrompt();
        }
    }

    private bool CanInteract()
    {
        if (_playerTransform == null) return false;
        if (requiresSurrender && _enemyAI != null && !_enemyAI.IsSurrendered) return false;
        return true;
    }

    private void StartDialogue()
    {
        if (dialogueNodes == null || dialogueNodes.Count == 0)
        {
            Debug.LogWarning($"[DialogueNPC] {npcName} no tiene diálogos configurados.");
            return;
        }

        _dialogueActive = true;

        // Detener al jugador durante el diálogo
        FindObjectOfType<PlayerController>()?.SetCanMove(false);
        UIManager.Instance?.HideInteractPrompt();

        // Hacer que el NPC mire al jugador
        Vector3 lookDir = _playerTransform.position - transform.position;
        lookDir.y = 0;
        transform.rotation = Quaternion.LookRotation(lookDir);

        // Ir al primer node (ID 0)
        ShowNode(0);
    }

    /// <summary>
    /// Mostrar un node de diálogo en la UI.
    /// UIManager se encarga del display, nosotros solo le damos los datos.
    /// </summary>
    private void ShowNode(int nodeId)
    {
        _currentNode = dialogueNodes.Find(n => n.nodeId == nodeId);
        if (_currentNode == null)
        {
            EndDialogue(DialogueOutcome.Neutral);
            return;
        }

        // Construir lista de textos de opciones para la UI
        List<string> optionTexts = new List<string>();
        foreach (var opt in _currentNode.options)
            optionTexts.Add(opt.playerText);

        UIManager.Instance?.ShowDialogue(npcName, _currentNode.npcText, optionTexts, OnOptionSelected);
    }

    /// <summary>
    /// Callback cuando el jugador elige una opción.
    /// Se le pasa como delegate a UIManager para que lo llame al hacer clic.
    /// </summary>
    private void OnOptionSelected(int optionIndex)
    {
        if (_currentNode == null || optionIndex >= _currentNode.options.Count) return;

        DialogueOption chosen = _currentNode.options[optionIndex];

        // ¿Hay un siguiente node?
        if (chosen.nextNodeId >= 0)
        {
            ShowNode(chosen.nextNodeId);
        }
        else
        {
            // Fin del diálogo con este outcome
            EndDialogue(chosen.outcome);
        }
    }

    private void EndDialogue(DialogueOutcome outcome)
    {
        _dialogueActive = false;
        _dialogueCompleted = true;

        // Registrar en el tracker
        GameStateTracker.Instance?.RegisterInterrogation(npcName, outcome.ToString());

        // Notificar a la IA
        AICompanion.Instance?.TriggerComment(
            AICompanion.AITrigger.InterrogationDone,
            $"El interrogatorio a {npcName} resultó en: {outcome}"
        );

        // Restaurar movimiento del jugador
        FindObjectOfType<PlayerController>()?.SetCanMove(true);

        // Cerrar la UI de diálogo
        UIManager.Instance?.HideDialogue();

        onDialogueCompleted?.Invoke(npcName, outcome);
    }
}
