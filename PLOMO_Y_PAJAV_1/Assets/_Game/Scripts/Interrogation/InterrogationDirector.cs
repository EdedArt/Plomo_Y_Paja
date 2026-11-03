using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Escena de interrogatorio (fase 3C.3), reutilizable: Centavo hoy, guardias mañana.
/// DÓNDE VA: raíz del prefab Prefabs/UI/InterrogationCanvas (junto a InterrogationCanvas). Lo arma el menú
/// Plomo y Paja → Contador → Crear UI de interrogatorio. Quien interroga lo instancia y llama Enter.
///
/// ENTER(target, flow) — todo en tiempo real:
///  1. Bloquea al jugador (PlayerController y ShotgunController apagados, como DialogueNPC) y suelta el cursor.
///     PlayerAim.GameplayBlocked() pasa a true: se suelta el apuntado (D7), se ocultan las manos y la mira
///     (el mismo mecanismo que el diálogo de los guardias). El FOV lo sigue llevando PlayerAim: aquí no se toca.
///  2. "Agacharse": la Main Camera va al ancla del objetivo en 0.6 s (ease in-out). Solo posición/rotación.
///  3. Time.timeScale = 0 (guarda el valor anterior; si había un hit-stop de CartoonFx en curso, guarda 1 y le
///     quita el control del tiempo con CartoonFx.TimeHeld). Sin AudioMixer en el proyecto: el ambiente sigue sonando.
///     Las voces de la escena tienen ignoreListenerPause (D5).
///  4. Desenfoque: Volume global propio (prioridad 100) de 0 → 1 en 0.35 s: DoF Gaussian (0 → 1 m, radio 1.5),
///     viñeta +0.15 y exposición −0.6 sobre lo que haya. El canvas va en Overlay: el retrato queda nítido.
///  5. Oculta el dibujo del mundo y muestra el retrato. Después corre flow(this) (el contenido: el flujo de CentavoInterrogation, hecho con InterrogationData).
/// EXIT(outcome): lo inverso. El flujo lo llama al final; si el flujo termina sin llamarlo, sale como Cancelled.
///
/// PAUSA: GameManager no deja pausar con Escape mientras IsActive (la escena ya congela el tiempo).
///
/// EPÍLOGO (3C.4.1): Exit(outcome, outro) cierra la escena (retrato, papel, desenfoque fuera, el tiempo vuelve a
/// correr) pero el jugador sigue bloqueado, sin manos ni mira (IsActive sigue en true) mientras corre outro(this):
/// movimientos de cámara (MoveCamera, TrackCamera) y un pensamiento en la banda de arriba (OutroThought, canvas en
/// modo "solo pensamiento"). Al terminar, la cámara vuelve a la cabeza del jugador y se devuelve el control.
/// IsActive = escena o epílogo (jugador bloqueado). IsSceneOpen = solo la escena (las voces del mundo callan).
/// </summary>
public class InterrogationDirector : MonoBehaviour
{
    public enum Speaker { Target, Player }

    /// <summary>Escena o epílogo en curso (desde que empieza Enter hasta que se devuelve el control).</summary>
    public static InterrogationDirector Active { get; private set; }
    public static bool IsActive => Active != null;

    /// <summary>Escena abierta (retrato y papel): ninguna voz del mundo debe sonar. En el epílogo ya es false.</summary>
    public static bool IsSceneOpen => Active != null && Active._sceneOpen;

    [SerializeField] private InterrogationCanvas view;
    [Tooltip("Voz del interrogado (2D). La lee el lip-sync del retrato")]
    [SerializeField] private AudioSource targetVoice;
    [Tooltip("Voz de Wilbur (2D). No mueve la boca del retrato")]
    [SerializeField] private AudioSource playerVoice;
    [SerializeField] private string playerName = "WILBUR";

