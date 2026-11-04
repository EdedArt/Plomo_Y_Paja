using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Don Lobo, el jefe final, en el salón de la guarida (fase N2.2, decisiones L6 y L11).
/// DÓNDE VA: raíz del prefab Prefabs/Level2/DonLobo. Lo crea y lo coloca Plomo y Paja → Nivel 2 → Colocar Don Lobo (y también
/// Generar Campamento). Las anclas salen de la escena, nunca de coordenadas fijas.
///
/// ESTADOS:
///   Dormant → Intro → Combat → Hit → Kneeling → (Confrontation, N2.3) → Dead | Pardoned
/// - Dormant: idle detrás de su escritorio, ignora al jugador. Despierta con Trigger_Salon o con el primer tiro.
/// - Intro: lobo_combate_01 mientras se pone de pie.
/// - Combat: Cover → Peek → Telegraph (0.4 s, cuadro de apuntar y un brillo) → Burst (3 tiros) → Cover, con cambios de cubierta
///   (Move, a 2.5 m/s por el NavMesh, nunca hacia el jugador). Al 50 %: Call (call_01, lobo_combate_04, 2 refuerzos) y de ahí en
///   adelante vuelve al escritorio y dispara más seguido.
/// - Hit: al llegar al umbral cae de rodillas. Ese golpe no puede matarlo: la vida se queda en el umbral. hit_01, lobo_cae_01,
///   suelta la pistola y los refuerzos que sigan vivos se rinden.
/// - Kneeling: par rodillas con lip-sync, lobo_rodillas_01 una vez. Jugador a ≤ 3 m con vista → OnConfrontationRequested (N2.3).
///   Si le disparan aquí, antes de la escena → Dead sin confrontación, y a los 2.5 s el final.
/// - Confrontation / Dead / Pardoned: los resuelve N2.3 con BeginConfrontation, ResolveKilled y ResolvePardoned.
///
/// DAÑO: IDamageable. Vida y tope por disparo están pensados para las dos armas (24 por bala, hasta 8 × 15 por cartucho): todo lo
/// que llega en el mismo cuadro (los perdigones de un cartucho) suma como máximo maxDamagePerShot.
/// Cubierto, la cápsula baja de la altura del mueble: escritorio y columnas se llevan las balas, de ida y de vuelta.
/// TAMAÑO (L11): la raíz del prefab va a ×1.2; las medidas de aquí son a escala 1 o se multiplican por WorldScale.
/// Morir en la pelea recarga la escena (LevelDeathRestart): él vuelve a Dormant y la foto del tracker deja LoboState en None.
/// </summary>
public class LoboBossController : MonoBehaviour, IDamageable, IInterrogationTarget
{
    public enum State { Dormant, Intro, Combat, Hit, Kneeling, Confrontation, Dead, Pardoned }

    private enum CombatPhase { Cover, Peek, Telegraph, Burst, Move, Call }

    // Líneas del guion (Docs/guiones/nivel2.md §2)
    private const string LineIntro = "lobo_combate_01";
    private const string LineShooting = "lobo_combate_02";
    private const string LinePeek = "lobo_combate_03";
    private const string LineCall = "lobo_combate_04";
    private const string LineCover = "lobo_combate_05";
    private const string LineFall = "lobo_cae_01";
    private const string LineKneeling = "lobo_rodillas_01";

    [Header("Escena (los pone el menú)")]
    [SerializeField] private Transform startAnchor;
    [Tooltip("La 0 es la del escritorio: en la segunda fase vuelve ahí")]
    [SerializeField] private Transform[] coverAnchors = new Transform[0];
    [SerializeField] private Transform[] reinforcementAnchors = new Transform[0];
    [SerializeField] private Collider salonTrigger;
    [SerializeField] private Transform confrontationCam;

    [Header("Partes del prefab")]
    [SerializeField] private BillboardSprite billboard;
    [Tooltip("Padre del dibujo y del placeholder: se escala de rodillas y baja al cubrirse")]
    [SerializeField] private Transform visual;
    [SerializeField] private CapsuleCollider hitbox;
    [SerializeField] private AudioSource voiceSource;
    [SerializeField] private LipSyncDriver lipSync;
    [Tooltip("Pistola dorada suelta (apagada): solo adorno, cae al quedar de rodillas")]
    [SerializeField] private GameObject droppedPistol;
    [Tooltip("Enemy_Guard: los dos refuerzos")]
    [SerializeField] private GameObject reinforcementPrefab;

    [Header("Vida (balance: ver el audit N2.2)")]
    [SerializeField, Min(1f)] private float maxHealth = 280f;
    [Tooltip("Al llegar a esta fracción cae de rodillas (ese golpe no lo mata)")]
    [SerializeField, Range(0.01f, 0.5f)] private float kneelThreshold = 0.15f;
    [Tooltip("A esta fracción llama a los refuerzos")]
    [SerializeField, Range(0.1f, 0.9f)] private float callThreshold = 0.5f;
    [Tooltip("Lo más que le quita un solo disparo: suma los perdigones que llegan en el mismo cuadro")]
    [SerializeField, Min(1f)] private float maxDamagePerShot = 48f;

