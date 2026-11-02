using System;
using UnityEngine;

/// <summary>
/// Centavo, el contador de Don Lobo, en el cuarto de herramientas del patio de Porky (fase 3C.2).
/// DÓNDE VA: raíz del prefab Prefabs/Level1/Centavo. Lo crea y lo coloca el menú
/// Plomo y Paja → Contador → Colocar Centavo (y también "Construir patio y cuarto" y "Generar Nivel Porky").
///
/// ESTADOS:
///   Dormant → Alert → Combat → Hit → Wounded → (Interrogation, 3C.3) → Dead | Fleeing → Gone
/// - Dormant: idle_01/02 en Anchor_CentavoStand; ignora al jugador mientras algún guardia del patio siga en pie (D2).
/// - Alert: guardias fuera de combate + jugador dentro de la esfera de 12 m con línea de vista → contador_ver_01, peek_01.
/// - Combat: nunca se mueve (D3). Cubierto (1.0–1.8 s) → asomado (0.4–0.7 s) → dispara → cubierto. 6 tiros y recarga 2 s.
/// - Hit: cualquier golpe del jugador lo tumba (D3) sin matarlo: hit_01 0.35 s, contador_cae_01, suelta el revólver,
///   sangre, y pasa a Anchor_CentavoSeated.
/// - Wounded: par herido_01/02 con lip-sync, goteo de sangre, barks herido_01/02. Jugador a ≤ 3 m con vista →
///   OnInterrogationRequested (lo consume 3C.3). Si le disparan aquí → Dead sin interrogatorio (D4).
/// - Dead: death_01 fijo, el collider ya no recibe golpes.
/// - Fleeing: limp_01/02 a 4 cuadros/s por Exit_Path a 1.2 m/s, contador_huye_01; al final se apaga (Gone).
///
/// DAÑO: IDamageable (ShotgunController lo busca solo si no hay EnemyHealth: los guardias no cambian).
/// TODAS las posiciones salen de ContadorShed (anclas del prefab del cuarto), nunca de coordenadas fijas.
/// </summary>
public class CentavoController : MonoBehaviour, IDamageable, IInterrogationTarget
{
    public enum State { Dormant, Alert, Combat, Hit, Wounded, Interrogation, Dead, Fleeing, Gone }

    private enum CombatPhase { Cover, Peek, Shoot, Reload }

    // Líneas del guion (Docs/guiones/contador.md §4 y §5)
    private const string LineSpotted = "contador_ver_01";
    private const string LineReload = "contador_ver_02";
    private const string LineShooting = "contador_ver_03";
    private const string LineCover = "contador_cubre_01";
    private const string LineHit = "contador_cae_01";
    private const string LineWounded = "contador_herido_01";
    private const string LineWoundedRepeat = "contador_herido_02";
    private const string LineFlee = "contador_huye_01";

    [Header("Escena (los pone el menú)")]
    [Tooltip("Cuarto de herramientas: anclas, camino de huida y zona de activación")]
    [SerializeField] private ContadorShed shed;

    [Tooltip("Guardias del patio (patrullero y centinela). Centavo despierta cuando todos están muertos o rendidos (D2)")]
    [SerializeField] private EnemyHealth[] yardGuards = new EnemyHealth[0];

    [Header("Partes del prefab")]
    [SerializeField] private BillboardSprite billboard;
    [Tooltip("Padre del dibujo y del placeholder: se escala sentado y baja al cubrirse")]
    [SerializeField] private Transform visual;
    [SerializeField] private CapsuleCollider hitbox;
    [SerializeField] private AudioSource voiceSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private LipSyncDriver lipSync;
    [SerializeField] private Light muzzleFlash;
    [Tooltip("Revolver suelto (apagado) que cae al recibir el golpe")]
    [SerializeField] private GameObject droppedRevolver;

    [Header("Tamaño en el mundo (D12)")]
    [Tooltip("Escala de los cuadros sentados (llenan el lienzo de 2.05 m). 0.52 ≈ 1.06 m de alto, apoyado en el banco de 1 m")]
    [SerializeField, Range(0.3f, 1f)] private float seatedScale = 0.52f;

