using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Rayo del menú de inicio (M4): destello doble, sacudida de pantalla, trueno y corte a los cuadros de tormenta.
/// Después, relámpagos suaves de ambiente cada 12–20 s. Todo en tiempo real y sin basura por cuadro.
/// DÓNDE VA: objeto "Destello" de CanvasMenu (lo arma Plomo y Paja → Crear menu de inicio).
/// Sin arte de tormenta: destello y trueno igual, y siguen los cuadros de espera. Sin trueno: solo el destello.
/// </summary>
public class LightningFlash : MonoBehaviour
{
    [SerializeField] private Image flash;
    [SerializeField] private MenuFrameAnimator background;

    [Header("Golpe: alfa de cada pico")]
    [SerializeField, Range(0f, 1f)] private float firstPeak = 0.9f;
    [SerializeField, Range(0f, 1f)] private float valley = 0.2f;
    [SerializeField, Range(0f, 1f)] private float secondPeak = 0.8f;

    [Header("Golpe: duración de cada tramo (s)")]
    [SerializeField] private float riseSeconds = 0.04f;
    [SerializeField] private float dipSeconds = 0.06f;
    [SerializeField] private float reboundSeconds = 0.04f;
    [SerializeField] private float decaySeconds = 0.35f;

    [Header("Sacudida")]
    [SerializeField] private float shakePixels = 6f;
    [SerializeField] private float shakeSeconds = 0.25f;

    [Header("Trueno")]
    [SerializeField] private string thunderId = "sfx_trueno";
    [Tooltip("Placeholder: el trueno de la lluvia del nivel, hasta que llegue sfx_trueno")]
    [SerializeField] private string thunderFallbackId = "trueno";

    [Header("Relámpagos de ambiente")]
    [SerializeField] private float ambientMinSeconds = 12f;
    [SerializeField] private float ambientMaxSeconds = 20f;
    [SerializeField, Range(0f, 1f)] private float ambientPeak = 0.35f;
    [SerializeField] private float ambientRiseSeconds = 0.06f;
    [SerializeField] private float ambientDecaySeconds = 0.45f;
    [SerializeField] private string farThunderId = "sfx_trueno_lejos";
    [Tooltip("Volumen del trueno normal cuando no hay sfx_trueno_lejos")]
    [SerializeField, Range(0f, 1f)] private float farThunderFallbackVolume = 0.4f;

    /// <summary>Cae el rayo (lo escucha MenuTitleLogo).</summary>
    public event System.Action StrikeStarted;
    /// <summary>Empieza un relámpago lejano (lo escucha MenuTitleLogo).</summary>
    public event System.Action AmbientFlashStarted;

    private Coroutine _ambient;

    private void Awake()
    {
        SetAlpha(0f);
    }

    /// <summary>El golpe completo. Corta a la tormenta en el primer pico.</summary>
    public IEnumerator Strike()
    {
        StrikeStarted?.Invoke();
        if (!BotonCartoon.PlayIfPresent(thunderId)) BotonCartoon.PlayIfPresent(thunderFallbackId);
        StartCoroutine(Shake());

        yield return Ramp(0f, firstPeak, riseSeconds);
        if (background == null || !background.CutToStorm())
        {
            Debug.LogWarning("[Menu] Sin cuadros menu_rayo_*: el rayo cae sobre la imagen de espera.");
        }
        yield return Ramp(firstPeak, valley, dipSeconds);
        yield return Ramp(valley, secondPeak, reboundSeconds);
        yield return Ramp(secondPeak, 0f, decaySeconds);
    }

    public void StartAmbient()
    {
        if (_ambient == null) _ambient = StartCoroutine(Ambient());
    }

    public void StopAmbient()
    {
        if (_ambient != null) StopCoroutine(_ambient);
        _ambient = null;
        SetAlpha(0f);
    }

    private IEnumerator Ambient()
    {
        while (true)
        {
            float wait = Random.Range(ambientMinSeconds, ambientMaxSeconds);
            for (float t = 0f; t < wait; t += Time.unscaledDeltaTime) yield return null;

            AmbientFlashStarted?.Invoke();
            if (!BotonCartoon.PlayIfPresent(farThunderId))
            {
                if (!BotonCartoon.PlayIfPresent(thunderId, farThunderFallbackVolume))
                    BotonCartoon.PlayIfPresent(thunderFallbackId, farThunderFallbackVolume);
            }
            yield return Ramp(0f, ambientPeak, ambientRiseSeconds);
            yield return Ramp(ambientPeak, 0f, ambientDecaySeconds);
        }
    }

    private IEnumerator Ramp(float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            SetAlpha(Mathf.Lerp(from, to, t / seconds));
            yield return null;
        }
        SetAlpha(to);
    }

    private IEnumerator Shake()
    {
        if (background == null) yield break;
        for (float t = 0f; t < shakeSeconds; t += Time.unscaledDeltaTime)
        {
            float amount = shakePixels * (1f - t / shakeSeconds);
            background.SetExtraOffset(new Vector2(Random.Range(-amount, amount), Random.Range(-amount, amount)));
            yield return null;
        }
        background.SetExtraOffset(Vector2.zero);
    }

    private void SetAlpha(float alpha)
    {
        if (flash == null) return;
        Color c = flash.color;
        c.a = alpha;
        flash.color = c;
        flash.enabled = alpha > 0f;
    }
}
