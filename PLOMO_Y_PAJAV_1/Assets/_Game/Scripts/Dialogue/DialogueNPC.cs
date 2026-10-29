using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Resultado de un interrogatorio (lo lee GameStateTracker para los finales).
/// </summary>
public enum InterrogationResult
{
    Helpful,     // Dio información útil sobre la verdad
    Misleading,  // Mintió o confundió al jugador
    Refused      // Se negó a hablar
}

/// <summary>
/// Guardia que puede ser interrogado después de rendirse.
/// DÓNDE VA: mismo GameObject que EnemyHealth (con canSurrender = true y npcId).
///
/// SISTEMA DE DIÁLOGO:
/// - El árbol sale de DialogueDatabase.Get(npcId) (datos en C#, sin .asset)
/// - Solo se puede interrogar mientras está rendido y una sola vez
/// - Jugador a menos de interactDistance y mirándolo (ángulo < lookAngle) → "[E] Interrogar"
/// - Durante el diálogo se desactivan PlayerController y ShotgunController
///   y se libera el cursor; al cerrar todo vuelve a como estaba
///
/// AL TERMINAR:
/// - Evento estático OnInterrogationCompleted(npcId, resultado, clueId o null)
///   (GameStateTracker se suscribe en el Día 3)
/// - La IA comenta (AITrigger.InterrogationEnded)
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
public class DialogueNPC : MonoBehaviour
{
    /// <summary>(npcId, resultado, clueId o null) — se dispara al terminar cualquier interrogatorio.</summary>
    public static event Action<string, InterrogationResult, string> OnInterrogationCompleted;

    [Header("Interacción")]
    [Tooltip("Distancia máxima (m) entre el jugador y el guardia para interrogar")]
    [SerializeField] private float interactDistance = 2.5f;

    [Tooltip("Ángulo máximo (grados) entre el centro de la cámara y el guardia")]
    [SerializeField] private float lookAngle = 30f;

    [Tooltip("Texto del prompt de interacción")]
    [SerializeField] private string interactPrompt = "[E] Interrogar";

    [Tooltip("Texto de la opción que cierra un nodo final")]
    [SerializeField] private string endOptionText = "Terminar el interrogatorio.";

    [Header("Eventos")]
    [Tooltip("Se dispara al terminar el interrogatorio de ESTE guardia: (npcId, resultado)")]
    public UnityEvent<string, InterrogationResult> onDialogueCompleted;

    // ============ Estado ============
    private EnemyHealth _health;
    private DialogueTree _tree;
    private DialogueNode _currentNode;
    private bool _dialogueActive = false;
    private bool _interrogated = false;
    private bool _promptShown = false;
    private bool _restorePending = false;

    // Referencias del jugador (se buscan al abrir el diálogo)
    private Transform _playerTransform;
    private Camera _playerCamera;
    private PlayerController _playerController;
    private ShotgunController _shotgun;

    // Estado guardado para restaurarlo exactamente al cerrar
    private bool _movementWasEnabled;
    private bool _shotgunWasEnabled;
    private CursorLockMode _previousLockState;
    private bool _previousCursorVisible;

    public bool WasInterrogated => _interrogated;
    public string DisplayName => _tree != null ? _tree.displayName : name;

    private void Awake()
    {
        _health = GetComponent<EnemyHealth>();
    }

