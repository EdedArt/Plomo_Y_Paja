using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fila de munición del HUD (fase HUD.1, H2/H8). La crea HudWilbur y la mueve HudWilbur con los eventos del arma (H3).
/// - Un ícono por lugar del cargador: hud_cartucho_lleno / _vacio (Shells, escopeta) o hud_bala_llena / _vacia (Cylinder,
///   revólver). Sin dibujos: rectángulos de color (rojo / gris, dorado / marrón).
/// - Disparo: el ícono gastado hace un golpecito 1.2 → 1 en 0.1 s. Recarga bala por bala (revólver): cada bala que entra
///   hace el golpecito. Recarga de una vez (escopeta): los íconos se llenan en cascada, 0.04 s cada uno.
/// - Con 2 balas o menos, las llenas se tiñen de rojo. Encima: "¡RECARGA!" (vacía con reserva) o "SIN MUNICIÓN" (vacía y sin
///   reserva), titilando. Al lado: la reserva "×18".
/// - Cambio de arma: la fila entra deslizándose en 0.2 s.
/// - HUD.1.1: medallón de la escopeta (hud_medallon_balas de Edgar: tres cartuchos en el mismo marco redondo que la cara) a
///   la izquierda de la fila, solo con la escopeta (Shells). Sin ese dibujo, la fila empieza en x = 0 como antes.
/// Sin basura por frame: los textos se rehacen solo cuando cambia el valor.
/// </summary>
public class HudAmmoRow : MonoBehaviour
{
    private const int MaxIcons = 8;
    private const float PopSeconds = 0.1f;
    private const float PopScale = 1.2f;
    private const float CascadeStep = 0.04f;
    private const float SlideSeconds = 0.2f;
    private const float SlideDistance = 50f;
    private const float MedallionSize = 58f;
    private const float MedallionGap = 8f;

    public const string ReloadText = "¡RECARGA!";
    public const string EmptyText = "SIN MUNICIÓN";

    private static readonly Color LowTint = new Color(1f, 0.45f, 0.4f, 1f);

    private RectTransform _root;
    private CanvasGroup _group;
    private RectTransform _iconsRoot;
    private readonly Image[] _icons = new Image[MaxIcons];
    private TextMeshProUGUI _reserve;
    private TextMeshProUGUI _state;
    private Image _medallion;

    private Sprite _shellFull, _shellEmpty, _bulletFull, _bulletEmpty;
    private IWeapon _weapon;
    private int _magazine;
    private int _shownLoaded;
    private int _targetLoaded;
    private float _cascadeTimer;
    private int _shownReserve = -1;
    private int _popIndex = -1;
    private float _popTimer;
    private float _slide;
    private int _stateShown; // 0 nada, 1 recarga, 2 sin munición
    private Vector2 _basePos;

    public void Init(Sprite shellFull, Sprite shellEmpty, Sprite bulletFull, Sprite bulletEmpty, TMP_FontAsset font,
                     Sprite shellMedallion = null)
    {
        _root = (RectTransform)transform;
        _basePos = _root.anchoredPosition;
        _group = gameObject.AddComponent<CanvasGroup>();
        _group.interactable = false;
        _group.blocksRaycasts = false;
        _shellFull = shellFull;
        _shellEmpty = shellEmpty;
        _bulletFull = bulletFull;
        _bulletEmpty = bulletEmpty;

        if (shellMedallion != null)
        {
            _medallion = HudUi.Image("Medallon", _root, shellMedallion, new Vector2(MedallionSize, MedallionSize));
            RectTransform mrt = _medallion.rectTransform;
            mrt.anchorMin = mrt.anchorMax = mrt.pivot = Vector2.zero;
            mrt.anchoredPosition = new Vector2(0f, -8f); // centrado con los cartuchos (46 px); el borde de arriba no pisa "¡RECARGA!" (y 50)
            _medallion.preserveAspect = true;
            _medallion.gameObject.SetActive(false);
        }

        _iconsRoot = HudUi.Rect("Iconos", _root, Vector2.zero);
        _iconsRoot.anchorMin = _iconsRoot.anchorMax = _iconsRoot.pivot = Vector2.zero;
        for (int i = 0; i < MaxIcons; i++)
        {
            _icons[i] = HudUi.Image("Icono_" + (i + 1), _iconsRoot, null, new Vector2(26f, 44f));
            RectTransform rt = _icons[i].rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0f);
            _icons[i].preserveAspect = true;
            _icons[i].gameObject.SetActive(false);
        }

