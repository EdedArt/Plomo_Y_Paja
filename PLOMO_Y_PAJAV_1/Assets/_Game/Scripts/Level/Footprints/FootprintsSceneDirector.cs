using System.Collections;
using UnityEngine;

/// <summary>
/// Escena de las huellas (N1.4, N9; guion tutorial-huellas §3). Tiempo real: el mundo sigue con timeScale 1.
/// DÓNDE VA: objeto "Huellas_Escena" de "--- HUELLAS ---" en NivelPorky, con BoxCollider trigger detrás del cuarto
/// (Plomo y Paja → Nivel 1 → Colocar huellas, también desde Generar Nivel Porky y Ampliar mapa). Capa Ignore Raycast.
///
/// SE ARMA con el jugador en el trigger si: Centavo está Dead o Pardoned, terminó su epílogo (InterrogationDirector.IsActive = false y
/// sin el pensamiento de rematarlo pendiente), nada bloquea el juego (pausa, muerte, interrogatorio, cinemática, diálogo) y
/// hace quietAfterLine s que no sale otra línea de la IA. Con Centavo vivo o herido no pasa nada: el portón sigue con cadena.
///
/// SECUENCIA: franjas 0.4 s, jugador bloqueado, sin manos ni HUD (UiHideReasons.Cinematic) → mira las huellas 1.2 s → recorre el
/// rastro a la altura de los ojos 3 s hasta el portón → wilbur_huellas_01 → pensamiento de la IA (se pide al empezar, tope de 5 s
/// reales desde ese momento; si no, respaldo del guion) → 1 s → wilbur_cadena_01 → disparo, cadena, portón 0.6 s → la cámara
/// vuelve a la cabeza 0.5 s desde donde esté, franjas fuera, control. Nunca toca el FOV (lo lleva PlayerAim).
///
/// GUARDIAS (I7): CinematicState.IsPlaying los deja quietos y ciegos y el jugador no recibe daño.
/// LÍNEAS DE LA IA: con el jugador bloqueado y los guardias quietos no hay pistas ni bajas nuevas. Una línea que ya venía en camino
/// (AICompanion no se puede cancelar) se tapa volviendo a mostrar el pensamiento de la escena.
/// SALTAR (HoldToSkip): negro, portón abierto, todo restaurado.
/// UNA VEZ POR SESIÓN: _playedThisSession, estática; no está en la foto de GameStateTracker. Solo "Jugar" la borra. Morir y reiniciar
/// devuelve a Centavo (N2.1.1): el portón vuelve a estar cerrado y, al resolverlo otra vez, se abre solo, sin repetir la escena.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class FootprintsSceneDirector : MonoBehaviour
{
    private const string Speaker = "Wilbur";
    private const string TrailLineId = "wilbur_huellas_01";
    private const string TrailLine = "Huellas de lobo… y las de Porky. Por aquí se lo llevaron.";
    private const string ChainLineId = "wilbur_cadena_01";
    private const string ChainLine = "Con permiso.";

    private const string ThoughtSituation =
        "Wilbur sigue unas huellas en el barro detrás del cuarto de herramientas de la casa de Porky. Hay huellas de lobo y de cerdo, " +
        "una al lado de la otra, al mismo paso; no hay marcas de arrastre. Van hacia el bosque, donde se ve una luz lejana. Wilbur cree " +
        "que a Porky se lo llevaron a la fuerza.\n" +
        "Escribe un pensamiento de 1 a 2 oraciones, máximo 25 palabras, en español, tono noir, ambiguo. Puede sembrar la duda, pero " +
        "nunca afirmes que Porky fue por su cuenta.";

    private static readonly string[] FallbackThoughts =
    {
        "Dos pares de huellas, al mismo paso. Raro… los secuestrados suelen arrastrar los pies.",
        "El barro no miente, Wilbur. Lo que no dice es quién caminaba adelante.",
        "Si se lo llevaron a la fuerza, alguien le enseñó a caminar muy tranquilo.",
    };
    private const string FallbackPardoned = "Centavo se fue cojeando para el otro lado. Estas huellas no cojean.";
    private const string FallbackDead = "Al contador ya no le puedes preguntar. A las huellas, sí.";

    private static bool _playedThisSession;

    [Header("Lo pone el builder")]
    [SerializeField] private ForestGate gate;
    [Tooltip("Hacia dónde queda la vista al final: el bosque, al fondo del sendero")]
    [SerializeField] private Transform forestLook;
    [SerializeField] private AudioSource voice;
    [Tooltip("El rastro, en coordenadas de mundo")]
    [SerializeField] private Vector3[] trail = new Vector3[0];

    [Header("Tiempos (s)")]
    [SerializeField] private float lookDownSeconds = 1.2f;
    [SerializeField] private float glideSeconds = 3f;
    [SerializeField] private float thoughtTimeout = 5f;
    [SerializeField] private float beforeChainSeconds = 1f;
    [SerializeField] private float afterShotSeconds = 0.6f;
    [SerializeField] private float cameraBackSeconds = 0.5f;
    [SerializeField] private float skipFadeSeconds = 0.4f;
    [Tooltip("No empezar si otra línea de la IA acaba de salir: que se alcance a leer")]
    [SerializeField] private float quietAfterLine = 3f;
    [Tooltip("Sin clip de voz: segundos por carácter (mínimo 1.5 s)")]
    [SerializeField] private float secondsPerCharacter = 0.06f;

    [Header("Cámara y pensamiento")]
    [Tooltip("La cámara se detiene a esta distancia antes del portón")]
    [SerializeField] private float stopBeforeGate = 2.1f;
    [Tooltip("Las huellas que mira primero: a esta distancia, rastro adelante")]
    [SerializeField] private float printsAhead = 1.2f;
    [SerializeField] private string shotSfxId = "escopeta_disparo";
    [Tooltip("La IA a veces se pasa de las 25 palabras del guion. Con más que esto se usa el respaldo")]
    [SerializeField] private int maxThoughtWords = 30;

    private PlayerController _player;
    private Transform _camera, _cameraParent;
    private Vector3 _cameraLocalPos;
    private Quaternion _cameraLocalRot;
    private CentavoController _centavo;
    private AICompanion _ai;
    private Letterbox _letterbox;
    private CinematicSubtitles _subtitles;
    private ScreenFade _fade;
    private HoldToSkip _skip;

    private bool _inside, _running, _skipRequested;
    private float _lastLineAt = -100f;
    private float _requestedAt;
    private bool _aiDone;
    private string _aiLine;
    private string _thoughtOnScreen;
    private bool _reshowThought;
    private Coroutine _main;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _playedThisSession = false;
        GameStateTracker.NewGameStarted -= ResetSession;
        GameStateTracker.NewGameStarted += ResetSession;
    }

    /// <summary>Partida nueva: la escena vuelve a salir.</summary>
    public static void ResetSession() => _playedThisSession = false;

    private void Start()
    {
        _centavo = FindObjectOfType<CentavoController>(true);
        // Las piezas de pantalla son las de la cinemática del taxi: se reconstruyen con su propio menú, así que no se guardan
        _letterbox = FindObjectOfType<Letterbox>(true);
        _subtitles = FindObjectOfType<CinematicSubtitles>(true);
        _skip = FindObjectOfType<HoldToSkip>(true);
        _fade = FindSceneFade(gameObject);
        _ai = AICompanion.Instance;
        if (_ai != null) _ai.OnLineSpoken += HandleLineSpoken;
    }

    private void OnDestroy()
    {
        if (_ai != null) _ai.OnLineSpoken -= HandleLineSpoken;
        if (_skip != null) _skip.Skipped -= HandleSkip;
        if (_running) UiHideReasons.Set(UiHideReasons.Reason.Cinematic, false); // es estático: no debe quedar prendido en la escena siguiente
        CinematicState.Set(this, false);
    }

    /// <summary>El ScreenFade de esta escena. El de LevelDeathRestart sobrevive a las recargas y está apagado: no sirve.</summary>
    public static ScreenFade FindSceneFade(GameObject owner)
    {
        foreach (ScreenFade fade in FindObjectsOfType<ScreenFade>(true))
            if (fade.gameObject.scene == owner.scene) return fade;
        return null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) _inside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) _inside = false;
    }

    private void HandleLineSpoken(string line)
    {
        _lastLineAt = Time.unscaledTime;
        // No se puede volver a mostrar aquí: AICompanion arranca la línea nueva después de este evento y taparía la nuestra
        if (_thoughtOnScreen != null && line != _thoughtOnScreen) _reshowThought = true;
    }

    private void Update()
    {
        if (_running) return;
        GameStateTracker tracker = GameStateTracker.Instance;
        if (tracker == null) return;
        ContadorState state = tracker.Contador;
        if (state != ContadorState.Dead && state != ContadorState.Pardoned) return;
        if (InterrogationDirector.IsActive || (_centavo != null && _centavo.FinishedOffThoughtPending)) return;

        if (_playedThisSession)
        {
            if (gate != null) gate.OpenInstant();
            enabled = false;
            return;
        }

        if (!_inside || PlayerAim.GameplayBlocked() || UiHideReasons.IsActive(UiHideReasons.Reason.Death)) return;
        if (Time.unscaledTime - _lastLineAt < quietAfterLine) return;
        Begin();
    }

    // ==================================================================
    // Secuencia
    // ==================================================================

    private void Begin()
    {
        _playedThisSession = true;
        if (!FindPlayer())
        {
            Debug.LogWarning("[Huellas] Sin jugador o sin cámara: el portón se abre sin escena.");
            if (gate != null) gate.OpenInstant();
            enabled = false;
            return;
        }

        _running = true;
        CinematicState.Set(this, true);
        UiHideReasons.Set(UiHideReasons.Reason.Cinematic, true);
        _player.enabled = false;
        if (_letterbox != null) _letterbox.Show(true);
        if (_skip != null)
        {
            _skip.Rearm(); // si se saltó el taxi quedó marcado como saltado
            _skip.Skipped += HandleSkip;
            _skip.Armed = true;
        }
        RequestThought();
        _main = StartCoroutine(Run());
    }

    private bool FindPlayer()
    {
        GameObject go = GameObject.FindWithTag("Player");
        if (go == null || !go.TryGetComponent(out _player)) return false;
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

    private IEnumerator Run()
    {
        // La cámara queda suelta hasta el final: el cuerpo no se mueve
        _camera.SetParent(null, true);
        Vector3 eye = _camera.position;

        // 1. Baja la vista a las huellas
        Quaternion printsLook = LookFrom(eye, PrintsFocus(eye));
        yield return MoveCamera(eye, printsLook, lookDownSeconds);

        // 2. Sigue el rastro a la altura de los ojos, hasta el portón
        Vector3 gatePos = gate != null ? gate.transform.position : eye + _player.transform.forward * 4f;
        Vector3 gateForward = gate != null ? gate.transform.forward : _player.transform.forward;
        Vector3 stop = gatePos - gateForward * stopBeforeGate;
        stop.y = eye.y;
        Vector3 chainPoint = gate != null ? gate.ChainPoint : stop + gateForward;
        yield return MoveCamera(stop, LookFrom(stop, chainPoint), glideSeconds);

        // 3. Wilbur
        yield return Say(TrailLineId, TrailLine);

        // 4. La IA
        yield return ShowThought();

        // 5. La cadena
        yield return Wait(beforeChainSeconds);
        yield return Say(ChainLineId, ChainLine);
        AudioLibrary.PlaySfx2D(shotSfxId);
        if (gate != null) gate.BreakChainAndOpen();
        yield return MoveCamera(stop, LookFrom(stop, ForestPoint(stop, gateForward)), afterShotSeconds);

        // 6. Vuelve a la cabeza desde donde está (sin salto)
        FaceForest(gateForward);
        Vector3 fromPos = _camera.position;
        Quaternion fromRot = _camera.rotation;
        for (float t = 0f; t < cameraBackSeconds && !_skipRequested; t += Time.unscaledDeltaTime)
        {
            float e = CinematicEase.InOut(t / cameraBackSeconds);
            _camera.position = Vector3.Lerp(fromPos, _cameraParent.TransformPoint(_cameraLocalPos), e);
            _camera.rotation = Quaternion.Slerp(fromRot, _cameraParent.rotation * _cameraLocalRot, e);
            yield return null;
        }
        if (_skipRequested) yield break;
        ReattachCamera();
        Release();
    }

    private void RequestThought()
    {
        _aiDone = false;
        _aiLine = null;
        _requestedAt = Time.realtimeSinceStartup;
        if (_ai != null) _ai.RequestCustomLine(ThoughtSituation, line => { _aiDone = true; _aiLine = line; });
        else _aiDone = true;
    }

    private IEnumerator ShowThought()
    {
        // Se pidió al empezar: casi siempre ya llegó. Una respuesta después del tope no se muestra
        while (!_aiDone && Time.realtimeSinceStartup - _requestedAt < thoughtTimeout && !_skipRequested) yield return null;
        if (_skipRequested) yield break;

        bool fromApi = _aiDone && IsUsable(_aiLine);
        string line = fromApi ? _aiLine : PickFallback();
        if (_ai != null) _ai.ShowCustomLine(line, !fromApi); // la de la API ya la guardó RequestCustomLine
        else if (_subtitles != null) _subtitles.Show("", line);
        _thoughtOnScreen = line;

        float seconds = Mathf.Clamp(line.Length * 0.07f, 3.5f, 8f);
        for (float t = 0f; t < seconds && !_skipRequested; t += Time.unscaledDeltaTime)
        {
            if (_reshowThought)
            {
                _reshowThought = false;
                if (_ai != null) _ai.ShowCustomLine(line, false);
            }
            yield return null;
        }
        _thoughtOnScreen = null;
        if (_ai == null && _subtitles != null && !_skipRequested) _subtitles.Hide();
    }

    private bool IsUsable(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return false;
        int words = line.Trim().Split(new[] { ' ', '\n', '\t' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
        if (words <= maxThoughtWords) return true;
        Debug.LogWarning($"[Huellas] La IA devolvió {words} palabras: se usa el respaldo.");
        return false;
    }

    private static string PickFallback()
    {
        ContadorState state = GameStateTracker.Instance != null ? GameStateTracker.Instance.Contador : ContadorState.None;
        string conditional = state == ContadorState.Pardoned ? FallbackPardoned : state == ContadorState.Dead ? FallbackDead : null;
        int count = FallbackThoughts.Length + (conditional != null ? 1 : 0);
        int pick = Random.Range(0, count);
        return pick < FallbackThoughts.Length ? FallbackThoughts[pick] : conditional;
    }

    private IEnumerator Say(string id, string text)
    {
        AudioClip clip = AudioLibrary.GetVoice(id);
        float length = clip != null ? clip.length : Mathf.Max(1.5f, text.Length * secondsPerCharacter);
        if (clip == null) Debug.LogWarning("[Huellas] Falta la voz " + id + ": solo subtítulo.");
        if (clip != null && voice != null)
        {
            voice.clip = clip;
            voice.volume = AudioLibrary.VoiceVolume;
            voice.Play();
        }
        if (_subtitles != null) _subtitles.Show(Speaker, text);
        yield return Wait(length);
        if (_subtitles != null && !_skipRequested) _subtitles.Hide();
    }

    // ==================================================================
    // Cámara
    // ==================================================================

    private IEnumerator MoveCamera(Vector3 toPos, Quaternion toRot, float seconds)
    {
        Vector3 fromPos = _camera.position;
        Quaternion fromRot = _camera.rotation;
        for (float t = 0f; t < seconds && !_skipRequested; t += Time.unscaledDeltaTime)
        {
            float e = CinematicEase.InOut(t / seconds);
            _camera.position = Vector3.Lerp(fromPos, toPos, e);
            _camera.rotation = Quaternion.Slerp(fromRot, toRot, e);
            yield return null;
        }
        if (_skipRequested) yield break;
        _camera.position = toPos;
        _camera.rotation = toRot;
    }

    private static Quaternion LookFrom(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        return dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : Quaternion.identity;
    }

    /// <summary>El punto del rastro más cercano a los ojos, printsAhead metros más adelante.</summary>
    private Vector3 PrintsFocus(Vector3 eye)
    {
        if (trail == null || trail.Length < 2) return eye + _player.transform.forward * 2f + Vector3.down * 1.7f;

        int bestSegment = 0;
        float bestT = 0f, bestDistance = float.MaxValue;
        for (int i = 0; i < trail.Length - 1; i++)
        {
            Vector3 a = trail[i], ab = trail[i + 1] - a;
            float t = Mathf.Clamp01(Vector3.Dot(eye - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            Vector3 flat = a + ab * t - eye;
            flat.y = 0f;
            if (flat.sqrMagnitude >= bestDistance) continue;
            bestDistance = flat.sqrMagnitude;
            bestSegment = i;
            bestT = t;
        }

        float remaining = printsAhead;
        Vector3 point = Vector3.Lerp(trail[bestSegment], trail[bestSegment + 1], bestT);
        for (int i = bestSegment; i < trail.Length - 1; i++)
        {
            float length = Vector3.Distance(point, trail[i + 1]);
            if (length >= remaining) return Vector3.MoveTowards(point, trail[i + 1], remaining);
            remaining -= length;
            point = trail[i + 1];
        }
        return point;
    }

    private Vector3 ForestPoint(Vector3 from, Vector3 gateForward)
    {
        if (forestLook != null) return forestLook.position;
        return from + gateForward * 30f + Vector3.up * 0.5f;
    }

    /// <summary>El cuerpo y la cabeza quedan mirando al bosque: ahí vuelve la cámara y así queda el control.</summary>
    private void FaceForest(Vector3 gateForward)
    {
        if (_player == null || _cameraParent == null) return;
        Vector3 head = _cameraParent.position;
        Vector3 to = ForestPoint(head, gateForward) - head;
        _player.SetViewAngles(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg);
    }

    private void ReattachCamera()
    {
        if (_camera == null || _cameraParent == null) return;
        _camera.SetParent(_cameraParent, false);
        _camera.localPosition = _cameraLocalPos;
        _camera.localRotation = _cameraLocalRot;
    }

    // ==================================================================
    // Saltar y final
    // ==================================================================

    private void HandleSkip()
    {
        if (!_running || _skipRequested) return;
        _skipRequested = true;
        if (_main != null) StopCoroutine(_main);
        StartCoroutine(SkipToEnd());
    }

    private IEnumerator SkipToEnd()
    {
        if (_skip != null) _skip.Armed = false;
        if (_fade != null) yield return _fade.FadeTo(1f, skipFadeSeconds);
        if (voice != null) voice.Stop();
        if (_subtitles != null) _subtitles.Hide(true);
        _thoughtOnScreen = null;
        if (gate != null) gate.OpenInstant();
        FaceForest(gate != null ? gate.transform.forward : _player.transform.forward);
        ReattachCamera();
        Release();
        if (_fade != null) yield return _fade.FadeTo(0f, skipFadeSeconds);
    }

    /// <summary>Devuelve el control desde la vista actual.</summary>
    private void Release()
    {
        _running = false;
        if (_skip != null)
        {
            _skip.Armed = false;
            _skip.Skipped -= HandleSkip;
        }
        if (_letterbox != null) _letterbox.Show(false);
        if (_subtitles != null) _subtitles.Hide();
        if (_player != null) _player.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        UiHideReasons.Set(UiHideReasons.Reason.Cinematic, false);
        CinematicState.Set(this, false);
        enabled = false;
    }

    private IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds && !_skipRequested; t += Time.unscaledDeltaTime) yield return null;
    }
}
