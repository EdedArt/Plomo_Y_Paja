using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MENÚ: Plomo y Paja → HUD → Crear HUD (fase HUD.1, H1).
/// 1. Crea o actualiza Prefabs/UI/HUD_Wilbur.prefab: Canvas Screen Space Overlay (orden 4: debajo de la mira, 5),
///    CanvasScaler 1920x1080 con match = alto, AudioSource 2D del latido y HudWilbur con los dibujos de Art/UI/HUD que
///    existan (los que faltan quedan vacíos = placeholder) y la fuente Special Elite.
/// 2. Lo coloca en NivelPorky (la abre si hace falta) y en la escena abierta si tiene Player: una sola instancia por escena
///    (idempotente: conserva la que haya, borra copias). "Generar Nivel Porky" también lo coloca (PlaceInOpenScene).
/// 3. Lista en el cuadro y en la consola los ids de arte y audio que faltan.
/// Volver a correrlo toma los PNG nuevos. Nunca edita la escena a mano: David guarda (Ctrl+S).
/// </summary>
public static class HudBuilder
{
    public const string PrefabPath = "Assets/_Game/Prefabs/UI/HUD_Wilbur.prefab";
    private const string PrefabFolder = "Assets/_Game/Prefabs/UI";
    private const string ArtFolder = "Assets/_Game/Art/UI/HUD";
    private const string FontPath = "Assets/_Game/Fonts/SpecialElite SDF.asset";
    private const string InstanceName = "HUD_Wilbur";
    private const int SortingOrder = 4;

    public static readonly string[] FaceIds = { "hud_wilbur_01", "hud_wilbur_02", "hud_wilbur_03", "hud_wilbur_04" };
    public static readonly string[] OtherArtIds =
        { "hud_wilbur_dano", "hud_cartucho_lleno", "hud_cartucho_vacio", "hud_bala_llena", "hud_bala_vacia", "hud_barra_marco" };
    /// <summary>Sonidos que usa el HUD.1 (los demás del guion son de HUD.2 / HUD.3).</summary>
    public static readonly string[] SfxIds = { "sfx_latido", "sfx_hud_aviso" };

