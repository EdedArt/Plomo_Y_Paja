using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Arranque sin arma y "sacar la escopeta" (INTRO.3, I8 + I12). Estados: Holstered → Drawing → Ready.
/// DÓNDE VA: GameObject "Player", junto a PlayerAim y WeaponSwitcher (lo añaden "Construir taxi y llegada" y
/// "Crear Prefabs de Prueba"). Sin este componente el arma está siempre lista (como antes).
///
/// - CUÁNDO EMPIEZA ENFUNDADA: solo si la cinemática del taxi corre en esta carga (IntroCinematicDirector.PlaysThisLoad)
///   y el arma no se sacó antes en la sesión (GameStateTracker.WeaponDrawn + bandera estática de respaldo, como la del taxi).
///   Con playIntroCinematic apagado, al reintentar tras morir y en cualquier otro nivel: Ready desde el principio.
/// - HOLSTERED: manos ocultas (FirstPersonViewmodel lee HidesHands), mira en modo punto (RubberHoseCrosshair), sin disparo,
///   apuntado, recarga ni cambio de arma (PlayerAim.WeaponInputBlocked, que leen las dos armas, PlayerAim y WeaponSwitcher),
///   munición del HUD oculta (UiHideReasons.Holstered; la vida sigue). Caminar y correr, libres. La munición no se toca.
///   Cada sistema COMPONE sus razones (muerte, pausa, diálogo, interrogatorio, cinemática, enfundada): nadie apaga la
///   razón de otro (I12).
/// - DISPARADORES (el primero que llega gana; los demás se ignoran):
///   1. "guardia a la vista": cada 0.1 s (tiempo real, sin basura) un guardia vivo a ≤ 30 m, dentro del frustum de la cámara
///      y con línea de vista (rayo a la capa Default, como EnemyAI y la simulación de INTRO.2: paredes y camión tapan).
///   2. daño recibido (PlayerHealth.OnDamaged; durante la cinemática no hay daño).
///   3. un guardia pasa a Alert, Chase o Attack (EnemyAI.OnStateChanged).
///   4. el trigger de la puerta de Porky (WeaponDrawTrigger, lo pone el menú del taxi).
///   Ninguno cuenta mientras PlayerAim.GameplayBlocked() (cinemática, pausa, diálogo…).
/// - DRAWING (drawSeconds = 0.5 s, tiempo real: termina aunque haya hit-stop): wilbur_arma_01 (voz 2D) al empezar,
///   escopeta_bombeo a pumpAt; las manos suben desde abajo (ViewmodelMotion: la bajada de recarga al revés). Al llegar a
///   Ready la mira completa aparece con un fundido corto y se desbloquea el arma.
/// - Una vez sacada no se vuelve a enfundar.
/// </summary>
public class PlayerWeaponHolster : MonoBehaviour
{
    public enum HolsterState { Holstered, Drawing, Ready }

    private static bool _drawnThisSession;

    [Header("Sacar el arma")]
    [Tooltip("Segundos que tardan las manos en subir (tiempo real)")]
    [SerializeField] private float drawSeconds = 0.5f;
    [Tooltip("Segundo de la subida en que suena el bombeo")]
    [SerializeField] private float pumpAt = 0.3f;
    [SerializeField] private string drawVoiceId = "wilbur_arma_01";
    [Tooltip("Guion §6: si ya existe el bombeo de la recarga se usa ese (escopeta_bombeo)")]
    [SerializeField] private string pumpSfxId = "escopeta_bombeo";

    [Header("Guardia a la vista (I8)")]
    [SerializeField] private float sightRange = 30f;
    [SerializeField] private float checkInterval = 0.1f;
    [Tooltip("Capas que tapan la vista. Default = la misma regla que EnemyAI (paredes, camión)")]
    [SerializeField] private LayerMask sightBlockers = 1; // Default

    [Header("Depuración (apagada por defecto)")]
    [SerializeField] private bool debug = false;

    /// <summary>El del Player de esta escena (null si no hay).</summary>
    public static PlayerWeaponHolster Current { get; private set; }

