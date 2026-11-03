using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD de Wilbur: vida y munición (fase HUD.1, H1–H8). Canvas propio Screen Space Overlay, sin UIManager.
/// DÓNDE VA: prefab Prefabs/UI/HUD_Wilbur (lo crea y lo coloca Plomo y Paja → HUD → Crear HUD; también "Generar Nivel Porky").
/// El prefab trae el Canvas y las referencias (dibujos, fuente, sonido); las piezas se arman en Awake, como la mira.
///
/// ORDEN (H1): mundo &lt; viewmodel &lt; HUD (4) &lt; mira (5) &lt; subtítulos de la IA (10) &lt; diálogo (100) &lt; interrogatorio (300).
/// LUGAR (H2): un solo grupo abajo a la izquierda (las manos están abajo a la derecha): cara, barra a su derecha y la fila de
/// munición debajo de la barra. CanvasScaler 1920x1080 con match = alto: el margen crece con el alto de la pantalla y el
/// grupo queda igual en 16:9, 16:10 y 21:9.
/// EVENTOS (H3): PlayerHealth (OnHealthChanged, OnDamaged, OnHealed), IWeapon (OnAmmoChanged, OnFired, recarga),
/// WeaponSwitcher.OnWeaponChanged y PlayerAmmo.OnReserveChanged. No se consulta nada por frame salvo los estados de
/// ocultado que no tienen eventos (UiHideReasons.SyncLegacyStates, 3 bool estáticos).
/// OCULTADO (H6): UiHideReasons. HideAll = todo invisible (sigue corriendo para que el latido se apague); HideAmmo = solo
/// la fila de munición (arma enfundada, INTRO.3).
/// Depuración (debug, apagada): F5 daño desde un lado al azar, F6 cura, F7 vacía la reserva del arma, F8 aviso de prueba.
/// </summary>
public class HudWilbur : MonoBehaviour
{
    [Header("Dibujos (Art/UI/HUD; vacío = placeholder)")]
    [Tooltip("hud_wilbur_01..04: > 75 %, > 50 %, > 25 %, ≤ 25 % de vida")]
    [SerializeField] private Sprite[] faces = new Sprite[4];
    [SerializeField] private Sprite faceHurt;
    [SerializeField] private Sprite shellFull;
    [SerializeField] private Sprite shellEmpty;
    [SerializeField] private Sprite bulletFull;
    [SerializeField] private Sprite bulletEmpty;
    [SerializeField] private Sprite barFrame;

    [Header("Texto y sonido")]
    [Tooltip("Special Elite SDF (Fonts/); vacío = la fuente por defecto de TMP")]
    [SerializeField] private TMP_FontAsset font;
    [Tooltip("AudioSource 2D para el loop del corazón (sfx_latido)")]
    [SerializeField] private AudioSource heartbeat;

    [Header("Lugar (px de canvas a 1080p)")]
    [SerializeField] private Vector2 margin = new Vector2(40f, 34f);
    [SerializeField] private float faceSize = 150f;
    [SerializeField] private Vector2 barSize = new Vector2(330f, 30f);
    [Tooltip("Vida ≤ esto (0..1): latido rojo y corazón")]
    [SerializeField, Range(0f, 1f)] private float lowHealth = 0.25f;

    [Header("Depuración (apagada por defecto)")]
    [SerializeField] private bool debug = false;

    /// <summary>El HUD de la escena (para avisos desde otros sistemas). Null si no hay.</summary>
    public static HudWilbur Current { get; private set; }

    /// <summary>Aviso corto sobre el grupo del HUD ("+6 cartuchos"). No hace nada si no hay HUD.</summary>
    public static void ShowToast(string text)
    {
        if (Current != null && Current._toast != null) Current._toast.Show(text);
    }

    private CanvasGroup _content;
    private CanvasGroup _ammoGroup;
    private HudHealthPanel _health;
    private HudAmmoRow _ammo;
    private HudFeedback _feedback;
    private HudToast _toast;

    private PlayerHealth _playerHealth;
    private PlayerAmmo _playerAmmo;
    private WeaponSwitcher _switcher;
    private IWeapon _weapon;
    private float _nextBindTry;
    private bool _bound;
    private bool _healthPrimed; // el primer valor de vida se pone sin animación