    [Header("Tiempos (s, tiempo real)")]
    [SerializeField, Min(0.01f)] private float crouchSeconds = 0.6f;
    [SerializeField, Min(0.01f)] private float returnSeconds = 0.4f;
    [SerializeField, Min(0.01f)] private float blurSeconds = 0.35f;
    [SerializeField, Min(0.01f)] private float uiFadeSeconds = 0.25f;
    [Tooltip("Epílogo: segundos para volver de la última toma a la vista del jugador")]
    [SerializeField, Min(0.01f)] private float outroReturnSeconds = 0.6f;
    [Tooltip("Epílogo: aparición y desaparición del pensamiento")]
    [SerializeField, Min(0.01f)] private float outroThoughtFadeSeconds = 0.4f;

    [Header("Desenfoque y oscurecido")]
    [SerializeField] private float volumePriority = 100f;
    [SerializeField, Min(0f)] private float dofEnd = 1f;
    [SerializeField, Range(0.5f, 1.5f)] private float dofMaxRadius = 1.5f;
    [SerializeField] private float vignetteBoost = 0.15f;
    [SerializeField] private float exposureShift = -0.6f;

    [Header("Texto")]
    [Tooltip("La máquina de escribir dura esta fracción del clip")]
    [SerializeField, Range(0.3f, 1f)] private float typewriterFraction = 0.9f;
    [Tooltip("Sin clip: letras por segundo")]
    [SerializeField, Min(1f)] private float charactersPerSecond = 38f;
    [Tooltip("Pausa al terminar una línea antes de seguir (un clic la salta)")]
    [SerializeField, Min(0f)] private float linePause = 0.45f;
    [SerializeField, Min(0.05f)] private float dotsInterval = 0.35f;

    /// <summary>Terminó la escena (ya se devolvió todo al jugador).</summary>
    public event Action<InterrogationOutcome> OnExited;

    public bool IsBusy => _running;
    public InterrogationCanvas View => view;

    private IInterrogationTarget _target;
    private string _initialEmotion;
    private bool _running;
    private bool _exiting;
    private bool _sceneOpen;

    // Jugador
    private PlayerController _playerController;
    private ShotgunController[] _weapons = new ShotgunController[0];
    private bool _movementWasEnabled;
    private bool[] _weaponsWereEnabled = new bool[0];
    private CursorLockMode _previousLock;
    private bool _previousCursorVisible;
    private bool _restorePending;

    // Cámara
    private Transform _camera;
    private Vector3 _savedLocalPosition;
    private Quaternion _savedLocalRotation;

    // Tiempo
    private float _savedTimeScale = 1f;

    // Volume
    private Volume _volume;
    private Vignette _vignette;
    private ColorAdjustments _colorAdjustments;

    // ==================================================================
    // Entrar / salir
    // ==================================================================

    /// <summary>
    /// Empieza la escena. flow = el contenido; debe terminar llamando Exit(outcome). initialEmotion = par del retrato
    /// con el que entra (ej: "scared"). false si no se pudo.
    /// </summary>
    public bool Enter(IInterrogationTarget target, Func<InterrogationDirector, IEnumerator> flow, string initialEmotion = null)
    {
        if (_running || IsActive || target == null || view == null) return false;
        _target = target;
        _initialEmotion = initialEmotion;
        _running = true;
        _exiting = false;
        _sceneOpen = true;
        Active = this;
        StartCoroutine(EnterRoutine(flow));
        return true;
    }

    /// <summary>
    /// Cierra la escena y devuelve todo como estaba. El objetivo ya debe estar en su estado final.
    /// outro (opcional) = epílogo con el mundo corriendo y el jugador todavía bloqueado (ver arriba).
    /// </summary>
    public void Exit(InterrogationOutcome outcome, Func<InterrogationDirector, IEnumerator> outro = null)
    {
        if (!_running || _exiting) return;
        _exiting = true;
        StopAllCoroutines();
        StartCoroutine(ExitRoutine(outcome, outro));
    }

