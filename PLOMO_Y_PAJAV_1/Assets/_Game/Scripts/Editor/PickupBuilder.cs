using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Objetos recogibles (fase HUD.3). MENÚS: Plomo y Paja → HUD → Colocar objetos / Guardar posiciones de objetos.
/// - Arma los prefabs Prefabs/Pickups/Pickup_Botiquin, Pickup_Cartuchos y Pickup_BalasRevolver con el PNG de Art/Props/pickups
///   (si falta: cubo con etiqueta y aviso; volver a correr el menú toma el PNG).
/// - Coloca lo de Data/PickupLayout_&lt;Escena&gt; bajo "--- PICKUPS ---" (idempotente: rehace ese grupo) con la altura ajustada al
///   piso, más el GuardDropSpawner. "Generar Nivel Porky" también lo corre, así que regenerar no pierde nada.
/// - Guardar posiciones: escribe en el asset lo que se movió, agregó o borró en ese grupo de la escena abierta.
/// NIVEL 2 (Fase 4): su generador llama BuildPrefabs + PlaceInOpenScene y a Don Lobo le apaga dropsLoot (EnemyHealth);
/// el asset se crea vacío y se llena con "Guardar posiciones de objetos".
/// </summary>
public static class PickupBuilder
{
    private const string PrefabFolder = "Assets/_Game/Prefabs/Pickups";
    private const string ArtFolder = "Assets/_Game/Art/Props/pickups";
    private const string DataFolder = "Assets/_Game/Data";
    private const string RootName = "--- PICKUPS ---";
    private const string InstancePrefix = "Pickup_";
    private const int IgnoreRaycastLayer = 2;               // como el revólver del piso: no tapa la vista ni los perdigones

    private static readonly string[] AudioIds = { "sfx_botiquin", "sfx_municion_recoger" };
    private static readonly string[] VoiceIds = { "wilbur_botiquin_01", "wilbur_municion_01" };

    private struct KindInfo
    {
        public PickupKind kind;
        public string prefabName, artId, prompt;
        public Color placeholder;
    }

    private static readonly KindInfo[] Kinds =
    {
        new KindInfo { kind = PickupKind.Botiquin, prefabName = "Pickup_Botiquin", artId = "pickup_botiquin_01",
            prompt = "[E] Tomar botiquín", placeholder = new Color(0.95f, 0.9f, 0.75f) },
        new KindInfo { kind = PickupKind.Cartuchos, prefabName = "Pickup_Cartuchos", artId = "pickup_cartuchos_01",
            prompt = "[E] Tomar cartuchos", placeholder = new Color(0.8f, 0.15f, 0.12f) },
        new KindInfo { kind = PickupKind.BalasRevolver, prefabName = "Pickup_BalasRevolver", artId = "pickup_balas_01",
            prompt = "[E] Tomar balas", placeholder = new Color(0.85f, 0.65f, 0.15f) },
    };

    /// <summary>Lugares iniciales del Nivel 1 (solo al crear el asset; después manda el asset). La altura la ajusta al piso.</summary>
    private static PickupLayout.Entry[] NivelPorkySeed() => new[]
    {
        // Casa: rincón oeste de la cocina, lejos de la ruta directa (puerta sala → puerta trasera)
        Seed("Cartuchos_Casa", PickupKind.Cartuchos, new Vector3(-7.1f, 0.06f, 3.1f)),
        // Antes del portón del patio de Centavo (x -7, z 13.5), junto a la valla, fuera de la línea del centinela
        Seed("Botiquin_Porton", PickupKind.Botiquin, new Vector3(-8.9f, 0f, 12.3f)),
        // Premio: rincones de adelante, dentro del cuarto (centro en x -7, z 19; puerta en z 17.5; barriles y caja al fondo)
        Seed("Cartuchos_Cuarto", PickupKind.Cartuchos, new Vector3(-5.55f, 0.22f, 18f)),
        Seed("Botiquin_Cuarto", PickupKind.Botiquin, new Vector3(-8.45f, 0.22f, 18f)),
        // Salida del patio: junto al camino de huida (-7, 12.4) → (-9, 8.6), ya pasado el portón
        Seed("Balas_Salida", PickupKind.BalasRevolver, new Vector3(-7.6f, 0f, 9.4f)),
    };

    private static PickupLayout.Entry Seed(string id, PickupKind kind, Vector3 position) =>
        new PickupLayout.Entry { id = id, kind = kind, position = position };

