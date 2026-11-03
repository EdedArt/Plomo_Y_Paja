using TMPro;
using UnityEngine;

/// <summary>
/// Subtítulos de cinemática (I10): nombre en mayúsculas + texto, Special Elite, abajo, dentro de la franja negra.
/// Reutilizable, independiente del HUD y de UIManager. Tiempo real.
/// DÓNDE VA: un Canvas Overlay propio con un CanvasGroup (lo arma el builder de la cinemática).
/// </summary>
public class CinematicSubtitles : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text speaker;
    [SerializeField] private TMP_Text line;
    [SerializeField] private float fadeSeconds = 0.2f;

    private float _target;

    private void Awake()
    {
        if (group != null) group.alpha = 0f;
    }

    /// <summary>Muestra una línea. speakerName vacío = sin nombre.</summary>
    public void Show(string speakerName, string text)
    {
        if (speaker != null) speaker.text = string.IsNullOrEmpty(speakerName) ? "" : speakerName.ToUpperInvariant();
        if (line != null) line.text = text;
        _target = 1f;
    }

    public void Hide(bool instant = false)
    {
        _target = 0f;
        if (instant && group != null) group.alpha = 0f;
    }

    private void Update()
    {
        if (group == null) return;
        group.alpha = Mathf.MoveTowards(group.alpha, _target, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));
    }
}
