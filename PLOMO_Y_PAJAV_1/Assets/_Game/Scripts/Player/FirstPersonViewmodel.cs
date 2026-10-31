using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manos de Wilbur en primera persona (viewmodel 2D estilo rubber-hose) para el arma activa.
/// DÓNDE VA: cámara del Player (lo añade "Crear Prefabs de Prueba" con el perfil VM_Shotgun).
///
/// Crea en runtime:
///   ViewmodelCanvas (Screen Space - Overlay, Scale With Screen Size 1920x1080, match 0.5, debajo del HUD)
///   └── ViewmodelRoot (pantalla completa; lo mueve ViewmodelMotion)
///       ├── HandsImage (cuadro completo 16:9 apoyado abajo; preserveAspect, sin clics)
///       └── MuzzleFlashImage (encima de las manos)
/// y le añade ViewmodelAnimator (cuadros + fogonazo) y ViewmodelMotion (movimiento).
///
/// Sin perfil o sin cuadros en idle: no se crea nada (el juego sigue funcionando sin manos).
/// Se oculta en pausa, en diálogo/interrogatorio y al morir.
/// FASE 2 (revólver y cambio de arma): llamar SetProfile(perfilDelArma) y conectar el arma nueva con
/// HandleFired / HandleReloadStarted / HandleReloadFinished (hoy se conectan solos a ShotgunController).
/// </summary>
public class FirstPersonViewmodel : MonoBehaviour
{
    [SerializeField] private ViewmodelProfile profile;
    [Tooltip("Vacío = se busca en el Player")]
    [SerializeField] private ShotgunController shotgun;
    [Tooltip("Orden del canvas: por debajo del HUD (0), del BANG (-5) y del diálogo (100)")]
    [SerializeField] private int sortingOrder = -10;

    private PlayerController _player;
    private PlayerAim _aim;
    private Canvas _canvas;
    private RectTransform _root;
    private Image _hands;
    private Image _flash;
    private ViewmodelAnimator _animator;
    private ViewmodelMotion _motion;
    private bool _dead;

    public ViewmodelProfile Profile => profile;

    private void Awake()
    {
        _player = GetComponentInParent<PlayerController>();
        _aim = GetComponentInParent<PlayerAim>();
        if (shotgun == null) shotgun = transform.root.GetComponentInChildren<ShotgunController>();
        PlayerHealth health = GetComponentInParent<PlayerHealth>();
        if (health != null) health.OnDeath += HandleDeath;

        if (profile == null || !profile.idle.HasFrames)
        {
            Debug.LogWarning("[Viewmodel] Sin ViewmodelProfile con cuadros de idle: no se muestran las manos. " +
                             "Ejecuta Plomo y Paja → Crear Prefabs de Prueba.");
            enabled = false;
            return;
        }
        BuildCanvas();
        SetProfile(profile);
    }

    private void OnEnable()
    {
        if (shotgun == null) return;
        shotgun.OnFired += HandleFired;
        shotgun.OnReloadStarted += HandleReloadStarted;
        shotgun.OnReloadFinished += HandleReloadFinished;
    }

    private void OnDisable()
    {
        if (shotgun == null) return;
        shotgun.OnFired -= HandleFired;
        shotgun.OnReloadStarted -= HandleReloadStarted;
        shotgun.OnReloadFinished -= HandleReloadFinished;
    }

    /// <summary>Cambia de arma en runtime: cuadros, fogonazo, anclas, movimiento y tiempos de apuntado.</summary>
    public void SetProfile(ViewmodelProfile newProfile)
    {
        if (newProfile == null) return;
        profile = newProfile;
        if (_animator == null) return; // todavía sin canvas (Awake sin perfil)
        _motion.SetProfile(profile);
        _animator.SetProfile(profile);
        if (_aim != null) _aim.SetTiming(profile.aimInTime, profile.aimOutTime, profile.aimFovDelta);
    }

    // ============ Eventos del arma (públicos para el arma de la fase 2) ============

    public void HandleFired()
    {
        if (_animator != null) _animator.HandleFired();
    }

    public void HandleReloadStarted()
    {
        if (_animator != null) _animator.HandleReloadStarted(shotgun != null ? shotgun.ReloadDuration : 1f);
    }

    public void HandleReloadFinished()
    {
        if (_animator != null) _animator.HandleReloadFinished();
    }

    private void HandleDeath() => _dead = true;

    // ============ Canvas ============

    private void BuildCanvas()
    {
        var canvasGo = new GameObject("ViewmodelCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Raíz a pantalla completa; el pivote cerca de las manos (abajo a la derecha) para girar y escalar desde ahí
        var rootGo = new GameObject("ViewmodelRoot", typeof(RectTransform));
        rootGo.transform.SetParent(canvasGo.transform, false);
        _root = (RectTransform)rootGo.transform;
        _root.anchorMin = Vector2.zero;
        _root.anchorMax = Vector2.one;
        _root.pivot = new Vector2(0.65f, 0.1f);
        _root.offsetMin = _root.offsetMax = Vector2.zero;

        _hands = CreateImage("HandsImage", _root);
        RectTransform handsRect = _hands.rectTransform;
        handsRect.anchorMin = handsRect.anchorMax = new Vector2(0.5f, 0f);
        handsRect.pivot = new Vector2(0.5f, 0f);
        _hands.preserveAspect = true;

        // Hermano posterior = se dibuja encima de las manos
        _flash = CreateImage("MuzzleFlashImage", _root);
        RectTransform flashRect = _flash.rectTransform;
        flashRect.anchorMin = flashRect.anchorMax = flashRect.pivot = new Vector2(0.5f, 0.5f);
        _flash.preserveAspect = true;
        _flash.enabled = false;

        _motion = canvasGo.AddComponent<ViewmodelMotion>();
        _motion.Init(_root, _aim, _player);
        _animator = canvasGo.AddComponent<ViewmodelAnimator>();
        _animator.Init(_hands, _flash, _aim, _motion);
        FitHands();
    }

    private static Image CreateImage(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    /// <summary>Cuadro 16:9 completo apoyado abajo: en pantallas más angostas (4:3, 16:10) llena el ancho
    /// y queda pegado al borde inferior; en más anchas (21:9) llena el alto.</summary>
    private void FitHands()
    {
        if (_hands == null || _root == null) return;
        Rect area = _root.rect;
        if (area.width <= 0f || area.height <= 0f) return;
        const float aspect = 16f / 9f;
        Vector2 size = area.width / area.height > aspect
            ? new Vector2(area.height * aspect, area.height)
            : new Vector2(area.width, area.width / aspect);
        if (_hands.rectTransform.sizeDelta != size) _hands.rectTransform.sizeDelta = size;
    }

    private void LateUpdate()
    {
        if (_canvas == null) return;
        bool hidden = _dead || PlayerAim.GameplayBlocked();
        if (_canvas.enabled == hidden) _canvas.enabled = !hidden;
        FitHands();
    }
}
