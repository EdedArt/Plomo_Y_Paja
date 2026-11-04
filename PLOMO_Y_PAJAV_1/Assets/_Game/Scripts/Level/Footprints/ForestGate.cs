using System.Collections;
using UnityEngine;

/// <summary>
/// El portón del bosque (N1.4, N9): cerrado con cadena hasta que FootprintsSceneDirector lo abre.
/// DÓNDE VA: en la raíz de Gate_Bosque dentro de NivelPorky (lo agrega Plomo y Paja → Nivel 1 → Colocar huellas).
/// Al abrirse: la cadena y el candado caen al barro, las hojas giran hacia el bosque y se apaga la caja de la raíz; los postes
/// conservan la suya. Las hojas no tienen collider y no son estáticas (Nivel1MapBuilder las deja fuera del grupo estático).
/// Sin amb_bosque el ambiente no suena (AudioLibrary avisa una vez).
/// </summary>
public class ForestGate : MonoBehaviour
{
    [Header("Piezas (las pone el builder)")]
    [SerializeField] private Transform leafLeft;
    [SerializeField] private Transform leafRight;
    [SerializeField] private Transform chain;
    [SerializeField] private Transform padlock;
    [SerializeField] private BoxCollider blocker;
    [SerializeField] private AudioSource forestAmbience;

    [Header("Apertura")]
    [Tooltip("Grados que gira cada hoja hacia el bosque")]
    [SerializeField] private float openAngle = 100f;
    [SerializeField] private float openSeconds = 0.6f;
    [SerializeField] private float chainFallSeconds = 0.3f;
    [Tooltip("Altura del piso respecto de la raíz del portón: ahí queda la cadena")]
    [SerializeField] private float groundHeight = 0.03f;

    [Header("Audio")]
    [SerializeField] private string chainBreakId = "sfx_cadena_rota";
    [SerializeField] private string gateOpenId = "sfx_reja_abrir";
    [SerializeField] private string ambienceId = "amb_bosque";
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.7f;

    private Quaternion _leftClosed, _rightClosed;

    public bool IsOpen { get; private set; }

    /// <summary>Punto al que mira la cámara antes del disparo.</summary>
    public Vector3 ChainPoint
    {
        get
        {
            if (padlock != null && padlock.TryGetComponent(out Renderer r)) return r.bounds.center;
            return transform.position + Vector3.up;
        }
    }

    private void Awake()
    {
        if (leafLeft != null) _leftClosed = leafLeft.localRotation;
        if (leafRight != null) _rightClosed = leafRight.localRotation;
    }

    /// <summary>Disparo a la cadena: se rompe y se abre en openSeconds, con su sonido. El disparo lo suena quien llama.</summary>
    public void BreakChainAndOpen()
    {
        if (IsOpen) return;
        IsOpen = true;
        if (blocker != null) blocker.enabled = false;
        AudioLibrary.PlaySfx2D(chainBreakId);
        AudioLibrary.PlaySfx2D(gateOpenId);
        StartAmbience();
        StartCoroutine(Fall(chain));
        StartCoroutine(Fall(padlock));
        StartCoroutine(Swing());
    }

    /// <summary>Estado final sin animar ni sonar (saltar la escena, o ya se vio en esta sesión). Idempotente.</summary>
    public void OpenInstant()
    {
        StopAllCoroutines();
        IsOpen = true;
        if (blocker != null) blocker.enabled = false;
        SetSwing(1f);
        DropNow(chain);
        DropNow(padlock);
        StartAmbience();
    }

    private IEnumerator Swing()
    {
        for (float t = 0f; t < openSeconds; t += Time.unscaledDeltaTime)
        {
            SetSwing(CinematicEase.InOut(t / openSeconds));
            yield return null;
        }
        SetSwing(1f);
    }

    private void SetSwing(float amount)
    {
        // Las hojas crecen desde la bisagra hacia el centro; con estos signos se abren hacia +z (el bosque)
        if (leafLeft != null) leafLeft.localRotation = _leftClosed * Quaternion.Euler(0f, -openAngle * amount, 0f);
        if (leafRight != null) leafRight.localRotation = _rightClosed * Quaternion.Euler(0f, openAngle * amount, 0f);
    }

    private IEnumerator Fall(Transform piece)
    {
        if (piece == null) yield break;
        Vector3 from = piece.position;
        Vector3 to = from + Vector3.down * DropDistance(piece);
        for (float t = 0f; t < chainFallSeconds; t += Time.unscaledDeltaTime)
        {
            float u = t / chainFallSeconds;
            piece.position = Vector3.Lerp(from, to, u * u); // cae acelerando
            yield return null;
        }
        piece.position = to;
    }

    private void DropNow(Transform piece)
    {
        if (piece != null) piece.position += Vector3.down * DropDistance(piece);
    }

    /// <summary>Cuánto le falta para tocar el piso. Sus mallas tienen el pivote en la raíz: se mide con los bounds, no con la posición.</summary>
    private float DropDistance(Transform piece)
    {
        if (!piece.TryGetComponent(out Renderer r)) return 0f;
        return Mathf.Max(0f, r.bounds.min.y - (transform.position.y + groundHeight));
    }

    private void StartAmbience()
    {
        if (forestAmbience == null || forestAmbience.isPlaying) return;
        AudioClip clip = AudioLibrary.GetSfx(ambienceId);
        if (clip == null) return;
        forestAmbience.clip = clip;
        forestAmbience.loop = true;
        forestAmbience.volume = ambienceVolume * AudioLibrary.SfxVolume;
        forestAmbience.Play();
    }
}
