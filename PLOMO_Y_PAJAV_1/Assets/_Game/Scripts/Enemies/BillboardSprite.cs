using UnityEngine;                       // Tipos básicos de Unity: MonoBehaviour, Transform, Sprite, Camera...
using UnityEngine.Serialization;         // Para [FormerlySerializedAs]: renombrar un campo sin perder lo asignado en el Inspector

/// <summary>
/// Hace que un personaje dibujado en 2D (sprite) siempre mire a la cámara del jugador,
/// apoyado en sus pies, dentro de un mundo 3D. Es la técnica "billboard".
///
/// DÓNDE VA: en el GameObject "SpriteHolder", hijo de la raíz del enemigo.
///   LoboGuardia (EnemyAI, EnemyHealth, NavMeshAgent, CapsuleCollider)
///   └── SpriteHolder (BillboardSprite, RubberHoseAnimator)   ← su origen = los pies
///       └── Art (SpriteRenderer)                              ← este script lo recoloca solo
///
/// SPRITES (512x512 px, pies tocando el borde inferior, siempre de frente, sombra pintada):
///   Art/Characters/lobo_guardia/guardia_idle_01.png, guardia_alert_01.png,
///   guardia_surrender_01.png, guardia_death_01.png (informante_*, donlobo_* para los demás).
///
/// PIES COMO PUNTO DE APOYO:
///   Lo ideal es importar el PNG con Pivot = Bottom. Si no se hizo, este script mueve
///   el hijo "Art" para que el centro del borde inferior del dibujo quede justo en el
///   origen del SpriteHolder. Así el lobo nunca flota ni se hunde en el piso, y el
///   RubberHoseAnimator puede aplastarlo/estirarlo desde los pies.
///
/// NUNCA SE VOLTEA (sin flipX): la sombra está pintada en el dibujo y voltearlo
/// la dejaría del lado equivocado.
///
/// SIN ARTE: si no hay sprites asignados, no cambia nada y no da error (el placeholder sigue).
/// </summary>
public class BillboardSprite : MonoBehaviour
{
    // ===================== Tipos de billboard =====================

    // Las dos formas en que el dibujo puede girar hacia la cámara
    public enum BillboardType
    {
        [Tooltip("Gira en todos los ejes hacia la cámara (se inclina si miras desde arriba)")]
        Full,                            // Mira de lleno a la cámara, también inclinándose
        [Tooltip("Solo gira en el eje Y: el personaje queda siempre de pie (recomendado)")]
        YAxisOnly                        // Solo gira como una puerta giratoria: siempre vertical
    }

    // ===================== Campos del Inspector =====================

    [Header("Configuración")]                                   // Título de sección en el Inspector
    [Tooltip("Tipo de rotación del billboard")]                 // Texto de ayuda al pasar el mouse
    [SerializeField] private BillboardType billboardType = BillboardType.YAxisOnly; // Por defecto: siempre de pie

    [Tooltip("Mover el SpriteRenderer hijo para que los pies queden en el origen de este objeto")]
    [SerializeField] private bool anchorFeet = true;            // Activa la corrección del punto de apoyo

    [Header("Poses (un dibujo por pose basta; el RubberHoseAnimator le da vida)")]
    [Tooltip("Pose de pie / caminando: guardia_idle_01 (y _02, _03 si algún día hay más)")]
    [FormerlySerializedAs("walkSprites")]                        // Antes se llamaba walkSprites: se conserva lo asignado
    [SerializeField] private Sprite[] idleSprites;              // Lista de dibujos de la pose normal

    [Tooltip("Pose de alerta / ataque: guardia_alert_01")]
    [SerializeField] private Sprite[] alertSprites;             // Lista de dibujos de la pose de alerta

    [Tooltip("Pose de rendición (manos arriba): guardia_surrender_01")]
    [SerializeField] private Sprite[] surrenderSprites;         // Lista de dibujos de la pose de rendición

    [Tooltip("Dibujo de muerte: guardia_death_01")]
    [SerializeField] private Sprite deathSprite;                // Un solo dibujo fijo al morir

    [Tooltip("Cambios de dibujo por segundo cuando una pose tiene varios dibujos")]
    [SerializeField] private float animationSpeed = 8f;         // 8 cuadros por segundo

    // ===================== Estado interno =====================