    private void Awake()
    {
        Current = this;
        Build();
        UiHideReasons.Changed += ApplyVisibility;
    }

    private void OnDestroy()
    {
        UiHideReasons.Changed -= ApplyVisibility;
        Unbind();
        if (Current == this) Current = null;
    }

    private void Start()
    {
        TryBind();
        ApplyVisibility();
    }

    // ============ Armado ============

    private void Build()
    {
        var root = (RectTransform)transform;

        // Avisos de pantalla completa (debajo del grupo)
        var feedbackRt = HudUi.Rect("Avisos", root, Vector2.zero);
        _content = root.gameObject.GetComponent<CanvasGroup>();
        if (_content == null) _content = root.gameObject.AddComponent<CanvasGroup>();
        _content.interactable = false;
        _content.blocksRaycasts = false;
        _feedback = feedbackRt.gameObject.AddComponent<HudFeedback>();
        _feedback.Init(heartbeat);

        // Grupo abajo a la izquierda
        RectTransform cluster = HudUi.Rect("Grupo", root, new Vector2(faceSize + 18f + barSize.x + 20f, faceSize));
        cluster.anchorMin = cluster.anchorMax = cluster.pivot = Vector2.zero;
        cluster.anchoredPosition = margin;

        RectTransform healthRt = HudUi.Rect("Vida", cluster, Vector2.zero);
        healthRt.anchorMin = healthRt.anchorMax = healthRt.pivot = Vector2.zero;
        _health = healthRt.gameObject.AddComponent<HudHealthPanel>();
        _health.Init(faces, faceHurt, barFrame, font, faceSize, barSize);

        RectTransform ammoGroupRt = HudUi.Rect("MunicionGrupo", cluster, Vector2.zero);
        ammoGroupRt.anchorMin = ammoGroupRt.anchorMax = ammoGroupRt.pivot = Vector2.zero;
        _ammoGroup = ammoGroupRt.gameObject.AddComponent<CanvasGroup>();
        _ammoGroup.blocksRaycasts = false;
        RectTransform ammoRt = HudUi.Rect("Municion", ammoGroupRt, Vector2.zero);
        ammoRt.anchorMin = ammoRt.anchorMax = ammoRt.pivot = Vector2.zero;
        ammoRt.anchoredPosition = new Vector2(faceSize + 18f, 6f); // debajo de la barra
        _ammo = ammoRt.gameObject.AddComponent<HudAmmoRow>();
        _ammo.Init(shellFull, shellEmpty, bulletFull, bulletEmpty, font);

        RectTransform toastRt = HudUi.Rect("AvisosCortos", cluster, Vector2.zero);
        toastRt.anchorMin = toastRt.anchorMax = toastRt.pivot = Vector2.zero;
        toastRt.anchoredPosition = new Vector2(0f, faceSize + 16f);
        _toast = toastRt.gameObject.AddComponent<HudToast>();
        _toast.Init(font);
    }

    // ============ Jugador ============

    private void TryBind()
    {
        if (_bound) return;
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;
        _playerHealth = player.GetComponentInParent<PlayerHealth>();
        _playerAmmo = player.GetComponentInParent<PlayerAmmo>();
        _switcher = player.GetComponentInParent<WeaponSwitcher>();
        if (_playerHealth == null) return;

        _playerHealth.OnHealthChanged += HandleHealthChanged;
        _playerHealth.OnDamaged += HandleDamaged;
        _playerHealth.OnHealed += HandleHealed;
        if (_playerAmmo != null) _playerAmmo.OnReserveChanged += HandleReserveChanged;
        if (_switcher != null) _switcher.OnWeaponChanged += HandleWeaponChanged;
        _bound = true;

        // PlayerHealth llena la vida en su Start: si todavía está en 0 (y no murió), se espera su primer aviso
        if (_playerHealth.CurrentHealth > 0f || _playerHealth.IsDead)
            HandleHealthChanged(_playerHealth.CurrentHealth, _playerHealth.MaxHealth);
        IWeapon weapon = _switcher != null ? _switcher.ActiveWeapon : player.GetComponentInChildren<IWeapon>();
        BindWeapon(weapon, false);
    }

