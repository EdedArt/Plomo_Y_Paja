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
/// N1.2 (regla N7): cada objeto lleva PickupSpot y reposa en un mueble o contenedor, sin vaivén (solo brillo). Se avisa si flota
/// (más de 0.05 m) o si quedó en un piso sin muebles a 0.6 m. Una entrada con "container" usa su altura tal cual; sin él, baja al piso.
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

    private const int NivelPorkyLayoutVersion = 2; // N1.2. Un asset de antes (versión 0) se reemplaza por esta distribución
    private const string NivelPorkyScene = "NivelPorky";

    // Objetos colocados (N7): más chicos que el lienzo de 0.5 m del drop (van en un cajón o una repisa), quietos, y la lucecita
    // solo de cerca: dentro de la casa hay varios y URP da 8 luces por objeto
    private const float PlacedVisualSize = 0.34f;
    private const float PlacedLightDistance = 7f;
    private const float FurnitureRadius = 0.6f;
    private const float FloorMinSide = 3f;      // un collider con los dos lados así de largos es piso
    private const float FurnitureMaxSide = 2.5f;

    /// <summary>Distribución del Nivel 1 (N1.2): 4 cajas de cartuchos, 3 botiquines, 1 caja de balas. Nada justo antes del portón del patio.</summary>
    private static PickupLayout.Entry[] NivelPorkySeed() => new[]
    {
        Seed("Cartuchos_Cocina", PickupKind.Cartuchos, Nivel1FurnitureBuilder.CounterSpot, "Cocina: mostrador"),
        Seed("Cartuchos_Dormitorio", PickupKind.Cartuchos, Nivel1FurnitureBuilder.DrawerSpot, "Dormitorio: cajón abierto de la mesita de noche"),
        Seed("Botiquin_Bano", PickupKind.Botiquin, Nivel1FurnitureBuilder.CabinetSpot, "Baño: repisa del armario abierto"),
        Seed("Balas_Sala", PickupKind.BalasRevolver, Nivel1FurnitureBuilder.TinSpot, "Sala: lata abierta sobre la mesa"),
        Seed("Cartuchos_Vecino", PickupKind.Cartuchos, Nivel1MapBuilder.PorchTableSpot, "Vecino B: mesa del porche"),
        Seed("Botiquin_Callejon", PickupKind.Botiquin, Nivel1MapBuilder.AlleyCrateSpot, "Callejón lateral: caja"),
        Seed("Botiquin_Banco", PickupKind.Botiquin, ContadorYardBuilder.WorkbenchSpot, "Cuarto de Centavo: banco de trabajo"),
        Seed("Cartuchos_Caja_Cuarto", PickupKind.Cartuchos, ContadorYardBuilder.CrateSpot, "Cuarto de Centavo: caja"),
    };

    private static PickupLayout.Entry Seed(string id, PickupKind kind, Vector3 position, string container) =>
        new PickupLayout.Entry { id = id, kind = kind, position = position, container = container };

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

        if (string.IsNullOrEmpty(SceneManager.GetActiveScene().name))
        {
            EditorUtility.DisplayDialog("Guardar posiciones de objetos", "La escena abierta no tiene nombre: guárdala primero.", "Entendido");
            return;
        }

        PickupLayout layout = GetOrCreateLayout(SceneManager.GetActiveScene().name);
        var entries = new List<PickupLayout.Entry>();
        foreach (Pickup pickup in root.GetComponentsInChildren<Pickup>(true))
        {
            string objectName = pickup.gameObject.name;
            string id = objectName.StartsWith(InstancePrefix) ? objectName.Substring(InstancePrefix.Length) : objectName;
            string container = pickup.TryGetComponent(out PickupSpot spot) ? spot.ContainerTag : "";
            entries.Add(new PickupLayout.Entry { id = id, kind = pickup.Kind, position = pickup.transform.position, container = container });
        }
        layout.entries = entries;
        layout.version = NivelPorkyLayoutVersion; // lo guardado a mano ya no se reemplaza
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
        if (string.IsNullOrEmpty(scene.name))
        {
            // N1.4: una escena sin guardar no tiene distribución (antes creaba un PickupLayout_.asset vacío y no colocaba nada)
            Debug.LogError("[Pickups] La escena abierta no tiene nombre: guárdala antes de colocar objetos.");
            report.AppendLine("✗ La escena abierta no tiene nombre: guárdala antes de colocar objetos.");
            return;
        }
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
            bool hasContainer = !string.IsNullOrEmpty(entry.container);
            instance.transform.position = hasContainer ? entry.position : Pickup.SnapToGround(entry.position);
            PlaceOnSurface(instance, entry, report);
            ApplyPlacedLook(instance);
            Vector3 position = instance.transform.position;
            report.AppendLine($"• {entry.id} en ({position.x:F2}, {position.y:F2}, {position.z:F2})" + (hasContainer ? " · " + entry.container : ""));
        }

        // N7: el spawner queda en la escena, con los drops apagados (GuardDropSpawner.dropsEnabled)
        var dropPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(InfoFor(PickupKind.Cartuchos)));
        var spawnerGo = new GameObject("GuardDropSpawner");
        spawnerGo.transform.SetParent(root, false);
        var spawner = new SerializedObject(spawnerGo.AddComponent<GuardDropSpawner>());
        spawner.FindProperty("dropPrefab").objectReferenceValue = dropPrefab;
        spawner.ApplyModifiedPropertiesWithoutUndo();
        report.AppendLine($"✓ {layout.entries.Count} objetos en {scene.name} (drops de guardias apagados)");
    }

    /// <summary>N7: lo apoya en la superficie y avisa si flotaba o si quedó en un piso sin muebles cerca.</summary>
    private static void PlaceOnSurface(GameObject instance, PickupLayout.Entry entry, StringBuilder report)
    {
        PickupSpot spot = instance.AddComponent<PickupSpot>();
        spot.Setup(entry.container);

        if (!spot.Snap(out Collider surface, out float gap))
        {
            Warn(report, $"{entry.id}: no hay ninguna superficie debajo de {entry.position}.");
            return;
        }
        if (gap > PickupSpot.MaxGap) Warn(report, $"{entry.id}: estaba a {gap:0.00} m de la superficie ({surface.name}).");
        if (IsFloor(surface) && !HasFurnitureNear(instance.transform.position))
            Warn(report, $"{entry.id}: quedó en el piso ({surface.name}) sin muebles a {FurnitureRadius} m. N7: tiene que ir en un mueble o contenedor.");
    }

    private static void Warn(StringBuilder report, string text)
    {
        report.AppendLine("⚠ " + text);
        Debug.LogWarning("[Pickups] " + text);
    }

    private static bool IsFloor(Collider collider)
    {
        Vector3 size = collider.bounds.size;
        return size.x >= FloorMinSide && size.z >= FloorMinSide;
    }

    private static bool HasFurnitureNear(Vector3 position)
    {
        foreach (Collider collider in Physics.OverlapSphere(position + Vector3.up * 0.3f, FurnitureRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            Vector3 size = collider.bounds.size;
            if (!Pickup.IsActor(collider) && Mathf.Max(size.x, size.z) <= FurnitureMaxSide) return true;
        }
        return false;
    }

    /// <summary>N7: quieto, solo el brillo. Tamaño de mueble y lucecita solo de cerca. Se escribe en la instancia: el drop conserva el vaivén.</summary>
    private static void ApplyPlacedLook(GameObject instance)
    {
        var so = new SerializedObject(instance.GetComponent<Pickup>());
        so.FindProperty("bobAmplitude").floatValue = 0f;
        so.FindProperty("visualSize").floatValue = PlacedVisualSize;
        so.FindProperty("lightMaxDistance").floatValue = PlacedLightDistance;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Con el NavMesh horneado: hay un lugar alcanzable desde la calle, a ≤ 2.1 m en horizontal (el alcance es 2.2), desde donde el objeto
    /// se ve de pie (mismo rayo que Pickup.HasLineOfSight, con los ojos a 1.7 m), así aparece el aviso aunque esté en un cajón o un armario.
    /// Se prueban varios lugares alrededor: el más cercano puede ser un rincón cerrado, como detrás del banco de Centavo.
    /// </summary>
    public static bool ValidateSpots(StringBuilder report)
    {
        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            report.AppendLine("✗ No está " + RootName + " en la escena.");
            return false;
        }

        bool ok = true;
        int total = 0;
        foreach (Pickup pickup in root.GetComponentsInChildren<Pickup>(true))
        {
            total++;
            if (HasStandingSpot(pickup.transform.position)) continue;
            ok = false;
            report.AppendLine($"✗ {pickup.name}: ningún lugar alcanzable a 2.1 m desde donde se vea de pie.");
        }
        if (ok) report.AppendLine($"✓ Objetos: los {total} se alcanzan y se ven de pie.");
        return ok;
    }

    private static bool HasStandingSpot(Vector3 position)
    {
        const int directions = 12;
        const float maxHorizontal = 2.1f, eyeHeight = 1.7f, sampleRadius = 1.6f;
        Vector3 street = new Vector3(0f, 0f, -15f);
        foreach (float distance in new[] { 0.9f, 1.4f, 1.9f })
        for (int i = 0; i < directions; i++)
        {
            Vector3 around = position + Quaternion.Euler(0f, i * 360f / directions, 0f) * Vector3.forward * distance;
            if (!UnityEngine.AI.NavMesh.SamplePosition(around, out UnityEngine.AI.NavMeshHit floor, sampleRadius, UnityEngine.AI.NavMesh.AllAreas)) continue;

            Vector3 flat = floor.position - position;
            flat.y = 0f;
            if (flat.magnitude > maxHorizontal || !Nivel1MapBuilder.Reachable(street, floor.position)) continue;

            bool blocked = Physics.Linecast(floor.position + Vector3.up * eyeHeight, position + Vector3.up * 0.25f, out RaycastHit hit,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && !Pickup.IsActor(hit.collider);
            if (!blocked) return true;
        }
        return false;
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
        if (layout != null)
        {
            if (sceneName == NivelPorkyScene && layout.version < NivelPorkyLayoutVersion)
            {
                // N1.2: la distribución de HUD.3 ponía objetos en el piso. Lo guardado después de N1.2 no se toca
                layout.entries = new List<PickupLayout.Entry>(NivelPorkySeed());
                layout.version = NivelPorkyLayoutVersion;
                EditorUtility.SetDirty(layout);
                AssetDatabase.SaveAssets();
                Debug.Log("[Pickups] " + path + ": se puso la distribución de N1.2.");
            }
            return layout;
        }

        LevelBuilderUtils.EnsureFolder(DataFolder);
        layout = ScriptableObject.CreateInstance<PickupLayout>();
        if (sceneName == NivelPorkyScene)
        {
            layout.entries.AddRange(NivelPorkySeed());
            layout.version = NivelPorkyLayoutVersion;
        }
        else Debug.LogWarning("[Pickups] " + path + " se crea vacío: coloca objetos en la escena y corre \"Guardar posiciones de objetos\".");
        AssetDatabase.CreateAsset(layout, path);
        AssetDatabase.SaveAssets();
        return layout;
    }
}