    [Tooltip("Metros que baja el dibujo al cubrirse (cover_01 mide 1.78 m: así solo asoma la cabeza sobre el banco)")]
    [SerializeField, Min(0f)] private float coverSink = 0.4f;

    [Header("Combate")]
    [SerializeField] private Vector2 coverSeconds = new Vector2(1.0f, 1.8f);
    [SerializeField] private Vector2 peekSeconds = new Vector2(0.4f, 0.7f);
    [SerializeField, Min(0.05f)] private float shootPoseSeconds = 0.3f;
    [SerializeField, Min(1)] private int shotsPerReload = 6;
    [SerializeField, Min(0f)] private float reloadSeconds = 2f;
    [SerializeField, Min(0f)] private float alertSeconds = 1.2f;

    [Header("Puntería (amenaza baja a propósito)")]
    [SerializeField, Range(0f, 1f)] private float hitChance = 0.2f;
    [SerializeField, Range(0f, 1f)] private float hitChanceRunning = 0.1f;
    [SerializeField, Min(0f)] private float damage = 5f;
    [SerializeField, Min(1f)] private float maxShotDistance = 30f;

    [Header("Disparo (sin SFX de revólver: se reusa la escopeta más aguda)")]
    [SerializeField] private string shotSfx = "escopeta_disparo";
    [SerializeField, Range(0.5f, 3f)] private float shotPitch = 1.4f;
    [SerializeField, Range(0f, 1f)] private float shotVolume = 0.55f;
    [SerializeField, Min(0f)] private float flashSeconds = 0.06f;
    [SerializeField, Min(0f)] private float shakeSeconds = 0.18f;
    [SerializeField, Min(0f)] private float shakeAmount = 0.035f;

    [Header("Barks")]
    [Tooltip("Separación mínima entre el inicio de dos barks (y nunca se pisan)")]
    [SerializeField, Min(0f)] private float minBarkGap = 4f;
    [SerializeField, Range(0f, 1f)] private float shootingBarkChance = 0.3f;
    [SerializeField, Range(0f, 1f)] private float coverBarkChance = 0.35f;
    [SerializeField, Min(0f)] private float coverBarkCooldown = 10f;
    [SerializeField] private Vector2 woundedRepeatSeconds = new Vector2(8f, 10f);

    [Header("Golpe y herido")]
    [SerializeField, Min(0f)] private float hitPoseSeconds = 0.35f;
    [Tooltip("Tras quedar sentado, segundos en que no recibe daño (los otros perdigones del mismo disparo no lo matan)")]
    [SerializeField, Min(0f)] private float woundedGraceSeconds = 0.5f;
    [SerializeField, Min(0f)] private float interrogationDistance = 3f;
    [SerializeField] private Vector2 dripSeconds = new Vector2(0.5f, 0.9f);

    [Header("Huida")]
    [SerializeField, Min(0.1f)] private float fleeSpeed = 1.2f;
    [SerializeField, Min(0.5f)] private float limpFps = 4f;

    [Header("Hitbox (cápsula en la capa Default: la escopeta la toca)")]
    [SerializeField, Min(0.1f)] private float hitboxRadius = 0.3f;
    [SerializeField, Min(0.2f)] private float standingHeight = 1.8f;
    [Tooltip("Cubierto: más bajo que el banco (1.0 m) para que los perdigones choquen con el banco")]
    [SerializeField, Min(0.2f)] private float coverHeight = 0.7f;
    [SerializeField, Min(0.2f)] private float seatedHeight = 1.0f;

    [Header("Debug (apagado en el juego)")]
    [Tooltip("F9 = matar (como el final de la escena), F10 = perdonar y huir. Solo herido. Hasta que exista 3C.3")]
    [SerializeField] private bool debug = false;

    /// <summary>Herido y el jugador llegó a ≤ 3 m con línea de vista (se dispara una vez por acercamiento).</summary>
    public event Action OnInterrogationRequested;

