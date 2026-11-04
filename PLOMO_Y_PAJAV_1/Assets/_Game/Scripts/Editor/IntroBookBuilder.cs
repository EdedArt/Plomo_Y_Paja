using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MENÚ: Plomo y Paja → Intro → Construir escena del libro (INTRO.1.1, enmienda A7: libro 2D a pantalla completa)
/// Crea o actualiza, idempotente (correrlo dos veces deja lo mismo):
///  - Data/Intro/IntroBook.asset: libro_cerrado, hoja_papel y las páginas con los textos EXACTOS del guion §2.
///  - Fonts/IMFellEnglish SDF.asset: TMP estático desde Fonts/IMFellEnglish-Regular.ttf (mismo método que la fuente del
///    interrogatorio). Sin ese TTF se usa Special Elite y se avisa.
///  - Materials/Intro/IntroPageCurl.mat y IntroOldFilm.mat (shaders PlomoYPaja/Intro/PageCurl y /OldFilm).
///  - Assets/Scenes/Intro.unity, regenerada desde cero (EditorSceneManager, nunca YAML a mano):
///    cámara principal (no dibuja nada, solo limpia a negro y escucha), "Compositor" con 2 rigs que componen cada página
///    en un RT 1920×1080, Canvas UI_Libro (la página + el rizo), UI_Fundido, UI_Pelicula (filtro de película vieja),
///    UI_Saltar (aviso) y el director (StoryBookDirector + HoldToSkip + AudioLibraryLoader + 4 fuentes 2D).
///  - Build Settings: Intro (0), NivelPorky (1), el resto detrás; SampleScene se quita de la lista (la escena no se borra).
/// Lo que falte (texturas, audio, fuente) se reemplaza por un placeholder visible y se lista en el informe.
/// </summary>
public static class IntroBookBuilder
{
    public const string ScenePath = "Assets/Scenes/Intro.unity";
    public const string DataFolder = "Assets/_Game/Data/Intro";
    public const string DataPath = DataFolder + "/IntroBook.asset";
    public const string MaterialFolder = "Assets/_Game/Materials/Intro";
    public const string CurlMaterialPath = MaterialFolder + "/IntroPageCurl.mat";
    public const string FilmMaterialPath = MaterialFolder + "/IntroOldFilm.mat";
    private const string CurlShader = "PlomoYPaja/Intro/PageCurl";
    private const string FilmShader = "PlomoYPaja/Intro/OldFilm";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string BookFontTtf = "Assets/_Game/Fonts/IMFellEnglish-Regular.ttf";
    private const string BookFontAsset = "Assets/_Game/Fonts/IMFellEnglish SDF.asset";
    private const string ArtFolder = CharacterSpriteImporter.IntroFolder;
    private const int UiLayer = 5;

    // Maqueta de la página (A7 §3), en fracciones de la página 1920×1080 (origen abajo a la izquierda)
    private static readonly Vector2Int PageSize = new Vector2Int(1920, 1080);
    private const float IllustrationTop = 0.965f;        // margen de 3.5 % arriba
    private const float IllustrationHeight = 0.63f;      // ~68 % superior de la página contando márgenes
    private const float IllustrationMaxWidth = 0.86f;    // tope de ancho; el 16:9 sin recorte queda en 63 % (lo limita el alto)
    private const float InkFramePixels = 6f;
    private const float TextBottom = 0.035f;
    private const float TextTop = 0.315f;
    private const float TextMargin = 0.07f;              // texto a lo ancho del 86 % central
    private const float FellFontSize = 64f;              // narrador_intro_03 (el más largo) = 2 líneas de ≤ 1606 px en 1651
    private const float FallbackFontSize = 58f;          // Special Elite: 3 líneas

    private static readonly Color Ink = new Color32(0x2A, 0x1F, 0x1A, 0xFF);
    private static readonly Color Cream = new Color32(0xF2, 0xE6, 0xC8, 0xFF);
    private static readonly Color PaperFallback = new Color32(0xEE, 0xDF, 0xBF, 0xFF);
    private static readonly Color CoverRed = new Color32(0x4E, 0x12, 0x10, 0xFF);
    private static readonly Color Gold = new Color32(0xD9, 0xA8, 0x45, 0xFF);