    private void Unbind()
    {
        if (_playerHealth != null)
        {
            _playerHealth.OnHealthChanged -= HandleHealthChanged;
            _playerHealth.OnDamaged -= HandleDamaged;
            _playerHealth.OnHealed -= HandleHealed;
        }
        if (_playerAmmo != null) _playerAmmo.OnReserveChanged -= HandleReserveChanged;
        if (_switcher != null) _switcher.OnWeaponChanged -= HandleWeaponChanged;
        BindWeapon(null, false);
        _bound = false;
    }

    private void BindWeapon(IWeapon weapon, bool animate)
    {
        if (_weapon != null)
        {
            _weapon.OnAmmoChanged -= HandleAmmoChanged;
            _weapon.OnReloadStarted -= HandleReloadState;
            _weapon.OnReloadFinished -= HandleReloadState;
        }
        _weapon = weapon;
        if (_weapon != null)
        {
            _weapon.OnAmmoChanged += HandleAmmoChanged;
            _weapon.OnReloadStarted += HandleReloadState;
            _weapon.OnReloadFinished += HandleReloadState;
        }
        if (_ammo != null) _ammo.Bind(weapon, animate);
    }

    private void HandleHealthChanged(float current, float max)
    {
        float percent = max > 0f ? current / max : 0f;
        _health.SetValue(percent, !_healthPrimed);
        _healthPrimed = true;
        _feedback.SetLowHealth(percent > 0f && percent <= lowHealth);
    }

    private void HandleDamaged(float amount, Vector3? source)
    {
        _health.Hit();
        _feedback.Damaged(source);
    }

    private void HandleHealed(float amount)
    {
        _health.Healed();
        _feedback.Healed();
    }

    private void HandleWeaponChanged(IWeapon weapon) => BindWeapon(weapon, true);
    private void HandleAmmoChanged() => _ammo.OnAmmoChanged();
    private void HandleReloadState() => _ammo.Refresh();
    private void HandleReserveChanged(AmmoType type, int reserve)
    {
        if (_weapon != null && _weapon.AmmoType == type) _ammo.Refresh();
    }

    // ============ Ocultado (H6) ============

    private void ApplyVisibility()
    {
        bool all = !UiHideReasons.HideAll;
        bool ammo = !UiHideReasons.HideAmmo;
        if (_content != null) _content.alpha = all ? 1f : 0f;
        if (_ammoGroup != null) _ammoGroup.alpha = ammo ? 1f : 0f;
        if (_feedback != null) _feedback.SetVisible(all);
    }

    private void Update()
    {
        UiHideReasons.SyncLegacyStates();
        if (!_bound && Time.unscaledTime >= _nextBindTry)
        {
            _nextBindTry = Time.unscaledTime + 1f;
            TryBind();
        }
        if (debug) DebugKeys();
    }

    private void DebugKeys()
    {
        if (_playerHealth == null) return;
        if (Input.GetKeyDown(KeyCode.F5))
        {
            Vector2 r = Random.insideUnitCircle.normalized * 6f;
            _playerHealth.TakeDamage(10f, _playerHealth.transform.position + new Vector3(r.x, 1f, r.y));
        }
        if (Input.GetKeyDown(KeyCode.F6)) _playerHealth.Heal(25f);
        if (Input.GetKeyDown(KeyCode.F7) && _playerAmmo != null && _weapon != null) _playerAmmo.Take(_weapon.AmmoType, 999);
        if (Input.GetKeyDown(KeyCode.F8)) ShowToast("+6 cartuchos");
    }
}

/// <summary>Piezas de UI del HUD (fase HUD.1). Todas sin raycast.</summary>
public static class HudUi
{
    public static RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size;
        return rt;
    }

    public static Image Image(string name, Transform parent, Sprite sprite, Vector2 size)
    {
        RectTransform rt = Rect(name, parent, size);
        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    public static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size,
                                       TextAlignmentOptions alignment, Color color)
    {
        RectTransform rt = Rect(name, parent, new Vector2(200f, size * 1.4f));
        TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.richText = true;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(20, 14, 12, 255);
        return text;
    }

    /// <summary>Estira a todo el padre con un margen (negativo = sobresale).</summary>
    public static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }
}
