using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Hace que un sprite 2D siempre mire hacia la cámara del jugador.
/// Este es el efecto visual clave del juego: personajes dibujados en 2D que "viven" en un mundo 3D.
///
/// DÓNDE VA: GameObject del sprite del enemigo (el que tiene SpriteRenderer).
/// El enemigo tiene esta jerarquía (la crea "Crear Prefabs de Prueba"):
/// - EnemyRoot (con EnemyAI, NavMeshAgent, EnemyHealth)
///   └── Visual
///       ├── Body / Head (placeholder de cápsula)
///       └── SpriteHolder (con SpriteRenderer y BillboardSprite)
///
/// SPRITES:
/// - spriteSet (CharacterSpriteSet): animaciones idle/walk/shoot/hit/surrender/death/talk.
///   Si una animación falta se usa idle (o walk). Si el set no tiene ningún frame, el sprite se
///   oculta y el placeholder (placeholderRenderers) sigue visible.
/// - accessorySet (opcional): se dibuja encima con el mismo frame (ej: pañuelo rojo del informante).
/// - Poses sueltas (idle/walk/shoot/hit/surrender/talk/alert/death): modo antiguo, solo si no hay set con frames.
///
/// POSES (las pide EnemyAI / DialogueNPC): idle = quieto · walk = caminando o persiguiendo ·
/// shoot = golpe cuerpo a cuerpo (PlayAttack) · hit = un instante al recibir daño · surrender = manos arriba ·
/// death = muerto (fijo, sigue mirando a la cámara) · talk = durante el interrogatorio.
///
/// NUNCA SE VOLTEA por defecto (flipWithMovement = false): sombra/luz pintadas en el dibujo (Edgar, dia3).
/// PIES: el importador (CharacterSpriteImporter) pone el pivote en los pies. anchorFeet solo hace falta si el
/// SpriteRenderer está en un hijo y el PNG se importó fuera del pipeline (pivote al centro).
///
/// TIPOS DE BILLBOARD:
/// - Full: el sprite rota en todos los ejes para mirar a la cámara
/// - YAxis: solo rota en Y (el sprite "voltea" pero no se inclina) — más natural para personajes
///
/// LOOK (fase 3A2): el sprite y el accesorio usan el shader sin iluminación (CharacterLook), el
/// placeholder se ve plano con contorno negro, y la raíz recibe una BlobShadow.
/// </summary>
public class BillboardSprite : MonoBehaviour
{
    public enum BillboardType
    {
        [Tooltip("Rota en todos los ejes hacia la cámara")]
        Full,
        [Tooltip("Solo rota en el eje Y — recomendado para personajes")]
        YAxisOnly
    }

    [Header("Configuración")]
    [Tooltip("Tipo de rotación del billboard")]
    [SerializeField] private BillboardType billboardType = BillboardType.YAxisOnly;

    [Tooltip("¿Voltear el sprite horizontalmente según el movimiento? (apagado: la sombra está pintada en el dibujo)")]
    [SerializeField] private bool flipWithMovement = false;

    [Tooltip("Mover el SpriteRenderer HIJO para que el borde inferior del dibujo quede en el origen (solo PNG sin pivote en los pies)")]
    [SerializeField] private bool anchorFeet = false;

    [Tooltip("Segundos que se ve la pose hit tras recibir un golpe")]
    [SerializeField, Min(0f)] private float hitPoseDuration = 0.25f;

    [Tooltip("Personaje sin iluminación + contorno en el placeholder + sombra blob (regla de legibilidad)")]
    [SerializeField] private bool unlitLook = true;

    [Header("Set de sprites (pipeline de arte)")]
    [Tooltip("Animaciones del personaje. Vacío = se ve el placeholder")]
    [SerializeField] private CharacterSpriteSet spriteSet;

    [Tooltip("Accesorio dibujado encima con el mismo frame (opcional)")]
    [SerializeField] private CharacterSpriteSet accessorySet;

