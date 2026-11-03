using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// La "pantalla" del libro 2D (A7): un RawImage a pantalla completa (16:9) que muestra la página actual y hace el pase
/// de página con el shader PlomoYPaja/Intro/PageCurl (rizo cilíndrico de derecha a izquierda, dorso de papel más claro,
/// sombra suave sobre la página siguiente y brillo en el pliegue). También hace el acercamiento final (uvRect).
/// DÓNDE VA: el RawImage "Pagina" del Canvas UI_Libro de Intro.unity (lo arma el builder).
///
/// - Usa una copia propia del material: lo que se toca en Play no ensucia el asset.
/// - Sin material o con un shader que la tarjeta no soporta: respaldo rígido (la página cambia de golpe a mitad
///   del pase, con el sonido). Lo avisa una vez en la consola.
/// - Sin basura por frame: solo SetFloat / SetTexture con ids cacheados.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class PageCurlView : MonoBehaviour
{
    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int NextId = Shader.PropertyToID("_NextTex");
    private static readonly int PaperId = Shader.PropertyToID("_PaperTex");
    private static readonly int AspectId = Shader.PropertyToID("_Aspect");

    [SerializeField] private RawImage image;
    [Tooltip("Proporción de la página (ancho / alto)")]
    [SerializeField] private float aspect = 16f / 9f;

    private Material _material;
    private bool _curl;
    private Texture _next;
    private float _progress;

    public bool HasCurl => _curl;

    private RawImage Image
    {
        get
        {
            if (image == null) image = GetComponent<RawImage>();
            return image;
        }
    }

    private void Awake()
    {
        Material source = Image.material;
        _curl = source != null && source.shader != null && source.shader.isSupported && source.HasProperty(ProgressId);
        if (_curl)
        {
            _material = new Material(source) { name = source.name + " (Play)" };
            _material.SetFloat(AspectId, aspect);
            _material.SetFloat(ProgressId, 0f);
            Image.material = _material;
        }
        else
        {
            Debug.LogWarning("[Intro] Sin el shader del rizo (PlomoYPaja/Intro/PageCurl): el pase de página usa el respaldo rígido.");
            Image.material = null;
        }
    }

    /// <summary>Dorso de la hoja: hoja_papel (en espejo y aclarado por el shader). Sin papel: blanco tintado.</summary>
    public void SetPaper(Texture paper)
    {
        if (_curl) _material.SetTexture(PaperId, paper != null ? paper : Texture2D.whiteTexture);
    }

    /// <summary>Página que se ve (sin pase en curso).</summary>
    public void SetCurrent(Texture page)
    {
        Image.texture = page;
        SetProgress(0f);
    }

    /// <summary>Prepara un pase: la página actual se riza y deja ver "next".</summary>
    public void BeginTurn(Texture next)
    {
        _next = next;
        if (_curl) _material.SetTexture(NextId, next);
        SetProgress(0f);
    }

    /// <summary>0 = página actual entera, 1 = la siguiente entera (llega ya suavizado).</summary>
    public void SetProgress(float progress)
    {
        _progress = Mathf.Clamp01(progress);
        if (_curl)
        {
            _material.SetFloat(ProgressId, _progress);
        }
        else if (_next != null && _progress >= 0.5f)
        {
            Image.texture = _next; // respaldo rígido
        }
    }

    /// <summary>Termina el pase: la siguiente pasa a ser la actual.</summary>
    public void EndTurn()
    {
        if (_next != null) Image.texture = _next;
        _next = null;
        SetProgress(0f);
    }

    /// <summary>Acercamiento (1 = página entera) hacia "center" (0–1 de la página). Solo sin pase en curso.</summary>
    public void SetZoom(Vector2 center, float zoom)
    {
        zoom = Mathf.Max(1f, zoom);
        float size = 1f / zoom;
        float x = Mathf.Clamp(center.x - size * 0.5f, 0f, 1f - size);
        float y = Mathf.Clamp(center.y - size * 0.5f, 0f, 1f - size);
        Image.uvRect = new Rect(x, y, size, size);
    }

    private void OnDestroy()
    {
        if (_material != null) Destroy(_material);
    }
}