        _reserve = HudUi.Text("Reserva", _root, font, 34f, TextAlignmentOptions.Left, new Color32(0xF5, 0xEB, 0xD0, 0xFF));
        RectTransform reserveRt = _reserve.rectTransform;
        reserveRt.anchorMin = reserveRt.anchorMax = Vector2.zero;
        reserveRt.pivot = new Vector2(0f, 0f);
        reserveRt.sizeDelta = new Vector2(140f, 46f);

        _state = HudUi.Text("Estado", _root, font, 28f, TextAlignmentOptions.Left, new Color32(0xFF, 0xD2, 0x3F, 0xFF));
        RectTransform stateRt = _state.rectTransform;
        stateRt.anchorMin = stateRt.anchorMax = Vector2.zero;
        stateRt.pivot = new Vector2(0f, 0f);
        stateRt.anchoredPosition = new Vector2(0f, 50f);
        stateRt.sizeDelta = new Vector2(360f, 36f);
        _state.enabled = false;
    }

    /// <summary>Arma activa nueva. animate = entra deslizándose (cambio de arma).</summary>
    public void Bind(IWeapon weapon, bool animate)
    {
        _weapon = weapon;
        _shownReserve = -1;
        _popIndex = -1;
        if (weapon == null)
        {
            for (int i = 0; i < MaxIcons; i++) _icons[i].gameObject.SetActive(false);
            if (_medallion != null) _medallion.gameObject.SetActive(false);
            _reserve.text = "";
            _state.enabled = false;
            return;
        }

        bool cylinder = weapon.DisplayStyle == AmmoDisplayStyle.Cylinder;
        _magazine = Mathf.Clamp(weapon.MagazineSize, 0, MaxIcons);
        Vector2 size = cylinder ? new Vector2(22f, 42f) : new Vector2(26f, 46f);
        float spacing = cylinder ? 26f : 32f;
        bool medallion = _medallion != null && !cylinder;
        if (_medallion != null) _medallion.gameObject.SetActive(medallion);
        _iconsRoot.anchoredPosition = new Vector2(medallion ? MedallionSize + MedallionGap : 0f, 0f);
        for (int i = 0; i < MaxIcons; i++)
        {
            bool used = i < _magazine;
            _icons[i].gameObject.SetActive(used);
            if (!used) continue;
            RectTransform rt = _icons[i].rectTransform;
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(size.x * 0.5f + i * spacing, 0f);
            rt.localScale = Vector3.one;
        }
        _reserve.rectTransform.anchoredPosition = new Vector2(_iconsRoot.anchoredPosition.x + _magazine * spacing + 10f, 0f);
        _shownLoaded = _targetLoaded = weapon.LoadedRounds;
        RefreshIcons();
        Refresh();
        if (animate) _slide = SlideSeconds;
    }

    /// <summary>Cambiaron las balas cargadas (IWeapon.OnAmmoChanged).</summary>
    public void OnAmmoChanged()
    {
        if (_weapon == null) return;
        int loaded = Mathf.Clamp(_weapon.LoadedRounds, 0, _magazine);
        if (loaded > _targetLoaded + 1 || (loaded > _shownLoaded + 1))
        {
            // Recarga de una vez: cascada desde lo que se ve
            _targetLoaded = loaded;
            _cascadeTimer = CascadeStep;
        }
        else
        {
            if (loaded > _shownLoaded) Pop(loaded - 1);   // entró una bala
            else if (loaded < _shownLoaded) Pop(loaded);  // se gastó una
            _shownLoaded = _targetLoaded = loaded;
            RefreshIcons();
        }
        Refresh();
    }

    /// <summary>Reserva, recarga o estado cambiaron: rehace el texto de la reserva (solo si cambió) y el aviso.</summary>
    public void Refresh()
    {
        if (_weapon == null) return;
        int reserve = _weapon.ReserveRounds;
        if (reserve != _shownReserve)
        {
            _shownReserve = reserve;
            // "×" no está en Special Elite (fuente estática): sale de LiberationSans, que trae TMP
            _reserve.text = "<font=\"LiberationSans SDF\">×</font>" + reserve;
        }
        int loaded = _weapon.LoadedRounds;
        int state = loaded > 0 || _weapon.IsReloading ? 0 : reserve > 0 ? 1 : 2;
        if (state != _stateShown)
        {
            _stateShown = state;
            _state.enabled = state != 0;
            if (state == 1) _state.text = ReloadText;
            else if (state == 2) _state.text = EmptyText;
        }
    }

    private void Pop(int index)
    {
        if (index < 0 || index >= _magazine) return;
        if (_popIndex >= 0 && _popIndex != index) _icons[_popIndex].rectTransform.localScale = Vector3.one;
        _popIndex = index;
        _popTimer = PopSeconds;
    }

    private void RefreshIcons()
    {
        bool cylinder = _weapon != null && _weapon.DisplayStyle == AmmoDisplayStyle.Cylinder;
        Sprite full = cylinder ? _bulletFull : _shellFull;
        Sprite empty = cylinder ? _bulletEmpty : _shellEmpty;
        Color fullColor = full != null ? Color.white : cylinder ? new Color32(0xE8, 0xB8, 0x3A, 0xFF) : new Color32(0xC8, 0x32, 0x2A, 0xFF);
        Color emptyColor = empty != null ? Color.white : new Color32(0x5A, 0x50, 0x48, 0xC0);
        bool low = _shownLoaded <= 2;
        for (int i = 0; i < _magazine; i++)
        {
            bool isFull = i < _shownLoaded;
            Image icon = _icons[i];
            Sprite sprite = isFull ? (full != null ? full : HudSprites.Panel) : (empty != null ? empty : HudSprites.Panel);
            if (icon.sprite != sprite) icon.sprite = sprite;
            icon.type = sprite == HudSprites.Panel ? Image.Type.Sliced : Image.Type.Simple;
            Color c = isFull ? fullColor : emptyColor;
            if (isFull && low) c *= LowTint;
            icon.color = c;
        }
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (_shownLoaded < _targetLoaded)
        {
            _cascadeTimer -= dt;
            if (_cascadeTimer <= 0f)
            {
                _cascadeTimer = CascadeStep;
                _shownLoaded++;
                Pop(_shownLoaded - 1);
                RefreshIcons();
            }
        }

        if (_popIndex >= 0)
        {
            _popTimer -= dt;
            float t = 1f - Mathf.Clamp01(_popTimer / PopSeconds);
            float s = Mathf.Lerp(PopScale, 1f, t);
            _icons[_popIndex].rectTransform.localScale = new Vector3(s, s, 1f);
            if (_popTimer <= 0f) _popIndex = -1;
        }

        if (_state.enabled)
        {
            float a = Mathf.Repeat(Time.unscaledTime * 2f, 1f) < 0.6f ? 1f : 0.25f;
            if (!Mathf.Approximately(_state.alpha, a)) _state.alpha = a;
        }

        if (_slide > 0f)
        {
            _slide = Mathf.Max(0f, _slide - dt);
            float t = 1f - _slide / SlideSeconds;
            float e = 1f - (1f - t) * (1f - t);
            _root.anchoredPosition = _basePos + new Vector2(-SlideDistance * (1f - e), 0f);
            _group.alpha = e;
        }
    }
}
