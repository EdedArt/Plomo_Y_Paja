using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Manos de Wilbur en primera persona (viewmodel 2D estilo rubber-hose) para el arma activa.
/// DÓNDE VA: cámara del Player (lo añade "Crear Prefabs de Prueba" con el perfil VM_Shotgun).
///
/// Crea en runtime (capa "Viewmodel", la dibuja la ViewmodelCamera Overlay del stack; ver ViewmodelCameraRig):
///   ViewmodelCanvas (Screen Space - Camera sobre la ViewmodelCamera, planeDistance 1, CanvasScaler 1920x1080 con match = 1 (alto))
///   └── ViewmodelRoot (pantalla completa; lo mueve ViewmodelMotion)
///       ├── HandsImage (cuadro 16:9 anclado y con pivote ABAJO A LA DERECHA, más grande que la pantalla: overscan)
///       └── MuzzleFlashImage (encima de las manos)
/// y le añade ViewmodelAnimator (cuadros + fogonazo), ViewmodelMotion (movimiento) y ViewmodelLighting (tinte).
/// El HUD sigue en Screen Space - Overlay (sin grano ni viñeta).
///
/// ESQUINA: el tamaño sale del ALTO de la pantalla (match = 1) × overscan, siempre 16:9 como el arte. El borde derecho y el
/// inferior del arte se pasan de la pantalla. Pantallas más anchas que 16:9: pegado a la derecha. Más angostas (16:10, 4:3):
/// se recorta por la IZQUIERDA, nunca por la derecha ni abajo. ViewmodelMotion además mantiene esos dos bordes fuera de pantalla.
///
/// 3C.5.2: ViewmodelProfile.handsScale achica/agranda las manos desde la esquina inferior derecha; con hipClearLeftArtX
/// las manos se corren a la derecha lo justo para que el arma no tape la mira (16:9, 16:10, 4:3). La mira centrada al
/// apuntar sigue exacta (ViewmodelMotion la calcula con el tamaño y la posición reales de HandsImage).
/// Recarga por etapas (IStagedReloadWeapon, el revólver): cuadros abrir/meter/cerrar y balas visibles en el tambor
/// (ViewmodelChamberOverlay, hijo de HandsImage).
/// Sin perfil o sin cuadros en idle: no se crea nada (el juego sigue funcionando sin manos).
/// Se oculta en pausa, en diálogo/interrogatorio y al morir.
/// FASE 2 (revólver y cambio de arma): SetProfile(perfilDelArma) + SetWeapon(armaNueva). Cualquier arma que
/// implemente IWeapon se conecta sola (OnFired / OnReloadStarted / OnReloadFinished / ReloadDuration).
/// </summary>
public class FirstPersonViewmodel : MonoBehaviour
{
    private const float ArtAspect = 16f / 9f;

    [SerializeField] private ViewmodelProfile profile;
    [Tooltip("Orden entre canvases de la ViewmodelCamera")]
    [SerializeField] private int sortingOrder = -10;
    [Tooltip("Las manos se dibujan este factor más grandes que el ajuste por alto (1.08 = 8 % más). El sobrante queda " +
             "fuera de la pantalla por la derecha y por abajo")]
    [SerializeField, Range(1f, 1.3f)] private float overscan = 1.08f;

    [Header("Depuración (apagada por defecto)")]
    [Tooltip("Escribe en consola, cada frame durante 0.5 s después de disparar: posición, rotación Z y escala de ViewmodelRoot, " +
             "sprite, deltaTime, unscaledDeltaTime y timeScale, y la pose de las cámaras al renderizar (bug del salto al disparar)")]
    [SerializeField] private bool debugFireLog = false;
    [Tooltip("Mientras se apunta quieto (aim al 100 %), escribe cada 0.5 s dónde está en pantalla la punta de la mira delantera, el " +
             "centro de la pantalla y el error en píxeles (debe ser < 2 px en cualquier formato). Apagada por defecto")]
    [SerializeField] private bool debugSightLog = false;

