using UnityEngine;

/// <summary>
/// Movimiento "de goma" de las manos en primera persona, todo por código y en unidades de canvas (1920x1080):
/// respiración, balanceo en 8 al caminar (más grande al correr, manos abajo y giradas), inercia del mouse,
/// culatazo con resorte, bajada de recarga (si el arma no tiene cuadros de recarga) y paso a apuntar.
/// DÓNDE VA: lo crea FirstPersonViewmodel en "ViewmodelCanvas" y mueve "ViewmodelRoot".
/// Todos los valores salen del ViewmodelProfile activo (SetProfile). Independiente del frame rate.
/// </summary>
public class ViewmodelMotion : MonoBehaviour
{
    private RectTransform _root;
    private ViewmodelProfile _profile;
    private PlayerAim _aim;
    private PlayerController _player;
    private CharacterController _body;

    private float _time;
    private float _bobPhase;
    private float _moveBlend;            // 0 quieto .. 1 caminando
    private float _runBlend;             // 0 caminando .. 1 corriendo
    private Vector2 _sway;

    private float _kick;                 // 1 = culatazo completo; el resorte lo devuelve a 0 (con rebote)
    private float _kickVelocity;
    private float _kickStrength = 1f;

    private bool _reloadDipActive;
    private float _reloadTimer;
    private float _reloadDuration;
    private float _dip;                  // 0..1

    private float _punchTimer = -1f;
    private float _punchScale = 1f;
    private const float PunchDuration = 0.12f;

    public void Init(RectTransform root, PlayerAim aim, PlayerController player)
    {
        _root = root;
        _aim = aim;
        _player = player;
        if (player != null) player.TryGetComponent(out _body);
    }

    public void SetProfile(ViewmodelProfile profile)
    {
        _profile = profile;
        _kick = _kickVelocity = 0f;
        _punchTimer = -1f;
    }

    /// <summary>Culatazo. strength 1 = el del perfil (apuntando se multiplica por aimKickMultiplier).</summary>
    public void Kick(float strength = 1f)
    {
        if (_profile == null) return;
        bool aiming = _aim != null && _aim.IsAiming;
        _kickStrength = strength * (aiming ? _profile.aimKickMultiplier : 1f);
        _kick = 1f;
        _kickVelocity = 0f;
    }

    /// <summary>Bajar las manos durante la recarga real del arma (duración leída del arma).</summary>
    public void StartReloadDip(float duration)
    {
        _reloadDipActive = true;
        _reloadTimer = 0f;
        _reloadDuration = Mathf.Max(0.01f, duration);
    }

    public void EndReloadDip() => _reloadDipActive = false;

    /// <summary>Golpecito de escala (ej: 1.03 al cambiar al dibujo de apuntar).</summary>
    public void Punch(float scale)
    {
        _punchScale = scale;
        _punchTimer = 0f;
    }

