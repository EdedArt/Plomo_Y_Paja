using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mira "rubber-hose" del HUD: un punto blanco con contorno negro y 4 marcas cortas redondeadas alrededor.
/// DÓNDE VA: GameObject "Player" (lo añade "Crear Prefabs de Prueba"). Construye su propio canvas Screen Space - Overlay
/// (HUD: sin grano ni viñeta) porque UIManager no existe en ninguna escena; no depende de él.
///
/// - Los disparos salen del CENTRO de la cámara principal (ShotgunController.ShootPellet: origen = posición de la cámara,
///   dirección = su forward + dispersión), así que el punto central es exactamente adonde van los perdigones.
/// - Las marcas se abren con la dispersión del arma (IWeapon.SpreadAngle, convertida a píxeles con el FOV de la cámara y
///   escalada por spreadVisualScale; es una indicación, no la dispersión real) y un poco más si el jugador se mueve rápido
///   (adorno, la dispersión real no cambia al caminar). Al disparar se abren un 40 % y vuelven en 0.12 s (tiempo real).
/// - Se oculta al apuntar (PlayerAim), en diálogo, pausa y muerte.
/// - Marcador de impacto: las marcas se ponen rojas 0.1 s cuando un enemigo recibe daño (EnemyHealth.OnDamaged).
/// Los sprites se generan por código (sin PNG): relleno blanco, contorno negro, ~28 px en total a 1080p.
/// </summary>
public class RubberHoseCrosshair : MonoBehaviour
{
    // Tamaños en píxeles de referencia 1080p
    private const float DotWhiteRadius = 2.4f;
    private const float TickWhiteRadius = 1.6f;   // media anchura de la marca
    private const float TickHalfLength = 0.9f;    // el largo blanco = 2·hl + 2·r = 5
    private const float OutlineThickness = 1.8f;
    private const float TexDensity = 4f;          // píxeles de textura por píxel de pantalla (nítido y con antialias)
    private const float SpriteMargin = 2f;        // margen transparente del sprite (1 px por lado)

    [SerializeField] private int sortingOrder = 5;
    [SerializeField] private Color fillColor = Color.white;
    [SerializeField] private Color hitColor = new Color(1f, 0.15f, 0.15f, 1f);
    [Tooltip("Distancia del centro al borde interior de las marcas sin dispersión (px a 1080p). 5.6 → mira de ~28 px")]
    [SerializeField] private float baseRadius = 5.6f;
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
    private RectTransform _dotRect;
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
        scaler.matchWidthOrHeight = 1f; // 28 px a 1080p en cualquier ventana de ese alto

        var rootGo = new GameObject("Crosshair", typeof(RectTransform));
        rootGo.transform.SetParent(canvasGo.transform, false);
        var root = (RectTransform)rootGo.transform;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;

        // Punto central
        float dotSize = (DotWhiteRadius + OutlineThickness) * 2f + SpriteMargin;
        Image dot = CreateImage("Dot", root, _dotSprite, new Vector2(dotSize, dotSize));
        _dotRect = dot.rectTransform;

        // Marcas: arriba, derecha, abajo, izquierda (la capsula vertical se gira 0/-90/180/90 grados)
        float tickWidth = (TickWhiteRadius + OutlineThickness) * 2f + SpriteMargin;
        _tickLength = (TickHalfLength + TickWhiteRadius + OutlineThickness) * 2f; // el dibujo, sin el margen transparente
        float[] angles = { 0f, -90f, 180f, 90f };
        for (int i = 0; i < 4; i++)
        {
            Image tick = CreateImage("Tick" + i, root, _tickSprite, new Vector2(tickWidth, _tickLength + SpriteMargin));
            tick.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angles[i]);
            _ticks[i] = tick;
            _tickRects[i] = tick.rectTransform;
        }
        PlaceTicks(baseRadius);
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

    /// <summary>radius = distancia del centro al borde interior de cada marca.</summary>
    private void PlaceTicks(float radius)
    {
        float d = radius + _tickLength * 0.5f;
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
        PlaceTicks(_radius * (1f + popAmount * eased));
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
        _dotSprite = BuildSprite("CrosshairDot", dotUnits, dotUnits, DotWhiteRadius, p => p.magnitude);

        float tickW = (TickWhiteRadius + OutlineThickness) * 2f + SpriteMargin;
        float tickH = (TickHalfLength + TickWhiteRadius + OutlineThickness) * 2f + SpriteMargin;
        float hl = TickHalfLength;
        _tickSprite = BuildSprite("CrosshairTick", tickW, tickH, TickWhiteRadius,
            p => new Vector2(p.x, p.y - Mathf.Clamp(p.y, -hl, hl)).magnitude); // cápsula vertical
    }

    /// <summary>
    /// Textura RGBA con la forma dada por su distancia con signo (en píxeles de pantalla): relleno blanco hasta fillRadius y
    /// contorno negro de OutlineThickness más afuera, con antialias. El sprite lleva 1 px de margen transparente para que el
    /// filtro bilineal no recorte el contorno.
    /// </summary>
    private static Sprite BuildSprite(string name, float unitsW, float unitsH, float fillRadius, System.Func<Vector2, float> distance)
    {
        int w = Mathf.CeilToInt(unitsW * TexDensity), h = Mathf.CeilToInt(unitsH * TexDensity);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color32[w * h];
        float aa = 0.5f / TexDensity;           // medio píxel de textura
        float outer = fillRadius + OutlineThickness;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2((x + 0.5f) / TexDensity - w * 0.5f / TexDensity, (y + 0.5f) / TexDensity - h * 0.5f / TexDensity);
                float d = distance(p);
                float fill = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fillRadius - aa, fillRadius + aa, d));
                float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(outer - aa, outer + aa, d));
                byte c = (byte)Mathf.RoundToInt(fill * 255f);   // negro (contorno) → blanco (relleno)
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
