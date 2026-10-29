using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manos de Wilbur con la escopeta, dibujadas en 2D abajo a la derecha de la pantalla.
/// DÓNDE VA: cámara del Player (lo añade "Crear Prefabs de Prueba").
///
/// Usa el set "wilbur_hands" (animaciones idle / fire / reload). Sin sprites no se ve nada.
/// Se balancea al caminar, da un culatazo al disparar y baja durante la recarga.
/// La imagen no recibe clics y no llega al centro de la pantalla (la mira queda libre).
/// </summary>
public class FirstPersonViewmodel : MonoBehaviour
{
    [SerializeField] private CharacterSpriteSet handsSet;
    [Tooltip("Vacío = se busca en el Player")]
    [SerializeField] private ShotgunController shotgun;

    [Header("Tamaño y posición (resolución de referencia 1920x1080)")]
    [SerializeField] private Vector2 size = new Vector2(640f, 640f);
    [SerializeField] private Vector2 margin = new Vector2(0f, 0f);

    [Header("Movimiento")]
    [SerializeField] private float swayAmount = 14f;
    [SerializeField] private float swaySpeed = 9f;
    [SerializeField] private float fireKick = 70f;
    [SerializeField] private float fireKickRecovery = 0.25f;
    [SerializeField] private float reloadDip = 160f;

    private PlayerController _player;
    private Canvas _canvas;
    private Image _image;
    private RectTransform _rect;

    private CharacterSpriteSet.AnimClip _clip;
    private string _anim;
    private bool _loop;
    private int _frame;
    private float _frameTimer;

    private float _swayPhase;
    private float _kick;        // 1 → 0 tras disparar
    private float _dip;         // 0..1 suavizado durante la recarga
    private bool _reloading;

    private void Awake()
    {
        _player = GetComponentInParent<PlayerController>();
        if (shotgun == null) shotgun = transform.root.GetComponentInChildren<ShotgunController>();

        if (handsSet == null || !handsSet.HasAnyFrames)
        {
            enabled = false; // sin sprites: no se crea nada
            return;
        }
        BuildCanvas();
        SetAnim("idle", true);
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

    private void BuildCanvas()
    {
        var canvasGo = new GameObject("Viewmodel_Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = -10; // debajo de HUD, subtítulos y diálogo

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var imageGo = new GameObject("Hands", typeof(RectTransform));
        imageGo.transform.SetParent(canvasGo.transform, false);
        _rect = (RectTransform)imageGo.transform;
        _rect.anchorMin = _rect.anchorMax = new Vector2(1f, 0f);
        _rect.pivot = new Vector2(1f, 0f);
        _rect.sizeDelta = size;

        _image = imageGo.AddComponent<Image>();
        _image.preserveAspect = true;
        _image.raycastTarget = false;
    }

    private void HandleFired()
    {
        _kick = 1f;
        SetAnim("fire", false);
    }

    private void HandleReloadStarted()
    {
        _reloading = true;
        SetAnim("reload", true);
    }

    private void HandleReloadFinished()
    {
        _reloading = false;
        SetAnim("idle", true);
    }

    private void SetAnim(string anim, bool loop)
    {
        if (handsSet == null || _image == null) return;
        if (!handsSet.TryGet(anim, out CharacterSpriteSet.AnimClip clip) && !handsSet.TryGet("idle", out clip))
            return;
        _anim = anim;
        _clip = clip;
        _loop = loop;
        _frame = 0;
        _frameTimer = 0f;
        _image.sprite = clip.frames[0];
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // Frames
        if (_clip != null)
        {
            _frameTimer += dt * Mathf.Max(0.1f, _clip.fps);
            if (_frameTimer >= 1f)
            {
                _frameTimer -= 1f;
                _frame++;
                if (_frame >= _clip.frames.Length)
                {
                    if (_loop) _frame = 0;
                    else if (_anim == "fire") { SetAnim(_reloading ? "reload" : "idle", true); return; }
                    else _frame = _clip.frames.Length - 1;
                }
                _image.sprite = _clip.frames[_frame];
            }
        }

        // Balanceo al caminar
        bool moving = _player != null && _player.IsMoving;
        if (moving) _swayPhase += dt * swaySpeed;
        float swayWeight = moving ? 1f : 0.25f;
        float swayX = Mathf.Sin(_swayPhase) * swayAmount * swayWeight;
        float swayY = -Mathf.Abs(Mathf.Cos(_swayPhase)) * swayAmount * 0.6f * swayWeight;

        // Culatazo y bajada de recarga
        _kick = Mathf.MoveTowards(_kick, 0f, dt / fireKickRecovery);
        _dip = Mathf.MoveTowards(_dip, _reloading ? 1f : 0f, dt * 4f);
        float kickOffset = _kick * _kick * fireKick;

        _rect.anchoredPosition = new Vector2(-margin.x + swayX + kickOffset * 0.4f,
                                             margin.y + swayY - kickOffset - _dip * reloadDip);
        _rect.localRotation = Quaternion.Euler(0f, 0f, -_kick * 6f);
    }
}