    // Guion §2 (v1.1). Copiados EXACTOS: no cambiar sin cambiar el guion.
    private static readonly string[] PageTexts =
    {
        "Érase una vez, en la ciudad de Porcópolis, tres hermanos cerditos.",
        "Wilbur, el mayor, trabajaba de sol a sol. Su casa era de ladrillo, firme como su palabra.",
        "Max, el del medio, trabajaba… a ratos. Lo que ganaba se le iba en joyas, y su casa de madera brillaba más de lo que debía.",
        "Y Porky, el menor… Porky tenía un problema: los dados, las cartas y las deudas.",
        "Su casa de paja nunca resistió mucho. Y una noche de lluvia, ya no había casa. Ni Porky.",
        "Wilbur tomó su escopeta. Alguien iba a pagar por esto. Lo que no sabía era quién le debía a quién.",
    };

    [MenuItem("Plomo y Paja/Intro/Construir escena del libro")]
    public static void BuildMenu()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var report = new StringBuilder();
        var missing = new List<string>();

        TMP_FontAsset font = EnsureBookFont(report, missing, out float bodySize);
        IntroBookData data = BuildData(missing);
        CheckAudio(data, missing);
        Material curl = ShaderMaterial(CurlMaterialPath, CurlShader, report, missing);
        Material film = ShaderMaterial(FilmMaterialPath, FilmShader, report, missing);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BuildScene(scene, data, font, bodySize, curl, film, report);
        UpdateBuildSettings(report);
        AssetDatabase.SaveAssets();

