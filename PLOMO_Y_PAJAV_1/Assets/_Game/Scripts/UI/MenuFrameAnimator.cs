using UnityEngine;
using UnityEngine.UI;

// Anima el fondo del menu con varios frames.
// Modo Hervido: corta entre frames, estilo caricatura de los 30. Solo se ve
//   bien si los frames son casi identicos.
// Modo Fundido: mezcla suavemente entre frames. Sirve cuando los dibujos
//   tienen diferencias grandes.
// Usa tiempo sin escala para seguir animando con Time.timeScale en 0.
[RequireComponent(typeof(Image))]
public class MenuFrameAnimator : MonoBehaviour
{
    public enum Modo { Hervido, Fundido }

    [SerializeField] private Sprite[] frames;
    [SerializeField] private Modo modo = Modo.Fundido;

    [Header("Hervido")]
    [SerializeField, Range(1f, 24f)] private float fps = 12f;
    [SerializeField, Range(0f, 0.5f)] private float irregularidad = 0.25f;

    [Header("Fundido")]
    [SerializeField, Range(0.2f, 5f)] private float segundosPorFrame = 1.6f;
    [SerializeField, Range(0.1f, 3f)] private float duracionFundido = 0.8f;

    [Header("Respiracion (opcional)")]
    [SerializeField] private bool respirar = true;
    [SerializeField, Range(0f, 0.03f)] private float amplitud = 0.012f;
    [SerializeField, Range(0.1f, 2f)] private float velocidadRespiracion = 0.45f;

    private Image imagenA;      // frame actual
    private Image imagenB;      // frame siguiente, solo en modo Fundido
    private RectTransform rt;
    private int indice;
    private float siguienteCambio;
    private float inicioFundido = -1f;
    private Vector3 escalaBase;

    private void Awake()
    {
        imagenA = GetComponent<Image>();
        rt = GetComponent<RectTransform>();
        escalaBase = rt.localScale;

        if (modo == Modo.Fundido) CrearCapaB();
        if (frames != null && frames.Length > 0) imagenA.sprite = frames[0];

        siguienteCambio = Time.unscaledTime + Intervalo();
    }

    private void CrearCapaB()
    {
        // Capa encima que entra con alfa de 0 a 1 para el fundido.
        var go = new GameObject("FrameFundido", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);

        var r = (RectTransform)go.transform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;

        imagenB = go.GetComponent<Image>();
        imagenB.raycastTarget = false;
        imagenB.color = new Color(1f, 1f, 1f, 0f);
    }

    private float Intervalo()
    {
        if (modo == Modo.Fundido) return segundosPorFrame;

        // Pequena variacion para que no suene a metronomo.
        float baseT = 1f / Mathf.Max(fps, 0.01f);
        return baseT * (1f + Random.Range(-irregularidad, irregularidad));
    }

    private void Update()
    {
        if (frames == null || frames.Length < 2) return;

        float t = Time.unscaledTime;

        if (t >= siguienteCambio)
        {
            siguienteCambio = t + Intervalo();
            int sig = (indice + 1) % frames.Length;

            if (modo == Modo.Hervido)
            {
                indice = sig;
                imagenA.sprite = frames[indice];
            }
            else if (inicioFundido < 0f)
            {
                imagenB.sprite = frames[sig];
                inicioFundido = t;
            }
        }

        if (modo == Modo.Fundido && inicioFundido >= 0f)
        {
            float k = Mathf.Clamp01((t - inicioFundido) / duracionFundido);
            k = k * k * (3f - 2f * k);                     // suavizado
            imagenB.color = new Color(1f, 1f, 1f, k);

            if (k >= 1f)
            {
                indice = (indice + 1) % frames.Length;
                imagenA.sprite = frames[indice];
                imagenB.color = new Color(1f, 1f, 1f, 0f);
                inicioFundido = -1f;
            }
        }

        if (respirar)
        {
            float s = 1f + Mathf.Sin(t * velocidadRespiracion * Mathf.PI * 2f) * amplitud;
            rt.localScale = escalaBase * s;
        }
    }
}
