using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Compone cada doble página del libro (papel + ilustración a lo ancho de las dos páginas + texto) en un RenderTexture
/// (enmienda A1). DÓNDE VA: raíz de Prefabs/Intro/StoryBook.prefab, con dos "rigs" (cámara ortográfica + Canvas en
/// Screen Space - Camera, capa UI) lejos del libro. Lo arma Plomo y Paja → Intro → Construir escena del libro.
///
/// - Dos RenderTexture que se turnan: la doble página s usa el rig s % 2. Mientras pasa la hoja s solo se ven la
///   doble página s-1 (saliente) y la s (entrante), así que dos bastan; las caras que usan el rig que se reescribe
///   están tapadas.
/// - La mitad izquierda del RT va al dorso de la hoja s y la derecha al frente de la hoja s+1 (materiales con
///   tiling 0.5 y offset 0 / 0.5): el dibujo sigue continuo a través del lomo y gira con las hojas rígidas.
/// - Las dos cámaras de los rigs quedan SIEMPRE encendidas (dibujan solo un Canvas sencillo: barato). No se apagan para
///   "congelar" el RT porque un Canvas Screen Space - Camera con la cámara apagada puede acabar dibujado como Overlay
///   encima de la pantalla. El texto se escribe con maxVisibleCharacters: sin basura por frame.
/// - Sin ilustración: rectángulo gris con "ILUSTRACIÓN 0N". Sin libro_papel: papel crema liso.
/// </summary>
public class SpreadCompositor : MonoBehaviour
{
    /// <summary>Ancho de referencia del Canvas del rig (el builder maqueta en estas unidades).</summary>
    public const float ReferenceWidth = 2048f;

    [Serializable]
    public class Rig
    {
        public Camera camera;
        public Canvas canvas;
        public RawImage paperLeft;
        public RawImage paperRight;
        public GameObject storyGroup;
        public RawImage illustration;
        public TMP_Text placeholder;
        public TMP_Text body;
        public GameObject titleGroup;
        public TMP_Text title;
        public TMP_Text subtitle;
    }

    [SerializeField] private Rig[] rigs = new Rig[0];
    [Tooltip("Ancho del RenderTexture de cada doble página (px); el alto sale de la proporción del libro")]
    [SerializeField] private int textureWidth = 2048;
    [Tooltip("Material base de las caras de las hojas (URP Lit, iluminado por la lámpara). Se copia por RT y mitad")]
    [SerializeField] private Material pageTemplate;
    [SerializeField] private Color paperFallback = new Color32(0xEE, 0xDF, 0xBF, 0xFF);
    [SerializeField] private Color placeholderColor = new Color32(0x8A, 0x84, 0x7A, 0xFF);

    private RenderTexture[] _targets;
    private Material[] _left;
    private Material[] _right;
    private int[] _characters;
    private bool _ready;

    public int TextureWidth { get; private set; }
    public int TextureHeight { get; private set; }

