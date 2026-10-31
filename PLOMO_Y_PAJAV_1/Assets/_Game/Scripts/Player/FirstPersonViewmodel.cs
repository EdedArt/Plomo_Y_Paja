using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manos de Wilbur en primera persona (viewmodel 2D estilo rubber-hose) para el arma activa.
/// DÓNDE VA: cámara del Player (lo añade "Crear Prefabs de Prueba" con el perfil VM_Shotgun).
///
/// Crea en runtime:
///   ViewmodelCanvas (Screen Space - Camera con la cámara del Player, pegado a la cámara: recibe el mismo post-procesado
///                    que el mundo (grano, viñeta, color, bloom). El HUD sigue en Overlay, sin post-procesado.
///                    Scale With Screen Size 1920x1080, match 0.5)
///   └── ViewmodelRoot (pantalla completa; lo mueve ViewmodelMotion)
///       ├── HandsImage (cuadro completo 16:9 apoyado abajo; preserveAspect, sin clics)
///       └── MuzzleFlashImage (encima de las manos)
/// y le añade ViewmodelAnimator (cuadros + fogonazo), ViewmodelMotion (movimiento) y ViewmodelLighting (tinte).
///
/// Sin perfil o sin cuadros en idle: no se crea nada (el juego sigue funcionando sin manos).
/// Se oculta en pausa, en diálogo/interrogatorio y al morir.
/// FASE 2 (revólver y cambio de arma): SetProfile(perfilDelArma) + SetWeapon(armaNueva). Cualquier arma que
/// implemente IWeapon se conecta sola (OnFired / OnReloadStarted / OnReloadFinished / ReloadDuration).
/// </summary>
public class FirstPersonViewmodel : MonoBehaviour
{
    [SerializeField] private ViewmodelProfile profile;
    [Tooltip("Orden entre canvases de cámara. El HUD, el BANG y el diálogo son Overlay y siempre quedan encima")]
    [SerializeField] private int sortingOrder = -10;
    [Tooltip("Distancia del canvas por delante del plano cercano de la cámara (m)")]
    [SerializeField] private float planeOffset = 0.02f;
    [Tooltip("Si nearClipPlane es mayor que esto, se baja a nearClipPlaneWhenLowered")]
    [SerializeField] private float maxNearClip = 0.05f;
    [SerializeField] private float nearClipPlaneWhenLowered = 0.03f;

    private IWeapon _weapon;
    private PlayerController _player;
    private PlayerAim _aim;
    private Canvas _canvas;
    private RectTransform _root;
    private Image _hands;
    private Image _flash;
    private ViewmodelAnimator _animator;
    private ViewmodelMotion _motion;
    private ViewmodelLighting _lighting;
    private Material _uiMaterial;
    private bool _dead;

    public ViewmodelProfile Profile => profile;

    private void Awake()
    {
        _player = GetComponentInParent<PlayerController>();
        _aim = GetComponentInParent<PlayerAim>();
        _weapon = transform.root.GetComponentInChildren<IWeapon>(true);
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

    private void OnEnable() => Subscribe(_weapon);

    private void OnDisable() => Unsubscribe(_weapon);

    private void OnDestroy()
    {
        if (_uiMaterial != null) Destroy(_uiMaterial);
    }

    private void Subscribe(IWeapon weapon)
    {
        if (weapon == null) return;
        weapon.OnFired += HandleFired;
        weapon.OnReloadStarted += HandleReloadStarted;
        weapon.OnReloadFinished += HandleReloadFinished;
    }

    private void Unsubscribe(IWeapon weapon)
    {
        if (weapon == null) return;
        weapon.OnFired -= HandleFired;
        weapon.OnReloadStarted -= HandleReloadStarted;
        weapon.OnReloadFinished -= HandleReloadFinished;
    }

    /// <summary>Cambia el arma activa en runtime (fase 2). Combínalo con SetProfile y PlayerAim.SetWeapon.</summary>
    public void SetWeapon(IWeapon weapon)
    {
        if (weapon == _weapon) return;
        if (isActiveAndEnabled) Unsubscribe(_weapon);
        _weapon = weapon;
        if (isActiveAndEnabled) Subscribe(_weapon);
        if (_aim != null) _aim.SetWeapon(weapon);
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
        if (_lighting != null) _lighting.Flash();
    }

    public void HandleReloadStarted()
    {
        if (_animator != null) _animator.HandleReloadStarted(_weapon != null ? _weapon.ReloadDuration : 1f);
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
        ConfigureCanvasRender();

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

        // Siempre por encima del mundo aunque la cámara esté pegada a una pared (ZTest Always)
        _uiMaterial = new Material(Graphic.defaultGraphicMaterial) { name = "ViewmodelUI_ZAlways" };
        _uiMaterial.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);

        _hands = CreateImage("HandsImage", _root);
        _hands.material = _uiMaterial;
        RectTransform handsRect = _hands.rectTransform;
        handsRect.anchorMin = handsRect.anchorMax = new Vector2(0.5f, 0f);
        handsRect.pivot = new Vector2(0.5f, 0f);
        _hands.preserveAspect = true;

        // Hermano posterior = se dibuja encima de las manos
        _flash = CreateImage("MuzzleFlashImage", _root);
        _flash.material = _uiMaterial;
        RectTransform flashRect = _flash.rectTransform;
        flashRect.anchorMin = flashRect.anchorMax = flashRect.pivot = new Vector2(0.5f, 0.5f);
        _flash.preserveAspect = true;
        _flash.enabled = false;

        _motion = canvasGo.AddComponent<ViewmodelMotion>();
        _motion.Init(_root, _aim, _player);
        _animator = canvasGo.AddComponent<ViewmodelAnimator>();
        _animator.Init(_hands, _flash, _aim, _motion);
        _lighting = canvasGo.AddComponent<ViewmodelLighting>();
        _lighting.Init(_hands);
        FitHands();
    }

    /// <summary>
    /// Screen Space - Camera con la cámara del Player, justo después del plano cercano. Así las manos pasan por
    /// el mismo post-procesado de URP que el mundo. El canvas se ajusta solo al frustum, así que el zoom del
    /// FOV al apuntar NO cambia el tamaño de las manos (lo da el CanvasScaler, que mira la pantalla).
    /// Sin cámara: vuelve a Overlay (se ve igual, sin post-procesado).
    /// </summary>
    private void ConfigureCanvasRender()
    {
        Camera cam = GetComponent<Camera>();
        if (cam == null) cam = GetComponentInChildren<Camera>();
        if (cam == null) cam = Camera.main;

        _canvas.sortingOrder = sortingOrder;
        if (cam == null)
        {
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Debug.LogWarning("[Viewmodel] Sin cámara: el viewmodel se dibuja en Overlay (sin post-procesado).");
            return;
        }

        if (cam.nearClipPlane > maxNearClip)
        {
            Debug.Log("[Viewmodel] nearClipPlane " + cam.nearClipPlane.ToString("0.###") + " → " +
                      nearClipPlaneWhenLowered.ToString("0.###") + " (el canvas de las manos va pegado a la cámara)");
            cam.nearClipPlane = nearClipPlaneWhenLowered;
        }
        _canvas.renderMode = RenderMode.ScreenSpaceCamera;
        _canvas.worldCamera = cam;
        _canvas.planeDistance = cam.nearClipPlane + planeOffset;
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