    [Tooltip("Renderers del placeholder (cápsula): se ocultan cuando hay sprites")]
    [SerializeField] private Renderer[] placeholderRenderers;

    [Header("Animación simple antigua (sin set): <anim>_<NN>.png sueltos")]
    [Tooltip("idle_01...: quieto (si falta, se usa walk)")]
    [SerializeField] private Sprite[] idleSprites;

    [Tooltip("Sprites para el ciclo de caminado (izquierda, centro, derecha, centro...)")]
    [SerializeField] private Sprite[] walkSprites;

    [Tooltip("shoot_01...: golpe cuerpo a cuerpo (si falta, se usa alert)")]
    [SerializeField] private Sprite[] shootSprites;

    [Tooltip("hit_01...: se muestra un instante al recibir daño")]
    [SerializeField] private Sprite[] hitSprites;

    [Tooltip("surrender_01...: manos arriba (si falta, se usa walk)")]
    [SerializeField] private Sprite[] surrenderSprites;

    [Tooltip("talk_01...: hablando durante el interrogatorio")]
    [SerializeField] private Sprite[] talkSprites;

    [Tooltip("Sprites para el estado de alerta")]
    [SerializeField] private Sprite[] alertSprites;

    [Tooltip("Sprite de muerte")]
    [SerializeField] private Sprite deathSprite;

    [Tooltip("Velocidad de animación (cambios de sprite por segundo)")]
    [SerializeField] private float animationSpeed = 8f;

    private Camera _mainCamera;
    private SpriteRenderer _spriteRenderer;
    private SpriteRenderer _accessoryRenderer;
    private float _animTimer;
    private int _currentFrame;
    private Sprite[] _currentAnimation;

    // Modo set
    private bool _useSet;
    private string _setAnim;
    private CharacterSpriteSet.AnimClip _setClip;
    private bool _setLoop = true;
    private string _resumeAnim;     // animación a la que volver tras "hit"
    private float _hitUntil = -1f;
    private Sprite[] _legacyResume;  // modo antiguo: pose a la que volver tras "hit"
    private bool _isDead;            // muerto: dibujo fijo, no se aceptan más poses
    private Sprite _anchoredSprite;  // último sprite para el que se recolocaron los pies (anchorFeet)

    /// <summary>¿Se están mostrando sprites (set o modo antiguo)? Si no, se ve el placeholder.</summary>
    public bool HasSprites => _useSet || HasFrames(walkSprites) || HasFrames(idleSprites);

    private void Awake()
    {
        _mainCamera = Camera.main;
        // En este objeto (prefab generado) o en un hijo "Art" (jerarquía de Edgar)
        if (!TryGetComponent(out _spriteRenderer)) _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_spriteRenderer != null && !flipWithMovement) _spriteRenderer.flipX = false;
        if (unlitLook) ApplyUnlitLook();

        if (HasFrames(idleSprites)) SwitchLegacy(idleSprites);
        else if (HasFrames(walkSprites)) _currentAnimation = walkSprites;