    private void Start()
    {
        _tree = DialogueDatabase.Get(_health.NpcId);
        if (_tree == null)
            Debug.LogWarning($"[DialogueNPC] '{name}': no hay diálogo para npcId '{_health.NpcId}' en DialogueDatabase.");

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
            _playerController = player.GetComponent<PlayerController>();
        }
        _shotgun = FindObjectOfType<ShotgunController>();
    }

    private void OnDisable()
    {
        // Si lo destruyen o desactivan a mitad de algo, no dejar al jugador bloqueado
        if (_dialogueActive)
        {
            _dialogueActive = false;
            if (DialogueUI.Exists) DialogueUI.Instance.Hide();
            RestorePlayer();
        }
        if (_restorePending)
        {
            _restorePending = false;
            RestorePlayer();
        }
        SetPrompt(false);
    }

    private void Update()
    {
        if (_dialogueActive) return;

        if (!CanInteract())
        {
            SetPrompt(false);
            return;
        }

        SetPrompt(true);
        if (Input.GetKeyDown(KeyCode.E))
            StartDialogue();
    }

    private void LateUpdate()
    {
        // Se restaura al final del frame para que el clic que eligió la última
        // opción no dispare la escopeta en este mismo frame
        if (_restorePending)
        {
            _restorePending = false;
            RestorePlayer();
        }
    }

    private bool CanInteract()
    {
        if (_interrogated || _tree == null || _playerTransform == null) return false;
        if (!_health.IsSurrendered) return false; // muerto o sin rendirse
        if (DialogueUI.Exists && DialogueUI.Instance.IsOpen) return false; // otro diálogo abierto

        if (Vector3.Distance(_playerTransform.position, transform.position) > interactDistance) return false;

        if (_playerCamera == null) _playerCamera = Camera.main;
        if (_playerCamera == null) return false;

        Vector3 toNpc = GetLookTarget() - _playerCamera.transform.position;
        return Vector3.Angle(_playerCamera.transform.forward, toNpc) < lookAngle;
    }

    /// <summary>Centro del cuerpo (collider) o 1 m sobre el pivote.</summary>
    private Vector3 GetLookTarget()
    {
        Collider body = GetComponent<Collider>();
        return body != null && body.enabled ? body.bounds.center : transform.position + Vector3.up;
    }

    private void SetPrompt(bool show)
    {
        if (show == _promptShown) return;
        _promptShown = show;

        if (show) DialogueUI.Instance.ShowPrompt(interactPrompt);
        else if (DialogueUI.Exists) DialogueUI.Instance.HidePrompt();
    }

    // ============ Flujo del diálogo ============

    private void StartDialogue()
    {
        _dialogueActive = true;
        SetPrompt(false);
        LockPlayer();

        // Hacer que el guardia mire al jugador
        Vector3 lookDir = _playerTransform.position - transform.position;
        lookDir.y = 0;
        if (lookDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(lookDir);

        ShowNode(_tree.startNodeId);
    }

    private void ShowNode(string nodeId)
    {
        _currentNode = _tree.GetNode(nodeId);
        if (_currentNode == null)
        {
            Debug.LogWarning($"[DialogueNPC] {_tree.npcId}: no existe el nodo '{nodeId}'. Se cierra como Refused.");
            EndDialogue(InterrogationResult.Refused, null);
            return;
        }

        var optionTexts = new List<string>();
        if (_currentNode.IsEnd)
        {
            optionTexts.Add(endOptionText);
        }
        else
        {
            foreach (var option in _currentNode.options)
                optionTexts.Add(option.text);
        }

        DialogueUI.Instance.ShowLine(_tree.displayName, _currentNode.npcLine, optionTexts, OnOptionSelected);
    }

    /// <summary>
    /// Callback de DialogueUI con el índice elegido (clic o teclas 1-3).
    /// </summary>
    private void OnOptionSelected(int optionIndex)
    {
        if (!_dialogueActive || _currentNode == null) return;

        if (_currentNode.IsEnd)
        {
            EndDialogue(_currentNode.endResult, _currentNode.awardClueId);
            return;
        }

        if (optionIndex < 0 || optionIndex >= _currentNode.options.Length) return;
        ShowNode(_currentNode.options[optionIndex].nextNodeId);
    }

    private void EndDialogue(InterrogationResult result, string clueId)
    {
        _dialogueActive = false;
        _interrogated = true;
        _currentNode = null;

        DialogueUI.Instance.Hide();
        _restorePending = true;

        string awarded = string.IsNullOrEmpty(clueId) ? null : clueId;
        OnInterrogationCompleted?.Invoke(_tree.npcId, result, awarded);
        onDialogueCompleted?.Invoke(_tree.npcId, result);

        if (AICompanion.Instance != null) AICompanion.Instance.Speak(AITrigger.InterrogationEnded, $"{_tree.displayName}: {ResultToSpanish(result)}");
    }

    private static string ResultToSpanish(InterrogationResult result)
    {
        switch (result)
        {
            case InterrogationResult.Helpful:    return "dio información útil";
            case InterrogationResult.Misleading: return "mintió o intentó engañar a Wilbur";
            default:                             return "se negó a hablar";
        }
    }

    // ============ Bloquear / restaurar al jugador ============

    private void LockPlayer()
    {
        _previousLockState = Cursor.lockState;
        _previousCursorVisible = Cursor.visible;

        if (_playerController != null)
        {
            _movementWasEnabled = _playerController.enabled;
            _playerController.enabled = false;
        }
        if (_shotgun == null) _shotgun = FindObjectOfType<ShotgunController>();
        if (_shotgun != null)
        {
            _shotgunWasEnabled = _shotgun.enabled;
            _shotgun.enabled = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestorePlayer()
    {
        if (_playerController != null) _playerController.enabled = _movementWasEnabled;
        if (_shotgun != null) _shotgun.enabled = _shotgunWasEnabled;

        Cursor.lockState = _previousLockState;
        Cursor.visible = _previousCursorVisible;
    }
}
