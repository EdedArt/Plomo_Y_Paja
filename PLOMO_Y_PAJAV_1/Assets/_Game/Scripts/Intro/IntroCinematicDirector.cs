using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Taxi, bajada y llegada a la casa de Porky (INTRO.2), en el Nivel 1. Una vez por sesión (I6).
/// DÓNDE VA: objeto "IntroCinematic" de NivelPorky (lo arma Plomo y Paja → Intro → Construir taxi y llegada, que también
/// llama Generar Nivel Porky).
///
/// SECUENCIA (tiempo real):
///  1. Franjas de cine; fundido desde negro 0.8 s con el taxi ya andando (lluvia ahogada, amb_taxi_interior,
///     limpiaparabrisas). La cámara del jugador va sentada en Anchor_TaxiSeat con mirada limitada (±60° / ±30°).
///  2. Taxi_Path ~9 s (acelera y frena suave), sfx_taxi_frenar, se detiene en Anchor_TaxiStop.
///  3. Diálogo del guion §3 con subtítulos y 0.4 s entre líneas. El taxista gira la cabeza (habla_01/02 + lip-sync)
///     y vuelve a conduce_01 después de taxista_03. sfx_monedas con wilbur_taxi_02.
///  4. sfx_puerta_carro, negro 0.5 s, el jugador en Anchor_PlayerCurb mirando curbStandYaw (el lado de la acera).
///  5. Llegada (INTRO.2.1): a la vez vuelve la imagen, la mirada gira 1.8 s hacia Anchor_HouseLook y el taxi arranca por
///     Taxi_ExitPath (recto al oeste, a espaldas de la cámara: fuera de cuadro en toda la llegada) con sfx_taxi_arranca
///     (3D; N1.3: el motor baja en el último tramo). Al final del camino (antes de x -40, sin llegar a la barricada) se apaga,
///     y Release lo apaga sí o sí antes de devolver el control (sin esperar Renderer.isVisible). → wilbur_casa_01 →
///     pensamiento fijo de la IA (I9, AICompanion.ShowCustomLine, sin API) → RevealThoughtEnded (N1.3: la tarjeta
///     "Pensamientos" de TutorialDirector; la secuencia espera mientras HoldAfterReveal) → cartel "PLOMO Y PAJA" (opcional).
///  PUNTO BLANCO (INTRO.2.1): CinematicState.CenterDot solo sentado en el taxi con la imagen clara (se puede mirar);
///     apagado en los fundidos, en la llegada (giro, línea, pensamiento, cartel) y al saltar.
///  6. Fuera franjas y subtítulos; el control vuelve desde donde quedó la vista (sin salto; nunca toca el FOV).
///
/// MIENTRAS DURA (CinematicState.IsPlaying): PlayerAim.GameplayBlocked() = true (sin manos, mira ni disparo), la pausa
/// con Esc está desactivada, los guardias quedan quietos y ciegos, y el jugador no recibe daño (I7).
/// SALTAR (I5): mantener ESPACIO/Esc 1 s → negro, jugador en la acera mirando la casa, taxi fuera, control devuelto.
/// UNA VEZ POR SESIÓN (I6): GameStateTracker.IntroCinematicPlayed (sobrevive a recargar la escena) + una bandera estática
/// de respaldo. Al reintentar tras morir, el jugador ya está en la acera (el builder lo pone ahí) y no hay cinemática.
/// ARMA (INTRO.3): si la cinemática corre en esta carga (PlaysThisLoad), PlayerWeaponHolster arranca enfundada y así
/// queda también al saltar; se saca con el primer guardia a la vista, daño, alerta o el trigger de la puerta.
/// </summary>
public class IntroCinematicDirector : MonoBehaviour
{
    [Serializable]
    public class Line
    {
        public string speaker;
        public string voiceId;
        [TextArea(1, 3)] public string text;
        [Tooltip("La dice el taxista (gira la cabeza y mueve la boca)")]
        public bool taxista;
        [Tooltip("Efecto 2D que suena con la línea (ej: sfx_monedas)")]
        public string sfx;
    }

