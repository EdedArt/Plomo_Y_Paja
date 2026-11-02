using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// MENÚ: Plomo y Paja → Contador → Crear UI de interrogatorio (fase 3C.3)
/// Crea/actualiza:
///  - Fonts/SpecialElite SDF.asset: TMP desde SpecialElite-Regular.ttf (se busca en Assets/_Game/Fonts y en
///    Assets/TextMesh Pro/Fonts) con ASCII + ¡ ¿ … — – « » y vocales con tilde, ü, ñ. Se genera una vez y queda ESTÁTICA
///    (3C.4.1): en el juego no se genera nada. Si ya está completa (hecha por este menú o a mano con el Font Asset
///    Creator) no se toca. La comprobación lee la tabla de caracteres del asset, no el texto que devuelve TMP.
///    Sin el TTF se usa la fuente por defecto de TMP.
///  - Art/UI/ui_papel_dialogo.png importado como Sprite. Sin el PNG: panel crema con borde oscuro.
///  - Prefabs/UI/InterrogationCanvas.prefab (InterrogationDirector + InterrogationCanvas).
///  - Le asigna el prefab a CentavoInterrogation en Prefabs/Level1/Centavo.prefab (si existe).
/// Idempotente: se puede correr las veces que haga falta. No toca ninguna escena.
/// </summary>
public static class InterrogationUIBuilder
{
    public const string PrefabFolder = "Assets/_Game/Prefabs/UI";
    public const string PrefabPath = PrefabFolder + "/InterrogationCanvas.prefab";
    private const string FontFolder = "Assets/_Game/Fonts";
    private const string FontAssetPath = FontFolder + "/SpecialElite SDF.asset";
    private static readonly string[] FontSources =
    {
        "Assets/_Game/Fonts/SpecialElite-Regular.ttf",
        "Assets/TextMesh Pro/Fonts/SpecialElite-Regular.ttf",
    };
    private const string PaperPath = "Assets/_Game/Art/UI/ui_papel_dialogo.png";
    /// <summary>Bordes del 9-slice del papel en px del PNG (izq, abajo, der, arriba): las orillas rotas no se estiran.</summary>
    private static readonly Vector4 PaperBorder = new Vector4(150f, 110f, 150f, 110f);

    /// <summary>Lo que no es ASCII imprimible (32–126): signos del español y comillas tipográficas que puede escribir la IA.</summary>
    private const string ExtraCharacters = "¡¿…—–«»“”‘’áéíóúüñÁÉÍÓÚÜÑ";
    private const int SamplingPointSize = 56;

    private static readonly Vector2 Reference = new Vector2(1920f, 1080f);
    private static readonly Color Ink = new Color32(0x2A, 0x1F, 0x1A, 0xFF);
    private static readonly Color NameInk = new Color32(0x6B, 0x1E, 0x14, 0xFF);
    private static readonly Color Cream = new Color32(0xF2, 0xE6, 0xC8, 0xFF);
    private static readonly Color DarkBorder = new Color32(0x2A, 0x1A, 0x12, 0xFF);
    private static readonly Color Hover = new Color32(0x6B, 0x3A, 0x1E, 0xFF);

