using System;                        // Para Action (eventos de C#)
using System.Collections.Generic;    // Para List<T>
using UnityEngine;                   // Tipos básicos de Unity
using UnityEngine.Events;            // Para UnityEvent (eventos que se conectan desde el Inspector)

/// <summary>
/// Resultado de un interrogatorio (lo leen GameStateTracker y los finales).
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
/// GUARDIAS INTERROGABLES (el id NO se cambia: lo escribe el LevelBuilder de Cristian;
/// el nombre en pantalla sale de DialogueDatabase.displayName). Mapeo TENTATIVO,
/// pendiente de confirmar con el equipo:
///   guardia_porky    = Colmillo
///   guardia_barracon = Tuerto
///   guardia_registro = Contable
///
/// SISTEMA DE DIÁLOGO:
/// - El árbol sale de DialogueDatabase.Get(npcId) (datos en C#, sin .asset).
///   Cada nodo tiene la frase del guardia y de 0 a 3 opciones; un nodo sin opciones es un final
///   con su resultado (Helpful/Misleading/Refused) y, opcionalmente, una pista.
/// - Solo se puede interrogar mientras está rendido, y una sola vez.
/// - Jugador a menos de interactDistance y mirándolo (ángulo < lookAngle) → "[E] Interrogar".
/// - Durante el diálogo se desactivan PlayerController y ShotgunController
///   y se libera el cursor; al cerrar todo vuelve a como estaba.
/// - El guardia muestra la pose talk mientras habla y vuelve a surrender al terminar.
///
/// AL TERMINAR:
/// - GameStateTracker.RegisterInterrogation(nombre, resultado) → cuenta para los finales.
/// - Si el nodo final da pista: GameStateTracker.RegisterClueCollected(awardClueId, clueText).
/// - Evento estático OnInterrogationCompleted(npcId, resultado, clueId o null).
/// - UnityEvent onDialogueCompleted(npcId, resultado) para el Inspector.
/// - El compañero de IA comenta (AITrigger.InterrogationEnded).
/// </summary>
[RequireComponent(typeof(EnemyHealth))]              // Unity agrega EnemyHealth si falta
public class DialogueNPC : MonoBehaviour
{
    /// <summary>(npcId, resultado, clueId o null) — se dispara al terminar cualquier interrogatorio.</summary>
    public static event Action<string, InterrogationResult, string> OnInterrogationCompleted;

    // ===================== Configuración =====================

    [Header("Interacción")]
    [Tooltip("Distancia máxima (m) entre el jugador y el guardia para interrogar")]
    [SerializeField] private float interactDistance = 2.5f;     // Hay que acercarse

    [Tooltip("Ángulo máximo (grados) entre el centro de la cámara y el guardia")]
    [SerializeField] private float lookAngle = 30f;             // Y mirarlo más o menos de frente

    [Tooltip("Texto del prompt de interacción")]
    [SerializeField] private string interactPrompt = "[E] Interrogar";

    [Tooltip("Texto de la opción que cierra un nodo final")]
    [SerializeField] private string endOptionText = "Terminar el interrogatorio.";

    [Header("Eventos")]
    [Tooltip("Se dispara al terminar el interrogatorio de ESTE guardia: (npcId, resultado)")]
    public UnityEvent<string, InterrogationResult> onDialogueCompleted;

    // ===================== Estado =====================

    private EnemyHealth _health;             // Para saber si está rendido y su npcId
    private BillboardSprite _billboard;      // Su dibujo (opcional: para la pose talk)
    private DialogueTree _tree;              // Árbol de diálogo de este guardia
    private DialogueNode _currentNode;       // Nodo que se está mostrando
    private bool _dialogueActive = false;    // ¿Hay un interrogatorio abierto?
    private bool _interrogated = false;      // ¿Ya se le interrogó? (solo una vez)
    private bool _promptShown = false;       // ¿Se está mostrando "[E] Interrogar"?
    private bool _restorePending = false;    // Devolver el control al jugador al final del frame

    // Referencias del jugador (se buscan en Start)
    private Transform _playerTransform;
    private Camera _playerCamera;
    private PlayerController _playerController;  // Script de David: movimiento
    private ShotgunController _shotgun;          // Script de David: escopeta

    // Estado guardado para restaurarlo exactamente al cerrar
    private bool _movementWasEnabled;
    private bool _shotgunWasEnabled;
    private CursorLockMode _previousLockState;
    private bool _previousCursorVisible;

    public bool WasInterrogated => _interrogated;                                   // ¿Ya habló?
    public string DisplayName => _tree != null ? _tree.displayName : name;          // Nombre en pantalla (o el del GameObject)

