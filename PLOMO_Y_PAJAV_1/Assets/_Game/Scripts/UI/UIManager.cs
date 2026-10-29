using System;
using System.Collections;
using System.Collections.Generic;
using TMPro; // TextMeshPro — más bonito que el Text de Unity normal
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gestiona toda la UI del juego: HUD, diálogos, subtítulos de la IA, pantallas de muerte.
/// DÓNDE VA: Canvas principal de la escena. Crear un Canvas (UI → Canvas) y agregar este script.
///
/// JERARQUÍA DEL CANVAS (crear estos GameObjects hijos del Canvas):
/// Canvas
/// ├── HUD (siempre visible)
/// │   ├── HealthBar (Slider)
/// │   ├── AmmoText (TextMeshProUGUI: "6 / 18")
/// │   └── CluesCounter (TextMeshProUGUI: "Pistas: 0/5")
/// ├── CrosshairDot (Image pequeño, punto blanco en centro)
/// ├── InteractPrompt (TextMeshProUGUI, visible al acercarse a objetos)
/// ├── AISubtitlePanel (Panel + TextMeshProUGUI para la voz de la IA)
/// ├── ClueReadPanel (Panel para leer una pista completa)
/// │   ├── ClueTitle (TextMeshProUGUI)
/// │   └── ClueText (TextMeshProUGUI)
/// ├── DialoguePanel (Panel para interrogaciones)
/// │   ├── NPCNameText (TextMeshProUGUI)
/// │   ├── NPCDialogueText (TextMeshProUGUI)
/// │   └── OptionsContainer (Vertical Layout Group)
/// │       ├── OptionButton_0
/// │       ├── OptionButton_1
/// │       └── OptionButton_2
/// ├── DeathScreen (Panel rojo/oscuro)
/// │   ├── DeathText ("Moriste")
/// │   ├── RetryButton
/// │   └── MainMenuButton
/// └── PauseMenu (Panel oscuro semitransparente)
///     ├── ResumeButton
///     └── MainMenuButton
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("HUD")]
    [SerializeField] private Slider healthBar;
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TextMeshProUGUI cluesCounter;

    [Header("Crosshair")]
    [SerializeField] private GameObject crosshair;

    [Header("Prompt de interacción")]
    [SerializeField] private TextMeshProUGUI interactPromptText;
    [SerializeField] private GameObject interactPromptPanel;

    [Header("IA Subtítulo")]
    [SerializeField] private GameObject aiSubtitlePanel;
    [SerializeField] private TextMeshProUGUI aiSubtitleText;
    [SerializeField] private float aiSubtitleDuration = 6f;
    [SerializeField] private float aiSubtitleFadeDuration = 1f;

    [Header("Panel de pista")]
    [SerializeField] private GameObject clueReadPanel;
    [SerializeField] private TextMeshProUGUI clueTitleText;
    [SerializeField] private TextMeshProUGUI clueBodyText;

    [Header("Panel de diálogo")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TextMeshProUGUI npcNameText;
    [SerializeField] private TextMeshProUGUI npcDialogueText;
    [SerializeField] private Transform optionsContainer;
    [SerializeField] private Button optionButtonPrefab; // Prefab del botón de opción

    [Header("Pantalla de muerte")]
    [SerializeField] private GameObject deathScreen;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button mainMenuButtonDeath;

    [Header("Menú de pausa")]
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button mainMenuButtonPause;

    private Coroutine _aiSubtitleCoroutine;
    private List<Button> _dialogueOptionButtons = new List<Button>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // Ocultar paneles al iniciar
        if (aiSubtitlePanel != null) aiSubtitlePanel.SetActive(false);
        if (clueReadPanel != null) clueReadPanel.SetActive(false);
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (deathScreen != null) deathScreen.SetActive(false);
        if (pauseMenu != null) pauseMenu.SetActive(false);
        if (interactPromptPanel != null) interactPromptPanel.SetActive(false);

        // Conectar botones
        if (retryButton != null) retryButton.onClick.AddListener(GameManager.Instance.RetryCurrentLevel);
        if (mainMenuButtonDeath != null) mainMenuButtonDeath.onClick.AddListener(GameManager.Instance.GoToMainMenu);
        if (resumeButton != null) resumeButton.onClick.AddListener(() => GameManager.Instance.TogglePause());
        if (mainMenuButtonPause != null) mainMenuButtonPause.onClick.AddListener(GameManager.Instance.GoToMainMenu);
    }

    // ============ HUD ============

    /// <summary>
    /// Actualizar la barra de vida. healthPercent va de 0 a 1.
    /// Conectar a PlayerHealth.onHealthChanged en el Inspector.
    /// </summary>
    public void UpdateHealthUI(float healthPercent)
    {
        if (healthBar != null)
            healthBar.value = healthPercent;
    }

    /// <summary>
    /// Actualizar el contador de munición.
    /// Conectar a ShotgunController.onAmmoChanged en el Inspector.
    /// </summary>
    public void UpdateAmmoUI(int current, int reserve)
    {
        if (ammoText != null)
            ammoText.text = $"{current} / {reserve}";
    }

    public void UpdateCluesUI(int found, int total)
    {
        if (cluesCounter != null)
            cluesCounter.text = $"Pistas: {found}/{total}";
    }

    // ============ PROMPT DE INTERACCIÓN ============

    public void ShowInteractPrompt(string text)
    {
        if (interactPromptPanel != null) interactPromptPanel.SetActive(true);
        if (interactPromptText != null) interactPromptText.text = text;
    }

    public void HideInteractPrompt()
    {
        if (interactPromptPanel != null) interactPromptPanel.SetActive(false);
    }

    // ============ IA SUBTÍTULO ============

    /// <summary>
    /// Mostrar el subtítulo de la voz de la IA con fade in/out automático.
    /// </summary>
    public void ShowAISubtitle(string text)
    {
        if (_aiSubtitleCoroutine != null)
            StopCoroutine(_aiSubtitleCoroutine);

        _aiSubtitleCoroutine = StartCoroutine(ShowAISubtitleCoroutine(text));
    }

    private IEnumerator ShowAISubtitleCoroutine(string text)
    {
        if (aiSubtitlePanel != null) aiSubtitlePanel.SetActive(true);

        if (aiSubtitleText != null)
        {
            aiSubtitleText.text = text;
            aiSubtitleText.alpha = 0f;

            // Fade in
            float t = 0;
            while (t < aiSubtitleFadeDuration)
            {
                t += Time.deltaTime;
                aiSubtitleText.alpha = Mathf.Clamp01(t / aiSubtitleFadeDuration);
                yield return null;
            }

            // Esperar
            yield return new WaitForSeconds(aiSubtitleDuration);

            // Fade out
            t = 0;
            while (t < aiSubtitleFadeDuration)
            {
                t += Time.deltaTime;
                aiSubtitleText.alpha = 1f - Mathf.Clamp01(t / aiSubtitleFadeDuration);
                yield return null;
            }
        }

        if (aiSubtitlePanel != null) aiSubtitlePanel.SetActive(false);
    }

    // ============ PANEL DE PISTA ============

    public void ShowClueUI(string title, string bodyText, float duration)
    {
        if (clueReadPanel != null) clueReadPanel.SetActive(true);
        if (clueTitleText != null) clueTitleText.text = title;
        if (clueBodyText != null) clueBodyText.text = bodyText;

        StartCoroutine(HideClueAfterDelay(duration));
    }

    private IEnumerator HideClueAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (clueReadPanel != null) clueReadPanel.SetActive(false);
    }

    // ============ DIÁLOGO / INTERROGACIÓN ============

    /// <summary>
    /// Mostrar el panel de diálogo con las opciones del jugador.
    /// onOptionSelected: callback que se llama con el índice de la opción elegida.
    /// </summary>
    public void ShowDialogue(string npcName, string dialogueText, List<string> options, Action<int> onOptionSelected)
    {
        if (dialoguePanel != null) dialoguePanel.SetActive(true);
        if (crosshair != null) crosshair.SetActive(false);
        if (npcNameText != null) npcNameText.text = npcName;
        if (npcDialogueText != null) npcDialogueText.text = dialogueText;

        // Limpiar botones anteriores
        foreach (var btn in _dialogueOptionButtons)
            Destroy(btn.gameObject);
        _dialogueOptionButtons.Clear();

        // Crear botones para cada opción
        for (int i = 0; i < options.Count; i++)
        {
            int index = i; // Capturar el índice para el closure
            Button btn = Instantiate(optionButtonPrefab, optionsContainer);
            btn.GetComponentInChildren<TextMeshProUGUI>().text = options[i];
            btn.onClick.AddListener(() => onOptionSelected(index));
            _dialogueOptionButtons.Add(btn);
        }

        // Desbloquear el cursor para que el jugador pueda hacer clic
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void HideDialogue()
    {
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (crosshair != null) crosshair.SetActive(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ============ PANTALLAS DE ESTADO ============

    public void ShowDeathScreen()
    {
        if (deathScreen != null) deathScreen.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ShowPauseMenu(bool show)
    {
        if (pauseMenu != null) pauseMenu.SetActive(show);
    }
}
