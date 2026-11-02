using System.Collections.Generic;
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

    private IWeapon _weapon;
    private PlayerAim _aim;
    private Camera _camera;
    private CharacterController _body;
    private Canvas _canvas;
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
        bool hidden = _dead || (_aim != null && _aim.IsAiming) || PlayerAim.GameplayBlocked();
        if (_canvas.enabled == hidden) _canvas.enabled = !hidden;
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
