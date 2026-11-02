using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vista del interrogatorio (fase 3C.3, D9): Canvas propio, Screen Space Overlay, por encima de la mira y del HUD.
/// No depende de UIManager ni de DialogueUI. Lo arma el menú Plomo y Paja → Contador → Crear UI de interrogatorio
/// (prefab Prefabs/UI/InterrogationCanvas). Solo muestra cosas; el ritmo (tiempos, teclas, voces) lo lleva
/// InterrogationDirector.
///
/// Partes: retrato (InterrogationPortrait), papel del diálogo (nombre + texto con máquina de escribir),
/// 3 botones de opción, banda de pensamiento de la IA arriba y 2 botones grandes de decisión.
/// </summary>
public class InterrogationCanvas : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private InterrogationPortrait portrait;

    [Header("Papel")]
    [SerializeField] private TMP_Text speakerLabel;
    [SerializeField] private TMP_Text lineLabel;
    [SerializeField] private Button[] optionButtons = new Button[0];
    [SerializeField] private TMP_Text[] optionLabels = new TMP_Text[0];

    [Header("Decisión")]
    [SerializeField] private GameObject decisionRoot;
    [SerializeField] private Button[] decisionButtons = new Button[0];
    [SerializeField] private TMP_Text[] decisionLabels = new TMP_Text[0];

    [Header("Pensamiento de la IA")]
    [SerializeField] private CanvasGroup thoughtGroup;
    [SerializeField] private TMP_Text thoughtLabel;

    private Action<int> _optionChosen;
    private Action<int> _decisionChosen;

    public InterrogationPortrait Portrait => portrait;
    public int OptionCount => optionButtons.Length;

    private void Awake()
    {
        for (int i = 0; i < optionButtons.Length; i++)
        {
            int index = i;
            if (optionButtons[i] != null) optionButtons[i].onClick.AddListener(() => PickOption(index));
        }
        for (int i = 0; i < decisionButtons.Length; i++)
        {
            int index = i;
            if (decisionButtons[i] != null) decisionButtons[i].onClick.AddListener(() => PickDecision(index));
        }
        SetOpen(false);
        SetAlpha(0f);
        HideOptions();
        HideDecision();
        SetThought("", 0f);
        ShowLine("", "");
    }

    /// <summary>Abre/cierra el canvas (sin animación: el director anima con SetAlpha).</summary>
    public void SetOpen(bool open)
    {
        if (canvas != null) canvas.enabled = open;
        if (rootGroup != null)
        {
            rootGroup.interactable = open;
            rootGroup.blocksRaycasts = open;
        }
    }

    public void SetAlpha(float alpha)
    {
        if (rootGroup != null) rootGroup.alpha = alpha;
    }

    // ============ Papel ============

    /// <summary>Pone el nombre y el texto completo, sin letras visibles todavía. Devuelve cuántas letras tiene.</summary>
    public int ShowLine(string speaker, string text)
    {
        if (speakerLabel != null) speakerLabel.text = speaker;
        if (lineLabel == null) return 0;
        lineLabel.text = text;
        lineLabel.maxVisibleCharacters = 0;
        lineLabel.ForceMeshUpdate();
        return lineLabel.textInfo.characterCount;
    }

    /// <summary>Máquina de escribir: cuántas letras se ven.</summary>
    public void SetVisibleCharacters(int count)
    {
        if (lineLabel != null) lineLabel.maxVisibleCharacters = Mathf.Max(0, count);
    }

    // ============ Opciones ============

    public void ShowOptions(string[] texts, Action<int> chosen)
    {
        _optionChosen = chosen;
        for (int i = 0; i < optionButtons.Length; i++)
        {
            bool used = texts != null && i < texts.Length && !string.IsNullOrEmpty(texts[i]);
            if (optionButtons[i] != null) optionButtons[i].gameObject.SetActive(used);
            if (used && i < optionLabels.Length && optionLabels[i] != null) optionLabels[i].text = $"{i + 1}. {texts[i]}";
        }
    }

    public void HideOptions()
    {
        _optionChosen = null;
        foreach (Button b in optionButtons)
            if (b != null) b.gameObject.SetActive(false);
    }

    /// <summary>Elegir por teclado (1/2/3) o por clic.</summary>
    public void PickOption(int index)
    {
        if (_optionChosen == null || index < 0 || index >= optionButtons.Length) return;
        if (optionButtons[index] == null || !optionButtons[index].gameObject.activeSelf) return;
        Action<int> callback = _optionChosen;
        HideOptions();
        callback(index);
    }

    // ============ Decisión ============

    public void ShowDecision(string first, string second, Action<int> chosen)
    {
        _decisionChosen = chosen;
        if (decisionLabels.Length > 0 && decisionLabels[0] != null) decisionLabels[0].text = first;
        if (decisionLabels.Length > 1 && decisionLabels[1] != null) decisionLabels[1].text = second;
        if (decisionRoot != null) decisionRoot.SetActive(true);
    }

    public void HideDecision()
    {
        _decisionChosen = null;
        if (decisionRoot != null) decisionRoot.SetActive(false);
    }

    public void PickDecision(int index)
    {
        if (_decisionChosen == null || index < 0 || index >= decisionButtons.Length) return;
        Action<int> callback = _decisionChosen;
        HideDecision();
        callback(index);
    }

    // ============ Pensamiento ============

    public void SetThought(string text, float alpha)
    {
        if (thoughtLabel != null) thoughtLabel.text = text;
        if (thoughtGroup != null) thoughtGroup.alpha = alpha;
    }
}
