using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Pista que se examina en el nivel (PISTAS.1). Sin UIManager (no existe en ninguna escena).
/// DÓNDE VA: objeto de la pista. Hoy los ponen LevelBuilder_Campamento (Pista_Auto_* en sus anclas) y ClueRuntime (en los
/// marcadores Clue_* de LevelBuilder_NivelPorky). Los textos salen de ClueCatalog (guion de pistas); los campos de abajo
/// solo valen si el id no está en el catálogo (no se renombran: LevelBuilder_Campamento los escribe).
///
/// - P1: "[E] Examinar" bajo la mira (RubberHoseCrosshair.SetPrompt), mismas reglas que los objetos: ≤ 2.2 m en horizontal,
///   ≤ 2.2 m de alto, con línea de vista (no a través de paredes) y nunca en pausa/diálogo/interrogatorio/cinemática.
/// - P2/P3: E → sonido, cuenta en GameStateTracker, se abre la hoja (ClueCard); al cerrarla, aviso "Pista: título" y a los
///   2 s el pensamiento de la IA.
/// - P5: dibujo de ClueArt (billboard que solo gira en Y, ≈ 0.3 m, apoyado) o, si no hay, la hoja dibujada de siempre.
///   Brillo como los objetos (luz cálida + crece un poco cada 2–3 s), sin vaivén.
/// - P6: si GameStateTracker ya la tiene (reinicio, snapshot), no aparece.
/// Award(): la misma entrega para las pistas que se ganan hablando (Centavo): sonido, cuenta y aviso, sin hoja.
/// </summary>
public class CluePickup : MonoBehaviour
{
    private const string InteractPrompt = "[E] Examinar";
    private const float InteractDistance = 2.2f;
    private const float InteractHeight = 2.2f;
    private const float WorldSize = 0.3f;
    private const float AiDelay = 2f;

    [Header("Datos de la pista (si el id no está en ClueCatalog)")]
    [Tooltip("Identificador único de esta pista (usar nombres sin espacios)")]
    [SerializeField] private string clueId = "pista_sin_nombre";
    [Tooltip("Título de la hoja y del aviso")]
    [SerializeField] private string displayName = "Pista";
    [Tooltip("Resumen para el tracker y la IA")]
    [SerializeField] private string shortDescription = "Algo sospechoso...";
    [Tooltip("Texto de la hoja")]
    [TextArea(3, 8)]
    [SerializeField] private string fullText = "";

    [Header("Efectos")]
    [Tooltip("Efecto opcional que se apaga al examinarla")]
    [SerializeField] private GameObject highlightEffect;
    [SerializeField] private Color lightColor = new Color(1f, 0.85f, 0.55f);
    [SerializeField] private float lightRange = 1.4f;
    [SerializeField] private float lightBase = 0.2f;
    [SerializeField] private float lightPeak = 1.3f;
    [SerializeField] private float lightMaxDistance = 20f;
    [SerializeField] private Vector2 glintEvery = new Vector2(2f, 3f);
    [SerializeField] private float glintDuration = 0.3f;
    [SerializeField] private float glintScale = 0.08f;

    [Header("Eventos")]
    public UnityEvent onClueCollected;

    private static CluePickup _focused;
    private static float _focusedDistance;

    private bool _snapToSurface;
    private bool _collected;
    private Transform _player;
    private Camera _camera;
    private Transform _visual;
    private Vector3 _visualScale = Vector3.one;
    private bool _billboard;
    private Light _light;
    private float _glintStart = -1f;
    private float _nextGlint;

    public string ClueId => clueId;

    /// <summary>Para quien la crea por código (ClueRuntime): id y si se apoya en la superficie de abajo.</summary>
    public void Configure(string id, bool snapToSurface)
    {
        clueId = id;
        _snapToSurface = snapToSurface;
    }

    private void Start()
    {
        // P6: ya recogida antes (reinicio o snapshot) → no aparece
        if (GameStateTracker.Instance != null && GameStateTracker.Instance.HasClue(clueId))
        {
            gameObject.SetActive(false);
            return;
        }
        if (_snapToSurface && Pickup.TryFindSurface(transform.position, 0.25f, 2f, out RaycastHit surface))
            transform.position = surface.point;

        // P5: sin vaivén (PickupPresentation lo tenía; si un objeto viejo la trae, se apaga)
        if (TryGetComponent(out PickupPresentation presentation)) presentation.enabled = false;
        BuildVisual();
        BuildLight();
        if (highlightEffect != null) highlightEffect.SetActive(true);
        _nextGlint = Time.time + Random.Range(glintEvery.x, glintEvery.y);
    }

