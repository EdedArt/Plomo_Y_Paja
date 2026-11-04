using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    private enum ChipAction { Move, Run, Interact, Aim, Fire, Reload, Select1, Select2, Wheel, SwitchWeapon }

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

    // Modo tarjetas ilustradas: T4 es UNA tarjeta que se marca al cambiar de arma por cualquier medio.
    private static readonly Chip[] RevolverCardChips = { new Chip("", "Cambiar de arma", ChipAction.SwitchWeapon) };

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

    [Header("Tarjetas ilustradas (T2-T4). Si falta el arte de un paso, ese paso usa las etiquetas de texto")]
    [Tooltip("Las etiquetas de texto de antes (plan B)")]
    [SerializeField] private GameObject chipRow;
    [Tooltip("Fila de tarjetas: abajo al centro; se escala sola para caber en pantallas angostas")]
    [SerializeField] private RectTransform cardRow;
    [SerializeField] private RectTransform[] cardRects = new RectTransform[0];
    [SerializeField] private Image[] cardImages = new Image[0];
    [Tooltip("Sello de cada tarjeta (esquina superior derecha); se prende al marcarla")]
    [SerializeField] private RectTransform[] stampRects = new RectTransform[0];
    [SerializeField] private Sprite[] moveCardSprites = new Sprite[0];
    [SerializeField] private Sprite[] weaponCardSprites = new Sprite[0];
    [SerializeField] private Sprite[] revolverCardSprites = new Sprite[0];
    [SerializeField] private float cardHeight = 170f;
    [SerializeField] private float cardGap = 16f;
    [SerializeField] private float popSeconds = 0.25f;
    [SerializeField] private float popStagger = 0.08f;
    [SerializeField] private float stampSeconds = 0.15f;
    [SerializeField] private float wobbleDegrees = 1.5f;
    [SerializeField] private float wobblePeriod = 2f;
    [SerializeField] private Color doneTint = new Color(0.62f, 0.62f, 0.62f, 1f);

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

    // Tarjetas ilustradas (todo preasignado: sin basura por frame)
    private bool _cardMode;
    private int _cardCount;
    private float _fit = 1f;
    private bool _weaponChanged;
    private readonly float[] _tickAge = { -1f, -1f, -1f };
    private readonly float[] _stampTilt = new float[3];

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
        if (_switcher != null) _switcher.OnWeaponChanged += HandleWeaponChanged;
    }

    private void OnDestroy()
    {
        if (_intro != null) _intro.RevealThoughtEnded -= HandleRevealThoughtEnded;
        if (_holster != null) _holster.OnStateChanged -= HandleHolsterChanged;
        if (_switcher != null) _switcher.OnWeaponChanged -= HandleWeaponChanged;
        if (_cardOpen) CloseCard();
    }

    private void HandleHolsterChanged(PlayerWeaponHolster.HolsterState state)
    {
        if (state == PlayerWeaponHolster.HolsterState.Ready) _drawHappened = true;
    }

    private void HandleWeaponChanged(IWeapon weapon) => _weaponChanged = true;

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
        if (!Shown(Step.Move)) StartStep(Step.Move, MoveThought, MoveChips, MoveChips, moveCardSprites, moveSeconds);
        else if (WeaponIsDue()) StartStep(Step.Weapon, WeaponThought, WeaponChips, WeaponChips, weaponCardSprites, weaponSeconds);
        else if (RevolverIsDue()) StartStep(Step.Revolver, RevolverThought, RevolverChips, RevolverCardChips, revolverCardSprites, revolverSeconds);
    }

    private bool WeaponIsDue()
    {
        if (Shown(Step.Weapon)) return false;
        if (_drawHappened) return true;
        bool nothingToDraw = _holster == null || _holster.State == PlayerWeaponHolster.HolsterState.Ready;
        return nothingToDraw && _controlSeconds >= weaponFallbackDelay;
    }

    private bool RevolverIsDue() => !Shown(Step.Revolver) && _switcher != null && _switcher.HasRevolver;

    private void StartStep(Step step, string thought, Chip[] chips, Chip[] cardChips, Sprite[] sprites, float seconds)
    {
        MarkShown(step); // al empezar: si muere con el paso en pantalla, no se repite
        _active = step;
        _cardMode = CanUseCards(cardChips, sprites);
        _chips = _cardMode ? cardChips : chips;
        _weaponChanged = false;
        _elapsed = 0f;
        _limit = seconds;
        _doneTime = -1f;
        _tickedCount = 0;

        if (AICompanion.Instance != null) AICompanion.Instance.ShowCustomLine(thought, false); // no entra al historial de la API
        else Debug.LogWarning("[Tutorial] No hay AICompanion: el pensamiento no se muestra.");

        if (chipRow != null) chipRow.SetActive(!_cardMode);
        if (cardRow != null) cardRow.gameObject.SetActive(_cardMode);
        if (_cardMode) SetupCards(sprites);

        for (int i = 0; i < _ticked.Length; i++)
        {
            _ticked[i] = false;
            _tickAge[i] = -1f;
            if (_cardMode) continue;
            TMP_Text text = i < chipTexts.Length ? chipTexts[i] : null;
            if (text == null) continue;
            text.text = i < chips.Length ? ChipText(chips[i], false) : "";
            text.transform.parent.gameObject.SetActive(i < chips.Length);
        }
    }

    private bool CanUseCards(Chip[] cardChips, Sprite[] sprites)
    {
        if (cardRow == null || sprites == null || sprites.Length < cardChips.Length) return false;
        if (cardChips.Length > cardRects.Length || cardChips.Length > cardImages.Length || cardChips.Length > _ticked.Length) return false;
        for (int i = 0; i < cardChips.Length; i++)
            if (sprites[i] == null || cardRects[i] == null || cardImages[i] == null) return false;
        return true;
    }

    /// <summary>Asigna arte y reparte la fila (alto fijo, ancho por aspecto). Una vez por paso, no por frame.</summary>
    private void SetupCards(Sprite[] sprites)
    {
        _cardCount = _chips.Length;
        float total = cardGap * (_cardCount - 1);
        for (int i = 0; i < _cardCount; i++)
        {
            Rect r = sprites[i].rect;
            total += cardHeight * r.width / Mathf.Max(1f, r.height);
        }
        float x = -total * 0.5f;
        for (int i = 0; i < _cardCount; i++)
        {
            Rect r = sprites[i].rect;
            float w = cardHeight * r.width / Mathf.Max(1f, r.height);
            cardImages[i].sprite = sprites[i];
            cardImages[i].color = new Color(1f, 1f, 1f, 0f);
            cardRects[i].anchorMin = cardRects[i].anchorMax = new Vector2(0.5f, 0f);
            cardRects[i].sizeDelta = new Vector2(w, cardHeight);
            cardRects[i].anchoredPosition = new Vector2(x + w * 0.5f, cardHeight * 0.5f);
            cardRects[i].localScale = Vector3.zero;
            cardRects[i].localRotation = Quaternion.identity;
            cardRects[i].gameObject.SetActive(true);
            if (i < stampRects.Length && stampRects[i] != null) stampRects[i].gameObject.SetActive(false);
            x += w + cardGap;
        }
        for (int i = _cardCount; i < cardRects.Length; i++) if (cardRects[i] != null) cardRects[i].gameObject.SetActive(false);

        RectTransform canvasRect = cardRow.parent as RectTransform;
        float available = canvasRect != null ? canvasRect.rect.width * 0.96f : total;
        _fit = total > 1f ? Mathf.Min(1f, available / total) : 1f;
        cardRow.localScale = new Vector3(_fit, _fit, 1f);
    }

    /// <summary>Entrada con rebote (0.6 → 1.08 → 1), escalonada, y balanceo suave de goma. Tiempo real.</summary>
    private void AnimateCards(float dt, bool leaving)
    {
        float scaleAll = leaving ? Mathf.Lerp(0.8f, 1f, chipsGroup != null ? chipsGroup.alpha : 0f) : 1f;
        cardRow.localScale = new Vector3(_fit * scaleAll, _fit * scaleAll, 1f);

        for (int i = 0; i < _cardCount; i++)
        {
            float age = _elapsed - i * popStagger;
            float s;
            if (age < 0f) s = 0f;
            else
            {
                float t = Mathf.Clamp01(age / Mathf.Max(0.01f, popSeconds));
                s = t < 0.7f ? Mathf.Lerp(0.6f, 1.08f, t / 0.7f) : Mathf.Lerp(1.08f, 1f, (t - 0.7f) / 0.3f);
            }
            float wobble = Mathf.Sin((_elapsed / Mathf.Max(0.1f, wobblePeriod) + i * 0.37f) * Mathf.PI * 2f) * wobbleDegrees;
            cardRects[i].localScale = new Vector3(s, s, 1f);
            cardRects[i].localRotation = Quaternion.Euler(0f, 0f, wobble);

            float tint = 0f;
            if (_tickAge[i] >= 0f)
            {
                _tickAge[i] += dt;
                tint = Mathf.Clamp01(_tickAge[i] / 0.2f);
                if (i < stampRects.Length && stampRects[i] != null)
                {
                    float p = Mathf.Clamp01(_tickAge[i] / Mathf.Max(0.01f, stampSeconds));
                    float ss = p < 0.6f ? Mathf.Lerp(1.7f, 0.92f, p / 0.6f) : Mathf.Lerp(0.92f, 1f, (p - 0.6f) / 0.4f);
                    stampRects[i].localScale = new Vector3(ss, ss, 1f);
                }
            }
            Color c = Color.Lerp(Color.white, doneTint, tint);
            c.a = Mathf.Clamp01(age / 0.1f);
            cardImages[i].color = c;
        }
    }

    private void StampCard(int i)
    {
        _tickAge[i] = 0f;
        if (i >= stampRects.Length || stampRects[i] == null) return;
        _stampTilt[i] = Random.Range(-12f, 12f);
        stampRects[i].localRotation = Quaternion.Euler(0f, 0f, _stampTilt[i]);
        stampRects[i].localScale = Vector3.one * 1.7f;
        stampRects[i].gameObject.SetActive(true);
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
            if (_cardMode) StampCard(i);
            else if (i < chipTexts.Length && chipTexts[i] != null) chipTexts[i].text = ChipText(_chips[i], true);
        }
        if (_cardMode && _active != Step.None) AnimateCards(dt, false);

        bool allDone = _tickedCount >= _chips.Length;
        if (allDone && _doneTime < 0f) _doneTime = _elapsed;

        bool over = _elapsed >= _limit || (allDone && _elapsed - _doneTime >= doneLinger) || (_elapsed >= minVisibleWhenQueued && AnotherStepWaiting());
        if (chipsGroup != null)
        {
            float target = over ? 0f : 1f;
            chipsGroup.alpha = Mathf.MoveTowards(chipsGroup.alpha, target, dt / Mathf.Max(0.01f, chipFadeSeconds));
            if (_cardMode && over) AnimateCards(0f, true); // sale encogiéndose mientras se desvanece
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
            case ChipAction.SwitchWeapon:
                return _weaponChanged || (_switcher == null && (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Alpha2)
                    || Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.Keypad2) || Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f));
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
