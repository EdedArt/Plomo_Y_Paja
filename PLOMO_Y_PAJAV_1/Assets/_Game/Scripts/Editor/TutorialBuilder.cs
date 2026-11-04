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

        TMP_FontAsset font = InterrogationUIBuilder.EnsureFont(report);
        Sprite paper = AssetDatabase.LoadAssetAtPath<Sprite>(PaperPath);
        if (paper == null) report.AppendLine("⚠ No está " + PaperPath + " como Sprite: tarjeta y etiquetas usan un panel crema.");

        BuildCard(root, font, paper, out GameObject cardRoot, out CanvasGroup closeHint);
        BuildChips(root, font, paper, out CanvasGroup chipsGroup, out TMP_Text[] chipTexts);

        var go = new GameObject("TutorialDirector");
        go.transform.SetParent(root, false);
        var so = new SerializedObject(go.AddComponent<TutorialDirector>());
        so.FindProperty("cardRoot").objectReferenceValue = cardRoot;
        so.FindProperty("cardCloseHint").objectReferenceValue = closeHint;
        so.FindProperty("chipsGroup").objectReferenceValue = chipsGroup;
        SerializedProperty texts = so.FindProperty("chipTexts");
        texts.arraySize = chipTexts.Length;
        for (int i = 0; i < chipTexts.Length; i++) texts.GetArrayElementAtIndex(i).objectReferenceValue = chipTexts[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        report.AppendLine($"✓ Tutorial: tarjeta (canvas {CardOrder}), {ChipCount} etiquetas (canvas {ChipsOrder}), TutorialDirector");
    }

    private static void BuildCard(Transform root, TMP_FontAsset font, Sprite paper, out GameObject cardRoot, out CanvasGroup closeHint)
    {
        Canvas canvas = IntroBookBuilder.OverlayCanvas("UI_Tutorial_Tarjeta", CardOrder);
        canvas.transform.SetParent(root, false);
        cardRoot = canvas.gameObject;

        RectTransform dim = IntroBookBuilder.UiChild("Oscurecer", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image dimImage = dim.gameObject.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.55f);
        dimImage.raycastTarget = false;

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

    private static void BuildChips(Transform root, TMP_FontAsset font, Sprite paper, out CanvasGroup group, out TMP_Text[] texts)
    {
        Canvas canvas = IntroBookBuilder.OverlayCanvas("UI_Tutorial_Etiquetas", ChipsOrder);
        canvas.transform.SetParent(root, false);

        // Abajo al centro; el ancho sale del texto de cada etiqueta
        RectTransform row = IntroBookBuilder.UiChild("Fila", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-600f, 70f), new Vector2(600f, 130f));
        group = row.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
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