    private IWeapon _weapon;
    private PlayerController _player;
    private PlayerAim _aim;
    private Camera _mainCamera;
    private Camera _viewmodelCamera;
    private Canvas _canvas;
    private RectTransform _root;
    private Image _hands;
    private Image _flash;
    private ViewmodelAnimator _animator;
    private ViewmodelMotion _motion;
    private ViewmodelLighting _lighting;
    private ViewmodelChamberOverlay _chambers;
    private bool _dead;
    private float _debugUntil = -1f;
    private float _sightLogNext;
    private int _debugFrame;

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

    private void OnEnable()
    {
        Subscribe(_weapon);
        RenderPipelineManager.beginCameraRendering += LogCameraRender;
    }

    private void OnDisable()
    {
        Unsubscribe(_weapon);
        RenderPipelineManager.beginCameraRendering -= LogCameraRender;
    }

    private void Subscribe(IWeapon weapon)
    {
        if (weapon == null) return;
        weapon.OnFired += HandleFired;
        weapon.OnReloadStarted += HandleReloadStarted;
        weapon.OnReloadFinished += HandleReloadFinished;
        if (weapon is IStagedReloadWeapon staged)
        {
            staged.OnReloadOpened += HandleReloadOpened;
            staged.OnRoundInserting += HandleRoundInserting;
            staged.OnRoundLoaded += HandleRoundLoaded;
            staged.OnReloadClosing += HandleReloadClosing;
        }
    }

    private void Unsubscribe(IWeapon weapon)
    {
        if (weapon == null) return;
        weapon.OnFired -= HandleFired;
        weapon.OnReloadStarted -= HandleReloadStarted;
        weapon.OnReloadFinished -= HandleReloadFinished;
        if (weapon is IStagedReloadWeapon staged)
        {
            staged.OnReloadOpened -= HandleReloadOpened;
            staged.OnRoundInserting -= HandleRoundInserting;
            staged.OnRoundLoaded -= HandleRoundLoaded;
            staged.OnReloadClosing -= HandleReloadClosing;
        }
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
        if (_chambers != null) _chambers.SetProfile(profile);
        FitHands();
        if (_aim != null) _aim.SetTiming(profile.aimInTime, profile.aimOutTime, profile.aimFovDelta);
    }

    // ============ Eventos del arma (públicos para el arma de la fase 2) ============

    public void HandleFired()
    {
        if (_animator != null) _animator.HandleFired();
        if (_lighting != null) _lighting.Flash();
        if (debugFireLog)
        {
            _debugUntil = Time.unscaledTime + 0.5f;
            _debugFrame = 0;
            Debug.Log("[VM-DEBUG] OnFired — log de 0.5 s (frame | pos | rotZ | escala | sprite | dt | unscaledDt | timeScale)");
        }
    }

    public void HandleReloadStarted()
    {
        if (_animator != null)
            _animator.HandleReloadStarted(_weapon != null ? _weapon.ReloadDuration : 1f, _weapon is IStagedReloadWeapon);
    }

    // Recarga por etapas (3C.5.2)
    private void HandleReloadOpened()
    {
        if (_animator != null) _animator.StageOpened();
    }

    private void HandleRoundInserting()
    {
        if (_animator != null && _weapon is IStagedReloadWeapon staged) _animator.StageInserting(staged.RoundInsertSeconds);
    }

    private void HandleRoundLoaded(int loaded)
    {
        if (_chambers != null) _chambers.Punch(loaded - 1);
    }

    private void HandleReloadClosing()
    {
        if (_animator != null) _animator.StageClosing();
    }

    public void HandleReloadFinished()
    {
        if (_animator != null) _animator.HandleReloadFinished();
    }

    /// <summary>Cambio de arma (WeaponSwitcher, 3C.5): las manos bajan y suben en `duration` (tiempo real), con la misma
    /// bajada que la recarga sin cuadros. El perfil nuevo se pone a la mitad con SetProfile.</summary>
    public void PlaySwitchDip(float duration)
    {
        if (_motion != null) _motion.StartReloadDip(duration);
    }

    private void HandleDeath() => _dead = true;

    // ============ Canvas ============

