using UnityEngine;

/// <summary>
/// Parpadeo de una luz rota (farol de la calle, foco viejo).
/// DÓNDE VA: mismo GameObject que el Light.
///
/// - Temblor suave constante (ruido Perlin)
/// - Bajones aleatorios de intensidad
/// - Apagones cortos de 1 a 3 parpadeos seguidos
/// </summary>
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    [Header("Intensidad")]
    [Tooltip("Intensidad normal. Si es <= 0 se toma la del Light al iniciar")]
    [SerializeField] private float baseIntensity = 0f;

    [Tooltip("Cuánto tiembla la luz de forma constante (0 = nada, 1 = mucho)")]
    [SerializeField, Range(0f, 1f)] private float noiseAmount = 0.15f;

    [Tooltip("Velocidad del temblor constante")]
    [SerializeField] private float noiseSpeed = 4f;

    [Header("Bajones")]
    [Tooltip("Probabilidad por segundo de un bajón de intensidad")]
    [SerializeField] private float dipChancePerSecond = 0.8f;

    [Tooltip("Multiplicador mínimo durante un bajón")]
    [SerializeField, Range(0f, 1f)] private float dipMinMultiplier = 0.3f;

    [Tooltip("Duración de un bajón (mín, máx) en segundos")]
    [SerializeField] private Vector2 dipDuration = new Vector2(0.05f, 0.25f);

    [Header("Apagones")]
    [Tooltip("Probabilidad por segundo de una racha de apagones")]
    [SerializeField] private float offChancePerSecond = 0.12f;

    [Tooltip("Duración de cada apagón (mín, máx) en segundos")]
    [SerializeField] private Vector2 offDuration = new Vector2(0.04f, 0.3f);

    [Tooltip("Máximo de parpadeos seguidos en una racha")]
    [SerializeField, Range(1, 5)] private int maxBlinks = 3;

    private Light _light;
    private float _seed;
    private float _dipTimer;
    private float _dipMultiplier = 1f;
    private float _offTimer;
    private float _gapTimer;
    private int _blinksRemaining;

    private void Awake()
    {
        _light = GetComponent<Light>();
        if (baseIntensity <= 0f) baseIntensity = _light.intensity;
        _seed = Random.value * 100f;
    }

    /// <summary>Probabilidad de que ocurra un evento de "rate" por segundo en un frame de dt segundos (independiente del FPS).</summary>
    private static float Chance(float ratePerSecond, float dt) => 1f - Mathf.Exp(-Mathf.Max(0f, ratePerSecond) * dt);

    private void Update()
    {
        // Tiempo de juego (se congela con el hit-stop o la pausa); un tirón de FPS no dispara rachas
        float dt = Mathf.Min(Time.deltaTime, 0.1f);
        if (dt <= 0f) return;

        // Racha de apagones: alterna apagado / encendido breve
        if (_offTimer > 0f)
        {
            _offTimer -= dt;
            _light.intensity = 0f;
            if (_offTimer <= 0f && _blinksRemaining > 0)
                _gapTimer = Random.Range(0.04f, 0.15f);
            return;
        }
        if (_gapTimer > 0f)
        {
            _gapTimer -= dt;
            if (_gapTimer <= 0f && _blinksRemaining > 0)
                StartBlink();
        }
        else if (Random.value < Chance(offChancePerSecond, dt))
        {
            _blinksRemaining = Random.Range(1, maxBlinks + 1);
            StartBlink();
            _light.intensity = 0f;
            return;
        }

        // Bajones
        if (_dipTimer > 0f)
        {
            _dipTimer -= dt;
            if (_dipTimer <= 0f) _dipMultiplier = 1f;
        }
        else if (Random.value < Chance(dipChancePerSecond, dt))
        {
            _dipTimer = Random.Range(dipDuration.x, dipDuration.y);
            _dipMultiplier = Random.Range(dipMinMultiplier, 0.8f);
        }

        float noise = 1f - noiseAmount * Mathf.PerlinNoise(Time.time * noiseSpeed, _seed);
        _light.intensity = Mathf.Max(0f, baseIntensity * noise * _dipMultiplier);
    }

    private void StartBlink()
    {
        _blinksRemaining--;
        _offTimer = Random.Range(offDuration.x, offDuration.y);
    }
}
