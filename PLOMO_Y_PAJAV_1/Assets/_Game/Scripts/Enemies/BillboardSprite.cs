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
/// - walkSprites/alertSprites/deathSprite: modo antiguo, solo si no hay set con frames.
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

    [Tooltip("¿Voltear el sprite horizontalmente según el movimiento?")]
    [SerializeField] private bool flipWithMovement = true;

    [Tooltip("Personaje sin iluminación + contorno en el placeholder + sombra blob (regla de legibilidad)")]
    [SerializeField] private bool unlitLook = true;

    [Header("Set de sprites (pipeline de arte)")]
    [Tooltip("Animaciones del personaje. Vacío = se ve el placeholder")]
    [SerializeField] private CharacterSpriteSet spriteSet;

    [Tooltip("Accesorio dibujado encima con el mismo frame (opcional)")]
    [SerializeField] private CharacterSpriteSet accessorySet;

    [Tooltip("Renderers del placeholder (cápsula): se ocultan cuando hay sprites")]
    [SerializeField] private Renderer[] placeholderRenderers;

    [Header("Animación simple antigua (sin set)")]
    [Tooltip("Sprites para el ciclo de caminado (izquierda, centro, derecha, centro...)")]
    [SerializeField] private Sprite[] walkSprites;

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

    /// <summary>¿Se están mostrando sprites (set o modo antiguo)? Si no, se ve el placeholder.</summary>
    public bool HasSprites => _useSet || (walkSprites != null && walkSprites.Length > 0);

    private void Awake()
    {
        _mainCamera = Camera.main;
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (unlitLook) ApplyUnlitLook();

        if (walkSprites != null && walkSprites.Length > 0)
            _currentAnimation = walkSprites;

        RefreshMode();
    }

    private void Start()
    {
        if (unlitLook) AlignFeetToGround();
    }

    /// <summary>
    /// El NavMesh queda unos centímetros por encima del piso real (voxelización), así que el agente flota sobre su
    /// sombra. Se mide con un rayo hacia abajo y se compensa con baseOffset (o bajando el visual si no hay agente).
    /// Tolerancia 2 cm; ignora los colliders del propio personaje.
    /// </summary>
    private void AlignFeetToGround()
    {
        EnemyHealth owner = GetComponentInParent<EnemyHealth>();
        Transform root = owner != null ? owner.transform : (transform.parent != null ? transform.parent : transform);
        RaycastHit[] hits = Physics.RaycastAll(root.position + Vector3.up * 0.6f, Vector3.down, 1.6f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        float groundY = float.NegativeInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(root)) continue;
            if (hit.point.y > groundY) groundY = hit.point.y;
        }
        if (float.IsNegativeInfinity(groundY)) return;

        float gap = root.position.y - groundY;
        if (gap < 0.02f || gap > 0.5f) return;

        if (root.TryGetComponent(out NavMeshAgent agent) && agent.enabled)
            agent.baseOffset -= gap;
        else
            (transform.parent != null ? transform.parent : transform).localPosition -= Vector3.up * gap;
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
            go.transform.SetParent(transform, false);
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

        RotateTowardsCamera();
        if (_useSet) UpdateSetAnimation();
        else UpdateAnimation();
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

    private void UpdateAnimation()
    {
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
        if (_hitUntil > 0f)
        {
            _resumeAnim = anim; // termina el golpe y luego sigue con esta
            return;
        }
        SetAnim(anim, loop);
    }

    // ============ Métodos para cambiar animaciones desde EnemyAI ============

    /// <summary>Animación de reposo (en modo antiguo = caminado, como antes)</summary>
    public void PlayIdle()
    {
        if (_useSet) Play("idle");
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
        if (_currentAnimation != walkSprites)
        {
            _currentAnimation = walkSprites;
            _currentFrame = 0;
            _animTimer = 0;
        }
    }

    /// <summary>Cambiar a animación de alerta (con set: "shoot")</summary>
    public void PlayAlert()
    {
        if (_useSet)
        {
            Play("shoot");
            return;
        }
        if (_currentAnimation != alertSprites && alertSprites != null)
        {
            _currentAnimation = alertSprites;
            _currentFrame = 0;
            _animTimer = 0;
        }
    }

    /// <summary>Frame de golpe durante unos instantes y vuelve a la animación anterior.</summary>
    public void PlayHit(float duration = 0.15f)
    {
        if (!_useSet || !spriteSet.TryGet("hit", out _)) return;
        if (_hitUntil < 0f) _resumeAnim = _setAnim;
        _setAnim = null;
        SetAnim("hit", false);
        _hitUntil = Time.time + duration;
    }

    /// <summary>Manos arriba (en modo antiguo = caminado, como antes)</summary>
    public void PlaySurrender()
    {
        if (_useSet)
        {
            _hitUntil = -1f;
            SetAnim("surrender", true);
        }
        else PlayWalk();
    }

    /// <summary>Animación de hablar (interrogatorios)</summary>
    public void PlayTalk()
    {
        if (_useSet) Play("talk");
    }

    /// <summary>Mostrar la muerte: con set se reproduce una vez y se queda en el último frame</summary>
    public void PlayDeath()
    {
        if (_useSet)
        {
            _hitUntil = -1f;
            SetAnim("death", false);
            return;
        }

        _currentAnimation = null;
        if (deathSprite != null && _spriteRenderer != null)
            _spriteRenderer.sprite = deathSprite;
        enabled = false; // Dejar de actualizar este componente
    }

    /// <summary>Voltear el sprite según la dirección de movimiento</summary>
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