    private static bool _playedThisSession;

    [Header("General")]
    [Tooltip("Apagar para probar el nivel sin la cinemática")]
    [SerializeField] private bool playIntroCinematic = true;
    [SerializeField] private bool showTitleCard = true;
    [SerializeField] private bool debug = false;

    [Header("Taxi y anclas (las pone el builder)")]
    [SerializeField] private Transform taxi;
    [SerializeField] private TaxiInterior interior;
    [SerializeField] private Transform seat;
    [SerializeField] private LipSyncDriver taxistaLipSync;
    [SerializeField] private WaypointPath arrivePath;
    [SerializeField] private WaypointPath exitPath;
    [SerializeField] private Transform playerCurb;
    [SerializeField] private Transform houseLook;

    [Header("Pantalla")]
    [SerializeField] private ScreenFade fade;
    [SerializeField] private Letterbox letterbox;
    [SerializeField] private CinematicSubtitles subtitles;
    [SerializeField] private CanvasGroup titleCard;
    [SerializeField] private HoldToSkip skip;

    [Header("Audio")]
    [SerializeField] private AudioSource taxistaVoice;
    [SerializeField] private AudioSource wilburVoice;
    [SerializeField] private AudioSource ambience;
    [SerializeField] private AudioSource wipers;
    [Tooltip("En el taxi (3D): frenada y arranque")]
    [SerializeField] private AudioSource taxiEngine;
    [SerializeField] private string ambienceId = "amb_taxi_interior";
    [SerializeField] private string wipersId = "sfx_limpiaparabrisas";
    [SerializeField] private string brakeId = "sfx_taxi_frenar";
    [SerializeField] private string doorId = "sfx_puerta_carro";
    [SerializeField] private string driveAwayId = "sfx_taxi_arranca";
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float wipersVolume = 0.35f;
    [Tooltip("Corte del paso bajo de la lluvia que llega del libro mientras se está dentro del taxi (Hz)")]
    [SerializeField] private float carriedRainCutoff = 1100f;

    [Header("Diálogo (guion §3 y §4)")]
    [SerializeField] private Line[] taxiLines = new Line[0];
    [SerializeField] private Line houseLine = new Line();
    [TextArea(2, 4)] [SerializeField] private string aiThought = "";

    [Header("Tiempos (s)")]
    [SerializeField] private float fadeInSeconds = 0.8f;
    [SerializeField] private float driveSeconds = 9f;
    [Tooltip("Cuánto antes de parar suena la frenada")]
    [SerializeField] private float brakeLead = 1.4f;
    [SerializeField] private float afterStopSeconds = 0.6f;
    [SerializeField] private float lineGap = 0.4f;
    [SerializeField] private float exitFadeSeconds = 0.5f;
    [Tooltip("N1.3: el camino ahora es de 22 m (antes 45 m en 7 s)")]
    [SerializeField] private float exitDriveSeconds = 4.5f;
    [Tooltip("N1.3: el motor baja a cero en este último tramo: el taxi se apaga cerca y no debe cortarse el sonido")]
    [SerializeField] private float exitEngineFadeSeconds = 1.5f;
    [SerializeField] private float revealSeconds = 1.8f;
    [Tooltip("Hacia dónde mira Wilbur al bajarse (yaw del mundo, 0 = +z). 20 = hacia la cerca de la acera: el giro a la " +
             "casa va a la derecha y el taxi, que se va al oeste, nunca entra en cuadro (INTRO.2.1)")]
    [SerializeField] private float curbStandYaw = 20f;
    [SerializeField] private float aiThoughtSeconds = 5.5f;
    [SerializeField] private float titleFadeSeconds = 0.6f;
    [SerializeField] private float titleHoldSeconds = 2f;
    [SerializeField] private float skipFadeSeconds = 0.4f;
    [Tooltip("Sin clip de voz: segundos por carácter (mínimo 1.5 s)")]
    [SerializeField] private float fallbackSecondsPerChar = 0.06f;