        report.AppendLine(missing.Count == 0
            ? "✓ No falta ningún asset de la intro."
            : "⚠ Faltan (se usa placeholder): " + string.Join(", ", missing));
        Debug.Log("[Intro] Escena del libro:\n" + report);
        EditorUtility.DisplayDialog("Construir escena del libro", "Listo: " + ScenePath + "\n\n" + report, "Entendido");
    }

    // ==================================================================
    // Fuente, datos, materiales
    // ==================================================================

    /// <summary>IM Fell English estática; si falta el TTF o no se puede generar, Special Elite (y se avisa).</summary>
    private static TMP_FontAsset EnsureBookFont(StringBuilder report, List<string> missing, out float bodySize)
    {
        if (File.Exists(BookFontTtf))
        {
            TMP_FontAsset fell = InterrogationUIBuilder.EnsureFont(report, new[] { BookFontTtf }, BookFontAsset, "IM Fell English");
            if (fell != null && fell != TMP_Settings.defaultFontAsset && AssetDatabase.GetAssetPath(fell) == BookFontAsset)
            {
                bodySize = FellFontSize;
                return fell;
            }
            report.AppendLine("⚠ IM Fell English no se pudo generar: el libro usa Special Elite.");
        }
        else
        {
            report.AppendLine("⚠ No está " + BookFontTtf + ": el libro usa Special Elite.");
        }
        missing.Add("IMFellEnglish-Regular.ttf (respaldo: Special Elite)");
        bodySize = FallbackFontSize;
        return InterrogationUIBuilder.EnsureFont(report);
    }

    private static IntroBookData BuildData(List<string> missing)
    {
        LevelBuilderUtils.EnsureFolder(DataFolder);
        var data = AssetDatabase.LoadAssetAtPath<IntroBookData>(DataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<IntroBookData>();
            AssetDatabase.CreateAsset(data, DataPath);
        }

        data.coverTitle = "PLOMO Y PAJA";
        data.closedBookTexture = FindTexture("libro_cerrado", missing);
        data.paperTexture = FindTexture("hoja_papel", missing);

        var pages = new IntroBookData.Page[PageTexts.Length];
        for (int i = 0; i < pages.Length; i++)
        {
            string n = (i + 1).ToString("00");
            pages[i] = new IntroBookData.Page
            {
                clipId = "narrador_intro_" + n,
                text = PageTexts[i],
                illustration = FindTexture("ilus_intro_" + n, missing),
                illustrationLabel = "ILUSTRACIÓN " + n,
            };
        }
        data.pages = pages;
        data.openSfx = "sfx_libro_abrir";
        data.pageSfx = "sfx_pagina";
        data.music = "musica_intro";
        data.roomAmbience = "amb_intro_cuarto";
        data.rain = "lluvia_loop";
        data.nextScene = Path.GetFileNameWithoutExtension(LevelBuilder_NivelPorky.ScenePath);
        EditorUtility.SetDirty(data);
        return data;
    }

    /// <summary>Textura de Art/Intro por nombre de archivo (cualquier extensión). Null + "faltante" si no está.</summary>
    private static Texture2D FindTexture(string name, List<string> missing)
    {
        if (AssetDatabase.IsValidFolder(ArtFolder))
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Texture2D", new[] { ArtFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
        }
        missing.Add(name);
        return null;
    }

    private static void CheckAudio(IntroBookData data, List<string> missing)
    {
        AudioLibrary library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AudioLibraryBuilder.LibraryPath);
        if (library == null)
        {
            missing.Add("AudioLibrary.asset (correr Reconstruir Librería de Audio)");
            return;
        }
        foreach (IntroBookData.Page page in data.pages)
            if (!library.ContainsVoice(page.clipId)) missing.Add(page.clipId);
        foreach (string id in new[] { data.openSfx, data.pageSfx, data.music, data.roomAmbience, data.rain })
            if (!library.ContainsSfx(id)) missing.Add(id);
    }

    /// <summary>Material con un shader propio (crea o actualiza). Null si el shader no está o no compila.</summary>
    private static Material ShaderMaterial(string path, string shaderName, StringBuilder report, List<string> missing)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null || !shader.isSupported)
        {
            report.AppendLine("✗ Shader " + shaderName + " no encontrado o no soportado (¿errores en la consola?).");
            missing.Add(shaderName + " (respaldo sin efecto)");
            return null;
        }
        LevelBuilderUtils.EnsureFolder(MaterialFolder);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ==================================================================
    // Escena
    // ==================================================================

    private static void BuildScene(Scene scene, IntroBookData data, TMP_FontAsset font, float bodySize, Material curl,
        Material film, StringBuilder report)
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.fog = false;

        BuildMainCamera();
        PageCompositor compositor = BuildCompositor(font, bodySize);
        PageCurlView view = BuildDisplay(curl, out RectTransform page);
        ScreenFade fade = BuildFade();
        BuildFilm(film, page);
        BuildSkipHint(font, out CanvasGroup hint, out Image radial);
        BuildDirector(data, compositor, view, fade, hint, radial);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        report.AppendLine("✓ Escena " + ScenePath + " (libro 2D, páginas de " + PageSize.x + "×" + PageSize.y + ")");
    }

    /// <summary>La cámara principal no dibuja nada: limpia a negro (bandas si la pantalla no es 16:9) y escucha el audio.</summary>
    private static void BuildMainCamera()
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        Camera cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.cullingMask = 0;
        cam.orthographic = true;
        cam.depth = 0f;
        go.AddComponent<AudioListener>();
        UniversalAdditionalCameraData urp = cam.GetUniversalAdditionalCameraData();
        urp.renderPostProcessing = false;
        urp.antialiasing = AntialiasingMode.None;
    }

    // ------------------------------------------------------------------
    // Compositor: 2 rigs (cámara ortográfica + Canvas Screen Space - Camera) que pintan cada página en un RT
    // ------------------------------------------------------------------

    private static PageCompositor BuildCompositor(TMP_FontAsset font, float bodySize)
    {
        var root = new GameObject("Compositor");
        root.transform.position = new Vector3(0f, -500f, 0f); // lejos de todo
        var rigs = new PageCompositor.Rig[2];
        for (int i = 0; i < rigs.Length; i++) rigs[i] = BuildRig(i, root.transform, font, bodySize);

        PageCompositor compositor = root.AddComponent<PageCompositor>();
        compositor.EditorSetup(rigs, PageSize, IllustrationRect());
        EditorUtility.SetDirty(compositor);
        return compositor;
    }

    /// <summary>Ilustración 16:9 sin recorte: el alto manda; el ancho nunca pasa del 86 %.</summary>
    private static Rect IllustrationRect()
    {
        float pageAspect = (float)PageSize.x / PageSize.y;
        float height = IllustrationHeight;
        float width = height * (16f / 9f) / pageAspect;
        if (width > IllustrationMaxWidth)
        {
            width = IllustrationMaxWidth;
            height = width * pageAspect / (16f / 9f);
        }
        return new Rect(0.5f - width * 0.5f, IllustrationTop - height, width, height);
    }

    private static PageCompositor.Rig BuildRig(int index, Transform parent, TMP_FontAsset font, float bodySize)
    {
        var rigGo = new GameObject("Rig_" + index);
        rigGo.transform.SetParent(parent, false);
        rigGo.transform.localPosition = new Vector3(index * 100f, 0f, 0f); // separados: ninguna cámara ve el otro Canvas
        rigGo.layer = UiLayer;

        var camGo = new GameObject("Camara");
        camGo.transform.SetParent(rigGo.transform, false);
        camGo.layer = UiLayer;
        Camera cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 10f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = PaperFallback;
        cam.cullingMask = 1 << UiLayer;
        cam.depth = -10f;
        UniversalAdditionalCameraData urp = cam.GetUniversalAdditionalCameraData();
        urp.renderPostProcessing = false;
        urp.antialiasing = AntialiasingMode.None;
        urp.renderShadows = false;

        var canvasGo = new GameObject("Lienzo", typeof(RectTransform));
        canvasGo.transform.SetParent(rigGo.transform, false);
        canvasGo.layer = UiLayer;
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;
        Transform c = canvasGo.transform;

        var rig = new PageCompositor.Rig { camera = cam, canvas = canvas };

        // --- Apertura: libro_cerrado a pantalla completa (o rojo oscuro con el título) ---
        RectTransform coverGroup = UiChild("Apertura", c, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        rig.coverGroup = coverGroup.gameObject;
        rig.cover = Raw("LibroCerrado", coverGroup, Vector2.zero, Vector2.one);
        RectTransform placeholder = UiChild("LibroCerrado_Reemplazo", coverGroup, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image red = placeholder.gameObject.AddComponent<Image>();
        red.color = CoverRed;
        red.raycastTarget = false;
        rig.coverPlaceholder = placeholder.gameObject;
        rig.coverTitle = Label("Titulo", placeholder, new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.7f), font, 150f, Gold);
        rig.coverTitle.text = "PLOMO Y PAJA";
        placeholder.gameObject.SetActive(false);

        // --- Página: papel + marco de tinta + ilustración + texto ---
        RectTransform pageGroup = UiChild("Pagina", c, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        rig.pageGroup = pageGroup.gameObject;
        rig.paper = Raw("Papel", pageGroup, Vector2.zero, Vector2.one);
        rig.paper.color = PaperFallback;

        Rect ill = IllustrationRect();
        RectTransform frame = UiChild("MarcoTinta", pageGroup, ill.min, ill.max,
            new Vector2(-InkFramePixels, -InkFramePixels), new Vector2(InkFramePixels, InkFramePixels));
        Image frameImage = frame.gameObject.AddComponent<Image>();
        frameImage.color = Ink;
        frameImage.raycastTarget = false;
        rig.illustration = Raw("Ilustracion", pageGroup, ill.min, ill.max);
        rig.placeholder = Label("Ilustracion_Reemplazo", rig.illustration.transform, Vector2.zero, Vector2.one, font, 72f, Cream);
        rig.placeholder.gameObject.SetActive(false);

        rig.body = Label("Texto", pageGroup, new Vector2(TextMargin, TextBottom), new Vector2(1f - TextMargin, TextTop),
            font, bodySize, Ink);
        rig.body.enableWordWrapping = true;
        rig.body.overflowMode = TextOverflowModes.Overflow;
        rig.body.lineSpacing = 0f;
        pageGroup.gameObject.SetActive(false);

        SetLayerRecursive(rigGo, UiLayer);
        return rig;
    }

    // ------------------------------------------------------------------
    // Pantalla: la página (con el rizo) a 16:9, fundido, película, aviso de saltar
    // ------------------------------------------------------------------

    internal static Canvas OverlayCanvas(string name, int order)
    {
        var go = new GameObject(name);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static PageCurlView BuildDisplay(Material curl, out RectTransform page)
    {
        Canvas canvas = OverlayCanvas("UI_Libro", 0);
        RectTransform frame = UiChild("Marco16x9", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var fitter = frame.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = (float)PageSize.x / PageSize.y;

        RawImage image = Raw("Pagina", frame, Vector2.zero, Vector2.one);
        image.color = Color.white;
        if (curl != null) image.material = curl;
        page = image.rectTransform;
        PageCurlView view = image.gameObject.AddComponent<PageCurlView>();
        SetRef(view, "image", image);
        return view;
    }

    internal static ScreenFade BuildFade()
    {
        Canvas canvas = OverlayCanvas("UI_Fundido", 50);
        RectTransform black = UiChild("Fundido", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image blackImage = black.gameObject.AddComponent<Image>();
        blackImage.color = Color.black;
        blackImage.raycastTarget = false;
        black.gameObject.AddComponent<CanvasGroup>().alpha = 1f;
        return black.gameObject.AddComponent<ScreenFade>();
    }

    private static void BuildFilm(Material film, RectTransform weaveTarget)
    {
        Canvas canvas = OverlayCanvas("UI_Pelicula", 60);
        RectTransform rect = UiChild("Pelicula", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;
        if (film != null) image.material = film;
        OldFilmFilter filter = rect.gameObject.AddComponent<OldFilmFilter>();
        SetRef(filter, "graphic", image);
        SetRef(filter, "weaveTarget", weaveTarget);
    }

    /// <summary>Aviso "Mantén ESPACIO para saltar" con anillo radial (igual que INTRO.1), encima de la película.</summary>
    internal static void BuildSkipHint(TMP_FontAsset font, out CanvasGroup hint, out Image radial)
    {
        Canvas canvas = OverlayCanvas("UI_Saltar", 70);
        RectTransform skip = UiChild("Saltar", canvas.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-520f, 40f), new Vector2(-40f, 120f));
        hint = skip.gameObject.AddComponent<CanvasGroup>();
        hint.alpha = 0f;
        hint.blocksRaycasts = false;

        Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        RectTransform ring = UiChild("Anillo", skip, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-72f, -36f), new Vector2(0f, 36f));
        Image ringImage = ring.gameObject.AddComponent<Image>();
        ringImage.sprite = knob;
        ringImage.color = new Color(1f, 1f, 1f, 0.22f);
        ringImage.raycastTarget = false;

        RectTransform fill = UiChild("Relleno", ring, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
        radial = fill.gameObject.AddComponent<Image>();
        radial.sprite = knob;
        radial.color = Cream;
        radial.type = Image.Type.Filled;
        radial.fillMethod = Image.FillMethod.Radial360;
        radial.fillOrigin = (int)Image.Origin360.Top;
        radial.fillClockwise = true;
        radial.fillAmount = 0f;
        radial.raycastTarget = false;

        TMP_Text text = Label("Texto", skip, Vector2.zero, Vector2.one, font, 30f, Cream);
        ((RectTransform)text.transform).offsetMax = new Vector2(-88f, 0f);
        text.text = "Mantén ESPACIO para saltar";
        text.alignment = TextAlignmentOptions.MidlineRight;
        text.enableWordWrapping = false;
    }

    private static void BuildDirector(IntroBookData data, PageCompositor compositor, PageCurlView view, ScreenFade fade,
        CanvasGroup hint, Image radial)
    {
        var go = new GameObject("IntroDirector");

        // La AudioLibrary se registra aquí (en los niveles la registra el Player)
        var loader = go.AddComponent<AudioLibraryLoader>();
        SetRef(loader, "library", AudioLibraryBuilder.GetOrCreateLibrary());

        var skip = go.AddComponent<HoldToSkip>();
        SetRef(skip, "hint", hint);
        SetRef(skip, "radialFill", radial);

        AudioSource narrator = AudioChild(go.transform, "Narrador");
        AudioSource music = AudioChild(go.transform, "Musica");
        AudioSource ambience = AudioChild(go.transform, "Ambiente");
        AudioSource rain = AudioChild(go.transform, "Lluvia");

        var director = go.AddComponent<StoryBookDirector>();
        var so = new SerializedObject(director);
        so.FindProperty("data").objectReferenceValue = data;
        so.FindProperty("compositor").objectReferenceValue = compositor;
        so.FindProperty("view").objectReferenceValue = view;
        so.FindProperty("fade").objectReferenceValue = fade;
        so.FindProperty("skip").objectReferenceValue = skip;
        so.FindProperty("narratorSource").objectReferenceValue = narrator;
        so.FindProperty("musicSource").objectReferenceValue = music;
        so.FindProperty("ambienceSource").objectReferenceValue = ambience;
        so.FindProperty("rainSource").objectReferenceValue = rain;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static AudioSource AudioChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f; // 2D
        source.dopplerLevel = 0f;
        return source;
    }

    // ==================================================================
    // Build Settings
    // ==================================================================

    private static void UpdateBuildSettings(StringBuilder report)
    {
        report.AppendLine("Build Settings antes: " + Describe(new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes)));
        BuildSceneOrder.Ensure(report);
        report.AppendLine("Build Settings después: " + Describe(new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes)));
    }

    private static string Describe(List<EditorBuildSettingsScene> scenes)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < scenes.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(i).Append('=').Append(Path.GetFileNameWithoutExtension(scenes[i].path));
            if (!scenes[i].enabled) sb.Append(" (off)");
        }
        return sb.Length > 0 ? sb.ToString() : "(vacío)";
    }

    // ==================================================================
    // Utilidades
    // ==================================================================

    internal static RectTransform UiChild(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return rt;
    }

    private static RawImage Raw(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        RectTransform rt = UiChild(name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        RawImage image = rt.gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        return image;
    }

    internal static TMP_Text Label(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, TMP_FontAsset font,
        float size, Color color)
    {
        RectTransform rt = UiChild(name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    internal static void SetRef(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
    }
}