    [Header("Combate")]
    [SerializeField] private Vector2 coverSeconds = new Vector2(2f, 2.8f);
    [Tooltip("Segunda fase: la espera en cubierta se multiplica por esto")]
    [SerializeField, Range(0.1f, 1f)] private float secondPhaseCooldown = 0.75f;
    [SerializeField] private Vector2 peekSeconds = new Vector2(0.3f, 0.5f);
    [SerializeField, Min(0f)] private float telegraphSeconds = 0.4f;
    [SerializeField, Min(1)] private int shotsPerBurst = 3;
    [SerializeField, Min(0.01f)] private float secondsBetweenShots = 0.15f;
    [SerializeField, Min(0f)] private float introSeconds = 1.6f;
    [SerializeField, Min(0f)] private float callSeconds = 1.4f;

    [Header("Puntería")]
    [SerializeField, Range(0f, 1f)] private float hitChance = 0.3f;
    [SerializeField, Range(0f, 1f)] private float hitChanceRunning = 0.15f;
    [SerializeField, Min(0f)] private float damage = 7f;
    [SerializeField, Min(1f)] private float maxShotDistance = 30f;
    [SerializeField, Min(0f)] private float glintSize = 0.16f;
    [SerializeField, Min(0f)] private float shakeSeconds = 0.15f;
    [SerializeField, Min(0f)] private float shakeAmount = 0.04f;

