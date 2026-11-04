using TMPro;
using UnityEngine;

/// <summary>
/// Tutorial corto del Nivel 1 (fase N1.3, regla N8; textos del guion Guion-Tutorial-Huellas §2).
/// DÓNDE VA: "--- TUTORIAL ---/TutorialDirector" en NivelPorky (Plomo y Paja → Nivel 1 → Colocar tutorial, también lo corre Generar).
///
/// PASOS (cada uno una vez por sesión de juego; reiniciar tras morir no repite nada):
///  T1 Tarjeta "Pensamientos", la única que bloquea: al terminar el pensamiento fijo de la llegada
///     (IntroCinematicDirector.RevealThoughtEnded; la cinemática la espera). Sin cinemática, o si se saltó: 1.5 s después de
///     recibir el control. timeScale 0, entrada bloqueada, cursor visible, tiempo real; se cierra con clic, Espacio o Enter
///     después de 1 s y deja todo como estaba.
///  T2 Moverse: al recuperar el control (siempre después de T1).
///  T3 Escopeta: al sacarla (PlayerWeaponHolster.OnStateChanged → Ready). Si no hay nada que sacar (sin holster o ya lista,
///     como al entrar sin cinemática): 3 s después de recibir el control.
///  T4 Revólver: al tener el revólver (WeaponSwitcher.HasRevolver). WeaponPickup solo muestra su nombre, no los controles.
/// Los pensamientos van por AICompanion.ShowCustomLine, el mismo camino que el de la llegada (I9), nunca por la API.
///
/// COLA: nada aparece en interrogatorio, cinemática, diálogo, pausa o muerte: el paso espera; si ya estaba en pantalla, se oculta
/// y su tiempo no corre. Si hay otro paso esperando, el que está en pantalla termina a los 4 s. Sin basura por frame.
/// BANDERA: _shown, estática (dura toda la ejecución, como IntroCinematicDirector._playedThisSession). No está en
/// GameStateTracker porque ese archivo es de la otra sesión.
/// </summary>
public class TutorialDirector : MonoBehaviour
{
    private enum Step { None = 0, Card = 1, Move = 2, Weapon = 4, Revolver = 8 }
    private enum ChipAction { Move, Run, Interact, Aim, Fire, Reload, Select1, Select2, Wheel }

    private struct Chip
    {
        public string key, label;
        public ChipAction action;
        public Chip(string key, string label, ChipAction action) { this.key = key; this.label = label; this.action = action; }
    }

    // Guion §2 (textos exactos)
    private const string MoveThought = "Primero mira bien la escena. Los muertos no cuentan nada… pero las casas sí.";
    private const string WeaponThought = "Clic derecho para apuntar con calma. Clic izquierdo, y que hable la escopeta. De cerca no perdona; de lejos, ni cosquillas.";
    private const string RevolverThought = "El revólver de Centavo. Una bala, un problema menos… si apuntas bien.";

    private static readonly Chip[] MoveChips =
    {
        new Chip("WASD", "Caminar", ChipAction.Move), new Chip("Shift", "Correr (hace ruido)", ChipAction.Run), new Chip("E", "Examinar / tomar", ChipAction.Interact),
    };
    private static readonly Chip[] WeaponChips =
    {
        new Chip("Clic derecho", "Apuntar", ChipAction.Aim), new Chip("Clic izquierdo", "Disparar", ChipAction.Fire), new Chip("R", "Recargar", ChipAction.Reload),
    };
    private static readonly Chip[] RevolverChips =
    {
        new Chip("1", "Escopeta", ChipAction.Select1), new Chip("2", "Revólver", ChipAction.Select2), new Chip("Rueda", "Cambiar", ChipAction.Wheel),
    };

    private const string KeyColor = "#8A2A1E";

    private static int _shown;

    [Header("Tarjeta (T1)")]
    [SerializeField] private GameObject cardRoot;
    [Tooltip("\"Clic para continuar\": aparece cuando ya se puede cerrar")]
    [SerializeField] private CanvasGroup cardCloseHint;
    [SerializeField] private float cardMinSeconds = 1f;
    [Tooltip("Sin cinemática: segundos desde que se recibe el control")]
    [SerializeField] private float cardFallbackDelay = 1.5f;

    [Header("Etiquetas")]
    [SerializeField] private CanvasGroup chipsGroup;
    [Tooltip("Un texto por etiqueta (3). Su padre es la etiqueta de papel")]
    [SerializeField] private TMP_Text[] chipTexts = new TMP_Text[0];
    [SerializeField] private float moveSeconds = 10f;
    [SerializeField] private float weaponSeconds = 12f;
    [SerializeField] private float revolverSeconds = 10f;
    [Tooltip("Sin arma que sacar: segundos desde que se recibe el control hasta T3")]
    [SerializeField] private float weaponFallbackDelay = 3f;
    [Tooltip("Con otro paso esperando, el que está en pantalla dura esto como mínimo")]
    [SerializeField] private float minVisibleWhenQueued = 4f;
    [Tooltip("Cuánto se quedan al completarlas todas")]
    [SerializeField] private float doneLinger = 0.7f;
    [SerializeField] private float chipFadeSeconds = 0.25f;

