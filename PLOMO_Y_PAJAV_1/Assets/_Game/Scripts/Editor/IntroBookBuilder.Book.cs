using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Prefab del libro (INTRO.1): Prefabs/Intro/StoryBook.prefab. Lo llama IntroBookBuilder.BuildMenu.
///
/// Geometría (m, origen = lomo sobre la mesa; X a la derecha, Z hacia el fondo, Y arriba):
///  - Página: alto 0.30, ancho = alto × proporción de libro_papel (1086×1448 → 0.225). Tapa = ancho de página + 1.2 cm,
///    alto según libro_portada (1024×1536 → 0.237 × 0.355).
///  - Contratapa (cuero) y bloque de páginas derecho; la tapa lleva pegado el bloque izquierdo y las guardas (al abrir
///    quedan a la izquierda a la misma altura que el derecho).
///  - 7 hojas: quads de frente y dorso a ±0.2 mm del eje; cada hoja 0.8 mm por encima de la siguiente (sin z-fighting).
///    Al pasar, la bisagra va de leafRightY a leafLeftY: la hoja recién pasada queda arriba de la pila izquierda.
///  - Doble página (A1): 2 rigs con cámara ortográfica + Canvas Screen Space - Camera en la capa UI, 500 m bajo la mesa.
///    Maqueta en unidades de 2048 de ancho (= píxeles del RT): papel en cada mitad (el izquierdo en espejo),
///    ilustración 16:9 arriba (62 % del alto, centrada, marco de tinta de 7 px), banda de texto debajo, centrada.
///  - Anclas de cámara: Cam_Wide (libro cerrado y lámpara), Cam_Spread (libro abierto ≈ 85 % del cuadro),
///    Cam_Push (dentro de la ilustración de la doble página: con A1 la ilustración cruza las dos páginas).
/// </summary>
public static partial class IntroBookBuilder
{
    private const float BookFov = 35f;
    private const float PageHeight = 0.30f;
    private const float CoverThickness = 0.004f;
    private const float BlockHeight = 0.012f;
    private const float LeafGap = 0.0008f;
    private const float FaceOffset = 0.0002f;
    private const int LeafCount = 7;
    private const int RigCount = 2;
    private const int SpreadTextureWidth = 2048;
    private const float IllustrationHeight = 0.62f; // fracción del alto de la doble página
    private const float IllustrationTop = 0.055f;   // margen superior (fracción)
    private const float FramePixels = 7f;

    private static readonly Vector4 NoCrop = new Vector4(1f, 1f, 0f, 0f);
    private static readonly Color Gold = new Color32(0xD9, 0xA8, 0x45, 0xFF);

