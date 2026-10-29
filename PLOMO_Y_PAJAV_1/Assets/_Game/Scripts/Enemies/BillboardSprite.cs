using UnityEngine;

/// <summary>
/// Hace que un sprite 2D siempre mire hacia la cámara del jugador.
/// Este es el efecto visual clave del estilo Mouse P.I. For Hire:
/// personajes dibujados en 2D que "viven" en un mundo 3D.
///
/// DÓNDE VA: GameObject del sprite del enemigo (el que tiene SpriteRenderer).
/// El enemigo tiene esta jerarquía:
/// - EnemyRoot (con EnemyAI, NavMeshAgent, Rigidbody)
///   └── EnemySprite (con SpriteRenderer y BillboardSprite)
///
/// TIPOS DE BILLBOARD:
/// - Full: el sprite rota en todos los ejes para mirar a la cámara (como árbol en juegos viejos)
/// - YAxis: solo rota en Y (el sprite "voltea" pero no se inclina) — más natural para personajes
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

    [Header("Animación simple (sin Animator)")]
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
    private float _animTimer;
    private int _currentFrame;
    private Sprite[] _currentAnimation;

    private void Awake()
    {
        _mainCamera = Camera.main;
        _spriteRenderer = GetComponent<SpriteRenderer>();

        if (walkSprites != null && walkSprites.Length > 0)
            _currentAnimation = walkSprites;
    }

    private void LateUpdate()
    {
        // LateUpdate se ejecuta después de Update: perfecto para billboards
        // porque así los enemigos ya movieron antes de que actualizemos la rotación

        RotateTowardsCamera();
        UpdateAnimation();
    }

    private void RotateTowardsCamera()
    {
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
        if (_currentAnimation == null || _currentAnimation.Length == 0) return;

        _animTimer += Time.deltaTime * animationSpeed;
        if (_animTimer >= 1f)
        {
            _animTimer = 0;
            _currentFrame = (_currentFrame + 1) % _currentAnimation.Length;
            _spriteRenderer.sprite = _currentAnimation[_currentFrame];
        }
    }

    // ============ Métodos para cambiar animaciones desde EnemyAI ============

    /// <summary>Cambiar a animación de caminado</summary>
    public void PlayWalk()
    {
        if (_currentAnimation != walkSprites)
        {
            _currentAnimation = walkSprites;
            _currentFrame = 0;
            _animTimer = 0;
        }
    }

    /// <summary>Cambiar a animación de alerta</summary>
    public void PlayAlert()
    {
        if (_currentAnimation != alertSprites && alertSprites != null)
        {
            _currentAnimation = alertSprites;
            _currentFrame = 0;
            _animTimer = 0;
        }
    }

    /// <summary>Mostrar sprite de muerte (imagen estática)</summary>
    public void PlayDeath()
    {
        _currentAnimation = null;
        if (deathSprite != null)
            _spriteRenderer.sprite = deathSprite;
        enabled = false; // Dejar de actualizar este componente
    }

    /// <summary>Voltear el sprite según la dirección de movimiento</summary>
    public void SetFacingDirection(Vector3 movementDirection)
    {
        if (!flipWithMovement || _mainCamera == null) return;

        // Calcular si el enemigo se mueve hacia la izquierda o derecha RELATIVO a la cámara
        Vector3 camRight = _mainCamera.transform.right;
        float dot = Vector3.Dot(movementDirection.normalized, camRight);

        _spriteRenderer.flipX = dot < 0;
    }
}