    // ===================== Ciclo de vida =====================

    private void Awake()
    {
        _health = GetComponent<EnemyHealth>();                          // Siempre existe por el RequireComponent
        _billboard = GetComponentInChildren<BillboardSprite>();         // Puede ser null (placeholder sin sprite)
    }

    private void Start()
    {
        _tree = DialogueDatabase.Get(_health.NpcId);                    // Buscar su árbol por id
        if (_tree == null)                                              // Id vacío o mal escrito
            Debug.LogWarning($"[DialogueNPC] '{name}': no hay diálogo para npcId '{_health.NpcId}' en DialogueDatabase.");

        GameObject player = GameObject.FindWithTag("Player");          // Buscar al jugador por tag
        if (player != null)
        {
            _playerTransform = player.transform;
            _playerController = player.GetComponent<PlayerController>();
        }
        _shotgun = FindObjectOfType<ShotgunController>();               // La escopeta puede estar en la cámara, no en el jugador
    }

    private void OnDisable()
    {
        // Si lo destruyen o desactivan a mitad de algo, no dejar al jugador bloqueado
        if (_dialogueActive)
        {
            _dialogueActive = false;
            if (DialogueUI.Exists) DialogueUI.Instance.Hide();          // Cerrar la ventana de diálogo
            RestorePlayer();                                            // Devolver movimiento, escopeta y cursor
        }
        if (_restorePending)                                            // Iba a restaurar al final del frame
        {
            _restorePending = false;
            RestorePlayer();
        }
        SetPrompt(false);                                               // Quitar "[E] Interrogar"
    }