    private Camera _mainCamera;              // Cámara del jugador (a la que miramos)
    private SpriteRenderer _spriteRenderer;  // Componente que dibuja el sprite (en este objeto o en un hijo)
    private Sprite[] _currentAnimation;      // Pose que se está mostrando ahora
    private float _animTimer;                // Acumulador de tiempo para pasar al siguiente dibujo
    private int _currentFrame;               // Índice del dibujo actual dentro de la pose
    private bool _isDead;                    // true al morir: ya no se cambia de dibujo
    private Sprite _anchoredSprite;          // Último sprite para el que calculamos los pies
    private bool _warnedSameObject;          // Para avisar una sola vez del problema de jerarquía

    // ===================== Ciclo de vida de Unity =====================

    private void Awake()
    {
        _mainCamera = Camera.main;                                      // Buscar la cámara con tag "MainCamera"
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();     // Busca en este objeto y luego en sus hijos

        if (_spriteRenderer != null)                                    // Solo si existe un SpriteRenderer
            _spriteRenderer.flipX = false;                              // Por si el prefab venía volteado: la sombra debe quedar bien

        SetAnimation(idleSprites);                                      // Empezar en la pose normal (si no hay dibujos, no hace nada)
    }

    private void LateUpdate()
    {
        // LateUpdate corre DESPUÉS de todos los Update: el enemigo y la cámara
        // ya se movieron este frame, así el dibujo no "tiembla" un frame atrasado.

        if (_mainCamera == null) _mainCamera = Camera.main;             // Si la cámara apareció después (cambio de escena), buscarla otra vez

        RotateTowardsCamera();                                          // 1) Girar el dibujo hacia la cámara
        UpdateAnimation();                                              // 2) Pasar al siguiente dibujo de la pose
        UpdateFeetAnchor();                                             // 3) Recolocar los pies si cambió el dibujo
    }

    // ===================== Billboard =====================

    private void RotateTowardsCamera()
    {
        if (_mainCamera == null) return;                                // Sin cámara no hay a dónde mirar

        // Vector que va desde el personaje hacia la cámara
        Vector3 directionToCamera = _mainCamera.transform.position - transform.position;

        if (billboardType == BillboardType.YAxisOnly)                   // Modo "siempre de pie"
            directionToCamera.y = 0f;                                   // Ignorar la altura: así no se inclina hacia atrás

        if (directionToCamera.sqrMagnitude < 0.0001f) return;          // Cámara encima del pivote: no hay dirección válida

        // Un Quad/Sprite muestra su cara por el lado -Z (su "espalda" es +Z).
        // Por eso apuntamos el +Z del objeto en sentido CONTRARIO a la cámara:
        // así la cara dibujada queda mirando a la cámara.
        transform.rotation = Quaternion.LookRotation(-directionToCamera);
    }

    // ===================== Animación por dibujos =====================

    private void UpdateAnimation()
    {
        if (_isDead) return;                                            // Muerto: dibujo fijo
        if (_spriteRenderer == null) return;                            // Sin renderer no hay qué animar
        if (_currentAnimation == null || _currentAnimation.Length < 2) return; // Con 0 o 1 dibujo no hay que alternar

        _animTimer += Time.deltaTime * animationSpeed;                  // Sumar tiempo escalado por la velocidad
        if (_animTimer < 1f) return;                                    // Aún no toca cambiar de dibujo

        _animTimer -= 1f;                                               // Restar un "cuadro" (conserva el sobrante)
        _currentFrame = (_currentFrame + 1) % _currentAnimation.Length; // Siguiente dibujo; % vuelve a 0 al final
        ShowFrame();                                                    // Ponerlo en pantalla
    }

    /// <summary>Cambia a otra pose y muestra su primer dibujo enseguida.</summary>
    private void SetAnimation(Sprite[] animation)
    {
        if (_isDead) return;                                            // Muerto: ignorar cambios de pose
        if (animation == null || animation.Length == 0) return;         // Pose sin dibujos: dejar el dibujo actual (placeholder)
        if (_currentAnimation == animation) return;                     // Ya estamos en esa pose: no reiniciar

        _currentAnimation = animation;                                  // Guardar la nueva pose
        _currentFrame = 0;                                              // Empezar por su primer dibujo
        _animTimer = 0f;                                                // Reiniciar el reloj de cuadros
        ShowFrame();                                                    // Mostrarlo ya, sin esperar al siguiente cuadro
    }