    /// <summary>Cambió de estado.</summary>
    public event Action<State> OnStateChanged;

    public State CurrentState => _state;
    public ContadorShed Shed => shed;
    public BillboardSprite Billboard => billboard;
    public AudioSource VoiceSource => voiceSource;
    public LipSyncDriver LipSync => lipSync;

    /// <summary>¿Se puede empezar el interrogatorio ahora?</summary>
    public bool CanBeInterrogated => _state == State.Wounded;

    // IInterrogationTarget (fase 3C.3)
    public string SpeakerName => "CENTAVO";
    public Transform CameraAnchor => shed != null ? shed.InterrogationCamAnchor : null;
    public CharacterSpriteSet PortraitSet => billboard != null ? billboard.SpriteSet : null;

    // Máscara de vista: todo menos Ignore Raycast (2), UI (5) y Viewmodel (8)
    private const int SightMask = ~((1 << 2) | (1 << 5) | (1 << 8));
    private static readonly RaycastHit[] SightHits = new RaycastHit[12];

    private State _state = State.Dormant;
    private float _stateTime;

    private Transform _player;
    private PlayerHealth _playerHealth;
    private PlayerController _playerController;
    private Camera _playerCamera;

    // Combate
    private CombatPhase _phase;
    private float _phaseUntil;
    private int _ammo;
    private bool _saidReloadLine;
    private float _flashUntil;
    private float _shakeUntil;
    private Vector3 _visualBasePosition;
    private float _nextCoverBark;

    // Voz
    private float _nextBarkTime;

    // Herido
    private float _graceUntil;
    private bool _saidWoundedLine;
    private float _nextWoundedRepeat;
    private float _nextDrip;
    private bool _interrogationArmed = true;

    // Huida
    private bool _fleeLinePending;
    private int _pathIndex;
    private float _limpTimer;
    private int _limpFrame;

    private float _nextSightCheck;
    private SpriteRenderer _spriteRenderer;
    private float _flashSpriteUntil;

    // ==================================================================
    // Ciclo de vida
    // ==================================================================

    private void Awake()
    {
        if (billboard != null) _spriteRenderer = billboard.GetComponent<SpriteRenderer>();
        if (visual != null) _visualBasePosition = visual.localPosition;
        if (muzzleFlash != null) muzzleFlash.enabled = false;
        if (droppedRevolver != null) droppedRevolver.SetActive(false);
        if (voiceSource != null) voiceSource.ignoreListenerPause = true; // D5: la voz se oye con el juego en pausa
        _ammo = shotsPerReload;
    }

    private void Start()
    {
        FindPlayer();
        if (shed == null)
        {
            Debug.LogWarning("[Centavo] Sin ContadorShed asignado: correr Plomo y Paja → Contador → Colocar Centavo.");
            enabled = false;
            return;
        }
        PlaceAt(shed.StandAnchor);
        EnterDormant();
    }

    private void FindPlayer()
    {
        GameObject go = GameObject.FindWithTag("Player");
        if (go == null) return;
        _player = go.transform;
        _playerHealth = go.GetComponentInChildren<PlayerHealth>();
        if (_playerHealth == null) _playerHealth = go.GetComponentInParent<PlayerHealth>();
        _playerController = go.GetComponentInChildren<PlayerController>();
        if (_playerController == null) _playerController = go.GetComponentInParent<PlayerController>();
        _playerCamera = go.GetComponentInChildren<Camera>();
    }

    private void Update()
    {
        if (_player == null) FindPlayer();
        _stateTime += Time.deltaTime;

        switch (_state)
        {
            case State.Dormant: UpdateDormant(); break;
            case State.Alert: UpdateAlert(); break;
            case State.Combat: UpdateCombat(); break;
            case State.Hit: UpdateHit(); break;
            case State.Wounded: UpdateWounded(); break;
            case State.Interrogation: UpdateSeatedMouth(); break;
            case State.Fleeing: UpdateFleeing(); break;
        }

        UpdateShotEffects();
        UpdateDebugKeys();
    }

