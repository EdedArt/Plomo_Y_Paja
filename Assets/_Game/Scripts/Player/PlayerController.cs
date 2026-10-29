using UnityEngine;

/// <summary>
/// Controlador principal del jugador (Wilbur, el hermano mayor).
/// DÓNDE VA: GameObject "Player" con componente CharacterController.
///
/// SETUP EN INSPECTOR:
/// - Player (GameObject vacío)
///   ├── CharacterController (Add Component)
///   ├── PlayerController (este script)
///   └── CameraHolder (GameObject hijo vacío, en Y=1.7)
///       └── Main Camera
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    [Tooltip("Velocidad normal de caminado")]
    [SerializeField] private float walkSpeed = 4f;

    [Tooltip("Velocidad al correr (Shift)")]
    [SerializeField] private float runSpeed = 7f;

    [Tooltip("Fuerza del salto — ponlo en 0 si no quieres salto")]
    [SerializeField] private float jumpForce = 0f; // FPS noir: sin salto por defecto

    [Tooltip("Gravedad del personaje")]
    [SerializeField] private float gravity = -20f;

    [Header("Mirada (Mouse)")]
    [Tooltip("Sensibilidad del mouse")]
    [SerializeField] private float mouseSensitivity = 2f;

    [Tooltip("Ángulo máximo que puede mirar hacia arriba")]
    [SerializeField] private float maxLookUp = 80f;

    [Tooltip("Ángulo máximo que puede mirar hacia abajo")]
    [SerializeField] private float maxLookDown = -80f;

    [Header("Referencias")]
    [Tooltip("El Transform de la cámara (CameraHolder, hijo del Player)")]
    [SerializeField] private Transform cameraHolder;

    [Tooltip("LayerMask para detectar el suelo")]
    [SerializeField] private LayerMask groundMask;

    // ============ Estado interno ============
    private CharacterController _controller;
    private Vector3 _velocity;         // Velocidad vertical (gravedad)
    private float _xRotation = 0f;    // Rotación vertical de la cámara
    private bool _isGrounded;
    private bool _canMove = true;      // Se desactiva durante diálogos

    // ============ Propiedades públicas ============
    public bool IsRunning => Input.GetKey(KeyCode.LeftShift) && _canMove;
    public bool IsMoving => _controller.velocity.magnitude > 0.1f;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    private void Start()
    {
        // Bloquear y ocultar el cursor al iniciar
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleMouseLook();
        HandleMovement();
    }

    /// <summary>
    /// Rotar la vista con el mouse.
    /// El cuerpo gira horizontalmente, la cámara gira verticalmente.
    /// </summary>
    private void HandleMouseLook()
    {
        if (!_canMove) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Rotar el cuerpo del jugador horizontalmente
        transform.Rotate(Vector3.up * mouseX);

        // Rotar la cámara verticalmente (clampeado para no girar 360°)
        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, maxLookDown, maxLookUp);
        cameraHolder.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
    }

    /// <summary>
    /// Mover al jugador con WASD. Aplicar gravedad y detección de suelo.
    /// </summary>
    private void HandleMovement()
    {
        // Detectar si está en el suelo
        _isGrounded = _controller.isGrounded;
        if (_isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f; // Pequeño valor negativo para mantenerlo pegado al suelo
        }

        if (!_canMove)
        {
            // Durante diálogos: solo aplicar gravedad, no moverse
            _velocity.y += gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
            return;
        }

        // Leer input de movimiento
        float x = Input.GetAxis("Horizontal"); // A/D
        float z = Input.GetAxis("Vertical");   // W/S

        // Calcular dirección de movimiento relativa a donde mira el jugador
        Vector3 move = transform.right * x + transform.forward * z;

        // Velocidad según si corre o camina
        float speed = IsRunning ? runSpeed : walkSpeed;
        _controller.Move(move * speed * Time.deltaTime);

        // Salto (desactivado por defecto en este juego noir)
        if (jumpForce > 0 && Input.GetButtonDown("Jump") && _isGrounded)
        {
            _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
        }

        // Gravedad
        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }

    /// <summary>
    /// Llamar esto desde DialogueSystem para que el jugador no se mueva durante diálogos.
    /// </summary>
    public void SetCanMove(bool canMove)
    {
        _canMove = canMove;
        Cursor.lockState = canMove ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !canMove;
    }
}
