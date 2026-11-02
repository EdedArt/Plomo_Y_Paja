using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cuadros de las manos y fogonazo del viewmodel. Solo lee del ViewmodelProfile activo (SetProfile).
/// DÓNDE VA: lo crea FirstPersonViewmodel en "ViewmodelCanvas"; dibuja en "HandsImage" y "MuzzleFlashImage".
///
/// - Clips a su fps (por defecto 12, "de a dos"); idle con 2+ cuadros se repite a idleLoopFps (8).
///   Clip vacío → idle.
/// - Disparo = clip fire y después cycle (bombeo). Apuntando: aimFire (o aimIdle + culatazo) y aimCycle;
///   sin aimCycle sale del apuntado, bombea con cycle y vuelve si el botón sigue apretado.
/// - Cambio cadera ↔ apuntar: el dibujo cambia a mitad de la transición con un golpecito de escala.
/// - Recarga: clip reload en bucle o, si no hay, idle (ViewmodelMotion baja las manos).
/// - Fogonazo: centrado en el ancla del cañón del clip activo, giro y escala al azar, ~0.07 s,
///   nunca el mismo dibujo dos veces seguidas si hay 2 o más.
/// </summary>
public class ViewmodelAnimator : MonoBehaviour
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;

    private enum Mode { Idle, Fire, Cycle, Reload }

    private Image _hands;
    private Image _flash;
    private ViewmodelProfile _profile;
    private PlayerAim _aim;
    private ViewmodelMotion _motion;

    private Mode _mode = Mode.Idle;
    private ViewmodelProfile.Clip _clip;
    private bool _loop;
    private float _fps;
    private float _clipTime;
    private float _clipDuration;
    private int _frame = -1;
    private bool _aimPose;          // ¿se están mostrando los dibujos de apuntar?
    private bool _shotWhileAiming;
    private float _flashTimer = -1f;
    private int _lastFlash = -1;

    public void Init(Image hands, Image flash, PlayerAim aim, ViewmodelMotion motion)
    {
        _hands = hands;
        _flash = flash;
        _aim = aim;
        _motion = motion;
    }

    public void SetProfile(ViewmodelProfile profile)
    {
        _profile = profile;
        _mode = Mode.Idle;
        _aimPose = WantsAimPose();
        HideFlash();
        PlayIdlePose();
    }

    // ============ Eventos del arma ============

    public void HandleFired()
    {
        if (_profile == null) return;
        bool aiming = _aim != null && _aim.IsAiming;
        _shotWhileAiming = aiming;
        if (_motion != null) _motion.Kick(1f);

        _mode = Mode.Fire;
        if (aiming && _profile.aimFire.HasFrames) Play(_profile.aimFire, false);
        else if (aiming) Play(_profile.aimIdle, false, _profile.aimFire.minDuration);
        else Play(_profile.fire, false);

        ShowFlash(aiming);
    }

    public void HandleReloadStarted(float duration)
    {
        if (_profile == null) return;
        _mode = Mode.Reload;
        _aimPose = false; // la recarga cancela el apuntado
        if (_profile.reload.HasFrames) Play(_profile.reload, true);
        else
        {
            Play(_profile.idle, true);
            if (_motion != null) _motion.StartReloadDip(duration);
        }
    }

    public void HandleReloadFinished()
    {
        if (_motion != null) _motion.EndReloadDip();
        if (_profile == null || _mode != Mode.Reload) return;
        _mode = Mode.Idle;
        PlayIdlePose();
    }

    // ============ Cada frame ============

    private void Update()
    {
        if (_profile == null || _hands == null) return;
        float dt = ViewmodelTime.Delta; // tiempo real: el hit-stop no congela los cuadros

        if (_flashTimer >= 0f)
        {
            _flashTimer += dt;
            if (_flashTimer >= _profile.flashDuration) HideFlash();
        }

        // Cadera ↔ apuntar: el dibujo cambia a mitad de la transición
        if (_mode == Mode.Idle)
        {
            bool want = WantsAimPose();
            if (want != _aimPose)
            {
                _aimPose = want;
                PlayIdlePose();
                if (_motion != null) _motion.Punch(_profile.aimSwapPunch);
            }
        }

        if (_clip == null) return;
        _clipTime += dt;
        if (!_loop && _clipTime >= _clipDuration)
        {
            OnClipFinished();
            return;
        }
        ShowFrame();
    }

    private void OnClipFinished()
    {
        if (_mode == Mode.Fire)
        {
            _mode = Mode.Cycle;
            bool stillAiming = _shotWhileAiming && _aim != null && _aim.IsAiming;
            if (stillAiming && _profile.aimCycle.HasFrames)
            {
                Play(_profile.aimCycle, false);
                return;
            }
            if (_profile.cycle.HasFrames)
            {
                // Sin dibujo de bombear apuntando: salir a la cadera, bombear y volver si sigue apretado
                if (stillAiming && _aim != null) _aim.SuppressFor(ClipDuration(_profile.cycle, false));
                _aimPose = false;
                Play(_profile.cycle, false);
                return;
            }
        }
        _mode = Mode.Idle;
        _aimPose = WantsAimPose();
        PlayIdlePose();
    }

    // ============ Clips ============

    private bool WantsAimPose()
    {
        return _aim != null && _aim.AimBlend >= 0.5f && _profile != null && _profile.aimIdle.HasFrames;
    }

    private void PlayIdlePose()
    {
        if (_profile == null) return;
        Play(_aimPose ? _profile.aimIdle : _profile.idle, true);
    }

    /// <summary>Reproduce un clip; vacío → idle. minDurationOverride &gt; 0 alarga un clip prestado (ej: aimIdle al disparar).</summary>
    private void Play(ViewmodelProfile.Clip clip, bool loop, float minDurationOverride = 0f)
    {
        if (clip == null || !clip.HasFrames) clip = _profile.idle;
        if (clip == null || !clip.HasFrames)
        {
            _clip = null;
            if (_hands != null) _hands.enabled = false;
            return;
        }
        _clip = clip;
        _loop = loop;
        _fps = ClipFps(clip, loop);
        _clipDuration = Mathf.Max(ClipDuration(clip, loop), minDurationOverride);
        _clipTime = 0f;
        _frame = -1;
        _hands.enabled = true;
        ShowFrame();
    }

    private float ClipFps(ViewmodelProfile.Clip clip, bool loop)
    {
        if (clip.fps > 0f) return clip.fps;
        if (loop && clip.frames.Length >= 2 && (clip == _profile.idle || clip == _profile.aimIdle))
            return Mathf.Max(0.1f, _profile.idleLoopFps);
        return Mathf.Max(0.1f, _profile.defaultFps);
    }

    private float ClipDuration(ViewmodelProfile.Clip clip, bool loop)
    {
        if (clip == null || !clip.HasFrames) return 0f;
        return Mathf.Max(clip.frames.Length / ClipFps(clip, loop), clip.minDuration);
    }

    private void ShowFrame()
    {
        int n = _clip.frames.Length;
        int frame = Mathf.FloorToInt(_clipTime * _fps);
        frame = _loop ? frame % n : Mathf.Min(frame, n - 1);
        if (frame == _frame) return;
        _frame = frame;
        _hands.sprite = _clip.frames[frame];
    }

    // ============ Fogonazo ============

    private void ShowFlash(bool aiming)
    {
        if (_flash == null || _profile.flashSprites == null || _profile.flashSprites.Length == 0) return;

        int count = _profile.flashSprites.Length;
        int index = Random.Range(0, count);
        if (count >= 2 && index == _lastFlash) index = (index + 1 + Random.Range(0, count - 1)) % count;
        _lastFlash = index;
        Sprite sprite = _profile.flashSprites[index];
        if (sprite == null) return;

        // Ancla (px del cuadro 1920x1080, origen abajo-izquierda) → punto dentro del rect de HandsImage
        Vector2 anchor = aiming ? _profile.aimMuzzle : _profile.hipMuzzle;
        RectTransform handsRect = _hands.rectTransform;
        Rect r = handsRect.rect;
        Vector3 local = new Vector3(r.xMin + anchor.x / ReferenceWidth * r.width,
                                    r.yMin + anchor.y / ReferenceHeight * r.height, 0f);
        RectTransform flashRect = _flash.rectTransform;
        flashRect.position = handsRect.TransformPoint(local);

        float size = _profile.flashSize * Random.Range(_profile.flashScaleRange.x, _profile.flashScaleRange.y);
        flashRect.sizeDelta = new Vector2(size, size);
        flashRect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        _flash.sprite = sprite;
        _flash.enabled = true;
        _flashTimer = 0f;
    }

    private void HideFlash()
    {
        _flashTimer = -1f;
        if (_flash != null) _flash.enabled = false;
    }
}
