using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Resaltado de una opción del interrogatorio: letra más oscura y subrayada con el mouse encima.</summary>
public class InterrogationOptionHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private TMP_Text _label;
    private InterrogationCanvas _view;

    public void Init(TMP_Text label, InterrogationCanvas view)
    {
        _label = label;
        _view = view;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_view != null) _view.SetOptionHover(_label, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_view != null) _view.SetOptionHover(_label, false);
    }

    private void OnDisable()
    {
        if (_view != null) _view.SetOptionHover(_label, false);
    }
}
