using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Resaltado de una opción o de una decisión del interrogatorio (3C.4.1): con el mouse encima la letra pasa a tinta
/// roja, se subraya, crece (×1.06) y tiembla a saltos (±1.5 px a 12 Hz). Los valores salen de InterrogationCanvas.
/// Tiempo real: la escena corre con timeScale 0.
/// </summary>
public class InterrogationOptionHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private TMP_Text _label;
    private InterrogationCanvas _view;
    private RectTransform _rect;
    private Vector2 _basePosition;
    private bool _hovered;
    private float _nextStep;

    public void Init(TMP_Text label, InterrogationCanvas view)
    {
        _label = label;
        _view = view;
        _rect = label != null ? (RectTransform)label.transform : null;
        if (_rect != null) _basePosition = _rect.anchoredPosition;
        Apply(false);
    }

    public void OnPointerEnter(PointerEventData eventData) => Apply(true);

    public void OnPointerExit(PointerEventData eventData) => Apply(false);

    private void OnDisable() => Apply(false);

    private void Apply(bool hovered)
    {
        _hovered = hovered;
        if (_label == null || _view == null) return;
        _label.color = hovered ? _view.ChoiceHoverColor : _view.ChoiceColor;
        _label.fontStyle = hovered ? FontStyles.Underline : FontStyles.Normal;
        _rect.localScale = Vector3.one * (hovered ? _view.ChoiceHoverScale : 1f);
        _rect.anchoredPosition = _basePosition;
        _nextStep = 0f;
    }

    private void Update()
    {
        if (!_hovered || _rect == null || _view == null) return;
        float now = Time.unscaledTime;
        if (now < _nextStep) return;
        _nextStep = now + 1f / Mathf.Max(1f, _view.ChoiceWobbleHz);
        float px = _view.ChoiceWobblePixels;
        _rect.anchoredPosition = _basePosition + new Vector2(Random.Range(-px, px), Random.Range(-px, px));
    }
}