    private static GameObject BuildBookPrefab(IntroBookData data, TMP_FontAsset font, StringBuilder report)
    {
        LevelBuilderUtils.EnsureFolder(PrefabFolder);

        float pageW = PageHeight * AspectOf(data.paperTexture, 1086f / 1448f);
        float coverW = pageW + 0.012f;
        float coverH = Mathf.Max(PageHeight + 0.012f, coverW / AspectOf(data.coverTexture, 1024f / 1536f));
        float blockTop = CoverThickness + BlockHeight;
        float spreadAspect = 2f * pageW / PageHeight;
        IllustrationRect(data, spreadAspect, out Vector2 illMin, out Vector2 illMax);

        Material leather = LitMaterial("Libro_Cuero", new Color32(0x5E, 0x16, 0x12, 0xFF), null, NoCrop);
        Material edge = LitMaterial("Libro_Canto", new Color32(0xE2, 0xD3, 0xB0, 0xFF), null, NoCrop);
        Material cover = LitMaterial("Libro_Tapa", new Color32(0x7A, 0x1E, 0x18, 0xFF), data.coverTexture,
            CropToFill(data.coverTexture, coverW / coverH));
        Material endpaper = LitMaterial("Libro_Guardas", new Color32(0xC9, 0xA8, 0x6A, 0xFF), data.endpaperTexture,
            CropToFill(data.endpaperTexture, pageW / PageHeight));
        Material paper = LitMaterial("Libro_Papel", Cream, data.paperTexture, CropToFill(data.paperTexture, pageW / PageHeight));
        Material pageTemplate = LitMaterial("Libro_Pagina", Color.white, null, NoCrop);

        var root = new GameObject("StoryBook");
        Transform geometry = Child("Geometria", root.transform, Vector3.zero);

        // Contratapa y bloque derecho
        Box("Contratapa", geometry, new Vector3(coverW * 0.5f, CoverThickness * 0.5f, 0f), new Vector3(coverW, CoverThickness, coverH), leather, 0.0015f);
        Vector3 blockSize = new Vector3(pageW - 0.003f, BlockHeight, PageHeight - 0.004f);
        Box("Bloque_Derecho", geometry, new Vector3(pageW * 0.5f + 0.001f, CoverThickness + BlockHeight * 0.5f, 0f), blockSize, edge, 0.001f);

        // Tapa: cartón + portada + título dorado; debajo, el bloque izquierdo y las guardas
        float coverClosedY = blockTop + (LeafCount + 1) * LeafGap + BlockHeight;
        Transform coverPivot = Child("Tapa", geometry, new Vector3(0f, coverClosedY, 0f));
        Box("Tapa_Carton", coverPivot, new Vector3(coverW * 0.5f, CoverThickness * 0.5f, 0f), new Vector3(coverW, CoverThickness, coverH), leather, 0.0015f);
        Face("Tapa_Portada", coverPivot, coverW, coverH, CoverThickness + FaceOffset, true, cover);
        CoverTitle(coverPivot, data.coverTitle, font, coverW, coverH);
        Box("Bloque_Izquierdo", coverPivot, new Vector3(pageW * 0.5f + 0.001f, -BlockHeight * 0.5f, 0f), blockSize, edge, 0.001f);
        Face("Guardas", coverPivot, pageW, PageHeight, -BlockHeight - FaceOffset, false, endpaper);

        // Lomo: escala Y = alto (cerrado: todo el libro; abierto: hasta el borde de los bloques)
        float spineClosed = coverClosedY + CoverThickness;
        float spineOpen = blockTop;
        Transform spine = Child("Lomo", geometry, Vector3.zero);
        GameObject spineBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
        spineBox.name = "Lomo_Cuero";
        Object.DestroyImmediate(spineBox.GetComponent<Collider>());
        spineBox.transform.SetParent(spine, false);
        spineBox.transform.localPosition = new Vector3(-0.003f, 0.5f, 0f);
        spineBox.transform.localScale = new Vector3(0.008f, 1f, coverH);
        spineBox.GetComponent<MeshRenderer>().sharedMaterial = leather;
        spine.localScale = new Vector3(1f, spineClosed, 1f);

        // Hojas
        var pivots = new Transform[LeafCount];
        var fronts = new Renderer[LeafCount];
        var backs = new Renderer[LeafCount];
        var rightY = new float[LeafCount];
        var leftY = new float[LeafCount];
        for (int k = 0; k < LeafCount; k++)
        {
            rightY[k] = blockTop + (LeafCount - k) * LeafGap; // la hoja 1 arriba de todo
            leftY[k] = blockTop + (k + 1) * LeafGap;          // cada hoja pasada cae encima de la anterior
            pivots[k] = Child("Hoja_" + (k + 1).ToString("00"), geometry, new Vector3(0f, rightY[k], 0f));
            fronts[k] = Face("Frente", pivots[k], pageW, PageHeight, FaceOffset, true, paper);
            backs[k] = Face("Dorso", pivots[k], pageW, PageHeight, -FaceOffset, false, paper);
        }

        // Anclas de cámara
        Transform anchors = Child("Anclas_Camara", root.transform, Vector3.zero);
        float tanHalf = Mathf.Tan(BookFov * 0.5f * Mathf.Deg2Rad);
        const float spreadPitch = 58f;
        float fitWidth = coverW / 0.85f / (tanHalf * 16f / 9f);
        float fitHeight = PageHeight * Mathf.Sin(spreadPitch * Mathf.Deg2Rad) * 0.5f / 0.85f / tanHalf;
        Transform camSpread = Anchor("Cam_Spread", anchors, new Vector3(0f, blockTop, 0f), spreadPitch, 0f, Mathf.Max(fitWidth, fitHeight));
        Transform camWide = Anchor("Cam_Wide", anchors, new Vector3(0.1f, 0.03f, 0.06f), 32f, -14f, 1.05f);
        float illCenterZ = ((illMin.y + illMax.y) * 0.5f - 0.5f) * PageHeight;
        Transform camPush = Anchor("Cam_Push", anchors, new Vector3(0f, blockTop, illCenterZ), 64f, 0f, 0.17f);

        // Doble página en RenderTexture
        Transform rigsRoot = Child("DoblePagina_RT", root.transform, new Vector3(0f, -500f, 0f));
        var rigs = new SpreadCompositor.Rig[RigCount];
        for (int i = 0; i < RigCount; i++)
            rigs[i] = BuildRig(i, rigsRoot, font, spreadAspect, illMin, illMax);

        var compositor = root.AddComponent<SpreadCompositor>();
        compositor.EditorSetup(rigs, pageTemplate, SpreadTextureWidth);

        var storyBook = root.AddComponent<StoryBook>();
        storyBook.EditorSetup(coverPivot, pivots, fronts, backs, spine, coverClosedY, CoverThickness, rightY, leftY,
            spineClosed, spineOpen, spreadAspect, camWide, camSpread, camPush);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
        Object.DestroyImmediate(root);
        int rtHeight = Mathf.RoundToInt(SpreadTextureWidth / spreadAspect);
        report.AppendLine($"✓ Libro: página {pageW * 100f:0.#}×{PageHeight * 100f:0.#} cm, tapa {coverW * 100f:0.#}×{coverH * 100f:0.#} cm, " +
                          $"RT de doble página {SpreadTextureWidth}×{rtHeight} (mitad izquierda → dorso de la hoja i, derecha → frente de la i+1)");
        return ok ? saved : null;
    }