        RefreshMode();
    }

    private static bool HasFrames(Sprite[] animation)
    {
        return animation != null && animation.Length > 0;
    }

    // ============ Pies al suelo (fase 3A4-fix) ============
    // El NavMesh queda unos centímetros por encima del piso real (voxelización), así que el agente flota sobre su
    // sombra. Se mide el piso con un rayo y se compensa con NavMeshAgent.baseOffset.
    //  - Mide al aparecer y luego cada 0.5 s; el cambio se aplica suavizado (0.4 m/s), nunca de golpe ni cada frame.
    //  - La corrección total se limita a ±0.3 m respecto al baseOffset original. Si no hay piso, conserva el último valor.
    //  - No actúa con el agente apagado, fuera del NavMesh ni durante la muerte.
    private const float GroundProbeStart = 0.6f;
    private const float GroundProbeLength = 1.6f;
    private const float MaxCorrection = 0.3f;
    private const float MeasureInterval = 0.5f;
    private const float SmoothSpeed = 0.4f;
    private const float FeetTolerance = 0.02f;

    // Máscara del piso: todas las capas menos "Ignore Raycast" (2) y "UI" (5). Los personajes comparten la capa Default
    // con el escenario, así que además se descartan por componente (EnemyHealth, PlayerController, CharacterController),
    // los colliders propios, los triggers (QueryTriggerInteraction.Ignore) y las superficies no horizontales.
    // BlobShadow, LightCone, cielo y fondos son mallas sin collider: un raycast nunca las toca.
    private static readonly int GroundMask = ~((1 << 2) | (1 << 5));
    private static readonly RaycastHit[] GroundHits = new RaycastHit[8];

    private NavMeshAgent _agent;
    private EnemyHealth _owner;
    private EnemyAI _ai;
    private Transform _root;
    private float _originalBaseOffset;
    private float _targetBaseOffset;
    private float _nextMeasure;

    private void Start()
    {
        if (!unlitLook) return;
        _owner = GetComponentInParent<EnemyHealth>();
        _ai = GetComponentInParent<EnemyAI>();
        _root = _owner != null ? _owner.transform : (transform.parent != null ? transform.parent : transform);
        if (!_root.TryGetComponent(out _agent)) return;

        _originalBaseOffset = _agent.baseOffset;
        _targetBaseOffset = _agent.baseOffset;
        if (CanAlign()) MeasureGround(true);
        _nextMeasure = Time.time + MeasureInterval;
    }

    private bool CanAlign()
    {
        return _agent != null && _agent.enabled && _agent.isOnNavMesh
               && (_owner == null || !_owner.IsDead)
               && (_ai == null || _ai.CurrentState != EnemyAI.EnemyState.Dead);
    }

    private void UpdateGroundOffset()
    {
        if (_agent == null || !CanAlign()) return;

        if (Time.time >= _nextMeasure)
        {
            _nextMeasure = Time.time + MeasureInterval;
            MeasureGround(false);
        }
        if (!Mathf.Approximately(_agent.baseOffset, _targetBaseOffset))
            _agent.baseOffset = Mathf.MoveTowards(_agent.baseOffset, _targetBaseOffset, SmoothSpeed * Time.deltaTime);
    }

    private void MeasureGround(bool immediate)
    {
        int count = Physics.RaycastNonAlloc(_root.position + Vector3.up * GroundProbeStart, Vector3.down, GroundHits,
            GroundProbeLength, GroundMask, QueryTriggerInteraction.Ignore);

        float groundY = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = GroundHits[i];
            Collider c = hit.collider;
            if (c == null || c.transform.IsChildOf(_root)) continue;
            if (hit.normal.y < 0.7f) continue;                                           // solo superficies de piso
            if (c.GetComponentInParent<EnemyHealth>() != null || c.GetComponentInParent<PlayerController>() != null
                || c.GetComponent<CharacterController>() != null) continue;               // otros personajes
            if (hit.point.y > groundY) groundY = hit.point.y;
        }
        if (float.IsNegativeInfinity(groundY)) return;                                    // sin piso: se conserva el valor

        float feetHeight = _root.position.y - groundY;                                    // > 0 = flotando
        if (Mathf.Abs(feetHeight) >= FeetTolerance)
            _targetBaseOffset = Mathf.Clamp(_agent.baseOffset - feetHeight,
                _originalBaseOffset - MaxCorrection, _originalBaseOffset + MaxCorrection);
        else
            _targetBaseOffset = _agent.baseOffset;

        if (immediate) _agent.baseOffset = _targetBaseOffset;
    }

    private void ApplyUnlitLook()
    {
        CharacterLook.ApplySprite(_spriteRenderer);
        if (placeholderRenderers != null)
            foreach (Renderer r in placeholderRenderers)
                CharacterLook.ApplyPlaceholder(r);

        // La sombra va en la raíz del personaje (no en el sprite, que gira)
        EnemyHealth owner = GetComponentInParent<EnemyHealth>();
        Transform root = owner != null ? owner.transform : (transform.parent != null ? transform.parent : transform);
        if (root.GetComponent<BlobShadow>() == null) root.gameObject.AddComponent<BlobShadow>();
    }

    /// <summary>Cambia el set en runtime (ej: informantes de la fase 3B).</summary>
    public void SetSpriteSets(CharacterSpriteSet set, CharacterSpriteSet accessory)
    {
        spriteSet = set;
        accessorySet = accessory;
        RefreshMode();
    }

    private void RefreshMode()
    {
        _useSet = spriteSet != null && spriteSet.HasAnyFrames;

        if (_useSet && accessorySet != null && accessorySet.HasAnyFrames && _accessoryRenderer == null && _spriteRenderer != null)
        {
            var go = new GameObject("Accessory");
            go.transform.SetParent(_spriteRenderer.transform, false);
            _accessoryRenderer = go.AddComponent<SpriteRenderer>();
            _accessoryRenderer.sortingLayerID = _spriteRenderer.sortingLayerID;
            _accessoryRenderer.sortingOrder = _spriteRenderer.sortingOrder + 1;
            if (unlitLook) CharacterLook.ApplySprite(_accessoryRenderer);
            // Un pelín delante del sprite: con alpha clip ambos escriben profundidad
            go.transform.localPosition = new Vector3(0f, 0f, -0.005f);
        }
        if (_accessoryRenderer != null) _accessoryRenderer.enabled = false;

        bool showSprite = HasSprites;
        if (_spriteRenderer != null) _spriteRenderer.enabled = showSprite;
        if (placeholderRenderers != null)
            foreach (Renderer r in placeholderRenderers)
                if (r != null) r.enabled = !showSprite;

        if (_useSet) SetAnim("idle", true);
    }

    private void LateUpdate()
    {
        // LateUpdate se ejecuta después de Update: perfecto para billboards
        // porque así los enemigos ya movieron antes de que actualizemos la rotación

        UpdateGroundOffset();
        RotateTowardsCamera();
        if (_useSet) UpdateSetAnimation();
        else UpdateAnimation();
        UpdateFeetAnchor();
    }

    // ============ Pies en el origen del SpriteHolder (Edgar, dia3) ============
    // Solo con anchorFeet y el SpriteRenderer en un hijo: mueve el hijo para que el centro del borde inferior del
    // dibujo quede en el origen. Con el renderer en este objeto no hace nada (el importador ya puso el pivote en los pies).
    private void UpdateFeetAnchor()
    {
        if (!anchorFeet || _spriteRenderer == null || _spriteRenderer.transform == transform) return;
        Sprite sprite = _spriteRenderer.sprite;
        if (sprite == null || sprite == _anchoredSprite) return;
        _anchoredSprite = sprite;

        float ppu = sprite.pixelsPerUnit;
        float offsetX = (sprite.pivot.x - sprite.rect.width * 0.5f) / ppu;
        float offsetY = sprite.pivot.y / ppu;
        Transform art = _spriteRenderer.transform;
        Vector3 scale = art.localScale;
        art.localPosition = new Vector3(offsetX * scale.x, offsetY * scale.y, art.localPosition.z);
    }

    private void RotateTowardsCamera()
    {
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null) return;

        Vector3 directionToCamera = _mainCamera.transform.position - transform.position;

        if (billboardType == BillboardType.Full)
        {
            // Mirar directamente a la cámara en todos los ejes
            transform.rotation = Quaternion.LookRotation(-directionToCamera);
        }
        else // YAxisOnly
        {
            // Solo rotar en el eje Y, manteniendo el sprite vertical
            directionToCamera.y = 0;
            if (directionToCamera != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(-directionToCamera);
        }
    }

    /// <summary>Modo antiguo: cambia de pose y muestra su primer dibujo ya. Pose sin dibujos = se queda la actual.</summary>
    private void SwitchLegacy(Sprite[] anim)
    {
        if (!HasFrames(anim) || anim == _currentAnimation) return;
        _currentAnimation = anim;
        _currentFrame = 0;
        _animTimer = 0;
        if (_spriteRenderer != null && anim[0] != null) _spriteRenderer.sprite = anim[0];
    }

    /// <summary>Modo antiguo: pide una pose; si hay un "hit" en curso, se aplica cuando termina.</summary>
    private void RequestLegacy(Sprite[] anim)
    {
        if (_isDead || !HasFrames(anim)) return;
        if (_hitUntil > 0f) { _legacyResume = anim; return; }
        SwitchLegacy(anim);
    }

    private void UpdateAnimation()
    {
        if (_isDead) return;
        if (_hitUntil > 0f && Time.time >= _hitUntil)
        {
            _hitUntil = -1f;
            SwitchLegacy(_legacyResume);
        }
        if (_currentAnimation == null || _currentAnimation.Length == 0 || _spriteRenderer == null) return;

        _animTimer += Time.deltaTime * animationSpeed;
        if (_animTimer >= 1f)
        {
            _animTimer = 0;
            _currentFrame = (_currentFrame + 1) % _currentAnimation.Length;
            _spriteRenderer.sprite = _currentAnimation[_currentFrame];
        }
    }

    // ============ Modo set ============

    private void UpdateSetAnimation()
    {
        if (_hitUntil > 0f && Time.time >= _hitUntil)
        {
            _hitUntil = -1f;
            SetAnim(_resumeAnim ?? "idle", true);
        }

        if (_setClip == null) return;

        _animTimer += Time.deltaTime * Mathf.Max(0.1f, _setClip.fps);
        if (_animTimer >= 1f)
        {
            _animTimer -= 1f;
            int next = _currentFrame + 1;
            if (next >= _setClip.frames.Length) next = _setLoop ? 0 : _setClip.frames.Length - 1;
            if (next != _currentFrame)
            {
                _currentFrame = next;
                ApplySetFrame();
            }
        }
    }

    /// <summary>Busca la animación pedida; si falta, idle; si falta, walk; si no, la primera que haya.</summary>
    private CharacterSpriteSet.AnimClip ResolveClip(string anim)
    {
        if (spriteSet.TryGet(anim, out var clip)) return clip;
        if (spriteSet.TryGet("idle", out clip)) return clip;
        if (spriteSet.TryGet("walk", out clip)) return clip;
        foreach (var c in spriteSet.clips)
            if (c != null && c.frames != null && c.frames.Length > 0) return c;
        return null;
    }

    private void SetAnim(string anim, bool loop)
    {
        if (!_useSet) return;
        if (_setAnim == anim && _setClip != null) return;

        _setAnim = anim;
        _setLoop = loop;
        _setClip = ResolveClip(anim);
        _currentFrame = 0;
        _animTimer = 0f;
        ApplySetFrame();
    }

    private void ApplySetFrame()
    {
        if (_setClip == null || _spriteRenderer == null) return;
        _spriteRenderer.sprite = _setClip.frames[_currentFrame];

        if (_accessoryRenderer != null)
        {
            CharacterSpriteSet.AnimClip acc = null;
            bool hasAccessory = accessorySet != null && accessorySet.TryGet(_setClip.name, out acc);
            _accessoryRenderer.enabled = hasAccessory;
            if (hasAccessory)
            {
                _accessoryRenderer.sprite = acc.frames[_currentFrame % acc.frames.Length];
                _accessoryRenderer.flipX = _spriteRenderer.flipX;
            }
        }
    }

    /// <summary>Reproduce cualquier animación del set por nombre (sin efecto en modo antiguo).</summary>
    public void Play(string anim, bool loop = true)
    {
        if (_isDead) return;
        if (_hitUntil > 0f)
        {
            _resumeAnim = anim; // termina el golpe y luego sigue con esta
            return;
        }
        SetAnim(anim, loop);
    }

    // ============ Métodos para cambiar animaciones desde EnemyAI / DialogueNPC / EnemyCombatFx ============

    /// <summary>Animación de reposo (en modo antiguo = idle suelto o, si falta, caminado)</summary>
    public void PlayIdle()
    {
        if (_useSet) Play("idle");
        else if (HasFrames(idleSprites)) RequestLegacy(idleSprites);
        else PlayWalk();
    }

    /// <summary>Cambiar a animación de caminado</summary>
    public void PlayWalk()
    {
        if (_useSet)
        {
            Play("walk");
            return;
        }
        RequestLegacy(walkSprites);
    }

    /// <summary>Cambiar a animación de alerta (con set: "shoot")</summary>
    public void PlayAlert()
    {
        if (_useSet)
        {
            Play("shoot");
            return;
        }
        RequestLegacy(alertSprites);
    }

    /// <summary>Pose shoot: golpe cuerpo a cuerpo (Edgar). En modo antiguo, si falta shoot se usa alert.</summary>
    public void PlayAttack()
    {
        if (_useSet)
        {
            Play("shoot");
            return;
        }
        RequestLegacy(HasFrames(shootSprites) ? shootSprites : alertSprites);
    }

    /// <summary>Frame de golpe durante unos instantes y vuelve a la animación anterior.
    /// duration &lt; 0 = hitPoseDuration del Inspector.</summary>
    public void PlayHit(float duration = -1f)
    {
        if (_isDead) return;
        if (duration < 0f) duration = hitPoseDuration;

        if (!_useSet)
        {
            if (!HasFrames(hitSprites)) return;
            if (_hitUntil < 0f) _legacyResume = _currentAnimation;
            _currentAnimation = null;
            SwitchLegacy(hitSprites);
            _hitUntil = Time.time + duration;
            return;
        }

        if (!spriteSet.TryGet("hit", out _)) return;
        if (_hitUntil < 0f) _resumeAnim = _setAnim;
        _setAnim = null;
        SetAnim("hit", false);
        _hitUntil = Time.time + duration;
    }

    /// <summary>Manos arriba (en modo antiguo = surrender suelto o, si falta, caminado)</summary>
    public void PlaySurrender()
    {
        if (_isDead) return;
        if (_useSet)
        {
            _hitUntil = -1f;
            SetAnim("surrender", true);
        }
        else if (HasFrames(surrenderSprites))
        {
            _hitUntil = -1f;
            SwitchLegacy(surrenderSprites);
        }
        else PlayWalk();
    }

    /// <summary>Animación de hablar (interrogatorios)</summary>
    public void PlayTalk()
    {
        if (_useSet) Play("talk");
        else RequestLegacy(talkSprites);
    }

    /// <summary>Mostrar la muerte: con set se reproduce una vez y se queda en el último frame.
    /// El billboard sigue mirando a la cámara (el cuerpo no se ve "de canto").</summary>
    public void PlayDeath()
    {
        _hitUntil = -1f;
        if (_useSet)
        {
            SetAnim("death", false);
            _isDead = true;
            return;
        }

        _currentAnimation = null;
        if (deathSprite != null && _spriteRenderer != null)
            _spriteRenderer.sprite = deathSprite;
        _isDead = true;
    }

    /// <summary>Voltear el sprite según la dirección de movimiento (solo si flipWithMovement está activo)</summary>
    public void SetFacingDirection(Vector3 movementDirection)
    {
        if (!flipWithMovement || _mainCamera == null || _spriteRenderer == null) return;

        // Calcular si el enemigo se mueve hacia la izquierda o derecha RELATIVO a la cámara
        Vector3 camRight = _mainCamera.transform.right;
        float dot = Vector3.Dot(movementDirection.normalized, camRight);

        _spriteRenderer.flipX = dot < 0;
        if (_accessoryRenderer != null) _accessoryRenderer.flipX = _spriteRenderer.flipX;
    }
}