    private IntroCinematicDirector _intro;
    private PlayerWeaponHolster _holster;
    private PlayerController _player;
    private WeaponSwitcher _switcher;

    private bool _cardOpen;
    private float _cardTime;
    private bool _heldIntro;
    private bool _ownsCinematicState;
    private bool _playerWasEnabled;

    private Step _active;
    private Chip[] _chips;
    private readonly bool[] _ticked = new bool[3];
    private int _tickedCount;
    private float _elapsed;
    private float _limit;
    private float _doneTime = -1f;
    private float _controlSeconds;
    private bool _drawHappened;

    private static bool Shown(Step step) => (_shown & (int)step) != 0;
    private static void MarkShown(Step step) => _shown |= (int)step;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _shown = 0;

    private static bool Blocked() => PlayerAim.GameplayBlocked() || UiHideReasons.IsActive(UiHideReasons.Reason.Death);

    private void Awake()
    {
        if (cardRoot != null) cardRoot.SetActive(false);
        if (chipsGroup != null) chipsGroup.alpha = 0f;
    }

    private void Start()
    {
        _intro = FindObjectOfType<IntroCinematicDirector>();
        if (_intro != null && _intro.PlaysThisLoad) _intro.RevealThoughtEnded += HandleRevealThoughtEnded;

        _holster = PlayerWeaponHolster.Current;
        if (_holster != null) _holster.OnStateChanged += HandleHolsterChanged;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            player.TryGetComponent(out _player);
            if (!player.TryGetComponent(out _switcher)) _switcher = player.GetComponentInParent<WeaponSwitcher>();
        }
    }

    private void OnDestroy()
    {
        if (_intro != null) _intro.RevealThoughtEnded -= HandleRevealThoughtEnded;
        if (_holster != null) _holster.OnStateChanged -= HandleHolsterChanged;
        if (_cardOpen) CloseCard();
    }

    private void HandleHolsterChanged(PlayerWeaponHolster.HolsterState state)
    {
        if (state == PlayerWeaponHolster.HolsterState.Ready) _drawHappened = true;
    }

    private void HandleRevealThoughtEnded()
    {
        if (!Shown(Step.Card)) OpenCard(true);
    }

    // ==================================================================
    // Pasos
    // ==================================================================

    private void Update()
    {
        if (_cardOpen) return; // la tarjeta se atiende en LateUpdate

        bool blocked = Blocked();
        if (!blocked) _controlSeconds += Time.unscaledDeltaTime;

        if (_active != Step.None) UpdateActive(blocked);
        else if (!blocked) StartNext();
    }

    private void StartNext()
    {
        if (!Shown(Step.Card))
        {
            if (_controlSeconds >= cardFallbackDelay) OpenCard(false);
            return; // T2 siempre después de T1
        }
        if (!Shown(Step.Move)) StartStep(Step.Move, MoveThought, MoveChips, moveSeconds);
        else if (WeaponIsDue()) StartStep(Step.Weapon, WeaponThought, WeaponChips, weaponSeconds);
        else if (RevolverIsDue()) StartStep(Step.Revolver, RevolverThought, RevolverChips, revolverSeconds);
    }

    private bool WeaponIsDue()
    {
        if (Shown(Step.Weapon)) return false;
        if (_drawHappened) return true;
        bool nothingToDraw = _holster == null || _holster.State == PlayerWeaponHolster.HolsterState.Ready;
        return nothingToDraw && _controlSeconds >= weaponFallbackDelay;
    }

    private bool RevolverIsDue() => !Shown(Step.Revolver) && _switcher != null && _switcher.HasRevolver;

    private void StartStep(Step step, string thought, Chip[] chips, float seconds)
    {
        MarkShown(step); // al empezar: si muere con el paso en pantalla, no se repite
        _active = step;
        _chips = chips;
        _elapsed = 0f;
        _limit = seconds;
        _doneTime = -1f;
        _tickedCount = 0;

        if (AICompanion.Instance != null) AICompanion.Instance.ShowCustomLine(thought, false); // no entra al historial de la API
        else Debug.LogWarning("[Tutorial] No hay AICompanion: el pensamiento no se muestra.");

        for (int i = 0; i < _ticked.Length; i++)
        {
            _ticked[i] = false;
            TMP_Text text = i < chipTexts.Length ? chipTexts[i] : null;
            if (text == null) continue;
            text.text = i < chips.Length ? ChipText(chips[i], false) : "";
            text.transform.parent.gameObject.SetActive(i < chips.Length);
        }
    }

    private static string ChipText(Chip chip, bool done)
    {
        string text = $"<color={KeyColor}>{chip.key}</color>  {chip.label}";
        return done ? $"<s>{text}</s>" : text;
    }

    private void UpdateActive(bool blocked)
    {
        if (blocked)
        {
            if (chipsGroup != null) chipsGroup.alpha = 0f; // espera sin correr el tiempo
            return;
        }

        float dt = Time.unscaledDeltaTime;
        _elapsed += dt;
        for (int i = 0; i < _chips.Length && i < _ticked.Length; i++)
        {
            if (_ticked[i] || !Performed(_chips[i].action)) continue;
            _ticked[i] = true;
            _tickedCount++;
            if (i < chipTexts.Length && chipTexts[i] != null) chipTexts[i].text = ChipText(_chips[i], true);
        }

        bool allDone = _tickedCount >= _chips.Length;
        if (allDone && _doneTime < 0f) _doneTime = _elapsed;

        bool over = _elapsed >= _limit || (allDone && _elapsed - _doneTime >= doneLinger) || (_elapsed >= minVisibleWhenQueued && AnotherStepWaiting());
        if (chipsGroup != null)
        {
            float target = over ? 0f : 1f;
            chipsGroup.alpha = Mathf.MoveTowards(chipsGroup.alpha, target, dt / Mathf.Max(0.01f, chipFadeSeconds));
            if (over && chipsGroup.alpha > 0f) return; // termina de desvanecerse
        }
        if (over) _active = Step.None;
    }

    private bool AnotherStepWaiting() => (_active != Step.Weapon && WeaponIsDue()) || (_active != Step.Revolver && RevolverIsDue());

    private bool Performed(ChipAction action)
    {
        switch (action)
        {
            case ChipAction.Move: return Input.GetAxisRaw("Horizontal") != 0f || Input.GetAxisRaw("Vertical") != 0f;
            case ChipAction.Run: return _player != null && _player.IsRunning && _player.IsMoving;
            case ChipAction.Interact: return Input.GetKeyDown(KeyCode.E);
            case ChipAction.Aim: return Input.GetMouseButtonDown(1);
            case ChipAction.Fire: return Input.GetButtonDown("Fire1");
            case ChipAction.Reload: return Input.GetKeyDown(KeyCode.R);
            case ChipAction.Select1: return Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
            case ChipAction.Select2: return Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
            case ChipAction.Wheel: return Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f;
            default: return false;
        }
    }

    // ==================================================================
    // Tarjeta "Pensamientos" (T1)
    // ==================================================================

    private void OpenCard(bool fromIntro)
    {
        MarkShown(Step.Card);
        if (cardRoot == null)
        {
            Debug.LogWarning("[Tutorial] Falta la tarjeta (corre Plomo y Paja → Nivel 1 → Colocar tutorial): se salta T1.");
            return;
        }

        _cardOpen = true;
        _cardTime = 0f;
        _heldIntro = fromIntro && _intro != null;
        if (_heldIntro) _intro.SetHoldAfterReveal(true);

        // Sin cinemática la bloquea la tarjeta: GameplayBlocked (sin armas ni daño, guardias quietos) y Esc sin pausa
        _ownsCinematicState = !CinematicState.IsPlaying;
        if (_ownsCinematicState) CinematicState.Set(this, true);
        if (_player != null)
        {
            _playerWasEnabled = _player.enabled;
            _player.enabled = false;
        }

        CartoonFx.TimeHeld = true; // la tarjeta es dueña de timeScale: un hit-stop en curso no lo devuelve a 1
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (cardCloseHint != null) cardCloseHint.alpha = 0f;
        cardRoot.SetActive(true);
    }

    /// <summary>En LateUpdate: el clic que cierra ya no lo ve el arma, que corrió su Update con la entrada bloqueada.</summary>
    private void LateUpdate()
    {
        if (!_cardOpen) return;
        _cardTime += Time.unscaledDeltaTime;
        bool canClose = _cardTime >= cardMinSeconds;
        if (cardCloseHint != null && canClose) cardCloseHint.alpha = Mathf.Clamp01((_cardTime - cardMinSeconds) * 3f);
        if (canClose && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            CloseCard();
    }

    private void CloseCard()
    {
        _cardOpen = false;
        if (cardRoot != null) cardRoot.SetActive(false);
        Time.timeScale = 1f;
        CartoonFx.TimeHeld = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (_player != null) _player.enabled = _playerWasEnabled;
        if (_ownsCinematicState) CinematicState.Set(this, false);
        _ownsCinematicState = false;
        if (_heldIntro && _intro != null) _intro.SetHoldAfterReveal(false);
        _heldIntro = false;
    }
}