    [Header("Mirada dentro del taxi")]
    [SerializeField] private float yawLimit = 60f;
    [SerializeField] private float pitchLimit = 30f;
    [SerializeField] private float lookSensitivity = 2f;

    private PlayerController _player;
    private CharacterController _body;
    private ShotgunController[] _weapons = new ShotgunController[0];
    private bool[] _weaponsWereEnabled = new bool[0];
    private Transform _camera;
    private Transform _cameraParent;
    private Vector3 _cameraLocalPos;
    private Quaternion _cameraLocalRot;
    private bool _seated;
    private float _seatYaw, _seatPitch;
    private bool _playing;
    private bool _finished;
    private bool _skipRequested;
    private bool _holdAfterReveal;
    private Coroutine _main;
    private Coroutine _exitDrive;
    private AudioLowPassFilter[] _carriedFilters = new AudioLowPassFilter[0];

    /// <summary>¿La cinemática del taxi corre en esta carga? (válido desde Awake; lo lee PlayerWeaponHolster en Start)</summary>
    public bool PlaysThisLoad => _playing;

    /// <summary>N1.3: terminó el pensamiento fijo de la llegada (no se dispara si se saltó antes). Lo escucha TutorialDirector.</summary>
    public event Action RevealThoughtEnded;

    /// <summary>N1.3: la secuencia se queda después de RevealThoughtEnded (y no se puede saltar) hasta que se pasa a false.</summary>
    public void SetHoldAfterReveal(bool hold)
    {
        _holdAfterReveal = hold;
        if (skip != null && !_finished && !_skipRequested) skip.Armed = !hold;
    }

    public static bool HasPlayed =>
        _playedThisSession || (GameStateTracker.Instance != null && GameStateTracker.Instance.IntroCinematicPlayed);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _playedThisSession = false;

    private void Awake()
    {
        _playing = playIntroCinematic && !HasPlayed;
        if (_playing)
        {
            CinematicState.Set(this, true); // antes del primer Update de los guardias
            if (fade != null) fade.Alpha = 1f;
            if (letterbox != null) letterbox.Show(true, true);
        }
        else
        {
            if (fade != null) fade.Alpha = 0f;
            if (letterbox != null) letterbox.Show(false, true);
            if (taxi != null) taxi.gameObject.SetActive(false);
        }
        if (titleCard != null) titleCard.alpha = 0f;
        if (subtitles != null) subtitles.Hide(true);
    }

    private void Start()
    {
        if (!_playing) return;
        if (!FindPlayer())
        {
            Debug.LogWarning("[Intro] No hay Player en la escena: se salta la cinemática del taxi.");
            Release();
            return;
        }
        UiHideReasons.Set(UiHideReasons.Reason.Cinematic, true);
        LockPlayer();
        PlacePlayerAtCurb(playerCurb != null ? playerCurb.eulerAngles.y : _player.transform.eulerAngles.y, 0f);
        if (skip != null)
        {
            skip.Skipped += HandleSkip;
            skip.Armed = true;
        }
        if (taxistaLipSync != null && taxistaVoice != null) taxistaLipSync.SetSource(taxistaVoice);
        _main = StartCoroutine(Run());
    }

    private void OnDestroy()
    {
        if (skip != null) skip.Skipped -= HandleSkip;
        CinematicState.Set(this, false);
    }

    // ==================================================================
    // Secuencia
    // ==================================================================