    [Header("Cambios de cubierta")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 2.5f;
    [Tooltip("Ráfagas en una cubierta antes de cambiar (primera fase)")]
    [SerializeField, Min(1)] private int burstsPerCover = 2;
    [Tooltip("No va a una cubierta a menos de esto del jugador")]
    [SerializeField, Min(0f)] private float minCoverDistanceToPlayer = 3.5f;
    [Tooltip("El camino recto a la cubierta no pasa a menos de esto del jugador; caminando, si lo tiene más cerca, se frena y dispara")]
    [SerializeField, Min(0f)] private float keepAwayFromPlayer = 2f;
    [SerializeField, Min(0.5f)] private float moveTimeout = 7f;
    [SerializeField, Min(0.5f)] private float walkFps = 4f;

    [Header("Voz")]
    [Tooltip("Separación mínima entre el inicio de dos gritos (y nunca se pisan)")]
    [SerializeField, Min(0f)] private float minBarkGap = 4f;
    [SerializeField, Range(0f, 1f)] private float shootingBarkChance = 0.4f;
    [SerializeField, Range(0f, 1f)] private float peekBarkChance = 0.3f;
    [SerializeField, Range(0f, 1f)] private float coverBarkChance = 0.35f;

    [Header("De rodillas")]
    [SerializeField, Min(0f)] private float hitPoseSeconds = 0.35f;
    [Tooltip("Los cuadros de rodillas llenan el lienzo: se achican a esto (0.6 × raíz 1.2 × lienzo 2.05 m ≈ 1.48 m de alto)")]
    [SerializeField, Range(0.3f, 1f)] private float kneelScale = 0.6f;
    [Tooltip("Tras caer, segundos en que no recibe daño (lo que queda del mismo disparo no lo remata)")]
    [SerializeField, Min(0f)] private float kneelGraceSeconds = 0.5f;
    [SerializeField, Min(0f)] private float confrontationDistance = 3f;
    [SerializeField] private Vector2 dripSeconds = new Vector2(0.6f, 1f);
    [Tooltip("Segundos hasta el final, después de rematarlo o de la decisión (L8)")]
    [SerializeField, Min(0f)] private float finaleDelay = 2.5f;

    [Header("Dibujo y cápsula (a escala 1: la raíz los agranda ×1.2)")]
    [Tooltip("Metros que baja el dibujo al cubrirse")]
    [SerializeField, Min(0f)] private float coverSink = 0.45f;
    [SerializeField, Min(0.1f)] private float sinkSpeed = 2.5f;
    [SerializeField, Min(0.1f)] private float hitboxRadius = 0.32f;
    [SerializeField, Min(0.2f)] private float standingHeight = 1.8f;
    [Tooltip("Cubierto: 0.7 × 1.2 = 0.84 m, más bajo que el escritorio (1.1 m) y que el billar (0.9 m)")]
    [SerializeField, Min(0.2f)] private float coverHeight = 0.7f;
    [SerializeField, Min(0.2f)] private float kneelingHeight = 0.9f;

    /// <summary>De rodillas, jugador a ≤ 3 m con vista. Se dispara una vez por acercamiento. Lo consume N2.3.</summary>
    public event Action OnConfrontationRequested;

    public event Action<State> OnStateChanged;

    public State CurrentState => _state;
    public float HealthFraction => _health / Mathf.Max(1f, maxHealth);
    public bool CanConfront => _state == State.Kneeling;
    public BillboardSprite Billboard => billboard;
    public AudioSource VoiceSource => voiceSource;
    public LipSyncDriver LipSync => lipSync;

    // IInterrogationTarget (N2.3)
    public string SpeakerName => "DON LOBO";
    public Transform CameraAnchor => confrontationCam;
    public CharacterSpriteSet PortraitSet => billboard != null ? billboard.SpriteSet : null;

    private const int SightMask = ~((1 << 2) | (1 << 5) | (1 << 8)); // sin Ignore Raycast, UI ni Viewmodel
    private static readonly RaycastHit[] SightHits = new RaycastHit[12];

    private State _state = State.Dormant;
    private float _stateTime;
    private float _health;

    private Transform _player;
    private PlayerHealth _playerHealth;
    private PlayerController _playerController;
    private Camera _playerCamera;
    private EnemyGunfire _gunfire;
    private NavMeshAgent _agent;
    private SpriteRenderer _spriteRenderer;
    private float _flashSpriteUntil = -1f;

    // Combate
    private CombatPhase _phase;
    private float _phaseUntil;
    private int _coverIndex;
    private int _burstsHere;
    private int _shotsLeft;
    private float _nextShot;
    private int _blindChecks;
    private bool _secondPhase;
    private bool _callPending;
    private float _nextBarkTime;
    private float _shakeUntil;
    private float _sink;
    private float _walkTimer;
    private int _walkFrame;
    private bool _warnedNoNavMesh;

    // Daño por disparo
    private int _damageFrame = -1;
    private float _damageThisFrame;

    // Refuerzos
    private readonly List<EnemyAI> _reinforcements = new List<EnemyAI>(2);

    // De rodillas
    private float _graceUntil;
    private bool _saidKneelingLine;
    private bool _confrontationArmed = true;
    private bool _warnedNoListener;
    private float _nextSightCheck;
    private float _nextDrip;

    private Vector3 _visualBasePosition;
    private float WorldScale => Mathf.Max(0.01f, transform.lossyScale.y);
    private float KneelHealth => maxHealth * kneelThreshold;

    // ==================================================================
    // Ciclo de vida
    // ==================================================================

    private void Awake()
    {
        _health = maxHealth;
        TryGetComponent(out _agent);
        if (billboard != null) _spriteRenderer = billboard.GetComponent<SpriteRenderer>();
        if (visual != null) _visualBasePosition = visual.localPosition;
        if (droppedPistol != null) droppedPistol.SetActive(false);
        if (voiceSource != null) voiceSource.ignoreListenerPause = true; // como Centavo: su voz sigue con el juego en pausa

        // El tiro sale de la pistola del dibujo. Sin el componente del menú, se agrega con el punto de respaldo
        if (!TryGetComponent(out _gunfire))
        {
            _gunfire = gameObject.AddComponent<EnemyGunfire>();
            _gunfire.SetFallback(new Vector2(0.55f, 0.55f), _spriteRenderer);
        }
    }

    private void Start()
    {
        FindPlayer();
        if (_agent != null)
        {
            _agent.updateRotation = false;
            _agent.speed = moveSpeed;
        }
        if (startAnchor != null) PlaceAt(startAnchor.position);
        else Debug.LogWarning("[Lobo] Sin Anchor_LoboStart: correr Plomo y Paja → Nivel 2 → Colocar Don Lobo.");

        _sink = coverSink;
        SetHitbox(standingHeight);
        if (billboard != null) billboard.Play("idle");
    }

    private void FindPlayer()
    {
        GameObject go = GameObject.FindWithTag("Player");
        if (go == null) return;
        _player = go.transform;
        go.TryGetComponent(out _playerHealth);
        go.TryGetComponent(out _playerController);
        _playerCamera = go.GetComponentInChildren<Camera>();
    }

    private void Update()
    {
        if (_player == null) FindPlayer();
        _stateTime += Time.deltaTime;

        // Como los guardias (I7): quieto durante una cinemática o un interrogatorio ajeno
        if (!CinematicState.IsPlaying && (!InterrogationDirector.IsActive || _state == State.Confrontation))
        {
            switch (_state)
            {
                case State.Dormant: UpdateDormant(); break;
                case State.Intro: UpdateIntro(); break;
                case State.Combat: UpdateCombat(); break;
                case State.Hit: UpdateHit(); break;
                case State.Kneeling: UpdateKneeling(); break;
                case State.Confrontation:
                case State.Pardoned: UpdateKneelingMouth(); break;
            }
        }
        UpdateVisual();
    }

    private void OnDisable()
    {
        if (_spriteRenderer != null) CharacterLook.SetFlash(_spriteRenderer, 0f);
    }

    private void SetState(State next)
    {
        if (_state == next) return;
        _state = next;
        _stateTime = 0f;
        OnStateChanged?.Invoke(next);
    }

    // ==================================================================
    // Dormido e intro
    // ==================================================================

    private void UpdateDormant()
    {
        if (Time.time < _nextSightCheck) return;
        _nextSightCheck = Time.time + 0.15f;
        if (!PlayerAlive() || salonTrigger == null) return;
        if (salonTrigger.bounds.Contains(_player.position + Vector3.up)) EnterIntro();
    }

    private void EnterIntro()
    {
        SetState(State.Intro);
        Say(LineIntro, true);
        if (billboard != null) billboard.Play("idle");
    }

    private void UpdateIntro()
    {
        AudioClip clip = voiceSource != null ? voiceSource.clip : null;
        float duration = clip != null && voiceSource.isPlaying ? Mathf.Max(introSeconds, clip.length) : introSeconds;
        if (_stateTime < duration) return;
        SetState(State.Combat);
        StartPhase(CombatPhase.Cover);
    }

    // ==================================================================
    // Combate
    // ==================================================================

    private void StartPhase(CombatPhase phase)
    {
        _phase = phase;
        switch (phase)
        {
            case CombatPhase.Cover:
                _phaseUntil = Time.time + RandomRange(coverSeconds) * (_secondPhase ? secondPhaseCooldown : 1f);
                SetHitbox(coverHeight);
                Show("cover", 0);
                if (_coverIndex == 0 && UnityEngine.Random.value < coverBarkChance) Say(LineCover, false); // la línea habla del escritorio
                break;

            case CombatPhase.Peek:
                _phaseUntil = Time.time + RandomRange(peekSeconds);
                SetHitbox(standingHeight);
                Show("peek", 0);
                if (UnityEngine.Random.value < peekBarkChance) Say(LinePeek, false);
                break;

            case CombatPhase.Telegraph:
                _phaseUntil = Time.time + telegraphSeconds;
                SetHitbox(standingHeight);
                Show("shoot", 0);
                if (_gunfire != null) CombatFx.MuzzleFlash(_gunfire.MuzzlePoint(), glintSize, new Color(1f, 0.92f, 0.55f));
                break;

            case CombatPhase.Burst:
                _shotsLeft = shotsPerBurst;
                _nextShot = Time.time;
                Show("shoot", 0);
                if (UnityEngine.Random.value < shootingBarkChance) Say(LineShooting, false);
                break;

            case CombatPhase.Move:
                _phaseUntil = Time.time + moveTimeout;
                SetHitbox(standingHeight);
                _walkTimer = 0f;
                _walkFrame = 0;
                Show("idle", 0);
                break;

            case CombatPhase.Call:
                _callPending = false;
                _secondPhase = true;
                _phaseUntil = Time.time + callSeconds;
                StopAgent();
                SetHitbox(standingHeight);
                Show("call", 0);
                Say(LineCall, true);
                SpawnReinforcements();
                break;
        }
    }

    private void UpdateCombat()
    {
        if (_callPending && _phase != CombatPhase.Burst)
        {
            StartPhase(CombatPhase.Call);
            return;
        }

        switch (_phase)
        {
            case CombatPhase.Cover:
                if (Time.time >= _phaseUntil) LeaveCover();
                break;
            case CombatPhase.Peek:
                if (Time.time >= _phaseUntil) StartPhase(PlayerAlive() ? CombatPhase.Telegraph : CombatPhase.Cover);
                break;
            case CombatPhase.Telegraph:
                if (Time.time >= _phaseUntil) StartPhase(PlayerAlive() ? CombatPhase.Burst : CombatPhase.Cover);
                break;
            case CombatPhase.Burst:
                UpdateBurst();
                break;
            case CombatPhase.Move:
                UpdateMove();
                break;
            case CombatPhase.Call:
                if (Time.time >= _phaseUntil) AfterCall();
                break;
        }
    }

    private void LeaveCover()
    {
        if (!PlayerAlive())
        {
            _phaseUntil = Time.time + 1f;
            return;
        }
        if (HasLineOfSight(StandingEye()))
        {
            _blindChecks = 0;
            StartPhase(CombatPhase.Peek);
            return;
        }
        // No lo ve: espera un poco, y a la segunda busca otra cubierta
        _blindChecks++;
        if (_blindChecks < 2 || !TryStartMove()) _phaseUntil = Time.time + RandomRange(coverSeconds) * 0.5f;
        else _blindChecks = 0;
    }

    private void UpdateBurst()
    {
        if (Time.time < _nextShot) return;
        if (_shotsLeft > 0)
        {
            _shotsLeft--;
            _nextShot = Time.time + secondsBetweenShots;
            FireShot();
            return;
        }

        // Terminó la ráfaga
        _burstsHere++;
        bool playerTooClose = _player != null && _coverIndex < coverAnchors.Length && coverAnchors[_coverIndex] != null &&
                              FlatDistance(coverAnchors[_coverIndex].position, _player.position) < minCoverDistanceToPlayer;
        bool wantsMove = _secondPhase ? DistanceToDesk() > 0.5f || playerTooClose : _burstsHere >= burstsPerCover;
        if (!wantsMove || !TryStartMove()) StartPhase(CombatPhase.Cover);
    }

    private void FireShot()
    {
        if (_gunfire == null) return;
        _shakeUntil = Time.time + shakeSeconds;
        bool canHit = _playerHealth != null && !_playerHealth.IsDead && _player != null &&
                      (PlayerChest() - _gunfire.MuzzlePoint()).sqrMagnitude <= maxShotDistance * maxShotDistance;
        bool running = _playerController != null && _playerController.IsRunning && _playerController.IsMoving;
        _gunfire.Fire(_player, canHit ? (running ? hitChanceRunning : hitChance) : 0f, damage, 0.5f);
    }

    // ---------- Cambios de cubierta ----------

    /// <summary>Elige cubierta y empieza a caminar. false = no hay a dónde ir (sigue donde está).</summary>
    private bool TryStartMove()
    {
        int target = ChooseCover();
        if (target < 0 || !AgentUsable()) return false;
        if (!_agent.SetDestination(coverAnchors[target].position)) return false;
        _agent.isStopped = false;
        _coverIndex = target;
        _burstsHere = 0;
        StartPhase(CombatPhase.Move);
        return true;
    }

    /// <summary>Segunda fase: el escritorio si se puede. Si no, una al azar. Nunca una junto al jugador ni detrás de él.</summary>
    private int ChooseCover()
    {
        if (_player == null) return -1;
        if (_secondPhase && DistanceToDesk() > 0.5f && CoverIsSafe(0)) return 0;

        int count = 0, chosen = -1;
        for (int i = 0; i < coverAnchors.Length; i++)
        {
            if (i == _coverIndex || !CoverIsSafe(i)) continue;
            count++;
            if (UnityEngine.Random.Range(0, count) == 0) chosen = i; // al azar entre las válidas, sin armar una lista
        }
        return chosen;
    }

    /// <summary>Metros hasta la cubierta del escritorio (la 0). Sin ella, 0: no hay a dónde volver.</summary>
    private float DistanceToDesk() =>
        coverAnchors.Length > 0 && coverAnchors[0] != null ? FlatDistance(transform.position, coverAnchors[0].position) : 0f;

    private bool CoverIsSafe(int index)
    {
        Transform anchor = index >= 0 && index < coverAnchors.Length ? coverAnchors[index] : null;
        if (anchor == null) return false;
        Vector3 player = _player.position;
        return FlatDistance(anchor.position, player) >= minCoverDistanceToPlayer &&
               SegmentDistance(transform.position, anchor.position, player) >= keepAwayFromPlayer;
    }

    private void UpdateMove()
    {
        _walkTimer += Time.deltaTime * walkFps;
        if (_walkTimer >= 1f)
        {
            _walkTimer -= 1f;
            _walkFrame = 1 - _walkFrame;
            Show("idle", _walkFrame);
        }

        bool arrived = !AgentUsable() || (!_agent.pathPending && _agent.remainingDistance <= 0.25f);
        bool blocked = _player != null && FlatDistance(transform.position, _player.position) < keepAwayFromPlayer;
        if (!arrived && !blocked && Time.time < _phaseUntil) return;

        StopAgent();
        if (blocked && !arrived)
        {
            StartPhase(CombatPhase.Peek); // lo tiene encima: le dispara desde donde está
            return;
        }
        StartPhase(CombatPhase.Cover);
    }

    private bool AgentUsable()
    {
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh) return true;
        if (!_warnedNoNavMesh)
        {
            _warnedNoNavMesh = true;
            Debug.LogWarning("[Lobo] Sin NavMesh en el salón: se queda en su cubierta (no cambia de lugar). Regenerar Campamento.");
        }
        return false;
    }

