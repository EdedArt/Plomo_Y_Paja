using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PISTAS.1 — menús de las pistas (idempotentes, no tocan ninguna escena):
/// - Plomo y Paja → Pistas → Crear hoja de pista: Resources/Clues/ClueCard.prefab (papel del interrogatorio + Special Elite;
///   sin el papel, panel crema) y Resources/Clues/ClueArt.asset con los PNG de Art/Props/pistas/ que existan.
/// - Plomo y Paja → Pistas → Validar pistas: cada pista del catálogo (textos, dibujos reales o placeholder) y dónde está en
///   la escena abierta (CluePickup, o marcador que recibe su pista en runtime).
/// </summary>
public static class ClueMenus
{
    private const string ResourcesFolder = "Assets/_Game/Resources/Clues";
    private const string CardPrefabPath = ResourcesFolder + "/ClueCard.prefab";
    private const string ArtAssetPath = ResourcesFolder + "/ClueArt.asset";
    private const string ArtFolder = "Assets/_Game/Art/Props/pistas";
    private const string PaperPath = "Assets/_Game/Art/UI/ui_papel_dialogo.png";

    [MenuItem("Plomo y Paja/Pistas/Crear hoja de pista")]
    public static void CreateCardMenu()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        var report = new StringBuilder();
        CreateCard(report);
        UpdateArt(report);
        AssetDatabase.SaveAssets();
        Debug.Log("[Pistas] Crear hoja de pista:\n" + report);
        EditorUtility.DisplayDialog("Crear hoja de pista", report.ToString(), "OK");
    }

    [MenuItem("Plomo y Paja/Pistas/Validar pistas")]
    public static void ValidateMenu()
    {
        var report = new StringBuilder();
        Validate(report);
        Debug.Log("[Pistas] Validar pistas:\n" + report);
        EditorUtility.DisplayDialog("Validar pistas", report.ToString(), "OK");
    }

    // ==================================================================
    // Hoja
    // ==================================================================

    private static void CreateCard(StringBuilder report)
    {
        LevelBuilderUtils.EnsureFolder(ResourcesFolder);
        TMP_FontAsset font = InterrogationUIBuilder.EnsureFont(report); // Special Elite (sin el TTF: la de TMP)
        Sprite paper = AssetDatabase.LoadAssetAtPath<Sprite>(PaperPath);
        report.AppendLine(paper != null
            ? "✓ Papel: ui_papel_dialogo.png"
            : "⚠ ui_papel_dialogo.png no está como Sprite (correr Contador → Crear UI de interrogatorio): panel crema");

        ClueCard card = ClueCard.Build(font, paper, null);
        try
        {
            PrefabUtility.SaveAsPrefabAsset(card.gameObject, CardPrefabPath);
            report.AppendLine("✓ Hoja: " + CardPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(card.gameObject);
        }
    }

    // ==================================================================
    // Dibujos
    // ==================================================================

    private static void UpdateArt(StringBuilder report)
    {
        LevelBuilderUtils.EnsureFolder(ResourcesFolder);
        ClueArt art = AssetDatabase.LoadAssetAtPath<ClueArt>(ArtAssetPath);
        if (art == null)
        {
            art = ScriptableObject.CreateInstance<ClueArt>();
            AssetDatabase.CreateAsset(art, ArtAssetPath);
        }

        foreach (ClueInfo info in ClueCatalog.All)
        {
            if (string.IsNullOrEmpty(info.WorldSprite) && string.IsNullOrEmpty(info.CardImage)) continue;
            ClueArt.Entry entry = art.entries.Find(e => e != null && e.clueId == info.Id);
            if (entry == null)
            {
                entry = new ClueArt.Entry { clueId = info.Id };
                art.entries.Add(entry);
            }
            // Un PNG que existe manda; si no existe, se conserva lo asignado a mano
            Sprite world = LoadArt(info.WorldSprite);
            if (world != null) entry.worldSprite = world;
            Sprite image = LoadArt(info.CardImage);
            if (image != null) entry.cardImage = image;
            report.AppendLine($"  {info.Id}: dibujo {Describe(entry.worldSprite, info.WorldSprite)}" +
                              (string.IsNullOrEmpty(info.CardImage) ? "" : $", imagen {Describe(entry.cardImage, info.CardImage)}"));
        }
        EditorUtility.SetDirty(art);
        report.AppendLine("✓ Dibujos: " + ArtAssetPath);
    }

    private static Sprite LoadArt(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/" + name + ".png");
    }

    private static string Describe(Sprite sprite, string wanted)
    {
        if (string.IsNullOrEmpty(wanted)) return "—";
        return sprite != null ? $"✓ {sprite.name}" : $"placeholder (falta {wanted}.png como Sprite)";
    }

    // ==================================================================
    // Validar
    // ==================================================================

    private static void Validate(StringBuilder report)
    {
        report.AppendLine(AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath) != null
            ? "✓ Hoja: " + CardPrefabPath
            : "⚠ Falta " + CardPrefabPath + " (Pistas → Crear hoja de pista); en el juego se arma por código con la fuente de TMP");
        ClueArt art = AssetDatabase.LoadAssetAtPath<ClueArt>(ArtAssetPath);
        report.AppendLine(art != null ? "✓ Dibujos: " + ArtAssetPath : "⚠ Falta " + ArtAssetPath + ": todas con placeholder");

        report.AppendLine();
        report.AppendLine("CATÁLOGO (textos del guion):");
        foreach (ClueInfo info in ClueCatalog.All)
        {
            ClueArt.Entry entry = art != null ? art.entries.Find(e => e != null && e.clueId == info.Id) : null;
            string kind = string.IsNullOrEmpty(info.Body) ? "solo aviso" : $"hoja de {info.Body.Length} caracteres";
            string world = string.IsNullOrEmpty(info.WorldSprite) ? "" :
                ", dibujo " + Describe(entry != null ? entry.worldSprite : null, info.WorldSprite);
            string image = string.IsNullOrEmpty(info.CardImage) ? "" :
                ", imagen " + Describe(entry != null ? entry.cardImage : null, info.CardImage);
            report.AppendLine($"- {info.Id} «{info.Title}»: {kind}{world}{image}");
        }

        Scene scene = SceneManager.GetActiveScene();
        report.AppendLine();
        report.AppendLine($"ESCENA {scene.name}:");
        var placed = new HashSet<string>();
        foreach (CluePickup pickup in Object.FindObjectsOfType<CluePickup>(true))
        {
            placed.Add(pickup.ClueId);
            string known = ClueCatalog.TryGet(pickup.ClueId, out _) ? "" : "  ⚠ no está en el catálogo (usa sus propios textos)";
            report.AppendLine($"- {pickup.ClueId}: {PathOf(pickup.transform)} {Format(pickup.transform.position)}{known}");
        }
        foreach (LevelMarker marker in Object.FindObjectsOfType<LevelMarker>(true))
        {
            if (marker.Type != MarkerType.Clue || string.IsNullOrEmpty(marker.Id) || placed.Contains(marker.Id)) continue;
            placed.Add(marker.Id);
            report.AppendLine($"- {marker.Id}: marcador {PathOf(marker.transform)} {Format(marker.transform.position)} → " +
                              "su pista se crea al jugar (ClueRuntime)");
        }
        if (placed.Count == 0) report.AppendLine("- (ninguna pista en esta escena)");
    }

    private static string PathOf(Transform t)
    {
        var sb = new StringBuilder(t.name);
        for (Transform p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
        return sb.ToString();
    }

    private static string Format(Vector3 v) => $"({v.x:0.##}, {v.y:0.##}, {v.z:0.##})";
}