    private void OnDisable()
    {
        if (muzzleFlash != null) muzzleFlash.enabled = false;
        if (_spriteRenderer != null) CharacterLook.SetFlash(_spriteRenderer, 0f);
    }

    // ==================================================================
    // Estados
    // ==================================================================

    private void SetState(State next)
    {
        if (_state == next) return;
        _state = next;
        _stateTime = 0f;
        OnStateChanged?.Invoke(next);
    }

    private void EnterDormant()
    {
        SetState(State.Dormant);
        StandingPose(false);
        if (billboard != null) billboard.Play("idle");
    }

    private void UpdateDormant()
    {
        if (Time.time < _nextSightCheck) return;
        _nextSightCheck = Time.time + 0.2f;
        if (!GuardsNeutralized() || !PlayerAlive()) return;
        if (!shed.IsInsideActivation(_player.position)) return;
        if (!HasLineOfSight(StandingEye())) return;

        SetState(State.Alert);
        StandingPose(false);
        if (billboard != null) billboard.ShowFrame("peek", 0);
        Say(LineSpotted, true);
    }

    private void UpdateAlert()
    {
        if (_stateTime < alertSeconds) return;
        SetState(State.Combat);
        StartPhase(CombatPhase.Cover);
    }

    // ---------- Combate ----------

    private void StartPhase(CombatPhase phase)
    {
        _phase = phase;
        switch (phase)
        {
            case CombatPhase.Cover:
                _phaseUntil = Time.time + RandomRange(coverSeconds);
                StandingPose(true);
                if (billboard != null) billboard.ShowFrame("cover", 0);
                if (Time.time >= _nextCoverBark && UnityEngine.Random.value < coverBarkChance && Say(LineCover, false))
                    _nextCoverBark = Time.time + coverBarkCooldown;
                break;

            case CombatPhase.Peek:
                _phaseUntil = Time.time + RandomRange(peekSeconds);
                StandingPose(false);
                if (billboard != null) billboard.ShowFrame("peek", 0);
                break;

            case CombatPhase.Shoot:
                _phaseUntil = Time.time + shootPoseSeconds;
                StandingPose(false);
                if (billboard != null) billboard.ShowFrame("shoot", 0);
                FireShot();
                if (UnityEngine.Random.value < shootingBarkChance) Say(LineShooting, false);
                break;

            case CombatPhase.Reload:
                _phaseUntil = Time.time + reloadSeconds;
                StandingPose(true);
                if (billboard != null) billboard.ShowFrame("cover", 0);
                if (!_saidReloadLine && Say(LineReload, false)) _saidReloadLine = true;
                break;
        }
    }

    private void UpdateCombat()
    {
        if (Time.time < _phaseUntil) return;

        switch (_phase)
        {
            case CombatPhase.Cover:
                if (_ammo <= 0) StartPhase(CombatPhase.Reload);
                else if (PlayerAlive() && HasLineOfSight(StandingEye())) StartPhase(CombatPhase.Peek);
                else _phaseUntil = Time.time + RandomRange(coverSeconds) * 0.5f; // sigue escondido y vuelve a mirar
                break;
            case CombatPhase.Peek:
                if (PlayerAlive()) StartPhase(CombatPhase.Shoot);
                else StartPhase(CombatPhase.Cover);
                break;
            case CombatPhase.Shoot:
                StartPhase(CombatPhase.Cover);
                break;
            case CombatPhase.Reload:
                _ammo = shotsPerReload;
                StartPhase(CombatPhase.Cover);
                break;
        }
    }

