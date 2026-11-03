using UnityEngine;

/// <summary>
/// Vida del taxi de la intro (INTRO.2): faroles de la calle que barren el interior, ruedas que giran, y el taxista
/// (conduce_01 al manejar; habla_01/02 con lip-sync cuando habla).
/// DÓNDE VA: raíz de Prefabs/Level1/Taxi_Intro.prefab (lo arma Plomo y Paja → Intro → Construir taxi y llegada).
///
/// - Barrido: cada 2.5–4 s (al azar) una luz spot cálida cruza el interior de adelante hacia atrás en sweepSeconds,
///   como un farol que pasa. Solo mientras el taxi anda (Driving). El dibujo del taxista (sin iluminación) se aclara
///   con el barrido para que no quede plano.
/// - Taxista: SetTalking(true) pone el par habla_01/habla_02 (LipSyncSpriteAdapter + LipSyncDriver de su voz);
///   SetTalking(false) vuelve a conduce_01.
/// - Sin basura por frame.
/// </summary>
public class TaxiInterior : MonoBehaviour
{
    [Header("Barrido de faroles")]
    [SerializeField] private Light sweepLight;
    [SerializeField] private Vector2 sweepInterval = new Vector2(2.5f, 4f);
    [SerializeField] private float sweepSeconds = 0.8f;
    [SerializeField] private float sweepIntensity = 3.2f;
    [Tooltip("Giro (grados, en Y local del pivote) al empezar y al terminar el barrido: de adelante hacia atrás")]
    [SerializeField] private Vector2 sweepYaw = new Vector2(-55f, 55f);

    [Header("Taxista")]
    [SerializeField] private SpriteRenderer driver;
    [SerializeField] private LipSyncSpriteAdapter mouth;
    [SerializeField] private Sprite driving;
    [SerializeField] private Sprite talkClosed;
    [SerializeField] private Sprite talkOpen;
    [Tooltip("Color del dibujo con el interior a oscuras")]
    [SerializeField] private Color driverDark = new Color(0.36f, 0.31f, 0.3f, 1f);
    [Tooltip("Color del dibujo cuando pasa el barrido")]
    [SerializeField] private Color driverLit = new Color(0.95f, 0.82f, 0.66f, 1f);

    [Header("Ruedas")]
    [SerializeField] private Transform[] wheels = new Transform[0];
    [SerializeField] private float wheelRadius = 0.38f;

    private float _nextSweep;
    private float _sweepT = -1f;
    private float _speed;

    /// <summary>El taxi está andando (hay barridos).</summary>
    public bool Driving { get; set; }

    private void Awake()
    {
        if (sweepLight != null) sweepLight.intensity = 0f;
        _nextSweep = Time.unscaledTime + 1f;
        SetTalking(false);
        ApplyDriverTint(0f);
    }

    /// <summary>Velocidad (m/s) para girar las ruedas.</summary>
    public void SetSpeed(float metersPerSecond) => _speed = metersPerSecond;

    public void SetTalking(bool talking)
    {
        if (mouth != null)
        {
            if (talking) mouth.SetPair(talkClosed != null ? talkClosed : driving, talkOpen != null ? talkOpen : driving);
            else mouth.SetPair(driving, driving);
        }
        else if (driver != null && driving != null)
        {
            driver.sprite = talking && talkClosed != null ? talkClosed : driving;
        }
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // Ruedas
        if (_speed > 0.01f && wheels != null)
        {
            float degrees = _speed * Time.deltaTime / Mathf.Max(0.05f, wheelRadius) * Mathf.Rad2Deg;
            for (int i = 0; i < wheels.Length; i++)
                if (wheels[i] != null) wheels[i].Rotate(degrees, 0f, 0f, Space.Self);
        }

        // Barrido de faroles
        if (_sweepT < 0f && Driving && Time.unscaledTime >= _nextSweep) _sweepT = 0f;
        float glow = 0f;
        if (_sweepT >= 0f)
        {
            _sweepT += dt / Mathf.Max(0.05f, sweepSeconds);
            float u = Mathf.Clamp01(_sweepT);
            glow = Mathf.Sin(u * Mathf.PI);
            if (sweepLight != null)
            {
                sweepLight.intensity = sweepIntensity * glow;
                sweepLight.transform.localRotation = Quaternion.Euler(0f, Mathf.Lerp(sweepYaw.x, sweepYaw.y, u), 0f);
            }
            if (_sweepT >= 1f)
            {
                _sweepT = -1f;
                _nextSweep = Time.unscaledTime + Random.Range(sweepInterval.x, sweepInterval.y);
                if (sweepLight != null) sweepLight.intensity = 0f;
            }
        }
        ApplyDriverTint(glow);
    }

    private void ApplyDriverTint(float glow)
    {
        if (driver != null) driver.color = Color.Lerp(driverDark, driverLit, glow);
    }

#if UNITY_EDITOR
    /// <summary>Solo para el builder del Editor.</summary>
    public void EditorSetup(Light sweep, SpriteRenderer driverRenderer, LipSyncSpriteAdapter lipSync, Sprite drive,
        Sprite closed, Sprite open, Transform[] wheelTransforms, float radius)
    {
        sweepLight = sweep;
        driver = driverRenderer;
        mouth = lipSync;
        driving = drive;
        talkClosed = closed;
        talkOpen = open;
        wheels = wheelTransforms;
        wheelRadius = radius;
    }
#endif
}