    /// <summary>Rectángulo de la ilustración (anclas normalizadas de la doble página): 16:9, 62 % del alto, centrado.</summary>
    private static void IllustrationRect(IntroBookData data, float spreadAspect, out Vector2 min, out Vector2 max)
    {
        float artAspect = 16f / 9f;
        foreach (IntroBookData.Page page in data.pages)
            if (page.illustration != null) { artAspect = AspectOf(page.illustration, artAspect); break; }

        float refH = SpreadCompositor.ReferenceWidth / spreadAspect;
        float height = IllustrationHeight;
        float widthPx = height * refH * artAspect;
        float maxWidthPx = 0.86f * SpreadCompositor.ReferenceWidth;
        if (widthPx > maxWidthPx)
        {
            widthPx = maxWidthPx;
            height = widthPx / artAspect / refH;
        }
        float halfW = widthPx / SpreadCompositor.ReferenceWidth * 0.5f;
        min = new Vector2(0.5f - halfW, 1f - IllustrationTop - height);
        max = new Vector2(0.5f + halfW, 1f - IllustrationTop);
    }

    private static SpreadCompositor.Rig BuildRig(int index, Transform parent, TMP_FontAsset font, float spreadAspect,
        Vector2 illMin, Vector2 illMax)
    {
        Transform rigRoot = Child("Rig_" + (char)('A' + index), parent, new Vector3(index * 100f, 0f, 0f));
        var rig = new SpreadCompositor.Rig();

        var camGo = new GameObject("Camara");
        camGo.transform.SetParent(rigRoot, false);
        Camera cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 1f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 3f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Cream;
        cam.cullingMask = 1 << UiLayer;
        cam.depth = -10f;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        // Encendida: SpreadCompositor le da su RenderTexture en Play. Sin RT (Editor) dibuja en pantalla con depth -10 y la
        // cámara principal (depth 0, fondo negro) la tapa. Apagada, Unity podría dibujar su Canvas como Overlay.
        cam.enabled = true;
        UniversalAdditionalCameraData urp = cam.GetUniversalAdditionalCameraData();
        urp.renderPostProcessing = false;
        urp.renderShadows = false;
        urp.antialiasing = AntialiasingMode.None;
        urp.requiresDepthOption = CameraOverrideOption.Off;
        urp.requiresColorOption = CameraOverrideOption.Off;
        rig.camera = cam;

        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(rigRoot, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;
        canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal |
                                           AdditionalCanvasShaderChannels.Tangent;
        rig.canvas = canvas;
        Transform c = canvasGo.transform;

        // Papel: una hoja por mitad; la izquierda en espejo para que los bordes oscuros queden afuera
        rig.paperLeft = Raw("Papel_Izquierdo", c, new Vector2(0f, 0f), new Vector2(0.5f, 1f));
        rig.paperLeft.uvRect = new Rect(1f, 0f, -1f, 1f);
        rig.paperRight = Raw("Papel_Derecho", c, new Vector2(0.5f, 0f), new Vector2(1f, 1f));

        // Cuento: ilustración a lo ancho (con marco de tinta) y banda de texto
        RectTransform story = UiChild("Cuento", c, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        rig.storyGroup = story.gameObject;
        RectTransform frame = UiChild("Marco", story, illMin, illMax, new Vector2(-FramePixels, -FramePixels), new Vector2(FramePixels, FramePixels));
        Image frameImage = frame.gameObject.AddComponent<Image>();
        frameImage.color = Ink;
        frameImage.raycastTarget = false;
        rig.illustration = Raw("Ilustracion", story, illMin, illMax);
        rig.placeholder = TmpLabel("Reemplazo", rig.illustration.transform, Vector2.zero, Vector2.one, font, 90f,
            new Color32(0x3A, 0x34, 0x2C, 0xFF), TextAlignmentOptions.Center, "ILUSTRACIÓN 01");
        rig.placeholder.gameObject.SetActive(false);
        rig.body = TmpLabel("Texto", story, new Vector2(0.09f, 0.045f), new Vector2(0.91f, illMin.y - 0.035f), font, 56f, Ink,
            TextAlignmentOptions.Center, "");
        rig.body.lineSpacing = 6f;

        // Portadilla (mitad derecha de la doble página 0)
        RectTransform title = UiChild("Portadilla", c, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        rig.titleGroup = title.gameObject;
        rig.title = TmpLabel("Titulo", title, new Vector2(0.55f, 0.5f), new Vector2(0.95f, 0.74f), font, 118f, Ink,
            TextAlignmentOptions.Center, "Plomo y Paja");
        RectTransform rule = UiChild("Filete", title, new Vector2(0.66f, 0.478f), new Vector2(0.84f, 0.482f), Vector2.zero, Vector2.zero);
        Image ruleImage = rule.gameObject.AddComponent<Image>();
        ruleImage.color = Ink;
        ruleImage.raycastTarget = false;
        rig.subtitle = TmpLabel("Subtitulo", title, new Vector2(0.55f, 0.3f), new Vector2(0.95f, 0.46f), font, 50f, Ink,
            TextAlignmentOptions.Center, "Una historia de los tres cerditos");
        rig.subtitle.fontStyle = FontStyles.Italic;
        title.gameObject.SetActive(false);

        SetLayerRecursive(canvasGo, UiLayer);
        return rig;
    }

    /// <summary>Título dorado "PLOMO Y PAJA" sobre la portada (TMP 3D, se ajusta solo al recuadro).</summary>
    private static void CoverTitle(Transform coverPivot, string text, TMP_FontAsset font, float coverW, float coverH)
    {
        var go = new GameObject("Tapa_Titulo", typeof(RectTransform));
        go.transform.SetParent(coverPivot, false);
        go.transform.localPosition = new Vector3(coverW * 0.5f, CoverThickness + FaceOffset * 3f, coverH * 0.1f);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // tendido sobre la tapa, legible desde la cámara
        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.color = Gold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.05f;
        tmp.fontSizeMax = 4f;
        tmp.characterSpacing = 6f;
        tmp.rectTransform.sizeDelta = new Vector2(coverW * 0.74f, coverH * 0.26f);
        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    /// <summary>Cara de hoja: quad del tamaño de la página, con la bisagra en x = 0. up = mira a +Y (frente).</summary>
    private static Renderer Face(string name, Transform parent, float width, float height, float y, bool up, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(width * 0.5f, y, 0f);
        // El quad de Unity mira a -Z: (90,0,0) lo acuesta mirando a +Y con u → +X y v → +Z.
        // El dorso además gira 180° en Z: mira a -Y y su u va hacia -X (al pasar la hoja queda derecho y sin espejo).
        Quaternion faceUp = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localRotation = up ? faceUp : Quaternion.Euler(0f, 0f, 180f) * faceUp;
        go.transform.localScale = new Vector3(width, height, 1f);
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        return renderer;
    }

    private static Transform Anchor(string name, Transform parent, Vector3 target, float pitch, float yaw, float distance)
    {
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Transform t = Child(name, parent, target - rot * Vector3.forward * distance);
        t.localRotation = rot;
        return t;
    }

    private static Transform Child(string name, Transform parent, Vector3 localPosition)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        return go.transform;
    }

    private static RawImage Raw(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        RectTransform rt = UiChild(name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        RawImage image = rt.gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text TmpLabel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, TMP_FontAsset font,
        float size, Color color, TextAlignmentOptions alignment, string content)
    {
        RectTransform rt = UiChild(name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        text.text = content;
        return text;
    }
}