    /// <summary>¿El arma no responde? (enfundada o sacándose). Lo leen las armas, PlayerAim y WeaponSwitcher.</summary>
    public static bool BlocksWeapons => Current != null && Current._state != HolsterState.Ready;

    /// <summary>¿Las manos se ocultan? Solo enfundada: al sacarla, el viewmodel se ve subir.</summary>
    public static bool HidesHands => Current != null && Current._state == HolsterState.Holstered;

    /// <summary>¿Ya se sacó el arma en esta sesión? (sobrevive a recargar la escena)</summary>
    public static bool WeaponDrawnThisSession =>
        _drawnThisSession || (GameStateTracker.Instance != null && GameStateTracker.Instance.WeaponDrawn);

    public HolsterState State => _state;
    /// <summary>Qué disparador sacó el arma ("" si sigue enfundada o empezó lista).</summary>
    public string DrawReason { get; private set; } = "";
    public event Action<HolsterState> OnStateChanged;

    private HolsterState _state = HolsterState.Ready;
    private PlayerHealth _health;
    private FirstPersonViewmodel _viewmodel;
    private Camera _camera;
    private EnemyAI[] _guards = new EnemyAI[0];
    private readonly Plane[] _planes = new Plane[6];
    private Action<EnemyAI.EnemyState, EnemyAI.EnemyState> _guardHandler;
    private float _nextCheck;
    private bool _listening;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _drawnThisSession = false;
        Current = null;
    }

    private void Awake()
    {
        Current = this;
        TryGetComponent(out _health);
        _viewmodel = GetComponentInChildren<FirstPersonViewmodel>(true);
        foreach (Camera cam in GetComponentsInChildren<Camera>(true))
            if (cam.name != ViewmodelCameraRig.CameraName) { _camera = cam; break; }
        if (_camera == null) _camera = Camera.main;
        _guardHandler = HandleGuardState;
    }

    private void Start()
    {
        // Los Awake de la escena ya corrieron: el director sabe si la cinemática corre en esta carga
        IntroCinematicDirector director = FindObjectOfType<IntroCinematicDirector>();
        bool introThisLoad = director != null && director.PlaysThisLoad;
        if (introThisLoad && !WeaponDrawnThisSession) EnterHolstered();
        Log(_state == HolsterState.Holstered ? "empieza enfundada" : "empieza lista");
    }

    private void OnDestroy()
    {
        StopListening();
        if (Current == this) Current = null;
    }

    /// <summary>Pide sacar el arma (disparadores y WeaponDrawTrigger). Solo hace algo si está enfundada.</summary>
    public void RequestDraw(string reason)
    {
        if (_state != HolsterState.Holstered) return;
        if (_health != null && _health.IsDead) return;
        DrawReason = reason;
        Log("sacar el arma: " + reason);
        StartCoroutine(Draw());
    }

    // ==================================================================
    // Enfundada
    // ==================================================================

    private void EnterHolstered()
    {
        SetState(HolsterState.Holstered);
        UiHideReasons.Set(UiHideReasons.Reason.Holstered, true);
        _guards = FindObjectsOfType<EnemyAI>(); // una vez: lista fija, sin basura por frame
        for (int i = 0; i < _guards.Length; i++)
            if (_guards[i] != null) _guards[i].OnStateChanged += _guardHandler;
        if (_health != null) _health.OnDamaged += HandleDamaged;
        _listening = true;
    }

    private void StopListening()
    {
        if (!_listening) return;
        _listening = false;
        for (int i = 0; i < _guards.Length; i++)
            if (_guards[i] != null) _guards[i].OnStateChanged -= _guardHandler;
        if (_health != null) _health.OnDamaged -= HandleDamaged;
    }

    private void HandleDamaged(float damage, Vector3? source)
    {
        if (!PlayerAim.GameplayBlocked()) RequestDraw("daño");
    }

    private void HandleGuardState(EnemyAI.EnemyState previous, EnemyAI.EnemyState next)
    {
        if (PlayerAim.GameplayBlocked()) return;
        if (next == EnemyAI.EnemyState.Alert || next == EnemyAI.EnemyState.Chase || next == EnemyAI.EnemyState.Attack)
            RequestDraw("guardia en alerta (" + next + ")");
    }

    private void Update()
    {
        if (_state != HolsterState.Holstered || PlayerAim.GameplayBlocked()) return;
        if (Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + checkInterval;
        EnemyAI seen = FindVisibleGuard();
        if (seen != null) RequestDraw("guardia a la vista (" + seen.name + ")");
    }

    /// <summary>
    /// Primer guardia vivo a ≤ sightRange, dentro del frustum de la cámara y sin pared en medio (I8). Mismo modelo de
    /// línea de vista que INTRO.2: rayo a la capa Default (paredes, camión) hacia el pecho (+1 m) o la cabeza (+1.6 m).
    /// Sin basura: planos y lista preasignados.
    /// </summary>
    private EnemyAI FindVisibleGuard()
    {
        if (_camera == null || !_camera.isActiveAndEnabled) return null;
        GeometryUtility.CalculateFrustumPlanes(_camera, _planes);
        Vector3 eye = _camera.transform.position;
        float maxSqr = sightRange * sightRange;
        for (int i = 0; i < _guards.Length; i++)
        {
            EnemyAI guard = _guards[i];
            if (guard == null || !guard.isActiveAndEnabled || guard.CurrentState == EnemyAI.EnemyState.Dead) continue;
            Vector3 feet = guard.transform.position;
            Vector3 chest = feet + Vector3.up;
            if ((chest - eye).sqrMagnitude > maxSqr) continue;
            if (!GeometryUtility.TestPlanesAABB(_planes, new Bounds(feet + Vector3.up * 0.9f, new Vector3(0.6f, 1.8f, 0.6f)))) continue;
            if (ClearLine(eye, chest, guard.transform) || ClearLine(eye, feet + Vector3.up * 1.6f, guard.transform)) return guard;
        }
        return null;
    }

    private bool ClearLine(Vector3 from, Vector3 to, Transform guard)
    {
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.01f) return true;
        if (!Physics.Raycast(from, delta / distance, out RaycastHit hit, distance, sightBlockers, QueryTriggerInteraction.Ignore))
            return true;
        return hit.transform.IsChildOf(guard); // lo primero que toca es el propio guardia
    }

    // ==================================================================
    // Sacar el arma
    // ==================================================================

    private IEnumerator Draw()
    {
        StopListening();
        // Se marca al empezar: si muere a mitad de la subida, el reintento empieza armado igual
        _drawnThisSession = true;
        if (GameStateTracker.Instance != null) GameStateTracker.Instance.MarkWeaponDrawn();

        SetState(HolsterState.Drawing);
        if (_viewmodel != null) _viewmodel.PlayDraw(drawSeconds);
        if (!AudioLibrary.PlayVoice2D(drawVoiceId)) Log("sin voz " + drawVoiceId + ": solo el bombeo (guion §8, plan B)");

        bool pumped = false;
        for (float t = 0f; t < drawSeconds; t += Time.unscaledDeltaTime) // tiempo real: el hit-stop no la frena
        {
            if (!pumped && t >= pumpAt)
            {
                pumped = true;
                AudioLibrary.PlaySfx2D(pumpSfxId);
            }
            yield return null;
        }
        if (!pumped) AudioLibrary.PlaySfx2D(pumpSfxId);

        UiHideReasons.Set(UiHideReasons.Reason.Holstered, false);
        SetState(HolsterState.Ready);
        Log("arma lista");
    }

    private void SetState(HolsterState state)
    {
        if (_state == state) return;
        _state = state;
        OnStateChanged?.Invoke(state);
    }

    private void Log(string message)
    {
        if (debug) Debug.Log("[Arma] " + message);
    }

    /// <summary>Partida nueva (N2.1.1): la escopeta vuelve a empezar enfundada. La bandera del tracker la limpia él.</summary>
    public static void ResetSession() => _drawnThisSession = false;
}
