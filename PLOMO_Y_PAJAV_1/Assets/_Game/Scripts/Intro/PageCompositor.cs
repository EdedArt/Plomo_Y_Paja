using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Compone cada página del libro 2D (A7) en un RenderTexture 16:9 (1920×1080): papel de fondo + ilustración arriba con
/// marco de tinta + texto abajo. También compone la apertura (libro cerrado a pantalla completa, con su acercamiento).
/// DÓNDE VA: objeto "Compositor" de Intro.unity, con dos "rigs" (cámara ortográfica + Canvas Screen Space - Camera,
/// capa UI) lejos del origen. Lo arma Plomo y Paja → Intro → Construir escena del libro.
///
/// - Dos RenderTexture que se turnan: la hoja n (0 = apertura, 1..6 = páginas) usa el rig n % 2. Durante un pase de
///   página solo se ven la saliente (n-1) y la entrante (n), así que dos bastan.
/// - Las cámaras de los rigs quedan SIEMPRE encendidas (un Canvas sencillo cada una: barato). Así el texto escrito a
///   máquina (maxVisibleCharacters, sin basura por frame) y el acercamiento de la apertura se ven en vivo.
/// - Sin libro_cerrado: rectángulo rojo oscuro con el título. Sin ilustración: gris con "ILUSTRACIÓN 0N".
///   Sin hoja_papel: papel crema liso.
/// </summary>
public class PageCompositor : MonoBehaviour
{
    /// <summary>Resolución de referencia de los Canvas de los rigs (el builder maqueta en estas unidades).</summary>
    public const float ReferenceWidth = 1920f;
    public const float ReferenceHeight = 1080f;

    [Serializable]
    public class Rig
    {
        public Camera camera;
        public Canvas canvas;
        [Header("Apertura")]
        public GameObject coverGroup;
        public RawImage cover;
        public GameObject coverPlaceholder;
        public TMP_Text coverTitle;
        [Header("Página")]
        public GameObject pageGroup;
        public RawImage paper;
        public RawImage illustration;
        public TMP_Text placeholder;
        public TMP_Text body;
    }

    [SerializeField] private Rig[] rigs = new Rig[0];
    [Tooltip("Tamaño de cada RenderTexture (px). 16:9")]
    [SerializeField] private Vector2Int textureSize = new Vector2Int(1920, 1080);
    [Tooltip("Rectángulo de la ilustración en la página (0–1, origen abajo a la izquierda). Lo fija el builder")]
    [SerializeField] private Rect illustrationRect = new Rect(0.18f, 0.335f, 0.64f, 0.63f);
    [SerializeField] private Color paperFallback = new Color32(0xEE, 0xDF, 0xBF, 0xFF);
    [SerializeField] private Color placeholderColor = new Color32(0x8A, 0x84, 0x7A, 0xFF);

    private RenderTexture[] _targets;
    private int[] _characters;
    private Texture _coverTexture;
    private bool _ready;

    /// <summary>Centro de la ilustración en coordenadas de página (0–1): hacia ahí entra la cámara al final.</summary>
    public Vector2 IllustrationCenter => illustrationRect.center;
    /// <summary>Alto de la ilustración en fracción de la página.</summary>
    public float IllustrationHeight => illustrationRect.height;