    private void StopAgent()
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;
        _agent.isStopped = true;
        _agent.ResetPath();
    }

    // ---------- Refuerzos ----------

    private void SpawnReinforcements()
    {
        if (reinforcementPrefab == null)
        {
            Debug.LogWarning("[Lobo] Sin prefab de refuerzos: correr Plomo y Paja → Nivel 2 → Colocar Don Lobo.");
            return;
        }
        foreach (Transform anchor in reinforcementAnchors)
        {
            if (anchor == null) continue;
            GameObject guard = Instantiate(reinforcementPrefab, anchor.position, anchor.rotation);
            guard.name = "Guard_Refuerzo_" + anchor.name;
            if (guard.TryGetComponent(out EnemyAI ai)) _reinforcements.Add(ai);
        }
        StartCoroutine(SendReinforcements());
    }

    /// <summary>EnemyAI.Start decide su primer estado: se les llama un cuadro después, ya despiertos.</summary>
    private IEnumerator SendReinforcements()
    {
        yield return null;
        foreach (EnemyAI ai in _reinforcements)
            if (ai != null) ai.CallToFight();
    }

    private void AfterCall()
    {
        // L6: se queda en el escritorio. Si no se puede llegar (jugador encima), sigue desde donde está
        if (DistanceToDesk() <= 0.5f || !TryStartMove()) StartPhase(CombatPhase.Cover);
    }

    // ==================================================================
    // Daño
    // ==================================================================

    /// <summary>IDamageable: perdigón o bala del jugador. Devuelve true solo si lo mata (rematado de rodillas, L6).</summary>
    public bool TakeDamage(float amount)
    {
        if (amount <= 0f || !enabled) return false;

        switch (_state)
        {
            case State.Dormant:
                ApplyDamage(amount);
                if (_state == State.Dormant) EnterIntro(); // el primer tiro lo despierta
                return false;

            case State.Intro:
            case State.Combat:
                ApplyDamage(amount);
                return false;

            case State.Kneeling:
                if (Time.time < _graceUntil) return false;
                Die(true);
                return true;

            default:
                return false; // Hit (resto del disparo), escena, muerto, perdonado
        }
    }

    private void ApplyDamage(float amount)
    {
        if (Time.frameCount != _damageFrame)
        {
            _damageFrame = Time.frameCount;
            _damageThisFrame = 0f;
            HitFeedback(); // uno por disparo, no uno por perdigón
        }
        float allowed = Mathf.Min(amount, maxDamagePerShot - _damageThisFrame);
        if (allowed <= 0f) return;
        _damageThisFrame += allowed;
        _health = Mathf.Max(KneelHealth, _health - allowed); // el golpe que lo tumba no lo mata

        if (_health <= KneelHealth)
        {
            EnterHit();
            return;
        }
        if (!_secondPhase && HealthFraction <= callThreshold) _callPending = true;
    }

    private void HitFeedback()
    {
        Vector3 chest = transform.position + Vector3.up * (1.2f * WorldScale);
        CartoonFx.BloodBurst(chest, AwayFromPlayer(), 8);
        AudioLibrary.PlaySfx("impacto_carne", chest);
        FlashSprite();
    }

    private void EnterHit()
    {
        SetState(State.Hit);
        _callPending = false;
        StopAgent();
        _graceUntil = float.MaxValue;
        SetHitbox(standingHeight);
        if (hitbox != null) hitbox.enabled = false;
        Show("hit", 0);
        Say(LineFall, true);

        Vector3 chest = transform.position + Vector3.up * (1.2f * WorldScale);
        CartoonFx.BloodBurst(chest, AwayFromPlayer());
        CartoonFx.HitStop(0.05f);
        AudioLibrary.PlaySfx("sangre_chorro", chest);
        DropPistol();
        SurrenderReinforcements();
    }

    private void SurrenderReinforcements()
    {
        foreach (EnemyAI ai in _reinforcements)
            if (ai != null) ai.TriggerSurrender();
    }

    private void DropPistol()
    {
        if (droppedPistol == null) return;
        Vector3 toPlayer = -AwayFromPlayer();
        droppedPistol.transform.SetParent(null, true);
        droppedPistol.transform.localScale = Vector3.one;
        droppedPistol.transform.position = transform.position + Vector3.up * (1.1f * WorldScale) + toPlayer * 0.5f;
        droppedPistol.SetActive(true);
        if (!droppedPistol.TryGetComponent(out Rigidbody body)) body = droppedPistol.AddComponent<Rigidbody>();
        body.mass = 1f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.velocity = toPlayer * 1.2f + Vector3.up * 1.2f;
    }

    private void UpdateHit()
    {
        if (_stateTime < hitPoseSeconds) return;

        // Cae donde está o en el piso alcanzable más cercano
        if (_agent != null) _agent.enabled = false;
        Vector3 at = transform.position;
        if (NavMesh.SamplePosition(at, out NavMeshHit floor, 1.5f, NavMesh.AllAreas)) at = floor.position;
        transform.position = at;

        SetState(State.Kneeling);
        if (visual != null) visual.localScale = Vector3.one * kneelScale;
        SetHitbox(kneelingHeight);
        if (hitbox != null) hitbox.enabled = true;
        _graceUntil = Time.time + kneelGraceSeconds;
        _nextDrip = Time.time + RandomRange(dripSeconds);
    }

    // ==================================================================
    // De rodillas
    // ==================================================================

    private void UpdateKneeling()
    {
        UpdateKneelingMouth();
        UpdateDrip();
        if (!PlayerAlive()) return;

        if (Time.time < _nextSightCheck) return;
        _nextSightCheck = Time.time + 0.15f;

        float distance = Vector3.Distance(transform.position, _player.position);
        if (distance > confrontationDistance + 0.75f) _confrontationArmed = true;
        if (!HasLineOfSight(KneelingEye())) return;

        if (distance <= confrontationDistance)
        {
            if (!_confrontationArmed) return;
            _confrontationArmed = false;
            if (OnConfrontationRequested != null) OnConfrontationRequested.Invoke();
            else if (!_warnedNoListener)
            {
                _warnedNoListener = true;
                Debug.LogWarning("[Lobo] Pidió la confrontación, pero nadie la atiende todavía (llega en N2.3).");
            }
            return;
        }
        if (!_saidKneelingLine && Say(LineKneeling, false)) _saidKneelingLine = true;
    }

    /// <summary>Par rodillas_01/02 según el lip-sync. Con los dibujos viejos de "donlobo": surrender.</summary>
    private void UpdateKneelingMouth()
    {
        if (billboard == null) return;
        bool open = lipSync != null && lipSync.IsOpen;
        billboard.ShowFrame(billboard.HasAnim("rodillas") || !billboard.HasAnim("surrender") ? "rodillas" : "surrender", open ? 1 : 0);
    }

    private void UpdateDrip()
    {
        if (Time.time < _nextDrip) return;
        _nextDrip = Time.time + RandomRange(dripSeconds);
        Vector3 side = Vector3.Cross(Vector3.up, AwayFromPlayer()).normalized;
        float s = WorldScale;
        CartoonFx.BloodDrip(transform.position + side * ((hitboxRadius + 0.12f) * s) - AwayFromPlayer() * (0.1f * s) + Vector3.up * (0.45f * s));
    }

    // ==================================================================
    // Muerte y API de la confrontación (N2.3)
    // ==================================================================

    /// <param name="finishedOff">true = rematado antes de la escena: sale el comentario normal de baja de la IA.
    /// false = "Jalar el gatillo": la escena ya puso lo suyo (N2.3 suprime ese comentario).</param>
    private void Die(bool finishedOff)
    {
        SetState(State.Dead);
        if (voiceSource != null) voiceSource.Stop();
        if (hitbox != null) hitbox.enabled = false;

        bool hasDrawing = billboard != null && billboard.HasDeathDrawing;
        if (billboard != null) billboard.PlayDeath();
        if (visual != null)
            visual.localScale = hasDrawing ? Vector3.one * kneelScale : new Vector3(kneelScale, kneelScale * 0.3f, kneelScale); // sin death_01: aplastado

        Vector3 chest = transform.position + Vector3.up * (0.6f * WorldScale);
        CartoonFx.BloodBurst(chest, AwayFromPlayer());
        AudioLibrary.PlaySfx("impacto_carne", chest);
        FlashSprite();

        if (GameStateTracker.Instance != null)
        {
            GameStateTracker.Instance.RegisterEnemyKilled("Don Lobo");
            GameStateTracker.Instance.RegisterLoboState(LoboState.Dead);
        }
        if (finishedOff && AICompanion.Instance != null) AICompanion.Instance.Speak(AITrigger.EnemyKilled);
        StartCoroutine(FinaleAfterDelay());
    }

    private IEnumerator FinaleAfterDelay()
    {
        yield return new WaitForSecondsRealtime(finaleDelay);
        FinaleResolver.GoToFinale();
    }

    /// <summary>Empieza la escena: deja de hablar en el mundo y no recibe disparos. false si no está de rodillas.</summary>
    public bool BeginConfrontation()
    {
        if (_state != State.Kneeling) return false;
        SetState(State.Confrontation);
        if (voiceSource != null) voiceSource.Stop();
        if (hitbox != null) hitbox.enabled = false;
        AimConfrontationCamera();
        return true;
    }

    /// <summary>Cae donde sea: el ancla de la cámara se pone delante de él, del lado del jugador, a la altura de sus ojos.</summary>
    private void AimConfrontationCamera()
    {
        if (confrontationCam == null) return;
        Vector3 eye = KneelingEye();
        Vector3 from = transform.position - AwayFromPlayer() * 1.8f + Vector3.up * 1.2f;
        confrontationCam.SetPositionAndRotation(from, Quaternion.LookRotation(eye - from, Vector3.up));
    }

    /// <summary>"Jalar el gatillo" (N2.3): death_01, LoboState Dead y el final.</summary>
    public void ResolveKilled()
    {
        if (_state != State.Confrontation && _state != State.Kneeling) return;
        Die(false);
    }

    /// <summary>"Bajar el arma" (N2.3): se queda de rodillas, LoboState Pardoned y el final.</summary>
    public void ResolvePardoned()
    {
        if (_state != State.Confrontation && _state != State.Kneeling) return;
        SetState(State.Pardoned);
        if (hitbox != null) hitbox.enabled = false;
        if (GameStateTracker.Instance != null) GameStateTracker.Instance.RegisterLoboState(LoboState.Pardoned);
        StartCoroutine(FinaleAfterDelay());
    }

    /// <summary>Oculta/muestra el dibujo del mundo (D6: en la escena se ve el retrato).</summary>
    public void SetWorldVisible(bool visible)
    {
        if (billboard != null) billboard.SetVisible(visible);
    }

    // ==================================================================
    // Poses
    // ==================================================================

    private void PlaceAt(Vector3 position)
    {
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.Warp(position);
        else transform.position = position;
    }

    private void Show(string anim, int frame)
    {
        if (billboard != null) billboard.ShowFrame(anim, frame);
    }

    private void SetHitbox(float height)
    {
        if (hitbox == null) return;
        hitbox.direction = 1;
        hitbox.radius = hitboxRadius;
        hitbox.height = Mathf.Max(height, hitboxRadius * 2f);
        hitbox.center = new Vector3(0f, hitbox.height * 0.5f, 0f);
    }

    /// <summary>Baja y sube el dibujo (detrás del mueble), tiembla al disparar y apaga el destello.</summary>
    private void UpdateVisual()
    {
        if (_spriteRenderer != null && _flashSpriteUntil > 0f && Time.unscaledTime >= _flashSpriteUntil)
        {
            _flashSpriteUntil = -1f;
            CharacterLook.SetFlash(_spriteRenderer, 0f);
        }
        if (visual == null) return;

        bool sunk = _state == State.Dormant || (_state == State.Combat && _phase == CombatPhase.Cover);
        _sink = Mathf.MoveTowards(_sink, sunk ? coverSink : 0f, sinkSpeed * Time.deltaTime);
        Vector3 position = _visualBasePosition + Vector3.down * _sink;
        if (Time.time < _shakeUntil)
        {
            float t = (_shakeUntil - Time.time) / Mathf.Max(0.01f, shakeSeconds);
            position.x += Mathf.Sin(Time.time * 90f) * shakeAmount * t;
        }
        visual.localPosition = position;
    }

    private void FlashSprite()
    {
        if (_spriteRenderer == null || !CharacterLook.SupportsFlash(_spriteRenderer)) return;
        CharacterLook.SetFlash(_spriteRenderer, 1f);
        _flashSpriteUntil = Time.unscaledTime + 0.08f;
    }

    // ==================================================================
    // Voz
    // ==================================================================

    /// <summary>Un grito por su propia fuente 3D (la lee el lip-sync). force = corta lo que esté diciendo. Sin clip: nada suena.</summary>
    private bool Say(string lineId, bool force)
    {
        if (voiceSource == null) return false;
        if (InterrogationDirector.IsSceneOpen) return false;
        if (!force && (voiceSource.isPlaying || Time.time < _nextBarkTime)) return false;

        AudioClip clip = AudioLibrary.GetVoice(lineId);
        float length = clip != null ? clip.length : 2f;
        _nextBarkTime = Time.time + Mathf.Max(minBarkGap, length);
        voiceSource.Stop();
        if (clip == null) return true;
        voiceSource.clip = clip;
        voiceSource.volume = AudioLibrary.VoiceVolume;
        voiceSource.Play();
        return true;
    }

    // ==================================================================
    // Jugador y vista
    // ==================================================================

    private bool PlayerAlive() => _player != null && (_playerHealth == null || !_playerHealth.IsDead);

    private Vector3 PlayerEye() => _playerCamera != null ? _playerCamera.transform.position : _player.position + Vector3.up * 1.6f;

    private Vector3 PlayerChest() => PlayerEye() - Vector3.up * 0.45f;

    private Vector3 StandingEye() => transform.position + Vector3.up * (1.6f * WorldScale);

    private Vector3 KneelingEye() => transform.position + Vector3.up * (0.8f * WorldScale);

    private Vector3 AwayFromPlayer()
    {
        Vector3 dir = _player != null ? transform.position - _player.position : -transform.forward;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.001f ? dir.normalized : -transform.forward;
    }

    private bool HasLineOfSight(Vector3 from)
    {
        if (_player == null) return false;
        Vector3 to = PlayerEye();
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.01f) return true;
        int count = Physics.RaycastNonAlloc(from, delta / distance, SightHits, distance, SightMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider c = SightHits[i].collider;
            if (c == null || c.transform.IsChildOf(transform) || c.transform.IsChildOf(_player)) continue;
            if (c.GetComponentInParent<EnemyHealth>() != null) continue; // sus propios refuerzos no le tapan al jugador
            return false;
        }
        return true;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    /// <summary>Distancia en planta de "point" al segmento a–b.</summary>
    private static float SegmentDistance(Vector3 a, Vector3 b, Vector3 point)
    {
        a.y = b.y = point.y = 0f;
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector3.Distance(a + ab * t, point);
    }

    private static float RandomRange(Vector2 range) => UnityEngine.Random.Range(range.x, range.y);
}
