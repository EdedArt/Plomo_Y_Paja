using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Balas visibles en el tambor durante la recarga del revólver (fase 3C.5.2, ChamberOverlay de VM2).
/// DÓNDE VA: lo crea FirstPersonViewmodel como hijo de "HandsImage" (capa Viewmodel), así sigue al dibujo en todo:
/// balanceo, culatazo, escala de las manos y formato de pantalla.
///
/// - Una Image por hueco con el dibujo de la bala vista de atrás (ViewmodelProfile.chamberRim, fx_round_rim_01).
/// - Los huecos se ubican con ANCLAS en coordenadas del arte (px 1920x1080 / tamaño del arte): no hay que acomodar nada a mano.
///   Las posiciones las mide ViewmodelProfileBuilder del PNG (chambersOpen para reload_01, chambersInsert para reload_02).
/// - Solo se ve con el cuadro "abrir" o "meter" (ViewmodelAnimator.ReloadStage 0 o 1). Huecos llenos = balas cargadas,
///   en el orden de la lista. La bala que acaba de entrar hace un golpecito de escala 1.15 → 1.
/// - Sin posiciones o sin dibujo: no muestra nada (la recarga sigue funcionando igual).
/// </summary>
public class ViewmodelChamberOverlay : MonoBehaviour
{
    private const float ArtWidth = 1920f;
    private const float ArtHeight = 1080f;
    private const float PunchScale = 1.15f;
    private const float PunchSeconds = 0.12f;

    private RectTransform _root;
    private ViewmodelProfile _profile;
    private Image[] _rims = new Image[0];
    private int _shownStage = -2;
    private int _shownLoaded = -1;
    private int _punchIndex = -1;
    private float _punchTime = -1f;

    /// <summary>Crea el overlay como hijo de las manos (estirado a todo el dibujo).</summary>
    public static ViewmodelChamberOverlay Create(Image hands)
    {
        var go = new GameObject("ChamberOverlay", typeof(RectTransform));
        go.layer = hands.gameObject.layer;
        go.transform.SetParent(hands.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var overlay = go.AddComponent<ViewmodelChamberOverlay>();
        overlay._root = rt;
        return overlay;
    }

    public void SetProfile(ViewmodelProfile profile)
    {
        _profile = profile;
        int count = 0;
        if (profile != null && profile.chamberRim != null && profile.chamberDiameter > 0f)
            count = Mathf.Max(Length(profile.chambersOpen), Length(profile.chambersInsert));

        if (_rims.Length != count)
        {
            foreach (Image old in _rims)
                if (old != null) Destroy(old.gameObject);
            _rims = new Image[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Chamber_" + (i + 1), typeof(RectTransform));
                go.layer = gameObject.layer;
                go.transform.SetParent(_root, false);
                Image image = go.AddComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                image.enabled = false;
                _rims[i] = image;
            }
        }
        foreach (Image rim in _rims) rim.sprite = profile != null ? profile.chamberRim : null;
        _shownStage = -2;
        Hide();
    }

    /// <summary>Lo llama FirstPersonViewmodel cada frame: cuadro de recarga actual (−1 = ninguno) y balas cargadas.</summary>
    public void Refresh(int stage, int loaded)
    {
        if (_rims.Length == 0 || _profile == null) return;
        Vector2[] points = stage == 0 ? _profile.chambersOpen : stage == 1 ? _profile.chambersInsert : null;
        if (points == null || points.Length == 0)
        {
            if (_shownStage != -1) Hide();
            _shownStage = -1;
            return;
        }

        if (stage != _shownStage || loaded != _shownLoaded)
        {
            _shownStage = stage;
            _shownLoaded = loaded;
            float radius = _profile.chamberDiameter * 0.5f * _profile.chamberRimScale;
            for (int i = 0; i < _rims.Length; i++)
            {
                bool on = i < points.Length && i < loaded;
                _rims[i].enabled = on;
                if (!on) continue;
                RectTransform rt = _rims[i].rectTransform;
                Vector2 p = points[i];
                rt.anchorMin = new Vector2((p.x - radius) / ArtWidth, (p.y - radius) / ArtHeight);
                rt.anchorMax = new Vector2((p.x + radius) / ArtWidth, (p.y + radius) / ArtHeight);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
        }
        UpdatePunch();
    }

    /// <summary>Golpecito de la bala que acaba de entrar (índice en el orden de llenado).</summary>
    public void Punch(int index)
    {
        _punchIndex = index;
        _punchTime = 0f;
    }

    private void UpdatePunch()
    {
        if (_punchIndex < 0 || _punchIndex >= _rims.Length) return;
        _punchTime += ViewmodelTime.Delta;
        float t = Mathf.Clamp01(_punchTime / PunchSeconds);
        float s = Mathf.Lerp(PunchScale, 1f, t);
        _rims[_punchIndex].rectTransform.localScale = new Vector3(s, s, 1f);
        if (t >= 1f) _punchIndex = -1;
    }

    private void Hide()
    {
        foreach (Image rim in _rims)
        {
            if (rim == null) continue;
            rim.enabled = false;
            rim.rectTransform.localScale = Vector3.one;
        }
        _shownLoaded = -1;
    }

    private static int Length(Vector2[] array) => array != null ? array.Length : 0;
}
