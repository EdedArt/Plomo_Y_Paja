using UnityEngine;

/// <summary>
/// Arma tirada en el piso que Wilbur recoge (fase 3C.5, D13). Hoy: el revólver que Centavo suelta al primer golpe
/// ("Revolver_Suelto" del prefab de Centavo, lo arma ContadorCentavoBuilder).
/// INTERACCIÓN: la misma que las pistas (CluePickup): acercarse a interactDistance y apretar E. El texto sale bajo la mira
/// (RubberHoseCrosshair.SetPrompt) porque el prompt de UIManager no existe en las escenas.
/// Se puede recoger en cuanto está activo y pasó pickupDelay (el tiempo de caer), en cualquier desenlace: Centavo herido,
/// matado en la escena, perdonado o rematado antes de la escena (D4). No se recoge en pausa, diálogo ni interrogatorio.
/// Brillo sutil: una luz puntual chica que late (no se usa PickupPresentation: movería el objeto que tiene Rigidbody).
/// NO es una pista: no toca GameStateTracker como pista ni llama a la IA.
/// </summary>
public class WeaponPickup : MonoBehaviour
{
    [SerializeField] private string displayName = "Revólver de Centavo";
    [SerializeField] private string interactPrompt = "[E] Recoger el revólver";
    [Tooltip("Distancia (m) desde el jugador, como CluePickup")]
    [SerializeField] private float interactDistance = 2.5f;
    [Tooltip("Segundos desde que aparece (cae de la mano de Centavo) hasta que se puede recoger")]
    [SerializeField] private float pickupDelay = 0.4f;

    [Header("Brillo")]
    [SerializeField] private Color glintColor = new Color(1f, 0.85f, 0.55f);
    [SerializeField] private float glintRange = 1.2f;
    [SerializeField] private Vector2 glintIntensity = new Vector2(0.15f, 1.1f);
    [Tooltip("Latidos por segundo")]
    [SerializeField] private float glintRate = 0.9f;

    private Transform _player;
    private WeaponSwitcher _switcher;
    private Light _glint;
    private float _availableAt;
    private bool _promptShown;
    private bool _taken;

    private void OnEnable()
    {
        _availableAt = Time.time + pickupDelay;
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

        if (!FindPlayer()) return;
        bool inRange = Time.time >= _availableAt
                       && !PlayerAim.GameplayBlocked()
                       && Vector3.Distance(transform.position, _player.position) <= interactDistance;
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

    private bool FindPlayer()
    {
        if (_player != null && _switcher != null) return true;
        GameObject go = GameObject.FindWithTag("Player");
        if (go == null) return false;
        _player = go.transform;
        go.TryGetComponent(out _switcher);
        return _switcher != null;
    }

    private void Take()
    {
        if (_taken || _switcher == null || !_switcher.GiveRevolver()) return;
        _taken = true;
        HidePrompt();
        AudioLibrary.PlaySfx2D("escopeta_cargar");
        if (RubberHoseCrosshair.Current != null) RubberHoseCrosshair.Current.ShowNotice(displayName);
        gameObject.SetActive(false);
    }

    private void HidePrompt()
    {
        if (!_promptShown) return;
        _promptShown = false;
        if (RubberHoseCrosshair.Current != null) RubberHoseCrosshair.Current.SetPrompt(null);
    }
}