    private IEnumerator EnterRoutine(Func<InterrogationDirector, IEnumerator> flow)
    {
        // 1. Jugador. Ninguna voz del mundo sigue sonando (A3, 3C.4): se corta el canal de voz de los guardias
        LockPlayer();
        DialogueUI.EnsureEventSystem();
        AudioLibrary.CutEnemyVoiceBelow(int.MaxValue);

        // 2. Cámara al ancla
        Camera cam = Camera.main;
        _camera = cam != null ? cam.transform : null;
        Transform anchor = _target.CameraAnchor;
        if (_camera != null)
        {
            _savedLocalPosition = _camera.localPosition;
            _savedLocalRotation = _camera.localRotation;
            if (anchor != null)
            {
                Vector3 fromPos = _camera.position;
                Quaternion fromRot = _camera.rotation;
                yield return Animate(crouchSeconds, t =>
                {
                    float e = Mathf.SmoothStep(0f, 1f, t);
                    _camera.SetPositionAndRotation(Vector3.Lerp(fromPos, anchor.position, e),
                        Quaternion.Slerp(fromRot, anchor.rotation, e));
                });
            }
        }

        // 3. Tiempo congelado (D5)
        _savedTimeScale = CartoonFx.IsHitStopActive ? 1f : Time.timeScale;
        if (_savedTimeScale <= 0f) _savedTimeScale = 1f;
        CartoonFx.TimeHeld = true;
        Time.timeScale = 0f;

        // 4. Desenfoque y oscurecido
        PrepareVolume();
        yield return Animate(blurSeconds, t => _volume.weight = t);

        // 5. Retrato en lugar del dibujo del mundo (D6)
        _target.SetWorldVisible(false);
        view.SetOpen(true);
        view.Portrait.Show(_target.PortraitSet, _initialEmotion);
        yield return Animate(uiFadeSeconds, t => view.SetAlpha(t));

        if (flow != null) yield return flow(this);
        if (!_exiting) Exit(InterrogationOutcome.Cancelled);
    }

