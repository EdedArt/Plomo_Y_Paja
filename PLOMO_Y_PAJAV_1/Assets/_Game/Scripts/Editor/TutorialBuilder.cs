using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Fase N1.3: tarjeta "Pensamientos", etiquetas de teclas y TutorialDirector en NivelPorky.
/// MENÚ: Plomo y Paja → Nivel 1 → Colocar tutorial (también lo corre Generar Nivel Porky). Idempotente: rehace "--- TUTORIAL ---".
/// Órdenes de canvas: HUD 4, mira 5, pensamiento 10, franjas 8, subtítulos de cine 45, fundido 50, diálogo 100, interrogatorio 300.
/// La tarjeta va en 40 (encima del HUD, debajo de cine e interrogatorio). Las etiquetas van en 5: entre HUD 4 y mira 5 no cabe un
/// entero; comparten orden con la mira, pero están abajo al centro y no se pisan. Papel: Art/UI/ui_papel_dialogo.png si está
/// como Sprite (no se toca su importación, es del interrogatorio); si no, panel crema con borde.
/// </summary>
public static class TutorialBuilder
{
    private const string RootName = "--- TUTORIAL ---";
    private const string PaperPath = "Assets/_Game/Art/UI/ui_papel_dialogo.png";
    private const int CardOrder = 40;
    private const int ChipsOrder = 5;
    private const int ChipCount = 3;

    private static readonly Color Ink = new Color32(0x2A, 0x1F, 0x1A, 0xFF);
    private static readonly Color Cream = new Color32(0xF2, 0xE6, 0xC8, 0xFF);
    private static readonly Color DarkBorder = new Color32(0x2A, 0x1A, 0x12, 0xFF);
    private static readonly Color NoteGray = new Color32(0x6E, 0x66, 0x5C, 0xFF);
    private static readonly Color BodyBrown = new Color32(0x3B, 0x2A, 0x1E, 0xFF);
    private static readonly Color StampRed = new Color32(0xB0, 0x1E, 0x1E, 0xFF);

    // Tarjeta ilustrada (tut_pensamientos, recortada 1500x884): zona de papel vacía, a la derecha. Medido sobre la imagen
    // recortada: papel x 770..1450 (0.513..0.967), y 240..835 desde arriba (0.272..0.945). Rectángulos normalizados con y hacia
    // ARRIBA y con margen interior. Cuerpo / nota gris / "Clic para continuar".
    private static readonly Vector2 BodyMin = new Vector2(0.540f, 0.250f), BodyMax = new Vector2(0.940f, 0.700f);
    private static readonly Vector2 NoteMin = new Vector2(0.540f, 0.145f), NoteMax = new Vector2(0.940f, 0.240f);
    private static readonly Vector2 CloseMin = new Vector2(0.540f, 0.070f), CloseMax = new Vector2(0.940f, 0.135f);
    private const float CardScreenHeight = 0.70f * 1080f; // 70 % del alto de referencia (1920x1080, match 0.5)

    // Guion §2, T1 (textos exactos)
    private const string CardTitle = "PENSAMIENTOS";
    private const string CardBody =
        "Lo que aparece arriba de la pantalla es lo que piensa Wilbur.\n\n" +
        "Esos pensamientos los escribe una inteligencia artificial en el momento, según lo que has visto y lo que has hecho. Por eso cada partida piensa distinto.\n\n" +
        "A veces te ayudan. A veces se equivocan. A veces te alejan de la verdad.\n\n" +
        "Tú decides a quién creerle.";
    private const string CardNote = "Necesita internet. Sin conexión, Wilbur usa pensamientos guardados.";
    private const string CardClose = "Clic para continuar";

    [MenuItem("Plomo y Paja/Nivel 1/Colocar tutorial")]
    public static void BuildMenu()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        if (!File.Exists(LevelBuilder_NivelPorky.ScenePath))
        {
            EditorUtility.DisplayDialog("Colocar tutorial", $"No existe {LevelBuilder_NivelPorky.ScenePath}. Primero: Plomo y Paja → Generar Nivel Porky.", "Entendido");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != LevelBuilder_NivelPorky.ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(LevelBuilder_NivelPorky.ScenePath, OpenSceneMode.Single);
        }