    [MenuItem("Plomo y Paja/Contador/Crear UI de interrogatorio")]
    public static void BuildMenu()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        var report = new StringBuilder();
        bool ok = Build(report);
        AssetDatabase.SaveAssets();
        Debug.Log("[Contador] UI de interrogatorio:\n" + report);
        EditorUtility.DisplayDialog("Crear UI de interrogatorio",
            (ok ? "Listo.\n\n" : "Revisa la consola: hay algo que falta.\n\n") + report, "Entendido");
    }

    /// <summary>Fuente, papel, prefab y enlace con Centavo. false si algo falló (queda en el reporte).</summary>
    public static bool Build(StringBuilder report)
    {
        TMP_FontAsset font = EnsureFont(report);
        Sprite paper = EnsurePaperSprite(report);
        GameObject prefab = BuildPrefab(font, paper, report);
        if (prefab == null) return false;
        LinkToCentavo(prefab, report);
        return true;
    }

    // ==================================================================
    // Fuente
    // ==================================================================

    /// <summary>ASCII imprimible + ExtraCharacters.</summary>
    public static string RequiredCharacters()
    {
        var sb = new StringBuilder();
        for (int c = 32; c < 127; c++) sb.Append((char)c);
        sb.Append(ExtraCharacters);
        return sb.ToString();
    }

    private static TMP_FontAsset EnsureFont(StringBuilder report)
    {
        Font source = FindSourceFont(report);
        TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

        // Lo que el TTF no trae no cuenta como faltante (TMP usa la fuente de respaldo para eso)
        string required = RequiredCharacters();
        bool engineOk = false;
        string notInFont = source != null ? CharactersNotInFont(source, required, report, out engineOk) : "";
        string wanted = Without(required, notInFont);

        // 1. ¿Ya está completa? (hecha por este menú o a mano) → no se toca, solo se deja estática
        if (asset != null && MissingFrom(asset, wanted).Length == 0)
        {
            MakeStatic(asset);
            report.AppendLine($"✓ Fuente Special Elite completa ({asset.characterTable.Count} caracteres), estática: no se regenera");
            return asset;
        }
        if (source == null)
        {
            report.AppendLine("⚠ No está SpecialElite-Regular.ttf: se usa la fuente por defecto de TMP.");
            return asset != null ? asset : TMP_Settings.defaultFontAsset;
        }

        // 2a. FontEngine no abre el TTF: no se toca lo que hay (nunca se vacía una fuente que funciona)
        if (!engineOk)
        {
            string lacking = asset != null ? MissingFrom(asset, wanted) : wanted;
            if (asset != null) MakeStatic(asset);
            report.AppendLine($"✗ No se pudieron generar «{lacking}»: FontEngine no abre el TTF. La fuente actual se deja como está " +
                              "(estática). Hacerla con Window → TextMeshPro → Font Asset Creator (pasos en la auditoría 3C.4.1).");
            return asset != null ? asset : TMP_Settings.defaultFontAsset;
        }

        // 2b. Generar, o completar el MISMO asset (conserva el GUID que usa el canvas y los glifos que ya tiene)
        if (asset == null)
        {
            LevelBuilderUtils.EnsureFolder(FontFolder);
            asset = TMP_FontAsset.CreateFontAsset(source, SamplingPointSize, 5, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, false);
            if (asset == null)
            {
                report.AppendLine("✗ TMP no pudo crear la fuente desde el TTF: se usa la fuente por defecto. Ver la auditoría 3C.4.1 (Font Asset Creator).");
                return TMP_Settings.defaultFontAsset;
            }
            asset.name = "SpecialElite SDF";
            AssetDatabase.CreateAsset(asset, FontAssetPath);
            // Textura y material dentro del mismo .asset (si no, se pierden al cerrar Unity)
            if (asset.atlasTextures != null && asset.atlasTextures.Length > 0 && asset.atlasTextures[0] != null)
            {
                asset.atlasTextures[0].name = "SpecialElite Atlas";
                AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            }
            if (asset.material != null)
            {
                asset.material.name = "SpecialElite Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }
        }
        else
        {
            var so = new SerializedObject(asset);
            SerializedProperty editorRef = so.FindProperty("m_SourceFontFile_EditorRef");
            if (editorRef != null) editorRef.objectReferenceValue = source;
            so.FindProperty("m_SourceFontFile").objectReferenceValue = source;
            so.ApplyModifiedPropertiesWithoutUndo();
            asset.atlasPopulationMode = AtlasPopulationMode.Dynamic; // para poder agregar (vuelve a estática abajo)
        }

        // Solo se agregan los que faltan (TryAddCharacters salta los que ya están). Su texto de "faltantes" no es
        // fiable (si FontEngine falla devuelve TODO el texto pedido): se comprueba con la tabla del asset.
        asset.TryAddCharacters(MissingFrom(asset, wanted), out _);
        string missing = MissingFrom(asset, wanted);
        if (missing.Length == 0)
        {
            MakeStatic(asset);
            report.AppendLine($"✓ Fuente Special Elite generada ({asset.characterTable.Count} caracteres) y estática");
        }
        else
        {
            EditorUtility.SetDirty(asset);
            report.AppendLine($"✗ Special Elite: no se pudieron generar «{missing}». Queda dinámica. " +
                              "Hacerla a mano con Window → TextMeshPro → Font Asset Creator (pasos en la auditoría 3C.4.1).");
        }
        return asset;
    }

    private static Font FindSourceFont(StringBuilder report)
    {
        foreach (string path in FontSources)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (font == null) continue;
            // El TTF tiene que traer sus datos (Include Font Data) para que TMP lo lea
            if (AssetImporter.GetAtPath(path) is TrueTypeFontImporter importer && !importer.includeFontData)
            {
                importer.includeFontData = true;
                importer.SaveAndReimport();
                font = AssetDatabase.LoadAssetAtPath<Font>(path);
                report.AppendLine("⚠ El TTF no tenía Include Font Data: se activó");
            }
            return font;
        }
        return null;
    }

    /// <summary>Caracteres que el TTF no tiene (consulta directa al FontEngine). Vacío si no se pudo abrir el TTF.</summary>
    private static string CharactersNotInFont(Font source, string characters, StringBuilder report, out bool engineOk)
    {
        FontEngineError error = FontEngine.LoadFontFace(source, SamplingPointSize);
        engineOk = error == FontEngineError.Success;
        if (!engineOk)
        {
            report.AppendLine($"⚠ FontEngine no pudo abrir el TTF ({error})");
            return "";
        }
        var missing = new StringBuilder();
        foreach (char c in characters)
            if (!FontEngine.TryGetGlyphWithUnicodeValue(c, GlyphLoadFlags.LOAD_DEFAULT, out _)) missing.Append(c);
        if (missing.Length > 0)
            report.AppendLine($"⚠ El TTF de Special Elite no trae «{missing}» (TMP los dibuja con la fuente de respaldo)");
        return missing.ToString();
    }

    /// <summary>Los caracteres que NO están en la tabla del asset.</summary>
    private static string MissingFrom(TMP_FontAsset asset, string characters)
    {
        var missing = new StringBuilder();
        foreach (char c in characters)
            if (!asset.HasCharacter(c, false, false)) missing.Append(c);
        return missing.ToString();
    }

    private static string Without(string characters, string remove)
    {
        if (string.IsNullOrEmpty(remove)) return characters;
        var sb = new StringBuilder();
        foreach (char c in characters)
            if (remove.IndexOf(c) < 0) sb.Append(c);
        return sb.ToString();
    }

    /// <summary>Estática: el atlas queda guardado tal cual y en el juego no se genera nada (TMP suelta la referencia al TTF).</summary>
    private static void MakeStatic(TMP_FontAsset asset)
    {
        if (asset.atlasPopulationMode != AtlasPopulationMode.Static) asset.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(asset);
        if (asset.atlasTextures != null)
            foreach (Texture2D texture in asset.atlasTextures)
                if (texture != null) EditorUtility.SetDirty(texture);
        if (asset.material != null) EditorUtility.SetDirty(asset.material);
    }

    // ==================================================================
    // Papel
    // ==================================================================

    private static Sprite EnsurePaperSprite(StringBuilder report)
    {
        if (!File.Exists(PaperPath))
        {
            report.AppendLine("⚠ No está Art/UI/ui_papel_dialogo.png: se usa el panel crema con borde.");
            return null;
        }

        // 3C.4: el PNG ya tiene alpha real (Tools/character_key.py --single) y se dibuja en 9-slice
        var importer = AssetImporter.GetAtPath(PaperPath) as TextureImporter;
        if (importer != null && (importer.textureType != TextureImporterType.Sprite ||
                                 importer.spriteImportMode != SpriteImportMode.Single || importer.mipmapEnabled ||
                                 !importer.alphaIsTransparency || importer.spriteBorder != PaperBorder))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.spriteBorder = PaperBorder;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PaperPath);
        report.AppendLine(sprite != null ? "✓ Papel: ui_papel_dialogo.png" : "⚠ ui_papel_dialogo.png no se pudo cargar como Sprite: panel crema.");
        return sprite;
    }

    // ==================================================================
    // Prefab
    // ==================================================================

    private static GameObject BuildPrefab(TMP_FontAsset font, Sprite paper, StringBuilder report)
    {
        LevelBuilderUtils.EnsureFolder(PrefabFolder);
        var root = new GameObject("InterrogationCanvas", typeof(RectTransform));
        try
        {
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300; // encima de la mira (5), el viewmodel y el diálogo de los guardias (100)
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.matchWidthOrHeight = 1f;
            root.AddComponent<GraphicRaycaster>();
            CanvasGroup rootGroup = root.AddComponent<CanvasGroup>();
            InterrogationCanvas view = root.AddComponent<InterrogationCanvas>();
            InterrogationDirector director = root.AddComponent<InterrogationDirector>();

            // Voces 2D que suenan con el juego congelado (D5)
            AudioSource targetVoice = VoiceSource("Voz_Interrogado", root.transform);
            AudioSource playerVoice = VoiceSource("Voz_Wilbur", root.transform);
            LipSyncDriver lipSync = targetVoice.gameObject.AddComponent<LipSyncDriver>();
            Set(lipSync, "voiceSource", targetVoice);

            // Retrato: abajo al centro, cuadrado; alto y posición finales en InterrogationCanvas.ApplyLayout (≈ 95 %)
            RectTransform portraitRect = Rect("Retrato", root.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.78f),
                new Vector2(0.5f, 0f));
            Image portraitImage = portraitRect.gameObject.AddComponent<Image>();
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;
            AspectRatioFitter portraitFit = portraitRect.gameObject.AddComponent<AspectRatioFitter>();
            portraitFit.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            portraitFit.aspectRatio = 1f;
            CanvasGroup portraitGroup = portraitRect.gameObject.AddComponent<CanvasGroup>();
            portraitGroup.blocksRaycasts = false;
            LipSyncImageAdapter mouth = portraitRect.gameObject.AddComponent<LipSyncImageAdapter>();
            Set(mouth, "driver", lipSync);
            InterrogationPortrait portrait = portraitRect.gameObject.AddComponent<InterrogationPortrait>();
            Set(portrait, "mouth", mouth);
            Set(portrait, "group", portraitGroup);

            // Papel del diálogo: tira baja delante del retrato (anclas finales en ApplyLayout)
            RectTransform paperRect = Rect("Papel", root.transform, new Vector2(0.21f, 0.015f), new Vector2(0.79f, 0.015f),
                new Vector2(0.5f, 0f));
            Image paperImage = paperRect.gameObject.AddComponent<Image>();
            paperImage.raycastTarget = false;
            if (paper != null)
            {
                paperImage.sprite = paper;
                paperImage.type = Image.Type.Sliced; // tamaño, márgenes y escala de bordes: InterrogationCanvas.ApplyLayout
            }
            else
            {
                paperImage.color = Cream;
                Outline border = paperRect.gameObject.AddComponent<Outline>();
                border.effectColor = DarkBorder;
                border.effectDistance = new Vector2(6f, -6f);
            }

            TMP_Text speaker = Label("Nombre", paperRect, new Vector2(0.09f, 0.77f), new Vector2(0.91f, 0.92f), font, 36f,
                NameInk, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            TMP_Text line = Label("Texto", paperRect, new Vector2(0.09f, 0.40f), new Vector2(0.91f, 0.77f), font, 31f,
                Ink, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            line.enableAutoSizing = true;
            line.fontSizeMin = 22f;
            line.fontSizeMax = 31f;

            // Opciones (2–3): en la parte baja del papel
            RectTransform options = Rect("Opciones", paperRect, new Vector2(0.08f, 0.07f), new Vector2(0.92f, 0.40f),
                new Vector2(0.5f, 0.5f));
            VerticalLayoutGroup optionsLayout = options.gameObject.AddComponent<VerticalLayoutGroup>();
            optionsLayout.spacing = 4f;
            optionsLayout.childControlHeight = true;
            optionsLayout.childControlWidth = true;
            optionsLayout.childForceExpandHeight = false;
            optionsLayout.childForceExpandWidth = true;
            var optionButtons = new Button[3];
            var optionLabels = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                optionButtons[i] = OptionButton($"Opcion_{i + 1}", options, font, out optionLabels[i]);
            }

            // Decisión: dos botones grandes en el mismo lugar que las opciones
            RectTransform decision = Rect("Decision", paperRect, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.42f),
                new Vector2(0.5f, 0.5f));
            HorizontalLayoutGroup decisionLayout = decision.gameObject.AddComponent<HorizontalLayoutGroup>();
            decisionLayout.spacing = 36f;
            decisionLayout.childControlHeight = true;
            decisionLayout.childControlWidth = true;
            decisionLayout.childForceExpandHeight = true;
            decisionLayout.childForceExpandWidth = true;
            var decisionButtons = new Button[2];
            var decisionLabels = new TMP_Text[2];
            decisionButtons[0] = DecisionButton("Decision_Disparar", decision, font, out decisionLabels[0]);
            decisionButtons[1] = DecisionButton("Decision_Perdonar", decision, font, out decisionLabels[1]);

            // Pensamiento de la IA: banda arriba, cursiva
            RectTransform band = Rect("Pensamiento", root.transform, new Vector2(0.12f, 0.86f), new Vector2(0.88f, 0.97f),
                new Vector2(0.5f, 0.5f));
            Image bandImage = band.gameObject.AddComponent<Image>();
            bandImage.color = new Color(0.05f, 0.04f, 0.08f, 0.62f);
            bandImage.raycastTarget = false;
            CanvasGroup bandGroup = band.gameObject.AddComponent<CanvasGroup>();
            bandGroup.blocksRaycasts = false;
            TMP_Text thought = Label("Texto", band, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f), font, 32f, Cream,
                FontStyles.Italic, TextAlignmentOptions.Center);
            thought.enableAutoSizing = true;
            thought.fontSizeMin = 20f;
            thought.fontSizeMax = 32f;

            // Aviso de pista (registro_01): sobre el papel, aparece y se va
            RectTransform notice = Rect("Aviso_Pista", root.transform, new Vector2(0.25f, 0.31f), new Vector2(0.75f, 0.38f),
                new Vector2(0.5f, 0.5f));
            CanvasGroup noticeGroup = notice.gameObject.AddComponent<CanvasGroup>();
            noticeGroup.blocksRaycasts = false;
            noticeGroup.alpha = 0f;
            Image noticeImage = notice.gameObject.AddComponent<Image>();
            noticeImage.color = new Color(DarkBorder.r, DarkBorder.g, DarkBorder.b, 0.85f);
            noticeImage.raycastTarget = false;
            TMP_Text noticeText = Label("Texto", notice, new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.95f), font, 34f, Cream,
                FontStyles.Bold, TextAlignmentOptions.Center);

            // Referencias
            var so = new SerializedObject(view);
            so.FindProperty("canvas").objectReferenceValue = canvas;
            so.FindProperty("rootGroup").objectReferenceValue = rootGroup;
            so.FindProperty("portrait").objectReferenceValue = portrait;
            so.FindProperty("speakerLabel").objectReferenceValue = speaker;
            so.FindProperty("lineLabel").objectReferenceValue = line;
            SetArray(so.FindProperty("optionButtons"), optionButtons);
            SetArray(so.FindProperty("optionLabels"), optionLabels);
            so.FindProperty("decisionRoot").objectReferenceValue = decision.gameObject;
            SetArray(so.FindProperty("decisionButtons"), decisionButtons);
            SetArray(so.FindProperty("decisionLabels"), decisionLabels);
            so.FindProperty("thoughtGroup").objectReferenceValue = bandGroup;
            so.FindProperty("thoughtLabel").objectReferenceValue = thought;
            so.FindProperty("noticeGroup").objectReferenceValue = noticeGroup;
            so.FindProperty("noticeLabel").objectReferenceValue = noticeText;
            so.ApplyModifiedPropertiesWithoutUndo();
            view.ApplyLayout(); // los valores de diseño de InterrogationCanvas quedan también en el prefab

            Set(director, "view", view);
            Set(director, "targetVoice", targetVoice);
            Set(director, "playerVoice", playerVoice);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            report.AppendLine(prefab != null ? $"✓ Prefab {PrefabPath}" : $"✗ No se pudo guardar {PrefabPath}");
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void LinkToCentavo(GameObject canvasPrefab, StringBuilder report)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ContadorCentavoBuilder.PrefabPath) == null)
        {
            report.AppendLine("⚠ Todavía no hay Centavo.prefab: correr Plomo y Paja → Contador → Colocar Centavo (lo enlaza solo).");
            return;
        }

        // Editar el prefab en su lugar: conserva los IDs (y los overrides de la instancia de la escena)
        GameObject contents = PrefabUtility.LoadPrefabContents(ContadorCentavoBuilder.PrefabPath);
        try
        {
            if (!contents.TryGetComponent(out CentavoInterrogation link)) link = contents.AddComponent<CentavoInterrogation>();
            Set(link, "canvasPrefab", canvasPrefab.GetComponent<InterrogationDirector>());
            PrefabUtility.SaveAsPrefabAsset(contents, ContadorCentavoBuilder.PrefabPath);
            report.AppendLine("✓ Centavo.prefab usa este canvas (CentavoInterrogation)");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // ==================================================================
    // Piezas
    // ==================================================================

    private static AudioSource VoiceSource(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        return source;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5; // UI
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    private static TMP_Text Label(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, TMP_FontAsset font,
        float size, Color color, FontStyles style, TextAlignmentOptions alignment)
    {
        RectTransform rect = Rect(name, parent, anchorMin, anchorMax, new Vector2(0.5f, 0.5f));
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        text.text = "";
        return text;
    }

    private static Button OptionButton(string name, Transform parent, TMP_FontAsset font, out TMP_Text label)
    {
        RectTransform rect = Rect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 40f;
        layout.minHeight = 34f;
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = Hover;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None; // resaltado = tinta (InterrogationOptionHover), sin recuadro
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0f);       // invisible: se lee como texto escrito en el papel
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.28f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.45f);
        colors.selectedColor = new Color(1f, 1f, 1f, 0f);
        colors.fadeDuration = 0.06f;
        button.colors = colors;
        label = Label("Texto", rect, new Vector2(0.02f, 0f), new Vector2(0.98f, 1f), font, 27f, Ink, FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft);
        label.enableAutoSizing = true;
        label.fontSizeMin = 18f;
        label.fontSizeMax = 27f;
        return button;
    }

    private static Button DecisionButton(string name, Transform parent, TMP_FontAsset font, out TMP_Text label)
    {
        RectTransform rect = Rect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f); // escrita en el papel: sin caja (3C.4.1); el fondo invisible recibe el clic
        Button button = rect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;
        label = Label("Texto", rect, Vector2.zero, Vector2.one, font, 52f, Ink, FontStyles.Normal,
            TextAlignmentOptions.Center);
        return button;
    }

    private static void Set(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray(SerializedProperty array, Object[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