    private IEnumerator Run()
    {
        // 1. En el taxi, ya andando
        SitInTaxi();
        MuffleCarriedRain(true);
        StartLoop(ambience, ambienceId, ambienceVolume);
        StartLoop(wipers, wipersId, wipersVolume);
        if (interior != null) interior.Driving = true;
        PlaceTaxiOn(arrivePath, 0f);
        Log("fundido de entrada");
        if (fade != null) StartCoroutine(fade.FadeTo(0f, fadeInSeconds));
        StartCoroutine(CenterDotAfter(fadeInSeconds)); // punto blanco cuando ya se ve (INTRO.2.1)

        // 2. Recorrido con aceleración y frenada suaves
        yield return Drive(arrivePath, driveSeconds, true);
        if (interior != null) { interior.Driving = false; interior.SetSpeed(0f); }
        FadeLoop(wipers, 0.6f);
        yield return Wait(afterStopSeconds);

        // 3. Diálogo
        for (int i = 0; i < taxiLines.Length; i++)
        {
            Line line = taxiLines[i];
            if (line.taxista && interior != null) interior.SetTalking(true);
            yield return Say(line);
            if (line.taxista && line.voiceId == "taxista_03" && interior != null) interior.SetTalking(false);
            yield return Wait(lineGap);
        }
        if (interior != null) interior.SetTalking(false);

        // 4. Bajada
        CinematicState.SetCenterDot(this, false);
        AudioLibrary.PlaySfx2D(doorId);
        if (fade != null) yield return fade.FadeTo(1f, exitFadeSeconds);
        StandUpAtCurb();
        FadeLoop(ambience, 0.4f);
        MuffleCarriedRain(false);

        // 5. Llegada: vuelve la imagen, gira hacia la casa y el taxi arranca, todo a la vez (INTRO.2.1)
        _exitDrive = StartCoroutine(DriveAway());
        if (fade != null) StartCoroutine(fade.FadeTo(0f, exitFadeSeconds));
        yield return LookAt(houseLook, revealSeconds);
        yield return Say(houseLine);
        if (!string.IsNullOrWhiteSpace(aiThought))
        {
            if (AICompanion.Instance != null) AICompanion.Instance.ShowCustomLine(aiThought);
            else Debug.LogWarning("[Intro] No hay AICompanion: el pensamiento de la llegada no se muestra.");
            yield return Wait(aiThoughtSeconds);
        }
        RevealThoughtEnded?.Invoke();
        while (_holdAfterReveal && !_skipRequested) yield return null;
        if (showTitleCard && titleCard != null)
        {
            yield return FadeGroup(titleCard, 1f, titleFadeSeconds);
            yield return Wait(titleHoldSeconds);
            yield return FadeGroup(titleCard, 0f, titleFadeSeconds);
        }

        // 6. Control de vuelta
        Release();
    }

    private IEnumerator Say(Line line)
    {
        if (line == null || string.IsNullOrEmpty(line.text)) yield break;
        AudioSource source = line.taxista ? taxistaVoice : wilburVoice;
        AudioClip clip = AudioLibrary.GetVoice(line.voiceId);
        float length = clip != null ? clip.length : Mathf.Max(1.5f, line.text.Length * fallbackSecondsPerChar);
        if (clip == null) Debug.LogWarning("[Intro] Falta la voz " + line.voiceId + ": solo subtítulo.");
        if (clip != null && source != null)
        {
            source.clip = clip;
            source.volume = AudioLibrary.VoiceVolume;
            source.Play();
        }
        if (!string.IsNullOrEmpty(line.sfx)) AudioLibrary.PlaySfx2D(line.sfx);
        if (subtitles != null) subtitles.Show(line.speaker, line.text);
        yield return Wait(length);
        if (subtitles != null && !_skipRequested) subtitles.Hide();
    }

    // ==================================================================
    // Taxi
    // ==================================================================

