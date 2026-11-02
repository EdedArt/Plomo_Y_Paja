using UnityEngine;

/// <summary>
/// Arma tirada en el piso que Wilbur recoge (fase 3C.5, D13). Hoy: el revólver que Centavo suelta al primer golpe
/// ("Revolver_Suelto" del prefab de Centavo, lo arma ContadorCentavoBuilder; CentavoController.DropRevolver lo suelta
/// delante del banco y llama SetSafeSpot).
/// INTERACCIÓN: la misma que las pistas (CluePickup): acercarse a interactDistance (en horizontal) y apretar E. El texto sale
/// bajo la mira (RubberHoseCrosshair.SetPrompt) porque el prompt de UIManager no existe en las escenas.
/// Se puede recoger en cuanto está activo y pasó pickupDelay (el tiempo de caer), en cualquier desenlace: Centavo herido,
/// matado en la escena, perdonado o rematado antes de la escena (D4). No se recoge en pausa, diálogo ni interrogatorio.
/// Se ve: look plano con contorno (CharacterLook, como las pistas) y una luz chica que late. No se usa PickupPresentation
/// porque mueve el objeto y este tiene Rigidbody.
/// Si atraviesa el piso (cae más de fallRescueDepth por debajo del punto seguro), vuelve al punto seguro y se queda quieto.
/// NO es una pista: no toca GameStateTracker como pista ni llama a la IA.
/// </summary>
public class WeaponPickup : MonoBehaviour
{
    [SerializeField] private string displayName = "Revólver de Centavo";
    [SerializeField] private string interactPrompt = "[E] Recoger el revólver";
    [Tooltip("Distancia horizontal (m) desde el jugador, como CluePickup")]
    [SerializeField] private float interactDistance = 2.5f;
    [Tooltip("Diferencia de altura máxima (m) entre el jugador y el arma")]
    [SerializeField] private float interactHeight = 2.2f;
    [Tooltip("Segundos desde que aparece (cae de la mano de Centavo) hasta que se puede recoger")]
    [SerializeField] private float pickupDelay = 0.4f;
    [Tooltip("Si cae este tanto por debajo del punto seguro, se lo devuelve ahí (atravesó el piso)")]
    [SerializeField] private float fallRescueDepth = 1.5f;

    [Header("Brillo")]
    [SerializeField] private Color glintColor = new Color(1f, 0.85f, 0.55f);
    [SerializeField] private float glintRange = 1.8f;
    [SerializeField] private Vector2 glintIntensity = new Vector2(0.4f, 1.8f);
    [Tooltip("Latidos por segundo")]
    [SerializeField] private float glintRate = 0.9f;

    [Header("Depuración (apagada por defecto)")]
    [Tooltip("Escribe en consola dónde aparece y dónde queda el arma, y cada 0.5 s la distancia al jugador y si está bloqueado")]
    [SerializeField] private bool debug = false;

    private Transform _player;
    private WeaponSwitcher _switcher;
    private Light _glint;
    private float _availableAt;
    private bool _promptShown;
    private bool _taken;
    private bool _hasSafeSpot;
    private Vector3 _safeSpot;
    private float _nextDebug;
    private bool _settledLogged;

    /// <summary>Punto del piso donde debería quedar (CentavoController.DropRevolver). Sirve para rescatarlo si atraviesa el piso.</summary>
    public void SetSafeSpot(Vector3 position)
    {
        _safeSpot = position;
        _hasSafeSpot = true;
    }