        var report = new StringBuilder();
        BuildInto(report);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Nivel 1] Tutorial:\n" + report);
        EditorUtility.DisplayDialog("Colocar tutorial", report + "\nEscena guardada.", "Entendido");
    }

    /// <summary>Arma todo en la escena abierta. Lo llaman el menú y Generar Nivel Porky.</summary>
    public static void BuildInto(StringBuilder report)
    {
        GameObject old = GameObject.Find("/" + RootName);
        if (old != null) Object.DestroyImmediate(old);
        Transform root = LevelBuilderUtils.CreateGroup(RootName);

        TutorialCardImporter.ImportAll(report); // recorta e importa las tarjetas ilustradas que haya; las que falten, plan B
        TMP_FontAsset font = InterrogationUIBuilder.EnsureFont(report);
        Sprite paper = AssetDatabase.LoadAssetAtPath<Sprite>(PaperPath);
        if (paper == null) report.AppendLine("⚠ No está " + PaperPath + " como Sprite: tarjeta y etiquetas usan un panel crema.");

        Sprite thoughtsArt = TutorialCardImporter.Load(TutorialCardImporter.Pensamientos);
        BuildCard(root, font, paper, thoughtsArt, out GameObject cardRoot, out CanvasGroup closeHint);
        BuildChips(root, font, paper, out CanvasGroup chipsGroup, out TMP_Text[] chipTexts, out GameObject chipRow, out CardRefs cards);

        var go = new GameObject("TutorialDirector");
        go.transform.SetParent(root, false);
        var so = new SerializedObject(go.AddComponent<TutorialDirector>());
        so.FindProperty("cardRoot").objectReferenceValue = cardRoot;
        so.FindProperty("cardCloseHint").objectReferenceValue = closeHint;
        so.FindProperty("chipsGroup").objectReferenceValue = chipsGroup;
        so.FindProperty("chipRow").objectReferenceValue = chipRow;
        so.FindProperty("cardRow").objectReferenceValue = cards.row;
        SetObjects(so.FindProperty("cardRects"), cards.rects);
        SetObjects(so.FindProperty("cardImages"), cards.images);
        SetObjects(so.FindProperty("stampRects"), cards.stamps);
        SetObjects(so.FindProperty("moveCardSprites"), TutorialCardImporter.LoadAll(TutorialCardImporter.MoveCards));
        SetObjects(so.FindProperty("weaponCardSprites"), TutorialCardImporter.LoadAll(TutorialCardImporter.WeaponCards));
        SetObjects(so.FindProperty("revolverCardSprites"), TutorialCardImporter.LoadAll(TutorialCardImporter.RevolverCards));
        SerializedProperty texts = so.FindProperty("chipTexts");
        texts.arraySize = chipTexts.Length;
        for (int i = 0; i < chipTexts.Length; i++) texts.GetArrayElementAtIndex(i).objectReferenceValue = chipTexts[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        report.AppendLine($"✓ Tutorial: tarjeta (canvas {CardOrder}), {ChipCount} etiquetas (canvas {ChipsOrder}), TutorialDirector");
    }

    private struct CardRefs
    {
        public RectTransform row;
        public RectTransform[] rects, stamps;
        public Image[] images;
    }

    private static void SetObjects<T>(SerializedProperty array, T[] items) where T : Object
    {
        array.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    /// <summary>Tarjeta "Pensamientos" ilustrada: el título ya está en la imagen; el texto va en el papel vacío de la derecha.</summary>
    private static void BuildIllustratedCard(Canvas canvas, TMP_FontAsset font, Sprite art, out CanvasGroup closeHint)
    {
        float height = CardScreenHeight;
        float width = height * art.rect.width / art.rect.height;
        RectTransform card = IntroBookBuilder.UiChild("Tarjeta", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-width * 0.5f, -height * 0.5f), new Vector2(width * 0.5f, height * 0.5f));
        Image image = card.gameObject.AddComponent<Image>();
        image.sprite = art;
        image.preserveAspect = true;
        image.raycastTarget = false;

        TMP_Text body = IntroBookBuilder.Label("Texto", card, BodyMin, BodyMax, font, 30f, BodyBrown);
        body.enableWordWrapping = true;
        body.alignment = TextAlignmentOptions.TopLeft;
        body.enableAutoSizing = true;
        body.fontSizeMin = 12f;
        body.fontSizeMax = 32f;
        body.overflowMode = TextOverflowModes.Truncate;
        body.text = CardBody;

        TMP_Text note = IntroBookBuilder.Label("Nota", card, NoteMin, NoteMax, font, 20f, NoteGray);
        note.enableWordWrapping = true;
        note.alignment = TextAlignmentOptions.Left;
        note.enableAutoSizing = true;
        note.fontSizeMin = 10f;
        note.fontSizeMax = 20f;
        note.overflowMode = TextOverflowModes.Truncate;
        note.text = CardNote;

        TMP_Text close = IntroBookBuilder.Label("Cerrar", card, CloseMin, CloseMax, font, 26f, BodyBrown);
        close.enableAutoSizing = true;
        close.fontSizeMin = 12f;
        close.fontSizeMax = 26f;
        close.overflowMode = TextOverflowModes.Truncate;
        close.text = CardClose;
        closeHint = close.gameObject.AddComponent<CanvasGroup>();
        closeHint.alpha = 0f;
        closeHint.blocksRaycasts = false;
    }

    private static void BuildCard(Transform root, TMP_FontAsset font, Sprite paper, Sprite art, out GameObject cardRoot, out CanvasGroup closeHint)
    {
        Canvas canvas = IntroBookBuilder.OverlayCanvas("UI_Tutorial_Tarjeta", CardOrder);
        canvas.transform.SetParent(root, false);
        cardRoot = canvas.gameObject;

        RectTransform dim = IntroBookBuilder.UiChild("Oscurecer", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image dimImage = dim.gameObject.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, art != null ? 0.6f : 0.55f);
        dimImage.raycastTarget = false;

        if (art != null)
        {
            BuildIllustratedCard(canvas, font, art, out closeHint);
            cardRoot.SetActive(false); // la prende TutorialDirector
            return;
        }

        RectTransform panel = IntroBookBuilder.UiChild("Papel", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-590f, -370f), new Vector2(590f, 370f));
        PaperImage(panel.gameObject, paper, 1f);

        TMP_Text title = IntroBookBuilder.Label("Titulo", panel, new Vector2(0.1f, 0.82f), new Vector2(0.9f, 0.95f), font, 56f, Ink);
        title.text = CardTitle;

        TMP_Text body = IntroBookBuilder.Label("Texto", panel, new Vector2(0.1f, 0.24f), new Vector2(0.9f, 0.82f), font, 31f, Ink);
        body.enableWordWrapping = true;
        body.alignment = TextAlignmentOptions.TopLeft;
        body.text = CardBody;

        TMP_Text note = IntroBookBuilder.Label("Nota", panel, new Vector2(0.1f, 0.15f), new Vector2(0.9f, 0.23f), font, 22f, NoteGray);
        note.enableWordWrapping = true;
        note.text = CardNote;

        TMP_Text close = IntroBookBuilder.Label("Cerrar", panel, new Vector2(0.1f, 0.05f), new Vector2(0.9f, 0.14f), font, 28f, Ink);
        close.text = CardClose;
        closeHint = close.gameObject.AddComponent<CanvasGroup>();
        closeHint.alpha = 0f;
        closeHint.blocksRaycasts = false;

        cardRoot.SetActive(false); // la prende TutorialDirector
    }

    private static void BuildChips(Transform root, TMP_FontAsset font, Sprite paper, out CanvasGroup group, out TMP_Text[] texts,
        out GameObject chipRow, out CardRefs cards)
    {
        Canvas canvas = IntroBookBuilder.OverlayCanvas("UI_Tutorial_Etiquetas", ChipsOrder);
        canvas.transform.SetParent(root, false);

        // Un grupo con el desvanecido para las dos filas: tarjetas ilustradas (se usan si hay arte) y etiquetas de texto (plan B)
        RectTransform all = IntroBookBuilder.UiChild("Grupo", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        group = all.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        cards = BuildCardRow(all);

        // Abajo al centro; el ancho sale del texto de cada etiqueta
        RectTransform row = IntroBookBuilder.UiChild("Fila", all, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-600f, 70f), new Vector2(600f, 130f));
        chipRow = row.gameObject;
        HorizontalLayoutGroup rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 16f;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;

        texts = new TMP_Text[ChipCount];
        for (int i = 0; i < ChipCount; i++)
        {
            RectTransform chip = IntroBookBuilder.UiChild($"Etiqueta_{i}", row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            PaperImage(chip.gameObject, paper, 5f);
            HorizontalLayoutGroup chipLayout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            chipLayout.padding = new RectOffset(22, 22, 10, 10);
            chipLayout.childControlWidth = chipLayout.childControlHeight = true;
            chipLayout.childForceExpandWidth = chipLayout.childForceExpandHeight = false;

            TMP_Text text = IntroBookBuilder.Label("Texto", chip, Vector2.zero, Vector2.one, font, 26f, Ink);
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            texts[i] = text;
        }
    }

    /// <summary>Fila de tarjetas ilustradas: abajo al centro; TutorialDirector da el tamaño y la posición de cada una según su arte.</summary>
    private static CardRefs BuildCardRow(Transform parent)
    {
        var refs = new CardRefs { rects = new RectTransform[ChipCount], stamps = new RectTransform[ChipCount], images = new Image[ChipCount] };
        refs.row = IntroBookBuilder.UiChild("Tarjetas", parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
        refs.row.pivot = new Vector2(0.5f, 0f);
        refs.row.anchoredPosition = new Vector2(0f, 60f);
        Sprite stamp = TutorialCardImporter.Load(TutorialCardImporter.Sello);
        for (int i = 0; i < ChipCount; i++)
        {
            RectTransform slot = IntroBookBuilder.UiChild($"Tarjeta_{i}", refs.row, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            slot.sizeDelta = new Vector2(280f, 170f);
            refs.rects[i] = slot;

            RectTransform art = IntroBookBuilder.UiChild("Arte", slot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image image = art.gameObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            refs.images[i] = image;

            RectTransform stampRect = IntroBookBuilder.UiChild("Sello", slot, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            stampRect.sizeDelta = new Vector2(96f, 96f);
            stampRect.anchoredPosition = new Vector2(-10f, -10f);
            BuildStamp(stampRect, stamp);
            stampRect.gameObject.SetActive(false);
            refs.stamps[i] = stampRect;
            slot.gameObject.SetActive(false);
        }
        refs.row.gameObject.SetActive(false);
        return refs;
    }

    /// <summary>Sello real (tut_sello_listo) o, si falta el archivo, un visto de tinta roja dibujado con dos trazos.</summary>
    private static void BuildStamp(RectTransform stamp, Sprite art)
    {
        if (art != null)
        {
            Image image = stamp.gameObject.AddComponent<Image>();
            image.sprite = art;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return;
        }
        MakeStroke("Trazo_corto", stamp, new Vector2(-17f, -6f), new Vector2(14f, 36f), 45f);
        MakeStroke("Trazo_largo", stamp, new Vector2(20f, 6f), new Vector2(14f, 72f), -45f);
    }

    private static void MakeStroke(string name, Transform parent, Vector2 center, Vector2 size, float degrees)
    {
        RectTransform rt = IntroBookBuilder.UiChild(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        rt.sizeDelta = size;
        rt.anchoredPosition = center;
        rt.localRotation = Quaternion.Euler(0f, 0f, degrees);
        Image image = rt.gameObject.AddComponent<Image>();
        image.color = StampRed;
        image.raycastTarget = false;
    }

    /// <summary>Papel en 9-slice si hay arte (scale agranda el dibujo para piezas chicas); si no, crema con borde oscuro.</summary>
    private static void PaperImage(GameObject go, Sprite paper, float scale)
    {
        Image image = go.AddComponent<Image>();
        image.raycastTarget = false;
        if (paper != null)
        {
            image.sprite = paper;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = scale;
            return;
        }
        image.color = Cream;
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = DarkBorder;
        outline.effectDistance = new Vector2(3f, -3f);
    }
}