    private void Update()
    {
        if (_root == null || _profile == null) return;
        ViewmodelProfile p = _profile;
        float dt = Time.deltaTime;
        _time += dt;

        float aimT = _aim != null ? Mathf.SmoothStep(0f, 1f, _aim.AimBlend) : 0f;
        float bobMul = Mathf.Lerp(1f, p.aimBobMultiplier, aimT);
        float swayMul = Mathf.Lerp(1f, p.aimSwayMultiplier, aimT);
        float breathMul = Mathf.Lerp(1f, p.aimBreathMultiplier, aimT);

        // --- Caminar / correr ---
        float speed = 0f;
        if (_body != null)
        {
            Vector3 v = _body.velocity;
            v.y = 0f;
            speed = v.magnitude;
        }
        bool moving = speed > 0.1f;
        bool running = moving && _player != null && _player.IsRunning;
        _moveBlend = Mathf.MoveTowards(_moveBlend, moving ? 1f : 0f, dt * p.bobBlendSpeed);
        _runBlend = Mathf.MoveTowards(_runBlend, running ? 1f : 0f, dt * p.bobBlendSpeed);

        float speed01 = p.bobReferenceSpeed > 0f ? Mathf.Clamp(speed / p.bobReferenceSpeed, 0f, 2f) : 1f;
        _bobPhase += dt * p.bobFrequency * Mathf.PI * speed01; // π = un paso (un rebote de |sin|)
        Vector2 amp = Vector2.Lerp(p.walkBobAmplitude, p.runBobAmplitude, _runBlend) * (_moveBlend * bobMul);
        float s = Mathf.Sin(_bobPhase);
        Vector2 bob = new Vector2(s * amp.x, -Mathf.Abs(s) * amp.y);

        // --- Respiración ---
        float breathWave = Mathf.Sin(_time * p.breathRate * 2f * Mathf.PI) * breathMul * (1f - _moveBlend * 0.5f);
        float breathY = breathWave * p.breathAmplitude;
        float breathScaleY = 1f + breathWave * p.breathScale;

        // --- Inercia del mouse (en contra del movimiento, suavizada y limitada) ---
        Vector2 mouse = PlayerAim.GameplayBlocked()
            ? Vector2.zero
            : new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        Vector2 target = -mouse * p.swayPerMouseUnit;
        target.x = Mathf.Clamp(target.x, -p.swayMax, p.swayMax);
        target.y = Mathf.Clamp(target.y, -p.swayMax, p.swayMax);
        _sway = Vector2.Lerp(_sway, target, 1f - Mathf.Exp(-p.swaySmoothing * dt));
        Vector2 sway = _sway * swayMul;

        // --- Culatazo: resorte amortiguado (vuelve en ~0.25 s con un pequeño rebote) ---
        float w = p.kickSpringFrequency;
        float accel = -w * w * _kick - 2f * p.kickSpringDamping * w * _kickVelocity;
        float step = Mathf.Min(dt, 1f / 30f);
        _kickVelocity += accel * step;
        _kick += _kickVelocity * step;
        float k = _kick * _kickStrength;

        // --- Recarga sin cuadros: bajar al principio, esperar, subir al final ---
        if (_reloadDipActive)
        {
            _reloadTimer += dt;
            float ease = Mathf.Min(p.reloadEase, _reloadDuration * 0.5f);
            float down = Mathf.Clamp01(_reloadTimer / ease);
            float up = Mathf.Clamp01((_reloadDuration - _reloadTimer) / ease);
            _dip = Mathf.SmoothStep(0f, 1f, Mathf.Min(down, up));
        }
        else _dip = Mathf.MoveTowards(_dip, 0f, dt / Mathf.Max(0.01f, p.reloadEase));

        // --- Apuntar ---
        bool hasAimSprite = p.aimIdle != null && p.aimIdle.HasFrames;
        Vector2 aimOffset = (hasAimSprite ? p.aimOffset : p.aimOffsetNoSprite) * aimT;
        float aimScale = Mathf.Lerp(1f, hasAimSprite ? 1f : p.aimScaleNoSprite, aimT);

        float punch = 1f;
        if (_punchTimer >= 0f)
        {
            _punchTimer += dt;
            float t = Mathf.Clamp01(_punchTimer / PunchDuration);
            punch = Mathf.Lerp(_punchScale, 1f, t);
            if (t >= 1f) _punchTimer = -1f;
        }

        // --- Juntar todo ---
        Vector2 pos = aimOffset + bob + sway + new Vector2(0f, breathY)
                      + p.kickOffset * k
                      + new Vector2(0f, -p.runLower * _runBlend * (1f - aimT))
                      + new Vector2(0f, -p.reloadDip * _dip);
        float rot = p.runTilt * _runBlend * (1f - aimT) + _sway.x * p.swayTiltPerPixel * swayMul + p.kickRotation * k;
        float sx = Mathf.LerpUnclamped(1f, p.kickSquash.x, k) * aimScale * punch;
        float sy = Mathf.LerpUnclamped(1f, p.kickSquash.y, k) * aimScale * punch * breathScaleY;

        _root.anchoredPosition = pos;
        _root.localRotation = Quaternion.Euler(0f, 0f, rot);
        _root.localScale = new Vector3(sx, sy, 1f);
    }
}