    /// <summary>Crea los RenderTexture y los materiales de las caras. Llamar antes de StoryBook.BindSpreads.</summary>
    public void Initialize(float spreadAspect, Texture paper)
    {
        if (_ready || rigs == null || rigs.Length == 0) return;
        TextureWidth = Mathf.Max(256, textureWidth);
        TextureHeight = Mathf.Max(128, Mathf.RoundToInt(TextureWidth / Mathf.Max(0.1f, spreadAspect)));

        int count = rigs.Length;
        _targets = new RenderTexture[count];
        _left = new Material[count];
        _right = new Material[count];
        _characters = new int[count];

        for (int i = 0; i < count; i++)
        {
            var rt = new RenderTexture(TextureWidth, TextureHeight, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "IntroSpread_" + i,
                useMipMap = true,
                autoGenerateMips = true,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
                wrapMode = TextureWrapMode.Clamp,
            };
            rt.Create();
            _targets[i] = rt;
            _left[i] = MakeMaterial(rt, 0f, "Spread" + i + "_L");
            _right[i] = MakeMaterial(rt, 0.5f, "Spread" + i + "_R");

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
                r.canvas.scaleFactor = TextureWidth / ReferenceWidth;
            }
            SetPaper(r.paperLeft, paper);
            SetPaper(r.paperRight, paper);
        }
        _ready = true;
    }

    public Material LeftMaterial(int spread) => _ready ? _left[RigOf(spread)] : null;
    public Material RightMaterial(int spread) => _ready ? _right[RigOf(spread)] : null;

    /// <summary>Portadilla (mitad derecha): título y subtítulo.</summary>
    public void ShowTitle(int spread, string title, string subtitle)
    {
        Rig r = RigFor(spread);
        if (r == null) return;
        if (r.storyGroup != null) r.storyGroup.SetActive(false);
        if (r.titleGroup != null) r.titleGroup.SetActive(true);
        if (r.title != null) r.title.text = title;
        if (r.subtitle != null) r.subtitle.text = subtitle;
        _characters[RigOf(spread)] = 0;
    }

    /// <summary>Doble página de cuento con el texto oculto (0 caracteres).</summary>
    public void ShowStory(int spread, Texture illustration, string placeholderLabel, string text)
    {
        Rig r = RigFor(spread);
        if (r == null) return;
        if (r.titleGroup != null) r.titleGroup.SetActive(false);
        if (r.storyGroup != null) r.storyGroup.SetActive(true);

        bool hasArt = illustration != null;
        if (r.illustration != null)
        {
            r.illustration.texture = illustration;
            r.illustration.color = hasArt ? Color.white : placeholderColor;
        }
        if (r.placeholder != null)
        {
            r.placeholder.gameObject.SetActive(!hasArt);
            if (!hasArt) r.placeholder.text = placeholderLabel;
        }

        int index = RigOf(spread);
        _characters[index] = 0;
        if (r.body != null)
        {
            r.body.text = text;
            r.body.maxVisibleCharacters = 0;
            r.body.ForceMeshUpdate();
            _characters[index] = r.body.textInfo.characterCount;
        }
    }

    /// <summary>Caracteres del texto de la doble página (incluye espacios), para el efecto máquina de escribir.</summary>
    public int CharacterCount(int spread) => _ready ? _characters[RigOf(spread)] : 0;

    /// <summary>Cuántos caracteres se ven (sin basura: solo cambia maxVisibleCharacters).</summary>
    public void SetVisibleCharacters(int spread, int count)
    {
        Rig r = RigFor(spread);
        if (r == null || r.body == null || r.body.maxVisibleCharacters == count) return;
        r.body.maxVisibleCharacters = count;
    }

    private Rig RigFor(int spread) => _ready ? rigs[RigOf(spread)] : null;

    private int RigOf(int spread)
    {
        int n = rigs.Length;
        return ((spread % n) + n) % n;
    }

    private void SetPaper(RawImage image, Texture paper)
    {
        if (image == null) return;
        image.texture = paper;
        image.color = paper != null ? Color.white : paperFallback;
    }

    private Material MakeMaterial(RenderTexture rt, float offsetX, string label)
    {
        Material m = pageTemplate != null ? new Material(pageTemplate) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.name = "Intro_" + label;
        var scale = new Vector2(0.5f, 1f);
        var offset = new Vector2(offsetX, 0f);
        if (m.HasProperty("_BaseMap"))
        {
            m.SetTexture("_BaseMap", rt);
            m.SetTextureScale("_BaseMap", scale);
            m.SetTextureOffset("_BaseMap", offset);
        }
        if (m.HasProperty("_MainTex"))
        {
            m.SetTexture("_MainTex", rt);
            m.SetTextureScale("_MainTex", scale);
            m.SetTextureOffset("_MainTex", offset);
        }
        return m;
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
            if (_left[i] != null) Destroy(_left[i]);
            if (_right[i] != null) Destroy(_right[i]);
        }
        _ready = false;
    }

#if UNITY_EDITOR
    /// <summary>Solo para el builder del Editor.</summary>
    public void EditorSetup(Rig[] newRigs, Material template, int width)
    {
        rigs = newRigs;
        pageTemplate = template;
        textureWidth = width;
    }
#endif
}