    /// <summary>Crea los RenderTexture y pone el papel. Llamar una vez antes de mostrar nada.</summary>
    public void Initialize(Texture paper)
    {
        if (_ready || rigs == null || rigs.Length == 0) return;
        int width = Mathf.Max(256, textureSize.x);
        int height = Mathf.Max(144, textureSize.y);

        _targets = new RenderTexture[rigs.Length];
        _characters = new int[rigs.Length];
        for (int i = 0; i < rigs.Length; i++)
        {
            var rt = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "IntroPagina_" + i,
                useMipMap = true,
                autoGenerateMips = true,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 2,
                wrapMode = TextureWrapMode.Clamp,
            };
            rt.Create();
            _targets[i] = rt;

            Rig r = rigs[i];
            if (r == null) continue;
            if (r.camera != null)
            {
                r.camera.targetTexture = rt;
                r.camera.enabled = true;
            }
            if (r.canvas != null)
            {
                r.canvas.worldCamera = r.camera;
                r.canvas.scaleFactor = width / ReferenceWidth;
            }
            if (r.paper != null)
            {
                r.paper.texture = paper;
                r.paper.color = paper != null ? Color.white : paperFallback;
                r.paper.uvRect = CoverFit(paper, 1f);
            }
        }
        _ready = true;
    }

    /// <summary>RenderTexture donde se compone la hoja n (0 = apertura).</summary>
    public RenderTexture Target(int sheet) => _ready ? _targets[RigOf(sheet)] : null;

    /// <summary>Apertura: el libro cerrado a pantalla completa (cover-fit 16:9), o el reemplazo rojo con el título.</summary>
    public void ShowCover(int sheet, Texture closedBook, string title)
    {
        Rig r = RigFor(sheet);
        if (r == null) return;
        if (r.pageGroup != null) r.pageGroup.SetActive(false);
        if (r.coverGroup != null) r.coverGroup.SetActive(true);
        bool hasArt = closedBook != null;
        _coverTexture = closedBook;
        if (r.cover != null)
        {
            r.cover.gameObject.SetActive(hasArt);
            r.cover.texture = closedBook;
            r.cover.uvRect = CoverFit(closedBook, 1f);
        }
        if (r.coverPlaceholder != null) r.coverPlaceholder.SetActive(!hasArt);
        if (r.coverTitle != null) r.coverTitle.text = title;
        _characters[RigOf(sheet)] = 0;
    }

    /// <summary>Acercamiento de la apertura (1 = sin zoom). Recorta la imagen hacia el centro, sin deformarla.</summary>
    public void SetCoverZoom(int sheet, float zoom)
    {
        Rig r = RigFor(sheet);
        if (r == null) return;
        zoom = Mathf.Max(1f, zoom);
        if (r.cover != null && r.cover.gameObject.activeSelf) r.cover.uvRect = CoverFit(_coverTexture, zoom);
        else if (r.coverPlaceholder != null) r.coverPlaceholder.transform.localScale = new Vector3(zoom, zoom, 1f);
    }

    /// <summary>Página de cuento con el texto oculto (0 caracteres).</summary>
    public void ShowPage(int sheet, Texture illustration, string placeholderLabel, string text)
    {
        Rig r = RigFor(sheet);
        if (r == null) return;
        if (r.coverGroup != null) r.coverGroup.SetActive(false);
        if (r.pageGroup != null) r.pageGroup.SetActive(true);

        bool hasArt = illustration != null;
        if (r.illustration != null)
        {
            r.illustration.texture = illustration;
            r.illustration.color = hasArt ? Color.white : placeholderColor;
            // La ilustración es 16:9 como su marco; si alguna no lo fuera se recorta centrada, sin deformar
            r.illustration.uvRect = CoverFit(illustration, 1f, illustrationRect.width * ReferenceWidth / (illustrationRect.height * ReferenceHeight));
        }
        if (r.placeholder != null)
        {
            r.placeholder.gameObject.SetActive(!hasArt);
            if (!hasArt) r.placeholder.text = placeholderLabel;
        }

        int index = RigOf(sheet);
        _characters[index] = 0;
        if (r.body != null)
        {
            r.body.text = text;
            r.body.maxVisibleCharacters = 0;
            r.body.ForceMeshUpdate();
            _characters[index] = r.body.textInfo.characterCount;
        }
    }

    /// <summary>Caracteres del texto de la hoja (incluye espacios), para la máquina de escribir.</summary>
    public int CharacterCount(int sheet) => _ready ? _characters[RigOf(sheet)] : 0;

    /// <summary>Cuántos caracteres se ven (sin basura: solo cambia maxVisibleCharacters).</summary>
    public void SetVisibleCharacters(int sheet, int count)
    {
        Rig r = RigFor(sheet);
        if (r == null || r.body == null || r.body.maxVisibleCharacters == count) return;
        r.body.maxVisibleCharacters = count;
    }

    private Rig RigFor(int sheet) => _ready ? rigs[RigOf(sheet)] : null;

    private int RigOf(int sheet)
    {
        int n = rigs.Length;
        return ((sheet % n) + n) % n;
    }

    /// <summary>uvRect para que la textura llene un marco 16:9 (o "frameAspect") sin deformarse, recortando centrado.</summary>
    private static Rect CoverFit(Texture texture, float zoom, float frameAspect = ReferenceWidth / ReferenceHeight)
    {
        float w = 1f, h = 1f;
        if (texture != null && texture.height > 0)
        {
            float texAspect = (float)texture.width / texture.height;
            if (texAspect > frameAspect) w = frameAspect / texAspect;
            else h = texAspect / frameAspect;
        }
        w /= zoom;
        h /= zoom;
        return new Rect((1f - w) * 0.5f, (1f - h) * 0.5f, w, h);
    }

    private void OnDestroy()
    {
        if (!_ready) return;
        for (int i = 0; i < _targets.Length; i++)
        {
            if (rigs[i] != null && rigs[i].camera != null) rigs[i].camera.targetTexture = null;
            if (_targets[i] != null)
            {
                _targets[i].Release();
                Destroy(_targets[i]);
            }
        }
        _ready = false;
    }

#if UNITY_EDITOR
    /// <summary>Solo para el builder del Editor.</summary>
    public void EditorSetup(Rig[] newRigs, Vector2Int size, Rect illustration)
    {
        rigs = newRigs;
        textureSize = size;
        illustrationRect = illustration;
    }
#endif
}
