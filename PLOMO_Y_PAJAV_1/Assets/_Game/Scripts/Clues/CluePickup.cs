using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Objeto interactuable que representa una pista en el nivel.
/// DÓNDE VA: GameObject de la pista en la escena (ej: carta, huella, foto).
///
/// SETUP EN INSPECTOR:
/// - Agregar Collider con Is Trigger = true
/// - Configurar clueId (único por pista), description y fullText
/// - Opcional: asignar sprite para mostrar en pantalla al recoger
///
/// PISTAS PLANEADAS (historia):
/// 1. "nota_porky_01"   - Una nota de Porky con lenguaje cómplice con Don Lobo
/// 2. "contrato_01"     - Un contrato firmado entre Porky y Don Lobo (antes del "secuestro")
/// 3. "foto_reunion_01" - Foto de Porky y Don Lobo reuniéndose en secreto
/// 4. "registro_01"     - Registro de deudas: Porky debe dinero a Don Lobo y no pagó
/// 5. "carta_lobo_01"   - Carta de Don Lobo explicando que no quiso hacerle daño a nadie
/// </summary>
public class CluePickup : MonoBehaviour
{
    [Header("Datos de la pista")]
    [Tooltip("Identificador único de esta pista (usar nombres sin espacios)")]
    [SerializeField] private string clueId = "pista_sin_nombre";

    [Tooltip("Nombre corto que aparece en pantalla al recoger")]
    [SerializeField] private string displayName = "Pista";

    [Tooltip("Descripción breve (aparece en el log de pistas del HUD)")]
    [SerializeField] private string shortDescription = "Algo sospechoso...";

    [Tooltip("Texto completo de la pista (el jugador lo lee en pantalla)")]
    [TextArea(3, 8)]
    [SerializeField] private string fullText = "El texto completo de la pista aquí.";

    [Header("Interacción")]
    [Tooltip("Distancia máxima desde la que se puede interactuar (E)")]
    [SerializeField] private float interactDistance = 3f;

    [Tooltip("Texto del prompt de interacción")]
    [SerializeField] private string interactPrompt = "Presiona [E] para examinar";

    [Header("Efectos")]
    [Tooltip("Efecto de partículas o brillo para señalar la pista")]
    [SerializeField] private GameObject highlightEffect;

    [Tooltip("Tiempo en segundos que se muestra el texto completo")]
    [SerializeField] private float displayDuration = 5f;

    [Header("Eventos")]
    public UnityEvent onClueCollected;

    private bool _collected = false;
    private bool _playerInRange = false;
    private Transform _playerTransform;

    private void Start()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null) _playerTransform = playerObject.transform;

        // Activar el highlight para que la pista sea visible en el mundo
        if (highlightEffect != null)
            highlightEffect.SetActive(true);
    }

    private void Update()
    {
        if (_collected) return;

        // Verificar si el jugador está en rango
        if (_playerTransform != null)
        {
            float distance = Vector3.Distance(transform.position, _playerTransform.position);
            _playerInRange = distance <= interactDistance;

            // Mostrar/ocultar el prompt de interacción
            if (_playerInRange)
            {
                if (UIManager.Instance != null) UIManager.Instance.ShowInteractPrompt(interactPrompt);

                // Recoger al presionar E
                if (Input.GetKeyDown(KeyCode.E))
                    CollectClue();
            }
            else
            {
                if (UIManager.Instance != null) UIManager.Instance.HideInteractPrompt();
            }
        }
    }

    private void CollectClue()
    {
        if (_collected) return;
        _collected = true;

        AudioLibrary.PlaySfx2D("pista_papel");

        // 1. Registrar en el tracker de estado del juego
        if (GameStateTracker.Instance != null) GameStateTracker.Instance.RegisterClueCollected(clueId, shortDescription);

        // 2. Mostrar el texto de la pista en pantalla
        if (UIManager.Instance != null) UIManager.Instance.ShowClueUI(displayName, fullText, displayDuration);
        if (UIManager.Instance != null) UIManager.Instance.HideInteractPrompt();

        // 3. Notificar a la IA para que comente (con delay para no solaparse con la UI)
        StartCoroutine(NotifyAIWithDelay(2f));

        // 4. Eventos externos
        onClueCollected?.Invoke();

        // 5. Desactivar el highlight y el collider
        if (highlightEffect != null)
            highlightEffect.SetActive(false);

        GetComponent<Collider>().enabled = false;

        // Opcional: ocultar el objeto visualmente (pero mantener el GO por si se necesita)
        // GetComponent<Renderer>()?.enabled = false;
    }

    private IEnumerator NotifyAIWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (AICompanion.Instance != null) AICompanion.Instance.Speak(AITrigger.ClueFound, $"{displayName}: {shortDescription}");
    }

    private void OnDrawGizmosSelected()
    {
        // Mostrar el radio de interacción en el editor
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}