    private void OnDisable()
    {
        ReleaseFocus();
    }

    private void Update()
    {
        if (_collected) return;
        UpdateGlint();
        if (_player == null)
        {
            GameObject go = GameObject.FindWithTag("Player");
            if (go == null) return;
            _player = go.transform;
        }

        Vector3 delta = transform.position - _player.position;
        float dy = Mathf.Abs(delta.y);
        delta.y = 0f;
        float horizontal = delta.magnitude;
        if (horizontal > InteractDistance || dy > InteractHeight || PlayerAim.GameplayBlocked() || ClueCard.IsOpen
            || !HasLineOfSight())
        {
            ReleaseFocus();
            return;
        }

        if (_focused != null && _focused != this && horizontal >= _focusedDistance) return; // otra pista está más cerca
        _focused = this;
        _focusedDistance = horizontal;
        if (RubberHoseCrosshair.Current != null) RubberHoseCrosshair.Current.SetPrompt(InteractPrompt);
        if (Input.GetKeyDown(KeyCode.E)) CollectClue();
    }

    private void LateUpdate()
    {
        if (_collected || _visual == null) return;
        if (_billboard)
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera != null)
            {
                Vector3 look = _visual.position - _camera.transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.0001f) _visual.rotation = Quaternion.LookRotation(look);
            }
        }
        _visual.localScale = _visualScale * (1f + glintScale * GlintAmount());
    }

    private void CollectClue()
    {
        if (_collected) return;
        _collected = true;
        ReleaseFocus();

        Resolve(clueId, displayName, shortDescription, fullText, out string title, out string summary, out string body);
        Register(clueId, summary);
        if (onClueCollected != null) onClueCollected.Invoke();
        HideInWorld();

        // P2: la hoja; P3: al cerrarla, aviso y luego la IA. Sin hoja posible: aviso y IA igual
        if (!ClueCard.Show(title, body, ClueArt.CardImage(clueId), () => AfterReading(title, summary)))
            AfterReading(title, summary);
    }

    private void AfterReading(string title, string summary)
    {
        ShowClueToast(clueId, title);
        if (isActiveAndEnabled) StartCoroutine(NotifyAIWithDelay(AiDelay, title, summary));
    }

    /// <summary>Apaga dibujo, luz, efecto y collider; el objeto sigue activo para la espera de la IA.</summary>
    private void HideInWorld()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        if (_light != null) _light.enabled = false;
        if (highlightEffect != null) highlightEffect.SetActive(false);
        if (TryGetComponent(out Collider col)) col.enabled = false;
    }

    // ==================================================================
    // Entrega (también para las pistas que se ganan hablando)
    // ==================================================================

    /// <summary>
    /// Entrega una pista SIN hoja (P3): sonido de papel, GameStateTracker (cuenta para los finales), aviso "Pista: título" y,
    /// si aiComment, el pensamiento de la IA a los 2 s. La usa CentavoInterrogation (registro_01, aiComment = false por D11).
    /// fullText y displayDuration se conservan por compatibilidad (la hoja solo se abre al examinar una pista física).
    /// </summary>
    public static void Award(string clueId, string displayName, string shortDescription, string fullText,
        float displayDuration, bool aiComment, MonoBehaviour runner)
    {
        Register(clueId, shortDescription);
        string title = ShowClueToast(clueId, displayName);
        if (aiComment && runner != null && runner.isActiveAndEnabled)
            runner.StartCoroutine(NotifyAIWithDelay(AiDelay, title, shortDescription));
    }

    /// <summary>Aviso del HUD "Pista: título" (título del catálogo; si no está, fallbackTitle). Devuelve el título usado.</summary>
    public static string ShowClueToast(string clueId, string fallbackTitle)
    {
        string title = ClueCatalog.TryGet(clueId, out ClueInfo info) && !string.IsNullOrEmpty(info.Title) ? info.Title : fallbackTitle;
        if (!string.IsNullOrEmpty(title)) HudWilbur.ShowToast("Pista: " + title);
        return title;
    }

    private static void Register(string clueId, string summary)
    {
        AudioLibrary.PlaySfx2D("pista_papel");
        if (GameStateTracker.Instance != null) GameStateTracker.Instance.RegisterClueCollected(clueId, summary);
    }

    /// <summary>Textos de la pista: los del catálogo y, para lo que falte, los del objeto.</summary>
    private static void Resolve(string clueId, string fallbackTitle, string fallbackSummary, string fallbackBody,
        out string title, out string summary, out string body)
    {
        title = fallbackTitle;
        summary = fallbackSummary;
        body = fallbackBody;
        if (!ClueCatalog.TryGet(clueId, out ClueInfo info)) return;
        if (!string.IsNullOrEmpty(info.Title)) title = info.Title;
        if (!string.IsNullOrEmpty(info.ShortDescription)) summary = info.ShortDescription;
        if (!string.IsNullOrEmpty(info.Body)) body = info.Body;
    }

    private static IEnumerator NotifyAIWithDelay(float delay, string title, string summary)
    {
        yield return new WaitForSeconds(delay);
        if (AICompanion.Instance != null) AICompanion.Instance.Speak(AITrigger.ClueFound, $"{title}: {summary}");
    }

    // ==================================================================
    // Aspecto (P5)
    // ==================================================================

    private void BuildVisual()
    {
        Sprite sprite = ClueArt.WorldSprite(clueId);
        Renderer[] existing = GetComponentsInChildren<Renderer>(true);
        if (sprite != null)
        {
            foreach (Renderer r in existing) r.enabled = false; // la hoja dibujada queda de respaldo, apagada
            var go = new GameObject("Dibujo");
            go.transform.SetParent(transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            CharacterLook.ApplySprite(sr);
            Vector3 size = sprite.bounds.size;
            float side = Mathf.Max(size.x, size.y);
            go.transform.localScale = Vector3.one * (side > 0.0001f ? WorldSize / side : 1f);
            _visual = go.transform;
            _billboard = true;
        }
        else if (existing.Length == 0)
        {
            // Sin dibujo ni hoja: la hoja dibujada de siempre (papel plano con contorno)
            GameObject paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
            paper.name = "Papel";
            Collider c = paper.GetComponent<Collider>();
            if (c != null) Destroy(c);
            paper.transform.SetParent(transform, false);
            paper.transform.localPosition = new Vector3(0f, 0.006f, 0f);
            paper.transform.localScale = new Vector3(0.24f, 0.01f, 0.32f);
            CharacterLook.ApplyPlaceholder(paper.GetComponent<Renderer>());
            _visual = paper.transform;
        }
        else
        {
            foreach (Renderer r in existing) CharacterLook.ApplyPlaceholder(r);
            _visual = existing[0].transform != transform ? existing[0].transform : null;
        }
        if (_visual != null) _visualScale = _visual.localScale;
    }

    private void BuildLight()
    {
        var lightGo = new GameObject("Brillo");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = Vector3.up * 0.25f;
        _light = lightGo.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = lightColor;
        _light.range = lightRange;
        _light.intensity = lightBase;
        _light.shadows = LightShadows.None;
    }

    private void UpdateGlint()
    {
        if (Time.time >= _nextGlint)
        {
            _glintStart = Time.time;
            _nextGlint = Time.time + Random.Range(glintEvery.x, glintEvery.y);
        }
        if (_light == null) return;
        bool near = _player != null && (transform.position - _player.position).sqrMagnitude <= lightMaxDistance * lightMaxDistance;
        if (_light.enabled != near) _light.enabled = near;
        if (near) _light.intensity = Mathf.Lerp(lightBase, lightPeak, GlintAmount());
    }

    private float GlintAmount()
    {
        if (_glintStart < 0f) return 0f;
        float t = (Time.time - _glintStart) / Mathf.Max(0.01f, glintDuration);
        return t >= 1f ? 0f : Mathf.Sin(t * Mathf.PI);
    }

    // ==================================================================
    // Aviso [E] (P1)
    // ==================================================================

    /// <summary>¿Nada entre el ojo y la pista? Su propio objeto, guardias y jugador no tapan.</summary>
    private bool HasLineOfSight()
    {
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return true;
        Vector3 target = transform.position + Vector3.up * 0.12f;
        if (!Physics.Linecast(_camera.transform.position, target, out RaycastHit hit,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return true;
        return hit.transform.IsChildOf(transform) || Pickup.IsActor(hit.collider);
    }

    private void ReleaseFocus()
    {
        if (_focused != this) return;
        _focused = null;
        if (RubberHoseCrosshair.Current != null) RubberHoseCrosshair.Current.SetPrompt(null);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _focused = null;
        _focusedDistance = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, InteractDistance);
    }
}
