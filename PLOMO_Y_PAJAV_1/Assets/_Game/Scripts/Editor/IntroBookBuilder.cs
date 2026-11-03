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
/// MENÚ: Plomo y Paja → Intro → Construir escena del libro (fase INTRO.1)
/// Crea o actualiza, idempotente (correrlo dos veces deja lo mismo):
///  - Data/Intro/IntroBook.asset: páginas con los textos EXACTOS del guion §2, ids de audio y texturas de Art/Intro.
///  - Materials/Intro/*.mat (URP Lit) y Settings/IntroVignetteProfile.asset (viñeta más fuerte encima del perfil del juego).
///  - Prefabs/Intro/StoryBook.prefab: libro 3D (ver IntroBookBuilder.Book.cs) + SpreadCompositor (doble página en RT, A1).
///  - Assets/Scenes/Intro.unity, regenerada desde cero (EditorSceneManager, nunca YAML a mano): mesa, lámpara de aceite
///    con parpadeo (LightFlicker de 3A), luz fría de relleno, el libro, la cámara, el Volume, el fundido, el aviso de
///    saltar y el director (StoryBookDirector + HoldToSkip + AudioLibraryLoader + 4 fuentes 2D).
///  - Build Settings: Intro en el índice 0 y NivelPorky en el 1 (el resto, detrás, en su orden).
/// Lo que falte (texturas, audio) se reemplaza por un placeholder visible y se lista en el informe.
/// </summary>
public static partial class IntroBookBuilder
{
    public const string ScenePath = "Assets/Scenes/Intro.unity";
    public const string PrefabFolder = "Assets/_Game/Prefabs/Intro";
    public const string PrefabPath = PrefabFolder + "/StoryBook.prefab";
    public const string DataFolder = "Assets/_Game/Data/Intro";
    public const string DataPath = DataFolder + "/IntroBook.asset";
    public const string MaterialFolder = "Assets/_Game/Materials/Intro";
    public const string VignetteProfilePath = "Assets/_Game/Settings/IntroVignetteProfile.asset";
    private const string ArtFolder = CharacterSpriteImporter.IntroFolder;
    private const string LitShader = "Universal Render Pipeline/Lit";
    private const int UiLayer = 5;

    private static readonly Color Ink = new Color32(0x2A, 0x1F, 0x1A, 0xFF);
    private static readonly Color Cream = new Color32(0xF2, 0xE6, 0xC8, 0xFF);

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
        CartoonKit.BeginSession();

        TMP_FontAsset font = InterrogationUIBuilder.EnsureFont(report);
        IntroBookData data = BuildData(missing);
        CheckAudio(data, missing);

