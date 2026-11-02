using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mira "rubber-hose" del HUD, estilo dibujo animado de los 30: un punto redondo blanco y 4 marcas en GOTA (el extremo gordo y
/// redondo hacia afuera, la punta fina apuntando al centro), relleno blanco y contorno oscuro de ~2 px. ~44 px en total a 1080p.
/// DÓNDE VA: GameObject "Player" (lo añade "Crear Prefabs de Prueba"). Construye su propio canvas Screen Space - Overlay
/// (HUD: sin grano ni viñeta) porque UIManager no existe en ninguna escena; no depende de él.
///
/// - Los disparos salen del CENTRO de la cámara principal (ShotgunController.ShootPellet: origen = posición de la cámara,
///   dirección = su forward + dispersión), así que el punto central es exactamente adonde van los perdigones.
/// - Las marcas se abren con la dispersión del arma (IWeapon.SpreadAngle, convertida a píxeles con el FOV de la cámara y
///   escalada por spreadVisualScale; es una indicación, no la dispersión real) y un poco más si el jugador se mueve rápido
///   (adorno, la dispersión real no cambia al caminar). Al disparar rebotan hacia afuera un 40 % con un poco de "squash and
///   stretch" (se alargan y se adelgazan) y vuelven en 0.12 s (tiempo real).
/// - Se oculta al apuntar (PlayerAim), en diálogo, pausa y muerte.
/// - Marcador de impacto: las marcas se ponen rojas 0.1 s cuando un enemigo recibe daño (EnemyHealth.OnDamaged).
/// - Munición (3C.5): debajo de la mira, una fila de puntos chicos = balas del arma activa (llenos = cargadas, tenues =
///   vacías) y "+N" = reserva. No hay HUD de munición (3B parte 4 pendiente); esto es lo mínimo. Sigue visible al apuntar.
/// - Avisos (3C.5): ShowNotice(texto) (ej: "Revólver de Centavo") y SetPrompt(texto) (ej: "[E] Recoger el revólver"),
///   en el mismo canvas porque el aviso de pistas y el prompt de UIManager no existen en las escenas.
/// - WeaponSwitcher llama SetWeapon al cambiar de arma: las marcas leen la dispersión del arma activa.
/// Los sprites se generan por código (sin PNG): una distancia con signo por forma, antialias de medio píxel de textura.
/// CÓMO AJUSTAR EL DIBUJO: las constantes de arriba (radios y largo en px a 1080p) y baseRadius en el inspector.
/// </summary>
public class RubberHoseCrosshair : MonoBehaviour
{
    // Tamaños en píxeles de referencia 1080p
    private const float DotWhiteRadius = 3.0f;      // punto central (blanco)
    private const float TickTipRadius = 0.7f;       // radio blanco de la punta fina de la gota (hacia el centro)
    private const float TickFatRadius = 3.8f;       // radio blanco del extremo gordo (hacia afuera)
    private const float TickWhiteLength = 10.4f;    // largo blanco de la gota, de punta a extremo gordo
    private const float OutlineThickness = 2.0f;    // contorno oscuro, alrededor de todo
    private const float TexDensity = 6f;            // píxeles de textura por píxel de pantalla (nítido y con antialias)
    private const float SpriteMargin = 2f;          // margen transparente del sprite (1 px por lado)
    private const byte OutlineLevel = 22;           // gris del contorno (casi negro, sin ser negro puro)
    private const float PopStretch = 0.16f;         // al rebotar: la gota se alarga un 16 %...
    private const float PopSquash = 0.12f;          // ...y se adelgaza un 12 %

    [SerializeField] private int sortingOrder = 5;
    [SerializeField] private Color fillColor = Color.white;
    [SerializeField] private Color hitColor = new Color(1f, 0.15f, 0.15f, 1f);
    [Tooltip("Distancia del centro a la punta de las gotas sin dispersión (px a 1080p). 7.6 → mira de ~44 px en total")]
    [SerializeField] private float baseRadius = 7.6f;
    [Tooltip("Cuánto de la dispersión real se muestra en las marcas (1 = a escala real)")]
    [SerializeField] private float spreadVisualScale = 0.3f;
    [Tooltip("Px extra de apertura caminando a la velocidad de referencia (adorno)")]
    [SerializeField] private float movingSpread = 6f;
    [SerializeField] private float referenceSpeed = 4f;
    [Tooltip("Apertura extra al disparar (0.4 = 40 %) y segundos que tarda en volver")]
    [SerializeField] private float popAmount = 0.4f;
    [SerializeField] private float popDuration = 0.12f;
    [Tooltip("Segundos que las marcas se ven rojas al acertar a un enemigo")]
    [SerializeField] private float hitDuration = 0.1f;
    [Tooltip("Suavizado de la apertura (más alto = sigue más rápido)")]
    [SerializeField] private float smoothing = 18f;

    [Header("Munición y avisos (3C.5)")]
    [SerializeField] private bool showAmmo = true;
    [Tooltip("Distancia (px a 1080p) del centro a la fila de balas, hacia abajo")]
    [SerializeField] private float ammoOffset = 34f;
    [SerializeField] private float ammoSpacing = 11f;
    [SerializeField, Range(0f, 1f)] private float emptyRoundAlpha = 0.22f;
    [SerializeField] private float noticeSeconds = 2.5f;

    /// <summary>La mira del jugador (para avisos de recogibles). Null si no hay.</summary>
    public static RubberHoseCrosshair Current { get; private set; }

    private IWeapon _weapon;
    private PlayerAim _aim;
    private Camera _camera;
    private CharacterController _body;
    private Canvas _canvas;
    private RectTransform _crosshairRoot;
    private RectTransform _ammoRoot;
    private readonly List<Image> _rounds = new List<Image>();
    private TextMeshProUGUI _reserveText;
    private TextMeshProUGUI _noticeText;
    private TextMeshProUGUI _promptText;
    private float _noticeUntil = -1f;
    private int _shownLoaded = -1, _shownMagazine = -1, _shownReserve = -1;
    private readonly RectTransform[] _tickRects = new RectTransform[4];
    private readonly Image[] _ticks = new Image[4];
    private float _tickLength;
    private Color _tickColor = Color.white;

    private float _radius;
    private float _pop;          // 1 → 0 tras disparar
    private float _hitTimer;     // > 0 mientras las marcas están rojas
    private bool _dead;
    private float _nextScan;
    private readonly HashSet<EnemyHealth> _watched = new HashSet<EnemyHealth>();

    private static Sprite _dotSprite;
    private static Sprite _tickSprite;

    private void Awake()
    {
        _aim = GetComponent<PlayerAim>();
        _weapon = GetComponentInChildren<IWeapon>(true);
        TryGetComponent(out _body);
        foreach (Camera cam in GetComponentsInChildren<Camera>(true))
            if (cam.name != ViewmodelCameraRig.CameraName) { _camera = cam; break; }
        if (_camera == null) _camera = Camera.main;
        if (TryGetComponent(out PlayerHealth health)) health.OnDeath += HandleDeath;
        BuildUi();
        _radius = baseRadius;
        Current = this;
    }

    /// <summary>Arma activa (WeaponSwitcher). Mueve la suscripción a OnFired y la fila de munición.</summary>
    public void SetWeapon(IWeapon weapon)
    {
        if (weapon == _weapon) return;
        if (isActiveAndEnabled && _weapon != null) _weapon.OnFired -= HandleFired;
        _weapon = weapon;
        if (isActiveAndEnabled && _weapon != null) _weapon.OnFired += HandleFired;
        _shownMagazine = -1; // rehacer la fila
    }

    /// <summary>Aviso corto bajo la mira (tiempo real).</summary>
    public void ShowNotice(string text)
    {
        if (_noticeText == null) return;
        _noticeText.text = text;
        _noticeText.enabled = true;
        _noticeUntil = Time.unscaledTime + noticeSeconds;
    }

    /// <summary>Texto de interacción bajo la mira; null o vacío lo oculta.</summary>
    public void SetPrompt(string text)
    {
        if (_promptText == null) return;
        bool on = !string.IsNullOrEmpty(text);
        if (on && _promptText.text != text) _promptText.text = text;
        if (_promptText.enabled != on) _promptText.enabled = on;
    }

    private void OnEnable()
    {
        if (_weapon != null) _weapon.OnFired += HandleFired;
    }

    private void OnDisable()
    {
        if (_weapon != null) _weapon.OnFired -= HandleFired;
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
        foreach (EnemyHealth enemy in _watched)
            if (enemy != null) enemy.OnDamaged -= HandleEnemyDamaged;
        _watched.Clear();
    }

    private void HandleDeath() => _dead = true;

    private void HandleFired() => _pop = 1f;

    private void HandleEnemyDamaged(float damage) => _hitTimer = hitDuration;

    // ============ Construcción ============

    private void BuildUi()
    {
        EnsureSprites();

        var canvasGo = new GameObject("CrosshairCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay; // HUD: URP lo dibuja después del post-procesado
        _canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f; // 44 px a 1080p en cualquier ventana de ese alto

        var rootGo = new GameObject("Crosshair", typeof(RectTransform));
        rootGo.transform.SetParent(canvasGo.transform, false);
        var root = (RectTransform)rootGo.transform;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;
        _crosshairRoot = root;

        // Munición y avisos: hermanos de la mira (siguen visibles al apuntar)
        var ammoGo = new GameObject("Ammo", typeof(RectTransform));
        ammoGo.transform.SetParent(canvasGo.transform, false);
        _ammoRoot = (RectTransform)ammoGo.transform;
        _ammoRoot.anchorMin = _ammoRoot.anchorMax = _ammoRoot.pivot = new Vector2(0.5f, 0.5f);
        _ammoRoot.anchoredPosition = new Vector2(0f, -ammoOffset);
        _reserveText = CreateText("Reserve", _ammoRoot, 15f, TextAlignmentOptions.Left);
        _noticeText = CreateText("Notice", canvasGo.transform, 30f, TextAlignmentOptions.Center);
        _noticeText.rectTransform.anchoredPosition = new Vector2(0f, -120f);
        _noticeText.enabled = false;
        _promptText = CreateText("Prompt", canvasGo.transform, 22f, TextAlignmentOptions.Center);
        _promptText.rectTransform.anchoredPosition = new Vector2(0f, -78f);
        _promptText.enabled = false;

        // Punto central
        float dotSize = (DotWhiteRadius + OutlineThickness) * 2f + SpriteMargin;
        CreateImage("Dot", root, _dotSprite, new Vector2(dotSize, dotSize));

        // Gotas: arriba, derecha, abajo, izquierda (el sprite apunta hacia arriba y se gira 0/-90/180/90 grados)
        float tickWidth = (TickFatRadius + OutlineThickness) * 2f + SpriteMargin;
        _tickLength = TickWhiteLength + OutlineThickness * 2f; // el dibujo, sin el margen transparente
        float[] angles = { 0f, -90f, 180f, 90f };
        for (int i = 0; i < 4; i++)
        {
            Image tick = CreateImage("Tick" + i, root, _tickSprite, new Vector2(tickWidth, _tickLength + SpriteMargin));
            tick.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angles[i]);
            _ticks[i] = tick;
            _tickRects[i] = tick.rectTransform;
        }
        PlaceTicks(baseRadius, 0f);
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        RectTransform rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, float size, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color32(22, 22, 22, 255);
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        bool left = alignment == TextAlignmentOptions.Left;
        RectTransform rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(left ? 0f : 0.5f, 0.5f);
        rt.sizeDelta = new Vector2(left ? 60f : 900f, size * 1.4f);
        return text;
    }

    /// <summary>Fila de balas del arma activa: un punto por bala del cargador, lleno = cargada. "+N" = reserva.</summary>
    private void UpdateAmmo()
    {
        bool show = showAmmo && _weapon != null;
        if (_ammoRoot.gameObject.activeSelf != show) _ammoRoot.gameObject.SetActive(show);
        if (!show) return;

        int magazine = Mathf.Clamp(_weapon.MagazineSize, 0, 12);
        int loaded = _weapon.LoadedRounds;
        int reserve = _weapon.ReserveRounds;
        if (magazine == _shownMagazine && loaded == _shownLoaded && reserve == _shownReserve) return;

        if (magazine != _shownMagazine)
        {
            float pip = ((DotWhiteRadius + OutlineThickness) * 2f + SpriteMargin) * 0.8f;
            while (_rounds.Count < magazine)
                _rounds.Add(CreateImage("Round" + _rounds.Count, _ammoRoot, _dotSprite, new Vector2(pip, pip)));
            float start = -(magazine - 1) * ammoSpacing * 0.5f;
            for (int i = 0; i < _rounds.Count; i++)
            {
                bool used = i < magazine;
                _rounds[i].gameObject.SetActive(used);
                if (used) _rounds[i].rectTransform.anchoredPosition = new Vector2(start + i * ammoSpacing, 0f);
            }
            _reserveText.rectTransform.anchoredPosition = new Vector2(-start + ammoSpacing * 0.9f, 0f);
        }
        for (int i = 0; i < magazine; i++)
            _rounds[i].color = new Color(1f, 1f, 1f, i < loaded ? 1f : emptyRoundAlpha);
        _reserveText.text = "+" + reserve;
        _shownMagazine = magazine;
        _shownLoaded = loaded;
        _shownReserve = reserve;
    }

    /// <summary>radius = distancia del centro a la punta de cada gota; pop 0..1 la alarga y la adelgaza (squash and stretch).</summary>
    private void PlaceTicks(float radius, float pop)
    {
        float stretch = 1f + PopStretch * pop;
        float squash = 1f - PopSquash * pop;
        float d = radius + _tickLength * 0.5f * stretch; // la punta fina se queda en `radius`; el extremo gordo es el que sale
        var scale = new Vector3(squash, stretch, 1f);
        for (int i = 0; i < 4; i++) _tickRects[i].localScale = scale;
        _tickRects[0].anchoredPosition = new Vector2(0f, d);
        _tickRects[1].anchoredPosition = new Vector2(d, 0f);
        _tickRects[2].anchoredPosition = new Vector2(0f, -d);
        _tickRects[3].anchoredPosition = new Vector2(-d, 0f);
    }

    // ============ Cada frame ============

    private void Update()
    {
        bool blocked = _dead || PlayerAim.GameplayBlocked();
        if (_canvas.enabled == blocked) _canvas.enabled = !blocked;
        if (blocked) return;

        UpdateAmmo();
        if (_noticeUntil > 0f && Time.unscaledTime >= _noticeUntil)
        {
            _noticeUntil = -1f;
            _noticeText.enabled = false;
        }

        bool hidden = _aim != null && _aim.IsAiming;
        if (_crosshairRoot.gameObject.activeSelf == hidden) _crosshairRoot.gameObject.SetActive(!hidden);
        if (hidden) return;

        float dt = ViewmodelTime.Delta;
        if (Time.unscaledTime >= _nextScan) ScanEnemies();

        _pop = Mathf.MoveTowards(_pop, 0f, dt / Mathf.Max(0.01f, popDuration));
        if (_hitTimer > 0f) _hitTimer -= dt;
        Color tickColor = _hitTimer > 0f ? hitColor : fillColor;
        if (tickColor != _tickColor)
        {
            _tickColor = tickColor;
            for (int i = 0; i < 4; i++) _ticks[i].color = tickColor;
        }

        // Apertura objetivo: dispersión del arma en píxeles (tan del ángulo / tan(FOV/2) × medio alto de 1080) + caminando
        float spreadPx = 0f;
        if (_weapon != null && _camera != null)
        {
            float offset = 0.01f * _weapon.SpreadAngle; // ShootPellet suma (rand·0.01) al forward unitario
            float halfFov = _camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            spreadPx = offset / Mathf.Tan(halfFov) * 540f;
        }
        float speed01 = 0f;
        if (_body != null)
        {
            Vector3 v = _body.velocity;
            v.y = 0f;
            speed01 = Mathf.Clamp01(v.magnitude / Mathf.Max(0.1f, referenceSpeed));
        }
        float target = baseRadius + spreadPx * spreadVisualScale + movingSpread * speed01;
        _radius = Mathf.Lerp(_radius, target, 1f - Mathf.Exp(-smoothing * dt));

        float eased = _pop * _pop; // sale rápido y vuelve suave
        PlaceTicks(_radius * (1f + popAmount * eased), eased);
    }

    /// <summary>Se suscribe a los enemigos que haya (y a los que aparezcan después) para el marcador de impacto.</summary>
    private void ScanEnemies()
    {
        _nextScan = Time.unscaledTime + 2f;
        foreach (EnemyHealth enemy in FindObjectsOfType<EnemyHealth>())
        {
            if (enemy == null || !_watched.Add(enemy)) continue;
            enemy.OnDamaged += HandleEnemyDamaged;
        }
    }

    // ============ Sprites procedurales ============

    private static void EnsureSprites()
    {
        if (_dotSprite != null && _tickSprite != null) return;

        float dotUnits = (DotWhiteRadius + OutlineThickness) * 2f + SpriteMargin;
        _dotSprite = BuildSprite("CrosshairDot", dotUnits, dotUnits, p => p.magnitude - DotWhiteRadius);

        float tickW = (TickFatRadius + OutlineThickness) * 2f + SpriteMargin;
        float tickH = TickWhiteLength + OutlineThickness * 2f + SpriteMargin;
        float h = TickWhiteLength - TickTipRadius - TickFatRadius;          // entre el centro de la punta y el del extremo gordo
        float tipCenterY = -TickWhiteLength * 0.5f + TickTipRadius;          // la punta fina abajo, el extremo gordo arriba
        _tickSprite = BuildSprite("CrosshairTick", tickW, tickH,
            p => UnevenCapsule(new Vector2(p.x, p.y - tipCenterY), TickTipRadius, TickFatRadius, h));
    }

    /// <summary>Distancia con signo a una cápsula con dos radios (r1 en el origen, r2 en (0, h)): la forma de gota.</summary>
    private static float UnevenCapsule(Vector2 p, float r1, float r2, float h)
    {
        p.x = Mathf.Abs(p.x);
        float b = (r1 - r2) / h;
        float a = Mathf.Sqrt(1f - b * b);
        float k = Vector2.Dot(p, new Vector2(-b, a));
        if (k < 0f) return p.magnitude - r1;
        if (k > a * h) return (p - new Vector2(0f, h)).magnitude - r2;
        return Vector2.Dot(p, new Vector2(a, b)) - r1;
    }

    /// <summary>
    /// Textura RGBA a partir de una distancia con signo (en px de pantalla; negativa = dentro): relleno blanco donde d ≤ 0 y
    /// contorno oscuro hasta d = OutlineThickness, con antialias de medio píxel de textura. 1 px de margen transparente por lado.
    /// </summary>
    private static Sprite BuildSprite(string name, float unitsW, float unitsH, System.Func<Vector2, float> signedDistance)
    {
        int w = Mathf.CeilToInt(unitsW * TexDensity), h = Mathf.CeilToInt(unitsH * TexDensity);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color32[w * h];
        float aa = 0.5f / TexDensity;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2((x + 0.5f) / TexDensity - w * 0.5f / TexDensity, (y + 0.5f) / TexDensity - h * 0.5f / TexDensity);
                float d = signedDistance(p);
                float fill = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-aa, aa, d));
                float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(OutlineThickness - aa, OutlineThickness + aa, d));
                byte c = (byte)Mathf.RoundToInt(Mathf.Lerp(OutlineLevel, 255f, fill));   // contorno oscuro → relleno blanco
                pixels[y * w + x] = new Color32(c, c, c, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), TexDensity);
        sprite.name = name;
        return sprite;
    }
}