    private IEnumerator ExitRoutine(InterrogationOutcome outcome, Func<InterrogationDirector, IEnumerator> outro)
    {
        StopVoices();
        view.HideOptions();
        view.HideDecision();
        float startAlpha = 1f;
        yield return Animate(uiFadeSeconds, t => view.SetAlpha(startAlpha * (1f - t)));
        view.Portrait.Hide();
        view.SetThought("", 0f);
        view.SetNotice("", 0f);
        view.SetOpen(false);

        // El dibujo del mundo vuelve en su estado final (muerto / cojeando)
        if (_target != null) _target.SetWorldVisible(true);
        _sceneOpen = false;

        if (outro != null)
        {
            yield return OutroRoutine(outro);
            FinishExit(outcome);
            yield break;
        }

        // Desenfoque fuera y cámara de vuelta a la cabeza, a la vez
        Vector3 fromLocalPos = _camera != null ? _camera.localPosition : Vector3.zero;
        Quaternion fromLocalRot = _camera != null ? _camera.localRotation : Quaternion.identity;
        float startWeight = _volume != null ? _volume.weight : 0f;
        float duration = Mathf.Max(returnSeconds, blurSeconds);
        yield return Animate(duration, t =>
        {
            float elapsed = t * duration;
            if (_volume != null) _volume.weight = startWeight * (1f - Mathf.Clamp01(elapsed / blurSeconds));
            if (_camera != null)
            {
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / returnSeconds));
                _camera.localPosition = Vector3.Lerp(fromLocalPos, _savedLocalPosition, e);
                _camera.localRotation = Quaternion.Slerp(fromLocalRot, _savedLocalRotation, e);
            }
        });

        RestoreTime();
        _restorePending = true;       // el control vuelve en LateUpdate: el clic de la decisión no dispara
        while (_restorePending) yield return null;
        FinishExit(outcome);
    }

    private void FinishExit(InterrogationOutcome outcome)
    {
        _running = false;
        _exiting = false;
        _sceneOpen = false;
        _target = null;
        if (Active == this) Active = null;
        OnExited?.Invoke(outcome);
    }

    /// <summary>Epílogo: sin desenfoque, tiempo normal, jugador bloqueado; al final la cámara vuelve y el control también.</summary>
    private IEnumerator OutroRoutine(Func<InterrogationDirector, IEnumerator> outro)
    {
        Cursor.lockState = CursorLockMode.Locked; // no hay nada que cliquear
        Cursor.visible = false;

        float startWeight = _volume != null ? _volume.weight : 0f;
        yield return Animate(blurSeconds, t => { if (_volume != null) _volume.weight = startWeight * (1f - t); });
        RestoreTime();

        view.SetThoughtOnly(true);
        view.SetOpen(true);
        view.SetAlpha(1f);
        yield return outro(this);
        view.SetThought("", 0f);
        view.SetOpen(false);
        view.SetThoughtOnly(false);

        yield return ReturnCamera(outroReturnSeconds);
        _restorePending = true;
        while (_restorePending) yield return null;
    }

    // ==================================================================
    // Epílogo: cámara y pensamiento (tiempo real)
    // ==================================================================

    /// <summary>Dónde estaban los ojos del jugador al empezar (altura de pie).</summary>
    public Vector3 PlayerEyePosition => _camera == null ? Vector3.zero
        : _camera.parent != null ? _camera.parent.TransformPoint(_savedLocalPosition) : _savedLocalPosition;

    public Vector3 CameraPosition => _camera != null ? _camera.position : Vector3.zero;
    public Quaternion CameraRotation => _camera != null ? _camera.rotation : Quaternion.identity;

    /// <summary>Rotación para mirar desde "from" hacia "to".</summary>
    public static Quaternion LookFrom(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        return dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir, Vector3.up) : Quaternion.identity;
    }

    /// <summary>Lleva la cámara (suave) a la posición y rotación dadas; se reevalúan cada frame (sirve para seguir a alguien).</summary>
    public IEnumerator MoveCamera(Func<Vector3> position, Func<Quaternion> rotation, float seconds)
    {
        if (_camera == null) yield break;
        Vector3 fromPos = _camera.position;
        Quaternion fromRot = _camera.rotation;
        yield return Animate(seconds, t =>
        {
            float e = Mathf.SmoothStep(0f, 1f, t);
            _camera.SetPositionAndRotation(Vector3.Lerp(fromPos, position(), e), Quaternion.Slerp(fromRot, rotation(), e));
        });
    }

    /// <summary>Deja la cámara en su lugar y la gira siguiendo una rotación objetivo durante "seconds".</summary>
    public IEnumerator TrackCamera(Func<Quaternion> rotation, float seconds, float sharpness = 6f)
    {
        if (_camera == null) yield break;
        float until = Time.unscaledTime + seconds;
        while (Time.unscaledTime < until)
        {
            float k = 1f - Mathf.Exp(-sharpness * Time.unscaledDeltaTime);
            _camera.rotation = Quaternion.Slerp(_camera.rotation, rotation(), k);
            yield return null;
        }
    }

    /// <summary>Vuelve la cámara a la pose del jugador (relativa a su cabeza).</summary>
    public IEnumerator ReturnCamera(float seconds)
    {
        if (_camera == null) yield break;
        Vector3 fromLocalPos = _camera.localPosition;
        Quaternion fromLocalRot = _camera.localRotation;
        yield return Animate(seconds, t =>
        {
            float e = Mathf.SmoothStep(0f, 1f, t);
            _camera.localPosition = Vector3.Lerp(fromLocalPos, _savedLocalPosition, e);
            _camera.localRotation = Quaternion.Slerp(fromLocalRot, _savedLocalRotation, e);
        });
    }

    /// <summary>
    /// Pensamiento del epílogo en la banda de arriba: "…" mientras result() devuelve null (quien llama pone su tiempo
    /// límite), aparece, se queda max(minSeconds, letras × secondsPerCharacter) y se va.
    /// </summary>
    public IEnumerator OutroThought(Func<string> result, float minSeconds, float secondsPerCharacter)
    {
        string line = null;
        int dots = 0;
        float nextDots = 0f;
        while (line == null)
        {
            line = result != null ? result() : "";
            if (line != null) break;
            if (Time.unscaledTime >= nextDots)
            {
                nextDots = Time.unscaledTime + dotsInterval;
                dots = dots % 3 + 1;
                view.SetThought(new string('.', dots), 1f);
            }
            yield return null;
        }
        if (string.IsNullOrEmpty(line)) yield break;
        yield return Animate(outroThoughtFadeSeconds, t => view.SetThought(line, t));
        float hold = Mathf.Max(minSeconds, line.Length * secondsPerCharacter);
        float until = Time.unscaledTime + hold;
        while (Time.unscaledTime < until) yield return null;
        yield return Animate(outroThoughtFadeSeconds, t => view.SetThought(line, 1f - t));
    }

    private void LateUpdate()
    {
        if (!_restorePending) return;
        _restorePending = false;
        RestorePlayer();
    }

    private void OnDisable()
    {
        // Nunca dejar el juego congelado ni al jugador bloqueado
        if (!_running) return;
        StopAllCoroutines();
        if (_volume != null) _volume.weight = 0f;
        if (_camera != null)
        {
            _camera.localPosition = _savedLocalPosition;
            _camera.localRotation = _savedLocalRotation;
        }
        if (_target != null) _target.SetWorldVisible(true);
        RestoreTime();
        RestorePlayer();
        _running = false;
        _exiting = false;
        _sceneOpen = false;
        if (Active == this) Active = null;
    }

    private void RestoreTime()
    {
        CartoonFx.TimeHeld = false;
        bool paused = GameManager.Instance != null && GameManager.Instance.IsGamePaused;
        if (!paused) Time.timeScale = _savedTimeScale;
    }

    // ==================================================================
    // Jugador
    // ==================================================================

    private void LockPlayer()
    {
        _previousLock = Cursor.lockState;
        _previousCursorVisible = Cursor.visible;

        GameObject player = GameObject.FindWithTag("Player");
        _playerController = player != null ? player.GetComponent<PlayerController>() : null;
        if (_playerController != null)
        {
            _movementWasEnabled = _playerController.enabled;
            _playerController.enabled = false;
        }

        _weapons = FindObjectsOfType<ShotgunController>();
        _weaponsWereEnabled = new bool[_weapons.Length];
        for (int i = 0; i < _weapons.Length; i++)
        {
            _weaponsWereEnabled[i] = _weapons[i].enabled;
            _weapons[i].enabled = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestorePlayer()
    {
        if (_playerController != null) _playerController.enabled = _movementWasEnabled;
        for (int i = 0; i < _weapons.Length; i++)
            if (_weapons[i] != null) _weapons[i].enabled = _weaponsWereEnabled[i];
        Cursor.lockState = _previousLock;
        Cursor.visible = _previousCursorVisible;
    }

    // ==================================================================
    // Volume del desenfoque
    // ==================================================================

    private void PrepareVolume()
    {
        if (_volume == null)
        {
            var go = new GameObject("Volume_Interrogatorio");
            go.layer = 0; // Default: la capa que mira el Volume Mask de la cámara
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = volumePriority;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Interrogatorio (runtime)";
            DepthOfField dof = profile.Add<DepthOfField>(false);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(0f);
            dof.gaussianEnd.Override(dofEnd);
            dof.gaussianMaxRadius.Override(dofMaxRadius);
            dof.highQualitySampling.Override(true);
            _vignette = profile.Add<Vignette>(false);
            _colorAdjustments = profile.Add<ColorAdjustments>(false);
            _volume.sharedProfile = profile;
        }
        _volume.weight = 0f;

        // "+0.15" y "−0.6" sobre lo que ya haya (el volume se mezcla por peso con los globales)
        VolumeStack stack = VolumeManager.instance.stack;
        Vignette baseVignette = stack != null ? stack.GetComponent<Vignette>() : null;
        ColorAdjustments baseColor = stack != null ? stack.GetComponent<ColorAdjustments>() : null;
        float vignette = baseVignette != null ? baseVignette.intensity.value : 0f;
        float exposure = baseColor != null ? baseColor.postExposure.value : 0f;
        _vignette.intensity.Override(Mathf.Clamp01(vignette + vignetteBoost));
        _colorAdjustments.postExposure.Override(exposure + exposureShift);
    }

    // ==================================================================
    // Presentación (las usan los flujos de contenido)
    // ==================================================================

    /// <summary>
    /// Una línea con voz: nombre + máquina de escribir (≈ 0.9 × duración del clip). Clic/Espacio completa el texto;
    /// otro clic corta el clip. emotion = par del retrato a mostrar (null = sin cambio). Sin clip: solo texto.
    /// Las voces nunca se pisan: cada Say corta la anterior (las dos fuentes).
    /// </summary>
    public IEnumerator Say(Speaker speaker, string text, string clipId, string emotion = null)
    {
        yield return Say(speaker, text, clipId, emotion, null);
    }

    /// <summary>
    /// Como Say, pero el clip se corta "cut.seconds" antes de terminar: suena cut.sfx (2D) y el retrato pasa a
    /// cut.emotion (ej: contador_muerte_01 cortado por el escopetazo, guion §5). Sin clip: el corte llega al final del texto.
    /// </summary>
    public IEnumerator SayCut(Speaker speaker, string text, string clipId, string emotion, LineCut cut)
    {
        yield return Say(speaker, text, clipId, emotion, cut);
    }

    /// <summary>Corte de una línea por un efecto (disparo).</summary>
    public class LineCut
    {
        public float seconds = 0.2f;
        public string sfx = "escopeta_disparo";
        public string emotion = "death";
    }

    private IEnumerator Say(Speaker speaker, string text, string clipId, string emotion, LineCut cut)
    {
        yield return null; // el clic que eligió la opción anterior no cuenta aquí
        bool fromTarget = speaker == Speaker.Target;
        if (fromTarget && !string.IsNullOrEmpty(emotion)) view.Portrait.SetEmotion(emotion);

        AudioSource source = fromTarget ? targetVoice : playerVoice;
        AudioClip clip = string.IsNullOrEmpty(clipId) ? null : AudioLibrary.GetVoice(clipId);
        StopVoices();
        if (clip != null && source != null)
        {
            source.clip = clip;
            source.volume = AudioLibrary.VoiceVolume;
            source.Play();
        }

        string name = fromTarget ? (_target != null ? _target.SpeakerName : "") : playerName;
        int total = view.ShowLine(name, text);
        float typeSeconds = clip != null ? clip.length * typewriterFraction : total / charactersPerSecond;
        float start = Time.unscaledTime;
        bool textDone = false;

        while (true)
        {
            if (cut != null)
            {
                bool due = clip != null && source != null
                    ? !source.isPlaying || source.time >= clip.length - cut.seconds
                    : textDone;
                if (due)
                {
                    if (source != null) source.Stop();
                    view.SetVisibleCharacters(total);
                    AudioLibrary.PlaySfx2D(cut.sfx);
                    if (!string.IsNullOrEmpty(cut.emotion)) view.Portrait.SetEmotion(cut.emotion);
                    break;
                }
            }

            bool advance = AdvancePressed();
            if (cut != null) advance = false; // la línea del disparo no se salta: el corte es el remate
            if (!textDone)
            {
                float t = typeSeconds > 0f ? (Time.unscaledTime - start) / typeSeconds : 1f;
                if (advance) t = 1f;
                view.SetVisibleCharacters(Mathf.CeilToInt(Mathf.Clamp01(t) * total));
                if (t >= 1f)
                {
                    textDone = true;
                    advance = false; // el clic que completó el texto no corta el clip
                }
            }
            else if (advance && source != null && source.isPlaying)
            {
                source.Stop();
                break;
            }

            if (textDone && (source == null || !source.isPlaying || clip == null)) break;
            yield return null;
        }

        // Pausa corta antes de seguir (la salta un clic)
        float until = Time.unscaledTime + linePause;
        while (Time.unscaledTime < until)
        {
            yield return null;
            if (AdvancePressed()) break;
        }
    }

    /// <summary>Opciones en el papel; teclas 1/2/3 o clic. chosen(índice).</summary>
    public IEnumerator Choose(string[] options, Action<int> chosen)
    {
        int picked = -1;
        view.ShowOptions(playerName, options, i => picked = i);
        while (picked < 0)
        {
            yield return null;
            int key = NumberKeyPressed();
            if (key >= 0) view.PickOption(key);
        }
        chosen?.Invoke(picked);
    }

    /// <summary>
    /// Banda de pensamiento: "…" animado mientras result() devuelve null (sin bloquear nada); a los timeout s usa
    /// fallback(). Luego el texto aparece suave y queda visible hasta HideThought.
    /// </summary>
    public IEnumerator Think(Func<string> result, float timeoutSeconds, Func<string> fallback)
    {
        float start = Time.unscaledTime;
        string line = null;
        int dots = 0;
        float nextDots = 0f;
        while (line == null)
        {
            if (result != null) line = result();
            if (line == null && Time.unscaledTime - start >= timeoutSeconds) line = fallback != null ? fallback() : "";
            if (line != null) break;
            if (Time.unscaledTime >= nextDots)
            {
                nextDots = Time.unscaledTime + dotsInterval;
                dots = dots % 3 + 1;
                view.SetThought(new string('.', dots), 1f);
            }
            yield return null;
        }
        yield return Animate(0.3f, t => view.SetThought(line, t));
    }

    public void HideThought() => view.SetThought("", 0f);

    /// <summary>Aviso corto que aparece y se va solo (ej: la pista registro_01). No bloquea el flujo.</summary>
    public void ShowNotice(string text, float seconds = 3f)
    {
        StartCoroutine(NoticeRoutine(text, seconds));
    }

    private IEnumerator NoticeRoutine(string text, float seconds)
    {
        yield return Animate(0.2f, t => view.SetNotice(text, t));
        float until = Time.unscaledTime + seconds;
        while (Time.unscaledTime < until) yield return null;
        yield return Animate(0.4f, t => view.SetNotice(text, 1f - t));
    }

    /// <summary>
    /// Decisión escrita en el papel ("1. Jalar el gatillo" | "2. Dejarlo ir"), con WILBUR de nombre; teclas 1/2 o clic.
    /// targetEmotion = par del retrato mientras se decide (ej: "plead").
    /// </summary>
    public IEnumerator Decide(string first, string second, Action<int> chosen, string targetEmotion = null)
    {
        int picked = -1;
        if (!string.IsNullOrEmpty(targetEmotion)) view.Portrait.SetEmotion(targetEmotion);
        view.ShowDecision(playerName, first, second, i => picked = i);
        while (picked < 0)
        {
            yield return null;
            int key = NumberKeyPressed();
            if (key == 0 || key == 1) view.PickDecision(key);
        }
        chosen?.Invoke(picked);
    }

    // ==================================================================
    // Utilidades
    // ==================================================================

    private void StopVoices()
    {
        if (targetVoice != null) targetVoice.Stop();
        if (playerVoice != null) playerVoice.Stop();
    }

    private static bool AdvancePressed() => Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space);

    private static int NumberKeyPressed()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) return 0;
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) return 1;
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) return 2;
        return -1;
    }

    /// <summary>Llama step(t) con t de 0 a 1 en "seconds" de tiempo real (el último paso siempre es 1).</summary>
    private static IEnumerator Animate(float seconds, Action<float> step)
    {
        float start = Time.unscaledTime;
        while (true)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - start) / seconds);
            step(t);
            if (t >= 1f) yield break;
            yield return null;
        }
    }
}