    private void FireShot()
    {
        _ammo--;
        Vector3 muzzle = MuzzlePosition();

        // Sonido: revólver "pop" (escopeta más aguda si no hay uno propio)
        if (sfxSource != null)
        {
            AudioClip clip = AudioLibrary.GetSfx(shotSfx);
            if (clip != null)
            {
                sfxSource.pitch = shotPitch;
                sfxSource.PlayOneShot(clip, shotVolume * AudioLibrary.SfxVolume);
            }
        }

        // Fogonazo chico y temblor del dibujo
        if (muzzleFlash != null)
        {
            muzzleFlash.transform.position = muzzle;
            muzzleFlash.enabled = true;
            _flashUntil = Time.time + flashSeconds;
        }
        _shakeUntil = Time.time + shakeSeconds;

        // Hitscan al jugador: mala puntería a propósito
        if (_playerHealth == null || _playerHealth.IsDead || _player == null) return;
        Vector3 target = PlayerChest();
        if ((target - muzzle).sqrMagnitude > maxShotDistance * maxShotDistance) return;
        if (!ClearLine(muzzle, target)) return;
        bool running = _playerController != null && _playerController.IsRunning && _playerController.IsMoving;
        if (UnityEngine.Random.value < (running ? hitChanceRunning : hitChance))
            _playerHealth.TakeDamage(damage);
    }

    private void UpdateShotEffects()
    {
        if (muzzleFlash != null && muzzleFlash.enabled && Time.time >= _flashUntil) muzzleFlash.enabled = false;
        if (_spriteRenderer != null && _flashSpriteUntil > 0f && Time.unscaledTime >= _flashSpriteUntil)
        {
            _flashSpriteUntil = -1f;
            CharacterLook.SetFlash(_spriteRenderer, 0f);
        }

        if (visual == null) return;
        Vector3 basePos = _visualBasePosition;
        if (_state == State.Combat && (_phase == CombatPhase.Cover || _phase == CombatPhase.Reload)) basePos.y -= coverSink;
        if (Time.time < _shakeUntil)
        {
            float t = (_shakeUntil - Time.time) / Mathf.Max(0.01f, shakeSeconds);
            basePos.x += Mathf.Sin(Time.time * 90f) * shakeAmount * t;
        }
        visual.localPosition = basePos;
    }

    // ---------- Golpe ----------

    /// <summary>IDamageable: perdigón del jugador. Devuelve true solo si lo mata (D4).</summary>
    public bool TakeDamage(float amount)
    {
        if (amount <= 0f || !enabled) return false;

        switch (_state)
        {
            case State.Dormant:
            case State.Alert:
            case State.Combat:
                EnterHit(); // D3: cualquier golpe lo tumba, nunca lo mata el primero
                return false;

            case State.Wounded:
                if (Time.time < _graceUntil) return false;
                Die(true);  // D4: rematarlo antes de la escena
                return true;

            default:
                return false; // Hit (resto del disparo), interrogatorio, muerto, huyendo
        }
    }

    private void EnterHit()
    {
        SetState(State.Hit);
        _graceUntil = float.MaxValue;
        if (muzzleFlash != null) muzzleFlash.enabled = false;
        StandingPose(false);
        if (hitbox != null) hitbox.enabled = false;
        if (billboard != null) billboard.ShowFrame("hit", 0);
        Say(LineHit, true);

        Vector3 away = AwayFromPlayer();
        Vector3 chest = transform.position + Vector3.up * 1.2f;
        CartoonFx.BloodBurst(chest, away);
        CartoonFx.HitStop(0.04f);
        AudioLibrary.PlaySfx("impacto_carne", chest);
        AudioLibrary.PlaySfx("sangre_chorro", chest);
        FlashSprite();
        DropRevolver(away);
    }

    private void UpdateHit()
    {
        if (_stateTime < hitPoseSeconds) return;

        PlaceAt(shed.SeatedAnchor);
        SetState(State.Wounded);
        SeatedPose();
        _graceUntil = Time.time + woundedGraceSeconds;
        _nextWoundedRepeat = Time.time + RandomRange(woundedRepeatSeconds);
        _nextDrip = Time.time + RandomRange(dripSeconds);
        if (hitbox != null) hitbox.enabled = true;
    }

