using UnityEngine;

/// <summary>
/// Movimiento "de goma" de las manos en primera persona, todo por código y en unidades de canvas (1920x1080):
/// respiración, balanceo en 8 al caminar (más grande al correr, manos abajo y giradas), inercia del mouse,
/// culatazo con resorte, bajada de recarga (si el arma no tiene cuadros de recarga) y paso a apuntar.
/// DÓNDE VA: lo crea FirstPersonViewmodel en "ViewmodelCanvas" y mueve "ViewmodelRoot".
/// Todos los valores salen del ViewmodelProfile activo (SetProfile). Independiente del frame rate.
///
/// TIEMPO: una sola fuente, ViewmodelTime.Delta (tiempo real, sin escala, recortado a 1/30 s). Así las manos siguen
/// recuperándose durante el hit-stop de CartoonFx (timeScale 0) y un tirón de FPS nunca mete un paso enorme. El resorte del
/// culatazo se integra en sub-pasos de 1/120 s (Euler semi-implícito, estable: w·h = 32/120 ≈ 0.27, muy por debajo del límite de 2).
///
/// BORDES: las manos se dibujan más grandes que la pantalla (overscan). Después de sumar todos los desplazamientos, la
/// posición se corrige para que el borde derecho y el inferior del arte NUNCA entren en la pantalla, con la rotación y la
/// escala ya aplicadas. Cada eje de escala queda en [0.8, 1.2].
/// </summary>
public class ViewmodelMotion : MonoBehaviour
{
    private const float SpringStep = 1f / 120f;
    private const float ArtWidth = 1920f;    // el arte de las manos se dibuja en 1920x1080
    private const float ArtHeight = 1080f;
    private const float MinScale = 0.8f;
    private const float MaxScale = 1.2f;

    private RectTransform _root;
    private RectTransform _hands;
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

    public void Init(RectTransform root, RectTransform hands, PlayerAim aim, PlayerController player)
    {
        _root = root;
        _hands = hands;
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
        float dt = ViewmodelTime.Delta;
        _time += dt;

        float aimT = _aim != null ? Mathf.SmoothStep(0f, 1f, _aim.AimBlend) : 0f;
        bool hasAimSprite = p.aimIdle != null && p.aimIdle.HasFrames;
        // Mira centrada: con dibujo de apuntar y punto de mira, al apuntar la punta de la mira va EXACTAMENTE al centro de la
        // pantalla y el balanceo, la inercia y la respiración se anulan del todo (solo el culatazo puede moverla)
        bool centered = hasAimSprite && p.aimSightPoint != Vector2.zero && _hands != null;
        float bobMul = centered ? 1f - aimT : Mathf.Lerp(1f, p.aimBobMultiplier, aimT);
        float swayMul = centered ? 1f - aimT : Mathf.Lerp(1f, p.aimSwayMultiplier, aimT);
        float breathMul = centered ? 1f - aimT : Mathf.Lerp(1f, p.aimBreathMultiplier, aimT);

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
        int steps = Mathf.Max(1, Mathf.CeilToInt(dt / SpringStep));
        float h = dt / steps;
        for (int i = 0; i < steps; i++)
        {
            float accel = -w * w * _kick - 2f * p.kickSpringDamping * w * _kickVelocity;
            _kickVelocity += accel * h;   // primero la velocidad...
            _kick += _kickVelocity * h;   // ...y luego la posición (semi-implícito)
        }
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
        Vector2 aimOffset = centered
            ? (SightCenterOffset(p.aimSightPoint) + p.aimOffset) * aimT
            : (hasAimSprite ? p.aimOffset : p.aimOffsetNoSprite) * aimT;
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

        sx = Mathf.Clamp(sx, MinScale, MaxScale);
        sy = Mathf.Clamp(sy, MinScale, MaxScale);
        // Con la mira centrada el dibujo de apuntar se corre a la izquierda en pantallas anchas: el borde derecho ya no se
        // protege (solo el inferior); el peso baja con el apuntado
        pos = KeepEdgesOffscreen(pos, rot, new Vector2(sx, sy), centered ? 1f - aimT : 1f);

        _root.anchoredPosition = pos;
        _root.localRotation = Quaternion.Euler(0f, 0f, rot);
        _root.localScale = new Vector3(sx, sy, 1f);
    }

    /// <summary>
    /// Corrige la posición para que el borde derecho y el inferior del dibujo de las manos no entren en la pantalla.
    /// Trabaja en el espacio de ViewmodelRoot en reposo (relativo a su pivote): la pantalla es root.rect y el dibujo es el
    /// rect de HandsImage (anclado abajo a la derecha). Se transforman 3 esquinas con la escala, la rotación y la
    /// posición del frame; un borde recto cumple si sus dos extremos cumplen.
    /// </summary>
    private Vector2 KeepEdgesOffscreen(Vector2 pos, float rotDegrees, Vector2 scale, float rightWeight)
    {
        if (_hands == null) return pos;
        Rect screen = _root.rect;
        Vector2 size = _hands.rect.size;
        float right = screen.xMax + _hands.anchoredPosition.x;
        float bottom = screen.yMin + _hands.anchoredPosition.y;
        float left = right - size.x;
        float top = bottom + size.y;

        float rad = rotDegrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
        Vector2 br = Transform2D(right, bottom, c, s, scale, pos);
        Vector2 tr = Transform2D(right, top, c, s, scale, pos);
        Vector2 bl = Transform2D(left, bottom, c, s, scale, pos);

        float dx = Mathf.Max(0f, screen.xMax - Mathf.Min(br.x, tr.x)) * rightWeight; // borde derecho: mover a la derecha si entró
        float dy = Mathf.Min(0f, screen.yMin - Mathf.Max(br.y, bl.y));   // borde inferior: mover hacia abajo si entró
        return pos + new Vector2(dx, dy);
    }

    /// <summary>
    /// Desplazamiento de ViewmodelRoot (en reposo, rotación 0 y escala 1) que lleva el punto del arte `artPoint` (px 1920x1080,
    /// origen abajo-izquierda) al centro EXACTO de la pantalla. Se recalcula cada frame con el rect real de la raíz y de HandsImage:
    /// vale para cualquier formato y resolución (el tamaño de las manos sale del alto, el ancla es la esquina inferior derecha).
    /// </summary>
    private Vector2 SightCenterOffset(Vector2 artPoint)
    {
        Rect screen = _root.rect;                       // relativo al pivote de la raíz
        Vector2 size = _hands.rect.size;
        float right = screen.xMax + _hands.anchoredPosition.x;
        float bottom = screen.yMin + _hands.anchoredPosition.y;
        var sight = new Vector2(right - size.x + artPoint.x / ArtWidth * size.x,
                                bottom + artPoint.y / ArtHeight * size.y);
        return screen.center - sight;
    }

    private static Vector2 Transform2D(float x, float y, float cos, float sin, Vector2 scale, Vector2 pos)
    {
        x *= scale.x;
        y *= scale.y;
        return new Vector2(x * cos - y * sin, x * sin + y * cos) + pos;
    }
}