    private void BuildCanvas()
    {
        _mainCamera = GetComponent<Camera>();
        if (_mainCamera == null) _mainCamera = GetComponentInChildren<Camera>();
        if (_mainCamera == null) _mainCamera = Camera.main;
        _viewmodelCamera = ViewmodelCameraRig.Ensure(_mainCamera);

        var canvasGo = new GameObject("ViewmodelCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.sortingOrder = sortingOrder;
        if (_viewmodelCamera != null)
        {
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = _viewmodelCamera;
            _canvas.planeDistance = ViewmodelCameraRig.PlaneDistance;
        }
        else
        {
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Debug.LogWarning("[Viewmodel] Sin capa '" + ViewmodelCameraRig.LayerName + "' o sin cámara: las manos se dibujan en " +
                             "Overlay (sin post-procesado y detrás de nada). Ejecuta Plomo y Paja → Crear Prefabs de Prueba.");
        }

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f; // el alto manda: el tamaño de las manos no cambia con el ancho de la pantalla

        // Raíz a pantalla completa; el pivote casi en la esquina inferior derecha (0.9, 0.05) para girar y escalar desde ahí:
        // con la raíz estirada la pose en reposo no cambia al mover el pivote, y el culatazo casi no mueve la esquina.
        var rootGo = new GameObject("ViewmodelRoot", typeof(RectTransform));
        rootGo.transform.SetParent(canvasGo.transform, false);
        _root = (RectTransform)rootGo.transform;
        _root.anchorMin = Vector2.zero;
        _root.anchorMax = Vector2.one;
        _root.pivot = new Vector2(0.9f, 0.05f);
        _root.offsetMin = _root.offsetMax = Vector2.zero;

        // Manos: ancla y pivote abajo a la derecha (la esquina nunca se despega)
        _hands = CreateImage("HandsImage", _root);
        RectTransform handsRect = _hands.rectTransform;
        handsRect.anchorMin = handsRect.anchorMax = new Vector2(1f, 0f);
        handsRect.pivot = new Vector2(1f, 0f);
        _hands.preserveAspect = true;

        // Hermano posterior = se dibuja encima de las manos
        _chambers = ViewmodelChamberOverlay.Create(_hands); // hijo de las manos: sigue al dibujo en todo

        _flash = CreateImage("MuzzleFlashImage", _root);
        RectTransform flashRect = _flash.rectTransform;
        flashRect.anchorMin = flashRect.anchorMax = flashRect.pivot = new Vector2(0.5f, 0.5f);
        _flash.preserveAspect = true;
        _flash.enabled = false;

        _motion = canvasGo.AddComponent<ViewmodelMotion>();
        _motion.Init(_root, handsRect, _aim, _player);
        _animator = canvasGo.AddComponent<ViewmodelAnimator>();
        _animator.Init(_hands, _flash, _aim, _motion);
        _lighting = canvasGo.AddComponent<ViewmodelLighting>();
        _lighting.Init(_hands);
        FitHands();

        // Todo el canvas en la capa Viewmodel: solo la ViewmodelCamera lo dibuja
        if (_viewmodelCamera != null) ViewmodelCameraRig.SetLayerRecursive(canvasGo, ViewmodelCameraRig.Layer);
    }

    private static Image CreateImage(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    /// <summary>
    /// Cuadro 16:9 con el ALTO de la pantalla × overscan, apoyado en la esquina inferior derecha y pasándose de ella
    /// por la derecha y por abajo. Más ancha que 16:9: pegado a la derecha. Más angosta: se recorta por la izquierda.
    /// </summary>
    private void FitHands()
    {
        if (_hands == null || _root == null) return;
        Rect area = _root.rect;
        if (area.width <= 0f || area.height <= 0f) return;

        float fitHeight = area.height;
        float height = fitHeight * overscan;
        var baseSize = new Vector2(height * ArtAspect, height);
        var offset = new Vector2(baseSize.x - fitHeight * ArtAspect, -(height - fitHeight)); // lo que sobra: a la derecha y abajo

        // 3C.5.2: escala del perfil desde la esquina inferior derecha del dibujo (la esquina no se mueve)
        float scale = profile != null ? Mathf.Clamp(profile.handsScale, 0.5f, 1.5f) : 1f;
        Vector2 size = baseSize * scale;

        // 3C.5.2: en la cadera el arma nunca tapa la mira. Borde izquierdo del arma en pantalla (relativo al rect de la raíz)
        if (profile != null && profile.hipClearLeftArtX > 0f)
        {
            float gunLeft = area.xMax + offset.x - size.x + profile.hipClearLeftArtX / 1920f * size.x;
            float minLeft = area.center.x + profile.hipCenterClearance;
            if (gunLeft < minLeft) offset.x += minLeft - gunLeft;
        }
        RectTransform rt = _hands.rectTransform;
        if (rt.sizeDelta != size) rt.sizeDelta = size;
        if (rt.anchoredPosition != offset) rt.anchoredPosition = offset;
    }

    private void LateUpdate()
    {
        if (_canvas == null) return;
        bool hidden = _dead || PlayerAim.GameplayBlocked();
        if (_canvas.enabled == hidden) _canvas.enabled = !hidden;
        FitHands();
        if (_chambers != null && _animator != null)
            _chambers.Refresh(_animator.ReloadStage, _weapon != null ? _weapon.LoadedRounds : 0);
        LogFireFrame();
        LogSightError();
    }

    // ============ Depuración del centrado al apuntar (fase 1.4, apagada por defecto) ============

    private void LogSightError()
    {
        if (!debugSightLog || _aim == null || profile == null || _hands == null || _canvas == null) return;
        if (_aim.AimBlend < 0.999f || Time.unscaledTime < _sightLogNext) return;
        _sightLogNext = Time.unscaledTime + 0.5f;

        // Punto del arte (px 1920x1080, origen abajo-izquierda) → rect de HandsImage → mundo → pantalla
        Rect r = _hands.rectTransform.rect;
        var local = new Vector3(r.xMin + profile.aimSightPoint.x / 1920f * r.width, r.yMin + profile.aimSightPoint.y / 1080f * r.height, 0f);
        Vector3 world = _hands.rectTransform.TransformPoint(local);
        Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
        var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Debug.Log(string.Format("[VM-SIGHT] pantalla {0}x{1} (aspecto {2:F3}) · mira=({3:F1},{4:F1}) centro=({5:F1},{6:F1}) error={7:F2}px (solo vale quieto y sin culatazo)",
            Screen.width, Screen.height, Screen.width / (float)Screen.height, screen.x, screen.y, center.x, center.y, (screen - center).magnitude));
    }

    // ============ Depuración del salto al disparar (fase 1.2, apagada por defecto) ============

    private void LogFireFrame()
    {
        if (!debugFireLog || Time.unscaledTime > _debugUntil || _root == null) return;
        Vector2 p = _root.anchoredPosition;
        Vector3 s = _root.localScale;
        Debug.Log(string.Format("[VM-DEBUG] f{0:00} pos=({1:F1},{2:F1}) rotZ={3:F1} scale=({4:F3},{5:F3}) sprite={6} dt={7:F4} unscaledDt={8:F4} timeScale={9:F2}",
            _debugFrame++, p.x, p.y, Mathf.DeltaAngle(0f, _root.localEulerAngles.z), s.x, s.y,
            _hands != null && _hands.sprite != null ? _hands.sprite.name : "-", Time.deltaTime, Time.unscaledDeltaTime, Time.timeScale));
    }

    /// <summary>Pose de la cámara principal y de la ViewmodelCamera justo antes de que cada una renderice
    /// (ShotgunFeedback mueve la principal en este mismo evento: el orden entre los dos no está garantizado).</summary>
    private void LogCameraRender(ScriptableRenderContext context, Camera cam)
    {
        if (!debugFireLog || Time.unscaledTime > _debugUntil || _mainCamera == null) return;
        if (cam != _mainCamera && cam != _viewmodelCamera) return;
        Transform m = _mainCamera.transform;
        Vector3 vm = _viewmodelCamera != null ? _viewmodelCamera.transform.localPosition : Vector3.zero;
        Debug.Log(string.Format("[VM-DEBUG] render '{0}': principal localPos=({1:F3},{2:F3},{3:F3}) localEuler=({4:F1},{5:F1},{6:F1}) | ViewmodelCamera localPos=({7:F3},{8:F3},{9:F3})",
            cam.name, m.localPosition.x, m.localPosition.y, m.localPosition.z,
            Mathf.DeltaAngle(0f, m.localEulerAngles.x), Mathf.DeltaAngle(0f, m.localEulerAngles.y), Mathf.DeltaAngle(0f, m.localEulerAngles.z),
            vm.x, vm.y, vm.z));
    }
}