    // ============ Menús ============

    [MenuItem("Plomo y Paja/HUD/Colocar objetos")]
    public static void PlaceMenu()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        if (!EnsureGameplayScene()) return;

        var report = new StringBuilder();
        BuildPrefabs(report);
        PlaceInOpenScene(report);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log("[Pickups] Colocar objetos:\n" + report);
        EditorUtility.DisplayDialog("Colocar objetos", report + "\nGuarda la escena (Ctrl+S).", "Entendido");
    }

    [MenuItem("Plomo y Paja/HUD/Guardar posiciones de objetos")]
    public static void SaveMenu()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            EditorUtility.DisplayDialog("Guardar posiciones de objetos",
                $"No está \"{RootName}\" en la escena abierta. Corre primero \"Colocar objetos\".", "Entendido");
            return;
        }

        PickupLayout layout = GetOrCreateLayout(SceneManager.GetActiveScene().name);
        var entries = new List<PickupLayout.Entry>();
        foreach (Pickup pickup in root.GetComponentsInChildren<Pickup>(true))
        {
            string objectName = pickup.gameObject.name;
            string id = objectName.StartsWith(InstancePrefix) ? objectName.Substring(InstancePrefix.Length) : objectName;
            entries.Add(new PickupLayout.Entry { id = id, kind = pickup.Kind, position = pickup.transform.position });
        }
        layout.entries = entries;
        EditorUtility.SetDirty(layout);
        AssetDatabase.SaveAssets();

        string text = $"{entries.Count} objetos guardados en {AssetDatabase.GetAssetPath(layout)}.";
        Debug.Log("[Pickups] " + text);
        EditorUtility.DisplayDialog("Guardar posiciones de objetos", text, "Entendido");
    }

    /// <summary>Si la escena abierta no tiene jugador, abre NivelPorky (como "Crear HUD"). false si no se pudo.</summary>
    private static bool EnsureGameplayScene()
    {
        if (GameObject.FindWithTag("Player") != null) return true;
        if (!File.Exists(LevelBuilder_NivelPorky.ScenePath))
        {
            EditorUtility.DisplayDialog("Colocar objetos", "No hay jugador en la escena abierta ni existe " +
                LevelBuilder_NivelPorky.ScenePath + ".", "Entendido");
            return false;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        EditorSceneManager.OpenScene(LevelBuilder_NivelPorky.ScenePath, OpenSceneMode.Single);
        return true;
    }

    // ============ Prefabs ============

    /// <summary>Crea o actualiza los tres prefabs con el arte que haya. Se conservan los GUID al volver a correrlo.</summary>
    public static void BuildPrefabs(StringBuilder report)
    {
        LevelBuilderUtils.EnsureFolder(PrefabFolder);
        foreach (KindInfo info in Kinds) BuildPrefab(info, report);

        AudioLibrary.SetActive(AudioLibraryBuilder.GetOrCreateLibrary()); // en el Editor no hay loader
        foreach (string id in AudioIds)
            if (AudioLibrary.GetSfx(id) == null) report.AppendLine("ℹ Falta el sonido " + id + " (sin él, no suena)");
        foreach (string id in VoiceIds)
            if (!AudioLibrary.HasVoice(id)) report.AppendLine("ℹ Falta la línea opcional " + id);
    }

    private static void BuildPrefab(KindInfo info, StringBuilder report)
    {
        var root = new GameObject(info.prefabName) { layer = IgnoreRaycastLayer };
        try
        {
            var visual = new GameObject("Visual") { layer = IgnoreRaycastLayer };
            visual.transform.SetParent(root.transform, false);

            string artPath = ArtFolder + "/" + info.artId + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(artPath);
            if (sprite != null)
            {
                BuildSprite(visual, sprite);
                report.AppendLine("✓ " + info.prefabName + ": " + artPath);
            }
            else
            {
                BuildPlaceholder(visual, info);
                report.AppendLine("⚠ Falta " + artPath + ": " + info.prefabName + " usa un cubo con etiqueta.");
                Debug.LogWarning("[Pickups] Falta " + artPath + ": se usa un placeholder.");
            }

            Pickup pickup = info.kind == PickupKind.Botiquin ? root.AddComponent<MedkitPickup>() : (Pickup)root.AddComponent<AmmoPickup>();
            var so = new SerializedObject(pickup);
            so.FindProperty("interactPrompt").stringValue = info.prompt;
            so.FindProperty("visual").objectReferenceValue = visual.transform;
            if (info.kind != PickupKind.Botiquin)
            {
                so.FindProperty("ammoType").enumValueIndex =
                    (int)(info.kind == PickupKind.Cartuchos ? AmmoType.Shotgun : AmmoType.Revolver);
                so.FindProperty("amount").intValue = 6;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(info));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void BuildSprite(GameObject visual, Sprite sprite)
    {
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        BillboardSprite billboard = visual.AddComponent<BillboardSprite>();
        var so = new SerializedObject(billboard);
        SerializedProperty idle = so.FindProperty("idleSprites");
        idle.arraySize = 1;
        idle.GetArrayElementAtIndex(0).objectReferenceValue = sprite;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildPlaceholder(GameObject visual, KindInfo info)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(cube.GetComponent<Collider>());
        cube.name = "Placeholder";
        cube.layer = IgnoreRaycastLayer;
        cube.transform.SetParent(visual.transform, false);
        cube.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        cube.transform.localScale = new Vector3(0.4f, 0.3f, 0.3f);
        var material = new Material(cube.GetComponent<MeshRenderer>().sharedMaterial) { color = info.placeholder };
        AssetDatabase.CreateAsset(material, PrefabFolder + "/" + info.prefabName + "_Placeholder.mat");
        cube.GetComponent<MeshRenderer>().sharedMaterial = material;

        var label = new GameObject("Etiqueta") { layer = IgnoreRaycastLayer };
        label.transform.SetParent(visual.transform, false);
        label.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        TextMesh text = label.AddComponent<TextMesh>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
        text.text = info.artId;
        text.characterSize = 0.04f;
        text.fontSize = 40;
        text.anchor = TextAnchor.MiddleCenter;
        text.color = Color.black;
    }

    private static string PrefabPath(KindInfo info) => PrefabFolder + "/" + info.prefabName + ".prefab";

    // ============ Colocar ============

    /// <summary>Rehace "--- PICKUPS ---" en la escena abierta con lo del asset de esa escena. No guarda la escena.</summary>
    public static void PlaceInOpenScene(StringBuilder report)
    {
        Scene scene = SceneManager.GetActiveScene();
        PickupLayout layout = GetOrCreateLayout(scene.name);

        GameObject old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        Transform root = LevelBuilderUtils.CreateGroup(RootName);

        Physics.SyncTransforms(); // para ajustar al piso lo recién construido
        foreach (PickupLayout.Entry entry in layout.entries)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(InfoFor(entry.kind)));
            if (prefab == null)
            {
                report.AppendLine("✗ Sin prefab para " + entry.id + ": corre \"Colocar objetos\".");
                continue;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            instance.name = InstancePrefix + entry.id;
            Vector3 position = Pickup.SnapToGround(entry.position);
            instance.transform.position = position;
            report.AppendLine($"• {entry.id} en ({position.x:F2}, {position.y:F2}, {position.z:F2})");
        }

        var dropPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(InfoFor(PickupKind.Cartuchos)));
        var spawnerGo = new GameObject("GuardDropSpawner");
        spawnerGo.transform.SetParent(root, false);
        var spawner = new SerializedObject(spawnerGo.AddComponent<GuardDropSpawner>());
        spawner.FindProperty("dropPrefab").objectReferenceValue = dropPrefab;
        spawner.ApplyModifiedPropertiesWithoutUndo();
        report.AppendLine($"✓ {layout.entries.Count} objetos y drops de guardias en {scene.name}");
    }

    private static KindInfo InfoFor(PickupKind kind)
    {
        foreach (KindInfo info in Kinds) if (info.kind == kind) return info;
        return Kinds[0];
    }

    private static PickupLayout GetOrCreateLayout(string sceneName)
    {
        string path = DataFolder + "/PickupLayout_" + sceneName + ".asset";
        var layout = AssetDatabase.LoadAssetAtPath<PickupLayout>(path);
        if (layout != null) return layout;

        LevelBuilderUtils.EnsureFolder(DataFolder);
        layout = ScriptableObject.CreateInstance<PickupLayout>();
        if (sceneName == "NivelPorky") layout.entries.AddRange(NivelPorkySeed());
        else Debug.LogWarning("[Pickups] " + path + " se crea vacío: coloca objetos en la escena y corre \"Guardar posiciones de objetos\".");
        AssetDatabase.CreateAsset(layout, path);
        AssetDatabase.SaveAssets();
        return layout;
    }
}