        // Escena nueva primero: el prefab se arma dentro de ella (nada temporal cae en la escena que estaba abierta)
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject prefab = BuildBookPrefab(data, font, report);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Construir escena del libro", "No se pudo guardar el prefab del libro.\n\n" + report, "Entendido");
            return;
        }
        BuildScene(scene, data, prefab, font, report);
        UpdateBuildSettings(report);
        AssetDatabase.SaveAssets();

        report.AppendLine(missing.Count == 0
            ? "✓ No falta ningún asset de la intro."
            : "⚠ Faltan (se usa placeholder): " + string.Join(", ", missing));
        Debug.Log("[Intro] Escena del libro:\n" + report);
        EditorUtility.DisplayDialog("Construir escena del libro", "Listo: " + ScenePath + "\n\n" + report, "Entendido");
    }

    // ==================================================================
    // Datos
    // ==================================================================

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
        data.titlePageTitle = "Plomo y Paja";
        data.titlePageSubtitle = "Una historia de los tres cerditos";
        data.coverTexture = FindTexture("libro_portada", missing);
        data.endpaperTexture = FindTexture("libro_guardas", missing);
        data.paperTexture = FindTexture("libro_papel", missing);

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

    // ==================================================================
    // Escena
    // ==================================================================

    private static void BuildScene(Scene scene, IntroBookData data, GameObject bookPrefab, TMP_FontAsset font, StringBuilder report)
    {
        // Luz ambiente: noche, casi negro azulado (la lámpara hace el trabajo)
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.05f, 0.055f, 0.08f);
        RenderSettings.fog = false;

        var room = new GameObject("Cuarto").transform;
        BuildTable(room);
        BuildLamp(room, new Vector3(0.43f, 0f, 0.24f));
        BuildFillLight(room);

        var book = (GameObject)PrefabUtility.InstantiatePrefab(bookPrefab, scene);
        book.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var storyBook = book.GetComponent<StoryBook>();
        var compositor = book.GetComponent<SpreadCompositor>();

        Camera cam = BuildCamera(storyBook);
        BuildVolumes();
        BuildOverlay(font, out ScreenFade fade, out CanvasGroup hint, out Image radial);
        BuildDirector(data, storyBook, compositor, cam, fade, hint, radial);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        report.AppendLine("✓ Escena " + ScenePath + " y prefab " + PrefabPath);
    }

    private static void BuildTable(Transform parent)
    {
        Material wood = CartoonKit.M("MaderaOscura");
        Material top = CartoonKit.M("Madera");
        Box("Mesa", parent, new Vector3(0.1f, -0.035f, 0.1f), new Vector3(1.6f, 0.07f, 1.0f), top, 0.012f);
        float legH = 0.72f;
        foreach (Vector2 c in new[] { new Vector2(-0.6f, -0.33f), new Vector2(0.8f, -0.33f), new Vector2(-0.6f, 0.53f), new Vector2(0.8f, 0.53f) })
            Box("Pata", parent, new Vector3(c.x, -0.07f - legH * 0.5f, c.y), new Vector3(0.07f, legH, 0.07f), wood, 0.01f);
        Box("Piso", parent, new Vector3(0f, -0.07f - legH - 0.025f, 0f), new Vector3(6f, 0.05f, 6f), wood, 0.01f);
    }

    /// <summary>Lámpara de aceite (latón + tubo de vidrio cálido) con luz puntual cálida, sombras suaves y parpadeo.</summary>
    private static void BuildLamp(Transform parent, Vector3 basePos)
    {
        Mesh mesh = CartoonKit.GetMesh("IntroLamparaAceite", b =>
        {
            b.Submesh = 0; // latón: pie, depósito y collar
            CartoonKit.AddLathe(b, Vector3.zero, Quaternion.identity,
                new[] { 0f, 0.012f, 0.03f, 0.05f, 0.075f, 0.09f, 0.1f, 0.115f },
                new[] { 0.06f, 0.06f, 0.042f, 0.05f, 0.056f, 0.04f, 0.03f, 0.03f }, 16);
            b.Submesh = 1; // tubo de vidrio encendido
            CartoonKit.AddLathe(b, Vector3.zero, Quaternion.identity,
                new[] { 0.115f, 0.14f, 0.17f, 0.2f, 0.25f },
                new[] { 0.027f, 0.04f, 0.043f, 0.03f, 0.022f }, 16, false, false);
        });
        GameObject lamp = CartoonKit.MeshObject("LamparaAceite", mesh, new[] { CartoonKit.M("Laton"), CartoonKit.M("VidrioCalido") },
            parent, basePos, Quaternion.identity);
        lamp.transform.localPosition = basePos;

        Light light = LevelBuilderUtils.CreatePointLight("LamparaAceite_Luz", basePos + new Vector3(0f, 0.165f, 0f),
            new Color(1f, 0.72f, 0.42f), 2.2f, 2.6f, parent);
        light.transform.localPosition = basePos + new Vector3(0f, 0.165f, 0f);
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.85f;
        light.shadowNearPlane = 0.05f;

        // Reutiliza el parpadeo de 3A (LightFlicker), afinado para llama de aceite: temblor suave, sin apagones
        var flicker = light.gameObject.AddComponent<LightFlicker>();
        var so = new SerializedObject(flicker);
        so.FindProperty("noiseAmount").floatValue = 0.1f;
        so.FindProperty("noiseSpeed").floatValue = 2.5f;
        so.FindProperty("dipChancePerSecond").floatValue = 0.25f;
        so.FindProperty("dipMinMultiplier").floatValue = 0.78f;
        so.FindProperty("dipDuration").vector2Value = new Vector2(0.08f, 0.3f);
        so.FindProperty("offChancePerSecond").floatValue = 0f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildFillLight(Transform parent)
    {
        var go = new GameObject("Relleno_Frio");
        go.transform.SetParent(parent, false);
        go.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
        Light light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.55f, 0.65f, 0.95f);
        light.intensity = 0.14f;
        light.shadows = LightShadows.None;
    }

    private static Camera BuildCamera(StoryBook book)
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        Camera cam = go.AddComponent<Camera>();
        cam.fieldOfView = BookFov; // el director nunca lo cambia
        cam.nearClipPlane = 0.02f;
        cam.farClipPlane = 30f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.cullingMask = ~(1 << UiLayer); // los rigs de doble página (capa UI) no se ven desde aquí
        cam.depth = 0f;
        go.AddComponent<AudioListener>();
        UniversalAdditionalCameraData urp = cam.GetUniversalAdditionalCameraData();
        urp.renderPostProcessing = true;
        urp.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        if (book != null && book.CamWide != null)
            go.transform.SetPositionAndRotation(book.CamWide.position, book.CamWide.rotation);
        return cam;
    }

    /// <summary>Volume del juego (Technicolor) + una viñeta más fuerte en un perfil propio con más prioridad.</summary>
    private static void BuildVolumes()
    {
        var root = new GameObject("PostProceso").transform;

        var global = new GameObject("Volume_Technicolor");
        global.transform.SetParent(root, false);
        Volume volume = global.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.sharedProfile = NoirVolumeBuilder.GetOrCreateTechnicolorProfile();

        var vignetteGo = new GameObject("Volume_IntroVineta");
        vignetteGo.transform.SetParent(root, false);
        Volume vignetteVolume = vignetteGo.AddComponent<Volume>();
        vignetteVolume.isGlobal = true;
        vignetteVolume.priority = 2f;
        vignetteVolume.sharedProfile = GetOrCreateVignetteProfile();
    }

    private static VolumeProfile GetOrCreateVignetteProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VignetteProfilePath);
        if (profile == null)
        {
            LevelBuilderUtils.EnsureFolder(Path.GetDirectoryName(VignetteProfilePath).Replace('\\', '/'));
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VignetteProfilePath);
        }
        if (!profile.TryGet(out Vignette vignette))
        {
            vignette = profile.Add<Vignette>();
            AssetDatabase.AddObjectToAsset(vignette, profile);
        }
        vignette.intensity.Override(0.5f);
        vignette.smoothness.Override(0.5f);
        vignette.color.Override(Color.black);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    /// <summary>Canvas Overlay propio: fundido a negro y aviso "Mantén ESPACIO para saltar" con anillo radial.</summary>
    private static void BuildOverlay(TMP_FontAsset font, out ScreenFade fade, out CanvasGroup hint, out Image radial)
    {
        var canvasGo = new GameObject("UI_Intro");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform black = UiChild("Fundido", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image blackImage = black.gameObject.AddComponent<Image>();
        blackImage.color = Color.black;
        blackImage.raycastTarget = false;
        black.gameObject.AddComponent<CanvasGroup>().alpha = 1f;
        fade = black.gameObject.AddComponent<ScreenFade>();

        RectTransform skip = UiChild("Saltar", canvasGo.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
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

        RectTransform label = UiChild("Texto", skip, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-88f, 0f));
        TextMeshProUGUI text = label.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = "Mantén ESPACIO para saltar";
        text.fontSize = 30f;
        text.color = Cream;
        text.alignment = TextAlignmentOptions.MidlineRight;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
    }

    private static void BuildDirector(IntroBookData data, StoryBook book, SpreadCompositor compositor, Camera cam,
        ScreenFade fade, CanvasGroup hint, Image radial)
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
        so.FindProperty("book").objectReferenceValue = book;
        so.FindProperty("compositor").objectReferenceValue = compositor;
        so.FindProperty("viewCamera").objectReferenceValue = cam;
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
        var before = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        report.AppendLine("Build Settings antes: " + Describe(before));

        var list = new List<EditorBuildSettingsScene>();
        foreach (EditorBuildSettingsScene s in before)
            if (s.path != ScenePath && s.path != LevelBuilder_NivelPorky.ScenePath) list.Add(s);

        list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        if (File.Exists(LevelBuilder_NivelPorky.ScenePath))
            list.Insert(1, new EditorBuildSettingsScene(LevelBuilder_NivelPorky.ScenePath, true));
        else
            report.AppendLine("⚠ No existe " + LevelBuilder_NivelPorky.ScenePath + ": el libro usará el índice 1 de Build Settings.");

        EditorBuildSettings.scenes = list.ToArray();
        report.AppendLine("Build Settings después: " + Describe(list));
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

    /// <summary>Caja redondeada del kit sin collider, colocada en coordenadas LOCALES del padre.</summary>
    private static GameObject Box(string name, Transform parent, Vector3 localCenter, Vector3 size, Material mat, float bevel)
    {
        GameObject go = CartoonKit.Box(name, Vector3.zero, size, mat, parent, default, bevel, false);
        go.transform.localPosition = localCenter;
        go.transform.localRotation = Quaternion.identity;
        return go;
    }

    private static RectTransform UiChild(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
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

    private static void SetRef(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Material URP Lit del libro (crea o actualiza). Con textura, el color base queda blanco.</summary>
    private static Material LitMaterial(string name, Color color, Texture texture, Vector4 st)
    {
        LevelBuilderUtils.EnsureFolder(MaterialFolder);
        string path = MaterialFolder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find(LitShader);
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        var scale = new Vector2(st.x, st.y);
        var offset = new Vector2(st.z, st.w);
        Color baseColor = texture != null ? Color.white : color;
        foreach (string tex in new[] { "_BaseMap", "_MainTex" })
        {
            if (!mat.HasProperty(tex)) continue;
            mat.SetTexture(tex, texture);
            mat.SetTextureScale(tex, scale);
            mat.SetTextureOffset(tex, offset);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
        if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 0f);
        if (mat.HasProperty("_EnvironmentReflections")) mat.SetFloat("_EnvironmentReflections", 0f);
        mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>Tiling/offset para que una textura llene un quad sin deformarse (recorta lo que sobra, centrado).</summary>
    private static Vector4 CropToFill(Texture texture, float quadAspect)
    {
        if (texture == null || texture.height == 0) return new Vector4(1f, 1f, 0f, 0f);
        float texAspect = (float)texture.width / texture.height;
        if (texAspect > quadAspect)
        {
            float s = quadAspect / texAspect;
            return new Vector4(s, 1f, (1f - s) * 0.5f, 0f);
        }
        float v = texAspect / quadAspect;
        return new Vector4(1f, v, 0f, (1f - v) * 0.5f);
    }

    private static float AspectOf(Texture texture, float fallback) =>
        texture != null && texture.height > 0 ? (float)texture.width / texture.height : fallback;

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
    }
}