    private void DropRevolver(Vector3 away)
    {
        if (droppedRevolver == null) return;
        droppedRevolver.transform.SetParent(null, true);
        droppedRevolver.transform.position = MuzzlePosition();
        droppedRevolver.SetActive(true);
        if (!droppedRevolver.TryGetComponent(out Rigidbody body)) body = droppedRevolver.AddComponent<Rigidbody>();
        body.mass = 0.8f;
        body.velocity = away * 1.2f + Vector3.up * 2.2f;
        body.angularVelocity = UnityEngine.Random.insideUnitSphere * 9f;
    }

    // ---------- Herido ----------

    private void UpdateWounded()
    {
        UpdateSeatedMouth();
        UpdateDrip();
        if (!PlayerAlive()) return;

        float distance = Vector3.Distance(transform.position, _player.position);
        bool sight = false;
        if (Time.time >= _nextSightCheck)
        {
            _nextSightCheck = Time.time + 0.15f;
            sight = HasLineOfSight(SeatedEye());
        }

        if (sight && !_saidWoundedLine && Say(LineWounded, false)) _saidWoundedLine = true;

        // Se repite mientras el jugador está entre 3 m y el radio de activación (12 m)
        if (Time.time >= _nextWoundedRepeat && distance > interrogationDistance && distance <= shed.ActivationRadius)
        {
            if (Say(LineWoundedRepeat, false)) _nextWoundedRepeat = Time.time + RandomRange(woundedRepeatSeconds);
            else _nextWoundedRepeat = Time.time + 1f; // canal ocupado: reintentar pronto
        }

        if (distance > interrogationDistance + 0.75f) _interrogationArmed = true;
        if (sight && _interrogationArmed && distance <= interrogationDistance)
        {
            _interrogationArmed = false;
            OnInterrogationRequested?.Invoke();
        }
    }

    /// <summary>Par herido_01/02 (o la emoción que toque) según el lip-sync de su voz.</summary>
    private void UpdateSeatedMouth()
    {
        if (billboard == null) return;
        bool open = lipSync != null && lipSync.IsOpen;
        billboard.ShowFrame("herido", open ? 1 : 0);
    }

    private void UpdateDrip()
    {
        if (Time.time < _nextDrip) return;
        _nextDrip = Time.time + RandomRange(dripSeconds);
        // Al costado (fuera de la cápsula, para que la gota no caiga sobre ella), del lado de la cámara
        Vector3 side = Vector3.Cross(Vector3.up, AwayFromPlayer()).normalized;
        Vector3 at = transform.position + side * (hitboxRadius + 0.12f) - AwayFromPlayer() * 0.1f + Vector3.up * 0.45f;
        CartoonFx.BloodDrip(at);
    }

    // ---------- Muerte y huida ----------

    /// <param name="killHook">true = muerte "normal" (D4): cuenta para el tracker y la IA comenta como cualquier baja.
    /// false = la escena de interrogatorio ya puso su pensamiento de la IA (D11).</param>
    private void Die(bool killHook)
    {
        SetState(State.Dead);
        StopVoice();
        if (hitbox != null) hitbox.enabled = false;
        SeatedPose();
        if (billboard != null) billboard.PlayDeath();

        Vector3 chest = transform.position + Vector3.up * 0.6f;
        CartoonFx.BloodBurst(chest, AwayFromPlayer());
        AudioLibrary.PlaySfx("impacto_carne", chest);
        FlashSprite();

        if (GameStateTracker.Instance != null)
        {
            GameStateTracker.Instance.RegisterContadorState(ContadorState.Dead);
            GameStateTracker.Instance.RegisterEnemyKilled("Centavo");
        }
        if (killHook && AICompanion.Instance != null) AICompanion.Instance.Speak(AITrigger.EnemyKilled);
    }

    private void StartFleeing()
    {
        SetState(State.Fleeing);
        if (hitbox != null) hitbox.enabled = false;
        StandingPose(false);
        _pathIndex = 0;
        _limpTimer = 0f;
        _limpFrame = 0;
        if (billboard != null) billboard.ShowFrame("limp", 0);
        _fleeLinePending = true; // contador_huye_01 suena al cerrarse la escena, no encima de ella
        if (GameStateTracker.Instance != null) GameStateTracker.Instance.RegisterContadorState(ContadorState.Pardoned);
    }

