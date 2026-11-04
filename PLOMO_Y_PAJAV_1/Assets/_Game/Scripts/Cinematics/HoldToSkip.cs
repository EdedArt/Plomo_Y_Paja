using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Mantén ESPACIO para saltar" (I5): mantener ESPACIO o Esc 1 s salta la cinemática. Un toque corto de ESPACIO cuenta
/// como "avanzar" (I4) y se lee con ConsumeTap(). Tiempo real. Reutilizable (libro INTRO.1, taxi INTRO.2).
/// DÓNDE VA: cualquier objeto de la cinemática; el aviso (CanvasGroup + Image radial "Filled") vive en su propio Canvas.
///
/// - El aviso aparece al pulsar cualquier tecla o clic y se va solo tras unos segundos sin tocar nada.
/// - Mientras se mantiene la tecla, el anillo se llena; al llegar a 1 dispara Skipped (una sola vez).
/// - Solo funciona con Armed = true (el director lo arma al empezar y lo desarma al terminar).
/// </summary>
public class HoldToSkip : MonoBehaviour
{
    [Header("Teclas")]
    [SerializeField] private KeyCode[] holdKeys = { KeyCode.Space, KeyCode.Escape };
    [Tooltip("Tecla cuyo toque corto cuenta como 'avanzar'")]
    [SerializeField] private KeyCode tapKey = KeyCode.Space;
    [SerializeField] private float holdSeconds = 1f;
    [Tooltip("Un toque más corto que esto (s) es 'avanzar', no el inicio de un salto")]
    [SerializeField] private float tapMaxSeconds = 0.25f;

    [Header("Aviso en pantalla")]
    [SerializeField] private CanvasGroup hint;
    [SerializeField] private Image radialFill;
    [Tooltip("Segundos que el aviso sigue visible después de la última tecla")]
    [SerializeField] private float hintLinger = 2.5f;
    [SerializeField] private float hintFadeSpeed = 4f;

    /// <summary>Se dispara una vez al completar el mantenido.</summary>
    public event Action Skipped;

    public bool Armed { get; set; }
    public bool SkipRequested { get; private set; }

    private float _held;
    private bool _tapPending;
    private float _hintTimer;

    private void Awake()
    {
        if (hint != null) hint.alpha = 0f;
        if (radialFill != null) radialFill.fillAmount = 0f;
    }

    /// <summary>¿Hubo un toque corto de ESPACIO desde la última consulta? (lo consume)</summary>
    public bool ConsumeTap()
    {
        bool tap = _tapPending;
        _tapPending = false;
        return tap;
    }

    /// <summary>N1.4: otra cinemática usa este mismo aviso, aunque la anterior se haya saltado (SkipRequested queda en true).</summary>
    public void Rearm()
    {
        SkipRequested = false;
        _held = 0f;
        _tapPending = false;
    }

    /// <summary>Olvida un toque pendiente (ej: uno hecho mientras pasaba la hoja).</summary>
    public void ClearTap() => _tapPending = false;

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        bool active = Armed && !SkipRequested;
        bool holding = false;

        if (active)
        {
            for (int i = 0; i < holdKeys.Length; i++)
                if (Input.GetKey(holdKeys[i])) { holding = true; break; }

            if (Input.GetKeyUp(tapKey) && _held > 0f && _held <= tapMaxSeconds) _tapPending = true;

            if (holding)
            {
                _held += dt;
                if (_held >= holdSeconds)
                {
                    SkipRequested = true;
                    _tapPending = false;
                    Skipped?.Invoke();
                }
            }
            else
            {
                _held = 0f;
            }

            if (Input.anyKeyDown || holding) _hintTimer = hintLinger;
        }
        else
        {
            _held = 0f;
            _hintTimer = 0f;
        }

        _hintTimer -= dt;
        if (hint != null)
        {
            float target = active && (holding || _hintTimer > 0f) ? 1f : 0f;
            hint.alpha = Mathf.MoveTowards(hint.alpha, target, hintFadeSpeed * dt);
        }
        if (radialFill != null) radialFill.fillAmount = SkipRequested ? 1f : Mathf.Clamp01(_held / holdSeconds);
    }
}