    private void OnEnable()
    {
        _availableAt = Time.time + pickupDelay;
        _settledLogged = false;
        foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>(true)) CharacterLook.ApplyPlaceholder(r);
        if (_glint == null)
        {
            var glintGo = new GameObject("Brillo");
            glintGo.transform.SetParent(transform, false);
            _glint = glintGo.AddComponent<Light>();
            _glint.type = LightType.Point;
            _glint.color = glintColor;
            _glint.range = glintRange;
            _glint.shadows = LightShadows.None;
        }
        _glint.enabled = true;
        if (debug) Debug.Log($"[WeaponPickup] {name} aparece en {transform.position:F2} (punto seguro {(_hasSafeSpot ? _safeSpot.ToString("F2") : "ninguno")})");
    }

    private void OnDisable()
    {
        HidePrompt();
    }

    private void Update()
    {
        if (_taken) return;
        if (_glint != null)
        {
            float t = 0.5f + 0.5f * Mathf.Sin(Time.time * glintRate * Mathf.PI * 2f);
            _glint.intensity = Mathf.Lerp(glintIntensity.x, glintIntensity.y, t * t);
        }
        RescueIfFallen();

        if (!FindPlayer())
        {
            if (debug && Time.time >= _nextDebug)
            {
                _nextDebug = Time.time + 0.5f;
                Debug.LogWarning("[WeaponPickup] No hay objeto con tag Player y WeaponSwitcher: no se puede recoger.");
            }
            return;
        }

        Vector3 delta = transform.position - _player.position;
        float dy = Mathf.Abs(delta.y);
        delta.y = 0f;
        float horizontal = delta.magnitude;
        bool blocked = PlayerAim.GameplayBlocked();
        bool ready = Time.time >= _availableAt;
        bool inRange = ready && !blocked && horizontal <= interactDistance && dy <= interactHeight;
        LogDebug(horizontal, dy, blocked, ready);

        if (!inRange)
        {
            HidePrompt();
            return;
        }

        if (RubberHoseCrosshair.Current != null)
        {
            RubberHoseCrosshair.Current.SetPrompt(interactPrompt);
            _promptShown = true;
        }
        if (Input.GetKeyDown(KeyCode.E)) Take();
    }

    private void RescueIfFallen()
    {
        if (!_hasSafeSpot || transform.position.y > _safeSpot.y - fallRescueDepth) return;
        if (debug) Debug.LogWarning($"[WeaponPickup] {name} atravesó el piso ({transform.position:F2}): vuelve a {_safeSpot:F2}");
        transform.position = _safeSpot;
        if (TryGetComponent(out Rigidbody body))
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true; // se queda en el punto seguro
        }
    }

    private void LogDebug(float horizontal, float dy, bool blocked, bool ready)
    {
        if (!debug) return;
        if (!_settledLogged && TryGetComponent(out Rigidbody body) && body.IsSleeping())
        {
            _settledLogged = true;
            Debug.Log($"[WeaponPickup] {name} quedó quieto en {transform.position:F2}");
        }
        if (Time.time < _nextDebug) return;
        _nextDebug = Time.time + 0.5f;
        Debug.Log($"[WeaponPickup] jugador a {horizontal:F2} m (alto {dy:F2} m; rango {interactDistance} m / {interactHeight} m) · " +
                  $"listo {ready} · bloqueado {blocked} · prompt {(_promptShown ? "sí" : "no")}");
    }

    private bool FindPlayer()
    {
        if (_player != null && _switcher != null) return true;
        GameObject go = GameObject.FindWithTag("Player");
        if (go == null) return false;
        _player = go.transform;
        if (!go.TryGetComponent(out _switcher)) _switcher = go.GetComponentInParent<WeaponSwitcher>();
        return _switcher != null;
    }

    private void Take()
    {
        if (_taken || _switcher == null || !_switcher.GiveRevolver()) return;
        _taken = true;
        HidePrompt();
        AudioLibrary.PlaySfx2D("escopeta_cargar");
        if (RubberHoseCrosshair.Current != null) RubberHoseCrosshair.Current.ShowNotice(displayName);
        if (debug) Debug.Log($"[WeaponPickup] {name} recogido");
        gameObject.SetActive(false);
    }

    private void HidePrompt()
    {
        if (!_promptShown) return;
        _promptShown = false;
        if (RubberHoseCrosshair.Current != null) RubberHoseCrosshair.Current.SetPrompt(null);
    }
}