    [MenuItem("Plomo y Paja/HUD/Crear HUD")]
    public static void BuildMenu()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        var report = new StringBuilder();
        GameObject prefab = BuildPrefab(report);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Crear HUD", "No se pudo guardar " + PrefabPath + ".\n\n" + report, "Entendido");
            return;
        }

        Scene active = SceneManager.GetActiveScene();
        if (active.path != LevelBuilder_NivelPorky.ScenePath && File.Exists(LevelBuilder_NivelPorky.ScenePath))
        {
            // La escena abierta (si tiene Player) también recibe el HUD antes de cambiar a NivelPorky
            if (HasPlayer()) PlaceInOpenScene(report);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(LevelBuilder_NivelPorky.ScenePath, OpenSceneMode.Single);
        }
        PlaceInOpenScene(report);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log("[HUD] Crear HUD:\n" + report);
        EditorUtility.DisplayDialog("Crear HUD", report + "\nGuarda la escena (Ctrl+S).", "Entendido");
    }

    /// <summary>Crea o actualiza el prefab con el arte que haya. Devuelve el prefab (o null si no se pudo guardar).</summary>
    public static GameObject BuildPrefab(StringBuilder report)
    {
        LevelBuilderUtils.EnsureFolder(PrefabFolder);
        AudioLibrary.SetActive(AudioLibraryBuilder.GetOrCreateLibrary()); // en el Editor no hay loader: para buscar los SFX
        var missing = new List<string>();

        var root = new GameObject(InstanceName, typeof(RectTransform));
        try
        {
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // el alto manda: el margen y el tamaño crecen con el alto (16:9, 16:10, 21:9)
            root.AddComponent<CanvasGroup>();

            AudioSource heart = root.AddComponent<AudioSource>();
            heart.playOnAwake = false;
            heart.loop = true;
            heart.spatialBlend = 0f;
            heart.volume = 0.8f;
            heart.clip = AudioLibrary.GetSfx(SfxIds[0]); // sin clip: HudFeedback lo vuelve a buscar en runtime

            HudWilbur hud = root.AddComponent<HudWilbur>();
            var so = new SerializedObject(hud);
            SerializedProperty faces = so.FindProperty("faces");
            faces.arraySize = FaceIds.Length;
            for (int i = 0; i < FaceIds.Length; i++)
                faces.GetArrayElementAtIndex(i).objectReferenceValue = LoadSprite(FaceIds[i], missing);
            so.FindProperty("faceHurt").objectReferenceValue = LoadSprite("hud_wilbur_dano", missing);
            so.FindProperty("shellFull").objectReferenceValue = LoadSprite("hud_cartucho_lleno", missing);
            so.FindProperty("shellEmpty").objectReferenceValue = LoadSprite("hud_cartucho_vacio", missing);
            so.FindProperty("bulletFull").objectReferenceValue = LoadSprite("hud_bala_llena", missing);
            so.FindProperty("bulletEmpty").objectReferenceValue = LoadSprite("hud_bala_vacia", missing);
            so.FindProperty("barFrame").objectReferenceValue = LoadSprite("hud_barra_marco", missing);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            so.FindProperty("font").objectReferenceValue = font;
            so.FindProperty("heartbeat").objectReferenceValue = heart;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            report.AppendLine(prefab != null ? "✓ Prefab " + PrefabPath : "✗ No se pudo guardar " + PrefabPath);
            report.AppendLine(font != null ? "✓ Fuente Special Elite" : "⚠ No está " + FontPath + ": se usa la fuente por defecto de TMP");

            var missingSfx = new List<string>();
            foreach (string id in SfxIds)
                if (AudioLibrary.Active == null || AudioLibrary.GetSfx(id) == null) missingSfx.Add(id);
            if (missing.Count > 0)
                report.AppendLine("⚠ Arte que falta (placeholder): " + string.Join(", ", missing) +
                                  ". Recortar _entrante_hud/HUD con: python Tools/character_key.py --center, y volver a correr este menú.");
            else report.AppendLine("✓ Arte del HUD completo");
            if (missingSfx.Count > 0) report.AppendLine("⚠ Sonido que falta (no suena): " + string.Join(", ", missingSfx));
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>Coloca el HUD en la escena abierta: una sola instancia en la raíz. La llama también Generar Nivel Porky.</summary>
    public static void PlaceInOpenScene(StringBuilder report)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) prefab = BuildPrefab(report ?? new StringBuilder());
        if (prefab == null) return;

        Scene scene = SceneManager.GetActiveScene();
        GameObject keep = null;
        var extras = new List<GameObject>();
        foreach (HudWilbur hud in Object.FindObjectsOfType<HudWilbur>(true))
        {
            if (hud.gameObject.scene != scene) continue;
            GameObject go = hud.gameObject;
            bool isInstance = PrefabUtility.GetCorrespondingObjectFromSource(go) == prefab;
            if (keep == null && isInstance) keep = go;
            else extras.Add(go);
        }
        foreach (GameObject extra in extras) Object.DestroyImmediate(extra);

        if (keep == null)
        {
            keep = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            keep.name = InstanceName;
        }
        keep.transform.SetParent(null, false);
        EditorSceneManager.MarkSceneDirty(scene);
        if (report != null)
            report.AppendLine("✓ HUD en " + (string.IsNullOrEmpty(scene.path) ? scene.name : scene.path) +
                              (extras.Count > 0 ? " (se borraron " + extras.Count + " copias)" : ""));
    }

    private static bool HasPlayer()
    {
        GameObject player = GameObject.FindWithTag("Player");
        return player != null;
    }

    private static Sprite LoadSprite(string id, List<string> missing)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/" + id + ".png");
        if (sprite == null) missing.Add(id);
        return sprite;
    }
}
