using System.Collections;
using UnityEngine;

/// <summary>
/// Libro 3D de la intro (INTRO.1, I2): tapa y 7 hojas RÍGIDAS con la bisagra en el lomo. Cada bisagra gira sobre su eje
/// Z local de 0° (a la derecha) a 180° (a la izquierda); no hay mallas que se doblen.
/// PREFAB: Prefabs/Intro/StoryBook.prefab (lo arma Plomo y Paja → Intro → Construir escena del libro; no editar a mano).
///
/// - Hoja 1 = portadilla (frente). Hoja k (1–6): su dorso muestra la mitad IZQUIERDA de la doble página k y el frente de
///   la hoja k+1 la mitad DERECHA (enmienda A1). Los materiales los pone BindSpreads con los RenderTexture del
///   SpreadCompositor; la hoja 7 queda con su dorso de papel (nunca se ve).
/// - Al girar, la altura de la bisagra pasa de la pila derecha a la izquierda (cada hoja cae encima de la anterior:
///   sin z-fighting), con un pequeño arco hacia arriba y un leve rebote al asentarse.
/// - La tapa lleva pegado el bloque de páginas izquierdo y las guardas; el lomo baja de alto al abrir.
/// - Tiempo real (unscaled): no le afecta el timeScale.
/// </summary>
public class StoryBook : MonoBehaviour
{
    [Header("Bisagras (las arma el builder)")]
    [SerializeField] private Transform coverPivot;
    [SerializeField] private Transform[] leafPivots = new Transform[0];
    [SerializeField] private Renderer[] leafFronts = new Renderer[0];
    [SerializeField] private Renderer[] leafBacks = new Renderer[0];
    [Tooltip("Pivote del lomo (escala Y = alto)")]
    [SerializeField] private Transform spine;

    [Header("Alturas del eje (m, locales al libro)")]
    [SerializeField] private float coverClosedY = 0.04f;
    [SerializeField] private float coverOpenY = 0.004f;
    [SerializeField] private float[] leafRightY = new float[0];
    [SerializeField] private float[] leafLeftY = new float[0];
    [SerializeField] private float spineClosedHeight = 0.04f;
    [SerializeField] private float spineOpenHeight = 0.016f;

    [Header("Movimiento")]
    [Tooltip("Cuánto sube la hoja a mitad del giro (m)")]
    [SerializeField] private float turnLift = 0.015f;
    [SerializeField] private float coverLift = 0.01f;
    [Tooltip("Grados que se pasa antes de asentarse")]
    [SerializeField] private float overshootDegrees = 1.5f;

    [Header("Doble página")]
    [Tooltip("Ancho / alto de la doble página abierta (2 × página). Sale de libro_papel")]
    [SerializeField] private float spreadAspect = 1.5f;

    [Header("Anclas de cámara")]
    [SerializeField] private Transform camWide;
    [SerializeField] private Transform camSpread;
    [SerializeField] private Transform camPush;

    public Transform CamWide => camWide;
    public Transform CamSpread => camSpread;
    public Transform CamPush => camPush;
    public float SpreadAspect => spreadAspect;
    public int LeafCount => leafPivots != null ? leafPivots.Length : 0;

    private void Awake()
    {
        // Estado inicial: libro cerrado (por si el prefab se guardó a medio abrir)
        if (coverPivot != null) Pose(coverPivot, 0f, coverClosedY);
        for (int i = 0; i < LeafCount; i++)
            if (leafPivots[i] != null && i < leafRightY.Length) Pose(leafPivots[i], 0f, leafRightY[i]);
        SetSpine(0f);
    }

    /// <summary>
    /// Pone los materiales de doble página: frente de la hoja k (0-based) = mitad derecha de la doble página k
    /// (k = 0 es la portadilla), dorso de la hoja k = mitad izquierda de la doble página k+1.
    /// </summary>
    public void BindSpreads(SpreadCompositor compositor)
    {
        if (compositor == null) return;
        for (int k = 0; k < LeafCount; k++)
        {
            if (k < leafFronts.Length && leafFronts[k] != null)
                leafFronts[k].sharedMaterial = compositor.RightMaterial(k);
            bool backShowsSpread = k + 1 < LeafCount; // el dorso de la última hoja nunca se ve: queda de papel
            if (backShowsSpread && k < leafBacks.Length && leafBacks[k] != null)
                leafBacks[k].sharedMaterial = compositor.LeftMaterial(k + 1);
        }
    }

    /// <summary>Abre la tapa (0 → 180°) con entrada y salida suaves; el lomo baja a la altura del libro abierto.</summary>
    public IEnumerator OpenCover(float duration)
    {
        if (coverPivot == null) yield break;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float u = t / duration;
            float e = CinematicEase.InOut(u);
            Pose(coverPivot, 180f * e, Mathf.Lerp(coverClosedY, coverOpenY, e) + coverLift * Mathf.Sin(Mathf.PI * u));
            SetSpine(e);
            yield return null;
        }
        Pose(coverPivot, 180f, coverOpenY);
        SetSpine(1f);
    }

    /// <summary>Pasa la hoja index (0-based) de derecha a izquierda: arco, leve rebote y asentado.</summary>
    public IEnumerator TurnLeaf(int index, float duration)
    {
        if (index < 0 || index >= LeafCount || leafPivots[index] == null) yield break;
        Transform pivot = leafPivots[index];
        float fromY = index < leafRightY.Length ? leafRightY[index] : pivot.localPosition.y;
        float toY = index < leafLeftY.Length ? leafLeftY[index] : fromY;

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float u = t / duration;
            float y = Mathf.Lerp(fromY, toY, CinematicEase.InOut(u)) + turnLift * Mathf.Sin(Mathf.PI * u);
            Pose(pivot, TurnAngle(u), y);
            yield return null;
        }
        Pose(pivot, 180f, toY);
    }

    /// <summary>0 → 180 + rebote hasta el 80 % del tiempo y vuelta a 180. El arco sigue alto mientras se pasa.</summary>
    private float TurnAngle(float u)
    {
        const float split = 0.8f;
        float peak = 180f + overshootDegrees;
        if (u < split) return peak * CinematicEase.InOut(u / split);
        return Mathf.Lerp(peak, 180f, CinematicEase.Out((u - split) / (1f - split)));
    }

    private void SetSpine(float open01)
    {
        if (spine == null) return;
        Vector3 s = spine.localScale;
        s.y = Mathf.Lerp(spineClosedHeight, spineOpenHeight, open01);
        spine.localScale = s;
    }

    private static void Pose(Transform pivot, float angle, float y)
    {
        pivot.localRotation = Quaternion.Euler(0f, 0f, angle);
        Vector3 p = pivot.localPosition;
        p.y = y;
        pivot.localPosition = p;
    }

#if UNITY_EDITOR
    /// <summary>Solo para el builder del Editor.</summary>
    public void EditorSetup(Transform cover, Transform[] leaves, Renderer[] fronts, Renderer[] backs, Transform spinePivot,
        float coverClosed, float coverOpen, float[] rightY, float[] leftY, float spineClosed, float spineOpen, float aspect,
        Transform wide, Transform spread, Transform push)
    {
        coverPivot = cover;
        leafPivots = leaves;
        leafFronts = fronts;
        leafBacks = backs;
        spine = spinePivot;
        coverClosedY = coverClosed;
        coverOpenY = coverOpen;
        leafRightY = rightY;
        leafLeftY = leftY;
        spineClosedHeight = spineClosed;
        spineOpenHeight = spineOpen;
        spreadAspect = aspect;
        camWide = wide;
        camSpread = spread;
        camPush = push;
    }
#endif
}