    private void Update()
    {
        if (_dialogueActive) return;                                    // Durante el diálogo manda DialogueUI

        if (!CanInteract())                                             // ¿Se puede interrogar ahora?
        {
            SetPrompt(false);
            return;
        }

        SetPrompt(true);                                                // Mostrar "[E] Interrogar"
        if (Input.GetKeyDown(KeyCode.E))                                // Input Manager clásico
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

    // ===================== ¿Se puede interrogar? =====================

    private bool CanInteract()
    {
        if (_interrogated || _tree == null || _playerTransform == null) return false; // Ya habló, sin árbol o sin jugador
        if (!_health.IsSurrendered) return false;                       // Muerto o sin rendirse
        if (DialogueUI.Exists && DialogueUI.Instance.IsOpen) return false; // Otro diálogo abierto

        if (Vector3.Distance(_playerTransform.position, transform.position) > interactDistance) return false; // Muy lejos

        if (_playerCamera == null) _playerCamera = Camera.main;         // Buscar la cámara si aún no la tenemos
        if (_playerCamera == null) return false;

        Vector3 toNpc = GetLookTarget() - _playerCamera.transform.position;         // De la cámara al guardia
        return Vector3.Angle(_playerCamera.transform.forward, toNpc) < lookAngle;   // ¿Lo está mirando?
    }

    /// <summary>Centro del cuerpo (collider) o 1 m sobre el pivote.</summary>
    private Vector3 GetLookTarget()
    {
        Collider body = GetComponent<Collider>();
        return body != null && body.enabled ? body.bounds.center : transform.position + Vector3.up;
    }

    private void SetPrompt(bool show)
    {
        if (show == _promptShown) return;                               // No repetir la misma orden cada frame
        _promptShown = show;

        if (show) DialogueUI.Instance.ShowPrompt(interactPrompt);       // Instance crea la UI si no existe
        else if (DialogueUI.Exists) DialogueUI.Instance.HidePrompt();   // Al ocultar no hace falta crearla
    }

    // ===================== Flujo del diálogo =====================

    private void StartDialogue()
    {
        _dialogueActive = true;
        SetPrompt(false);
        LockPlayer();                                                   // Congelar al jugador y soltar el cursor

        // Hacer que el guardia mire al jugador (solo en horizontal)
        Vector3 lookDir = _playerTransform.position - transform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(lookDir);

        if (_billboard != null) _billboard.PlayTalk();                  // Pose talk (sin dibujo, se queda surrender)
        ShowNode(_tree.startNodeId);                                    // Primera frase
    }

    private void ShowNode(string nodeId)
    {
        _currentNode = _tree.GetNode(nodeId);                           // Buscar el nodo por id
        if (_currentNode == null)                                       // Error en los datos: cerrar sin colgar el juego
        {
            Debug.LogWarning($"[DialogueNPC] {_tree.npcId}: no existe el nodo '{nodeId}'. Se cierra como Refused.");
            EndDialogue(InterrogationResult.Refused, null, null);
            return;
        }

        var optionTexts = new List<string>();                           // Textos de los botones
        if (_currentNode.IsEnd)
        {
            optionTexts.Add(endOptionText);                             // Nodo final: un solo botón para cerrar
        }
        else
        {
            foreach (var option in _currentNode.options)                // Una opción por respuesta de Wilbur
                optionTexts.Add(option.text);
        }

        DialogueUI.Instance.ShowLine(_tree.displayName, _currentNode.npcLine, optionTexts, OnOptionSelected);
    }

    /// <summary>
    /// Callback de DialogueUI con el índice elegido (clic o teclas 1-3).
    /// </summary>
    private void OnOptionSelected(int optionIndex)
    {
        if (!_dialogueActive || _currentNode == null) return;           // Clic tardío tras cerrar

        if (_currentNode.IsEnd)                                         // Botón "Terminar"
        {
            EndDialogue(_currentNode.endResult, _currentNode.awardClueId, _currentNode.clueText);
            return;
        }

        if (optionIndex < 0 || optionIndex >= _currentNode.options.Length) return; // Índice inválido
        ShowNode(_currentNode.options[optionIndex].nextNodeId);         // Seguir la rama elegida
    }

    private void EndDialogue(InterrogationResult result, string clueId, string clueText)
    {
        _dialogueActive = false;
        _interrogated = true;                                           // Ya no se le puede volver a interrogar
        _currentNode = null;

        DialogueUI.Instance.Hide();                                     // Cerrar la ventana
        _restorePending = true;                                         // Devolver el control en LateUpdate
        if (_billboard != null) _billboard.PlaySurrender();             // Vuelve a las manos arriba

        // Script de David: cuenta el interrogatorio para los finales
        if (GameStateTracker.Instance != null)
            GameStateTracker.Instance.RegisterInterrogation(_tree.displayName, ResultToSpanish(result));

        string awarded = string.IsNullOrEmpty(clueId) ? null : clueId;  // "" → null
        if (awarded != null) RegisterClue(awarded, clueText);           // Este final da pista: que cuente para los finales

        OnInterrogationCompleted?.Invoke(_tree.npcId, result, awarded); // Evento de código
        onDialogueCompleted?.Invoke(_tree.npcId, result);               // Evento del Inspector

        // Script de David: el compañero de IA comenta cómo fue
        if (AICompanion.Instance != null) AICompanion.Instance.Speak(AITrigger.InterrogationEnded, $"{_tree.displayName}: {ResultToSpanish(result)}");
    }

    /// <summary>
    /// Registrar en GameStateTracker la pista que dio el interrogatorio.
    /// Si al nodo le falta el texto, se usa uno por defecto y se avisa: la pista nunca se pierde en silencio.
    /// </summary>
    private void RegisterClue(string clueId, string clueText)
    {
        if (string.IsNullOrEmpty(clueText))                             // Olvidaron escribir el texto en DialogueDatabase
        {
            clueText = $"Pista obtenida al interrogar a {_tree.displayName}";  // Texto de emergencia
            Debug.LogWarning($"[DialogueNPC] {_tree.npcId}: la pista '{clueId}' no tiene clueText en DialogueDatabase. Se usa: \"{clueText}\".");
        }

        // Script de David: misma llamada que hace CluePickup con las pistas físicas
        if (GameStateTracker.Instance != null)
            GameStateTracker.Instance.RegisterClueCollected(clueId, clueText);
        else
            Debug.LogWarning($"[DialogueNPC] No hay GameStateTracker en la escena: la pista '{clueId}' no se pudo registrar.");
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

    // ===================== Bloquear / restaurar al jugador =====================

    private void LockPlayer()
    {
        _previousLockState = Cursor.lockState;                          // Guardar cómo estaba el cursor
        _previousCursorVisible = Cursor.visible;

        if (_playerController != null)
        {
            _movementWasEnabled = _playerController.enabled;            // Recordar si estaba activo
            _playerController.enabled = false;                          // No caminar ni girar durante el diálogo
        }
        if (_shotgun == null) _shotgun = FindObjectOfType<ShotgunController>(); // Por si apareció después
        if (_shotgun != null)
        {
            _shotgunWasEnabled = _shotgun.enabled;
            _shotgun.enabled = false;                                   // No disparar durante el diálogo
        }

        Cursor.lockState = CursorLockMode.None;                         // Soltar el cursor para hacer clic en opciones
        Cursor.visible = true;
    }

    private void RestorePlayer()
    {
        if (_playerController != null) _playerController.enabled = _movementWasEnabled; // Como estaba antes
        if (_shotgun != null) _shotgun.enabled = _shotgunWasEnabled;

        Cursor.lockState = _previousLockState;
        Cursor.visible = _previousCursorVisible;
    }
}