    /// <summary>Pone en el SpriteRenderer el dibujo actual de la pose.</summary>
    private void ShowFrame()
    {
        if (_spriteRenderer == null) return;                            // Sin renderer no hay dónde dibujar
        Sprite frame = _currentAnimation[_currentFrame];                // Dibujo que toca
        if (frame != null) _spriteRenderer.sprite = frame;              // Un hueco vacío en la lista no borra el dibujo
    }

    // ===================== Pies como punto de apoyo =====================

    private void UpdateFeetAnchor()
    {
        if (!anchorFeet || _spriteRenderer == null) return;             // Desactivado o sin renderer
        Sprite sprite = _spriteRenderer.sprite;                         // Dibujo que se ve ahora
        if (sprite == null || sprite == _anchoredSprite) return;        // Sin dibujo, o ya calculado para este dibujo
        _anchoredSprite = sprite;                                       // Recordarlo para no recalcular cada frame

        // Un píxel (x, y) del dibujo aparece en (píxel - pivot) / pixelsPerUnit metros.
        // Queremos que el centro del borde inferior (ancho/2, 0) quede en el origen,
        // así que movemos el renderer en sentido contrario a donde caería ese punto.
        float ppu = sprite.pixelsPerUnit;                               // Píxeles por metro del import
        float offsetX = (sprite.pivot.x - sprite.rect.width * 0.5f) / ppu; // Corrección horizontal (0 si el pivot está centrado)
        float offsetY = sprite.pivot.y / ppu;                           // Corrección vertical (0 si el pivot ya está abajo)

        if (_spriteRenderer.transform == transform)                     // El renderer está en ESTE objeto: no se puede mover sin mover todo
        {
            bool needsFix = Mathf.Abs(offsetX) > 0.001f || Mathf.Abs(offsetY) > 0.001f; // ¿El pivot no está en los pies?
            if (needsFix && !_warnedSameObject)                         // Avisar una sola vez
            {
                _warnedSameObject = true;
                Debug.LogWarning($"[BillboardSprite] '{name}': el sprite '{sprite.name}' no tiene el pivot en los pies. " +
                                 "Importa el PNG con Pivot = Bottom o pon el SpriteRenderer en un hijo.");
            }
            return;
        }

        Transform art = _spriteRenderer.transform;                      // El hijo que tiene el SpriteRenderer
        Vector3 scale = art.localScale;                                 // Su escala (si el hijo está escalado, la corrección también)
        art.localPosition = new Vector3(offsetX * scale.x,              // Mover a los lados
                                        offsetY * scale.y,              // Subir/bajar hasta que los pies toquen el origen
                                        art.localPosition.z);           // Conservar la profundidad que tenía
    }

    // ===================== Métodos que llama EnemyAI =====================

    /// <summary>Pose normal (de pie / caminando). El rebote lo pone RubberHoseAnimator.</summary>
    public void PlayWalk()
    {
        SetAnimation(idleSprites);                                      // Cambiar a la pose normal
    }

    /// <summary>Pose de alerta / ataque.</summary>
    public void PlayAlert()
    {
        SetAnimation(alertSprites);                                     // Cambiar a la pose de alerta
    }

    /// <summary>Pose de rendición (manos arriba). Sin dibujo, se queda la pose actual.</summary>
    public void PlaySurrender()
    {
        SetAnimation(surrenderSprites);                                 // Cambiar a la pose de rendición
    }

    /// <summary>
    /// Dibujo de muerte, fijo. El billboard SIGUE mirando a la cámara para que
    /// el cuerpo no se vea "de canto" si el jugador camina alrededor.
    /// </summary>
    public void PlayDeath()
    {
        if (_spriteRenderer != null && deathSprite != null)             // Solo si hay dónde y qué dibujar
            _spriteRenderer.sprite = deathSprite;                       // Poner el dibujo de muerte
        _currentAnimation = null;                                       // Ya no hay pose que animar
        _isDead = true;                                                 // Bloquear cualquier cambio de pose posterior
    }

    /// <summary>
    /// Ya no hace nada: los sprites son siempre de frente y no se voltean
    /// (la sombra pintada quedaría al revés). Se conserva para no romper
    /// a quien lo llame; EnemyAI deja de usarlo en el paso 2.
    /// </summary>
    public void SetFacingDirection(Vector3 movementDirection)
    {
        // Intencionalmente vacío.
    }
}