    private void UpdateFleeing()
    {
        if (_fleeLinePending && !InterrogationDirector.IsSceneOpen && Say(LineFlee, true)) _fleeLinePending = false;

        Transform[] path = shed.ExitPath;
        if (path == null || _pathIndex >= path.Length)
        {
            Vanish();
            return;
        }
        Transform point = path[_pathIndex];
        if (point == null)
        {
            _pathIndex++;
            return;
        }

        Vector3 pos = transform.position;
        Vector3 goal = point.position;
        goal.y = pos.y;
        Vector3 next = Vector3.MoveTowards(pos, goal, fleeSpeed * Time.deltaTime);
        Vector3 dir = goal - pos;
        if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        next.y = GroundY(next, pos.y);
        transform.position = next;

        Vector3 flat = goal - next;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.01f) _pathIndex++;

        _limpTimer += Time.deltaTime * limpFps;
        if (_limpTimer >= 1f)
        {
            _limpTimer -= 1f;
            _limpFrame = 1 - _limpFrame;
            if (billboard != null) billboard.ShowFrame("limp", _limpFrame);
        }
    }

    private void Vanish()
    {
        StopVoice();
        SetState(State.Gone);
        gameObject.SetActive(false);
    }

    // ==================================================================
    // API para el interrogatorio (3C.3 / 3C.4)
    // ==================================================================

    /// <summary>Empieza la escena: deja de hablar/ladrar en el mundo y no recibe disparos.</summary>
    public bool BeginInterrogation()
    {
        if (_state != State.Wounded) return false;
        SetState(State.Interrogation);
        StopVoice();
        if (hitbox != null) hitbox.enabled = false;
        return true;
    }

    /// <summary>Final "Jalar el gatillo": queda con death_01. La IA no comenta la baja (D11).</summary>
    public void ResolveKilled()
    {
        if (_state != State.Interrogation && _state != State.Wounded) return;
        Die(false);
    }

    /// <summary>Final "Dejarlo ir": se va cojeando por Exit_Path.</summary>
    public void ResolvePardoned()
    {
        if (_state != State.Interrogation && _state != State.Wounded) return;
        StartFleeing();
    }

    /// <summary>Huyendo: desaparece ya (el epílogo lo llama mientras la cámara mira al cielo, 3C.4.1).</summary>
    public void ForceGone()
    {
        if (_state == State.Fleeing) Vanish();
    }

    /// <summary>Oculta/muestra el dibujo del mundo (D6: en la escena se ve el retrato).</summary>
    public void SetWorldVisible(bool visible)
    {
        if (billboard != null) billboard.SetVisible(visible);
    }

    private void UpdateDebugKeys()
    {
        if (!debug) return;
        if (_state != State.Wounded && _state != State.Interrogation) return;
        if (Input.GetKeyDown(KeyCode.F9)) ResolveKilled();
        else if (Input.GetKeyDown(KeyCode.F10)) ResolvePardoned();
    }

    // ==================================================================
    // Poses, hitbox y escala
    // ==================================================================

    private void PlaceAt(Transform anchor)
    {
        if (anchor == null) return;
        transform.SetPositionAndRotation(anchor.position, anchor.rotation);
    }

    /// <summary>De pie (dibujo a escala 1). covered = cápsula baja, para que el banco tape los perdigones.</summary>
    private void StandingPose(bool covered)
    {
        if (visual != null) visual.localScale = Vector3.one;
        SetHitbox(covered ? coverHeight : standingHeight);
    }

    /// <summary>Sentado en el piso: los cuadros llenan el lienzo, se achican a seatedScale (D12).</summary>
    private void SeatedPose()
    {
        if (visual != null) visual.localScale = Vector3.one * seatedScale;
        SetHitbox(seatedHeight);
    }

    private void SetHitbox(float height)
    {
        if (hitbox == null) return;
        hitbox.direction = 1;
        hitbox.radius = hitboxRadius;
        hitbox.height = Mathf.Max(height, hitboxRadius * 2f);
        hitbox.center = new Vector3(0f, hitbox.height * 0.5f, 0f);
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

    /// <summary>Un bark por su propia fuente 3D (la lee el lip-sync). force = corta lo que esté diciendo.</summary>
    private bool Say(string lineId, bool force)
    {
        if (voiceSource == null) return false;
        if (InterrogationDirector.IsSceneOpen) return false; // en la escena solo hablan las voces del director (3C.4, A3)
        if (!force && (voiceSource.isPlaying || Time.time < _nextBarkTime)) return false;

        AudioClip clip = AudioLibrary.GetVoice(lineId);
        float length = clip != null ? clip.length : 2f;
        _nextBarkTime = Time.time + Mathf.Max(minBarkGap, length);
        voiceSource.Stop();
        if (clip == null) return true;
        voiceSource.clip = clip;
        voiceSource.volume = AudioLibrary.VoiceVolume;
        voiceSource.pitch = 1f;
        voiceSource.Play();
        return true;
    }

    private void StopVoice()
    {
        if (voiceSource != null) voiceSource.Stop();
    }

    // ==================================================================
    // Jugador, vista y piso
    // ==================================================================

    private bool PlayerAlive() => _player != null && (_playerHealth == null || !_playerHealth.IsDead);

    private bool GuardsNeutralized()
    {
        if (yardGuards == null) return true;
        foreach (EnemyHealth guard in yardGuards)
        {
            if (guard == null) continue;                 // destruido tras morir
            if (!guard.IsDead && !guard.IsSurrendered) return false;
        }
        return true;
    }

    private Vector3 PlayerEye()
    {
        if (_playerCamera != null) return _playerCamera.transform.position;
        return _player.position + Vector3.up * 1.6f;
    }

    private Vector3 PlayerChest() => PlayerEye() - Vector3.up * 0.45f;

    private Vector3 StandingEye() => transform.position + Vector3.up * 1.6f;

    private Vector3 SeatedEye() => transform.position + Vector3.up * 0.8f;

    private Vector3 MuzzlePosition()
    {
        Vector3 toPlayer = _player != null ? _player.position - transform.position : transform.forward;
        toPlayer.y = 0f;
        toPlayer = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : transform.forward;
        return transform.position + Vector3.up * 1.35f + toPlayer * 0.35f;
    }

    private Vector3 AwayFromPlayer()
    {
        Vector3 dir = _player != null ? transform.position - _player.position : -transform.forward;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.001f ? dir.normalized : -transform.forward;
    }

    private bool HasLineOfSight(Vector3 from)
    {
        if (_player == null) return false;
        return ClearLine(from, PlayerEye());
    }

    /// <summary>¿Nada sólido entre los dos puntos? Ignora sus propios colliders, al jugador y los triggers.</summary>
    private bool ClearLine(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.01f) return true;
        int count = Physics.RaycastNonAlloc(from, delta / distance, SightHits, distance, SightMask,
            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider c = SightHits[i].collider;
            if (c == null || c.transform.IsChildOf(transform)) continue;
            if (_player != null && c.transform.IsChildOf(_player)) continue;
            if (c.GetComponentInParent<PlayerController>() != null) continue;
            return false;
        }
        return true;
    }

    private float GroundY(Vector3 at, float fallback)
    {
        int count = Physics.RaycastNonAlloc(at + Vector3.up * 1f, Vector3.down, SightHits, 2.5f, SightMask,
            QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = SightHits[i];
            Collider c = hit.collider;
            if (c == null || c.transform.IsChildOf(transform) || hit.normal.y < 0.6f) continue;
            if (c.GetComponentInParent<PlayerController>() != null || c.GetComponentInParent<EnemyHealth>() != null) continue;
            if (hit.point.y > best) best = hit.point.y;
        }
        return float.IsNegativeInfinity(best) ? fallback : best;
    }

    private static float RandomRange(Vector2 range) => UnityEngine.Random.Range(range.x, range.y);
}