    private void PlaceTaxiOn(WaypointPath path, float distance)
    {
        if (taxi == null || path == null) return;
        path.Sample(distance, out Vector3 pos, out Vector3 fwd);
        taxi.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, Vector3.up));
    }

    /// <summary>Recorre el camino con suavizado de entrada y salida. brake = suena la frenada al final.</summary>
    private IEnumerator Drive(WaypointPath path, float seconds, bool brake)
    {
        if (taxi == null || path == null) yield break;
        float length = path.Length;
        bool braked = false;
        float last = 0f;
        for (float t = 0f; t < seconds && !_skipRequested; t += Time.unscaledDeltaTime)
        {
            float d = length * CinematicEase.InOut(t / seconds);
            PlaceTaxiOn(path, d);
            float dt = Mathf.Max(1e-4f, Time.unscaledDeltaTime);
            if (interior != null) interior.SetSpeed((d - last) / dt);
            last = d;
            if (brake && !braked && t >= seconds - brakeLead)
            {
                braked = true;
                PlayOnTaxi(brakeId);
            }
            yield return null;
        }
        PlaceTaxiOn(path, length);
    }

    /// <summary>
    /// El taxi se va (arranca suave) y se apaga al final del camino (INTRO.2.1: sin esperar Renderer.isVisible). El camino va
    /// recto al oeste, a espaldas de la cámara de la llegada. N1.3: termina antes de la barricada, todavía al alcance del motor,
    /// así que el motor baja a cero antes de apagarlo.
    /// </summary>
    private IEnumerator DriveAway()
    {
        if (taxi == null) yield break;
        if (interior != null) interior.Driving = false;
        PlayOnTaxi(driveAwayId);
        if (exitPath != null)
        {
            float length = exitPath.Length;
            float last = 0f;
            for (float t = 0f; t < exitDriveSeconds; t += Time.unscaledDeltaTime)
            {
                float u = t / exitDriveSeconds;
                float d = length * u * u * (2f - u); // arranca despacio, sale rápido
                PlaceTaxiOn(exitPath, d);
                if (interior != null) interior.SetSpeed((d - last) / Mathf.Max(1e-4f, Time.unscaledDeltaTime));
                last = d;
                if (taxiEngine != null) taxiEngine.volume = Mathf.Clamp01((exitDriveSeconds - t) / Mathf.Max(0.01f, exitEngineFadeSeconds));
                yield return null;
            }
            PlaceTaxiOn(exitPath, length);
        }
        if (interior != null) interior.SetSpeed(0f);
        _exitDrive = null; // esta misma corrutina: no hace falta pararla
        HideTaxi();
        Log("taxi al final del camino: apagado");
    }

    /// <summary>Apaga el taxi (fin del camino, saltar o devolver el control). Idempotente.</summary>
    private void HideTaxi()
    {
        if (_exitDrive != null)
        {
            StopCoroutine(_exitDrive);
            _exitDrive = null;
        }
        if (taxi != null && taxi.gameObject.activeSelf) taxi.gameObject.SetActive(false);
    }

    /// <summary>Punto blanco del centro cuando ya terminó el fundido de entrada al taxi (INTRO.2.1).</summary>
    private IEnumerator CenterDotAfter(float seconds)
    {
        yield return Wait(seconds);
        if (!_skipRequested && _seated) CinematicState.SetCenterDot(this, true);
    }

    private void PlayOnTaxi(string id)
    {
        AudioClip clip = AudioLibrary.GetSfx(id);
        if (clip == null || taxiEngine == null) return;
        taxiEngine.PlayOneShot(clip, AudioLibrary.SfxVolume);
    }

    // ==================================================================
    // Jugador y cámara (nunca toca el FOV)
    // ==================================================================

    private bool FindPlayer()
    {
        GameObject go = GameObject.FindWithTag("Player");
        if (go == null || !go.TryGetComponent(out _player)) return false;
        go.TryGetComponent(out _body);
        Transform holder = _player.CameraHolder;
        Camera cam = holder != null ? holder.GetComponentInChildren<Camera>(true) : null;
        if (cam == null) cam = Camera.main;
        if (cam == null) return false;
        _camera = cam.transform;
        _cameraParent = _camera.parent;
        _cameraLocalPos = _camera.localPosition;
        _cameraLocalRot = _camera.localRotation;
        return true;
    }

    private void LockPlayer()
    {
        _player.enabled = false;
        _weapons = FindObjectsOfType<ShotgunController>();
        _weaponsWereEnabled = new bool[_weapons.Length];
        for (int i = 0; i < _weapons.Length; i++)
        {
            _weaponsWereEnabled[i] = _weapons[i].enabled;
            _weapons[i].enabled = false;
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void PlacePlayerAtCurb(float yaw, float pitch)
    {
        if (_player == null) return;
        if (playerCurb != null)
        {
            bool had = _body != null && _body.enabled;
            if (_body != null) _body.enabled = false;
            _player.transform.position = playerCurb.position;
            if (_body != null) _body.enabled = had;
        }
        _player.SetViewAngles(yaw, pitch);
    }

    private void SitInTaxi()
    {
        if (_camera == null || seat == null) return;
        _camera.SetParent(seat, false);
        _camera.localPosition = Vector3.zero;
        _camera.localRotation = Quaternion.identity;
        _seatYaw = 0f;
        _seatPitch = 0f;
        _seated = true;
    }

    private void StandUpAtCurb()
    {
        _seated = false;
        if (_camera != null)
        {
            _camera.SetParent(_cameraParent, false);
            _camera.localPosition = _cameraLocalPos;
            _camera.localRotation = _cameraLocalRot;
        }
        PlacePlayerAtCurb(curbStandYaw, 0f);
    }

    private void Update()
    {
        if (!_seated || _camera == null) return;
        _seatYaw = Mathf.Clamp(_seatYaw + Input.GetAxis("Mouse X") * lookSensitivity * OptionsPanel.Sensibilidad, -yawLimit, yawLimit);
        _seatPitch = Mathf.Clamp(_seatPitch - Input.GetAxis("Mouse Y") * lookSensitivity * OptionsPanel.Sensibilidad, -pitchLimit, pitchLimit);
        _camera.localRotation = Quaternion.Euler(_seatPitch, _seatYaw, 0f);
    }

    /// <summary>Gira cuerpo (yaw) y cabeza (pitch) hacia el objetivo con suavizado; queda así para devolver el control.</summary>
    private IEnumerator LookAt(Transform target, float seconds)
    {
        if (target == null || _player == null || _camera == null) yield break;
        float fromYaw = _player.transform.eulerAngles.y;
        float fromPitch = _player.ViewPitch;
        Vector3 to = target.position - _camera.position;
        float toYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        float toPitch = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
        for (float t = 0f; t < seconds && !_skipRequested; t += Time.unscaledDeltaTime)
        {
            float e = CinematicEase.InOut(t / seconds);
            _player.SetViewAngles(Mathf.LerpAngle(fromYaw, toYaw, e), Mathf.Lerp(fromPitch, toPitch, e));
            yield return null;
        }
        if (!_skipRequested) _player.SetViewAngles(toYaw, toPitch);
    }

    // ==================================================================
    // Saltar y final
    // ==================================================================

    private void HandleSkip()
    {
        if (_finished || _skipRequested) return;
        _skipRequested = true;
        Log("saltar (mantenido)");
        if (_main != null) StopCoroutine(_main);
        StartCoroutine(SkipToEnd());
    }

    private IEnumerator SkipToEnd()
    {
        CinematicState.SetCenterDot(this, false);
        if (skip != null) skip.Armed = false;
        if (fade != null) yield return fade.FadeTo(1f, skipFadeSeconds);
        if (taxistaVoice != null) taxistaVoice.Stop();
        if (wilburVoice != null) wilburVoice.Stop();
        if (subtitles != null) subtitles.Hide(true);
        if (titleCard != null) titleCard.alpha = 0f;
        if (ambience != null) ambience.Stop();
        if (wipers != null) wipers.Stop();
        MuffleCarriedRain(false);
        HideTaxi();
        if (_seated) StandUpAtCurb();
        // Estado final: en la acera, mirando la casa
        if (houseLook != null && _camera != null && _player != null)
        {
            Vector3 to = houseLook.position - _camera.position;
            _player.SetViewAngles(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg,
                -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg);
        }
        Release();
        if (fade != null) yield return fade.FadeTo(0f, exitFadeSeconds);
    }

    /// <summary>Devuelve el control desde la vista actual. Idempotente.</summary>
    private void Release()
    {
        if (_finished) return;
        _finished = true;
        _seated = false;
        _playedThisSession = true;
        HideTaxi(); // INTRO.2.1: nunca queda un taxi en la calle cuando vuelve el control
        if (GameStateTracker.Instance != null) GameStateTracker.Instance.MarkIntroCinematicPlayed();
        if (skip != null) skip.Armed = false;
        if (letterbox != null) letterbox.Show(false);
        if (subtitles != null) subtitles.Hide();
        if (_player != null) _player.enabled = true;
        for (int i = 0; i < _weapons.Length; i++)
            if (_weapons[i] != null) _weapons[i].enabled = _weaponsWereEnabled[i];
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        UiHideReasons.Set(UiHideReasons.Reason.Cinematic, false);
        CinematicState.Set(this, false);
        Log("control devuelto");
    }

    // ==================================================================
    // Audio
    // ==================================================================

    private static void StartLoop(AudioSource source, string id, float volume)
    {
        if (source == null) return;
        AudioClip clip = AudioLibrary.GetSfx(id);
        if (clip == null) return;
        source.clip = clip;
        source.loop = true;
        source.volume = volume * AudioLibrary.SfxVolume;
        source.Play();
    }

    private void FadeLoop(AudioSource source, float seconds)
    {
        if (source != null && source.isPlaying) StartCoroutine(FadeOut(source, seconds));
    }

    private static IEnumerator FadeOut(AudioSource source, float seconds)
    {
        float from = source.volume;
        for (float t = 0f; t < seconds && source != null; t += Time.unscaledDeltaTime)
        {
            source.volume = Mathf.Lerp(from, 0f, t / seconds);
            yield return null;
        }
        if (source != null) source.Stop();
    }

    /// <summary>
    /// La lluvia que llega del libro (CarryOverAudio, sin cambios) se ahoga con un paso bajo mientras se está en el taxi,
    /// igual que la de RainEmitter (que detecta el techo del taxi). Al bajar se abre otra vez.
    /// </summary>
    private void MuffleCarriedRain(bool muffle)
    {
        if (muffle)
        {
            CarryOverAudio[] carried = FindObjectsOfType<CarryOverAudio>();
            _carriedFilters = new AudioLowPassFilter[carried.Length];
            for (int i = 0; i < carried.Length; i++)
            {
                if (!carried[i].TryGetComponent(out AudioLowPassFilter filter))
                    filter = carried[i].gameObject.AddComponent<AudioLowPassFilter>();
                filter.cutoffFrequency = carriedRainCutoff;
                _carriedFilters[i] = filter;
            }
        }
        else
        {
            for (int i = 0; i < _carriedFilters.Length; i++)
                if (_carriedFilters[i] != null) _carriedFilters[i].cutoffFrequency = 22000f;
        }
    }

    // ==================================================================
    // Utilidades
    // ==================================================================

    /// <summary>Espera en tiempo real; corta si se pidió saltar (las corrutinas anidadas también terminan).</summary>
    private IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds && !_skipRequested; t += Time.unscaledDeltaTime) yield return null;
    }

    private IEnumerator FadeGroup(CanvasGroup group, float target, float seconds)
    {
        float from = group.alpha;
        for (float t = 0f; t < seconds && !_skipRequested; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, target, CinematicEase.InOut(t / seconds));
            yield return null;
        }
        if (!_skipRequested) group.alpha = target;
    }

    private void Log(string message)
    {
        if (debug) Debug.Log("[Intro] " + message);
    }
}
