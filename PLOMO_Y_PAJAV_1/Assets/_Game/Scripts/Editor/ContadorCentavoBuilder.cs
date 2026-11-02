using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// MENÚ: Plomo y Paja → Contador → Colocar Centavo (fase 3C.2)
/// Crea/actualiza Prefabs/Level1/Centavo.prefab y lo coloca en NivelPorky, en Anchor_CentavoStand del cuarto
/// (posición leída de ContadorShed, nunca escrita a mano). Se puede correr las veces que haga falta: conserva la
/// instancia que ya exista y borra duplicados.
/// También lo llaman "Construir patio y cuarto" y "Generar Nivel Porky" (vía ContadorYardBuilder.BuildInto),
/// así que regenerar el nivel no lo pierde.
///
/// PREFAB:
///   Centavo (CentavoController, AudioSource de voz 3D, LipSyncDriver)
///   ├── Visual
///   │   ├── Placeholder (cápsula sin collider: se ve si faltan los sprites)
///   │   └── SpriteHolder (SpriteRenderer + BillboardSprite con el set "contador")
///   ├── Hitbox (CapsuleCollider, capa Default: la toca la escopeta)
///   ├── Sfx (AudioSource 3D del disparo)
///   ├── Fogonazo (Point Light apagada)
///   └── Revolver_Suelto (apagado; cae al recibir el golpe; capa Ignore Raycast)
/// </summary>
public static class ContadorCentavoBuilder
{
    public const string PrefabFolder = "Assets/_Game/Prefabs/Level1";
    public const string PrefabPath = PrefabFolder + "/Centavo.prefab";
    private const string InstanceName = "Centavo";
    private const string ActorsRoot = "--- ACTORS ---";
    private const string SpriteSetId = "contador";

    /// <summary>Guardias del patio que tienen que caer antes de que Centavo despierte (D2). Nombres de BuildActors.</summary>
    private static readonly string[] YardGuardNames = { "Guard_Patrulla_A", "Guard_Centinela" };

    private const int IgnoreRaycastLayer = 2;

    [MenuItem("Plomo y Paja/Contador/Colocar Centavo")]
    public static void PlaceMenu()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != LevelBuilder_NivelPorky.ScenePath)
        {
            if (!File.Exists(LevelBuilder_NivelPorky.ScenePath))
            {
                EditorUtility.DisplayDialog("Colocar Centavo",
                    $"No existe {LevelBuilder_NivelPorky.ScenePath}. Primero: Plomo y Paja → Generar Nivel Porky.", "Entendido");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(LevelBuilder_NivelPorky.ScenePath, OpenSceneMode.Single);
        }

        if (Object.FindObjectOfType<ContadorShed>(true) == null)
        {
            EditorUtility.DisplayDialog("Colocar Centavo",
                "La escena no tiene el cuarto de herramientas. Primero: Plomo y Paja → Contador → Construir patio y cuarto.",
                "Entendido");
            return;
        }

        var report = new StringBuilder();
        bool ok = BuildAndPlace(report);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[Contador] Colocar Centavo:\n" + report);
        EditorUtility.DisplayDialog("Colocar Centavo",
            (ok ? "Listo.\n\n" : "Revisa la consola: hay algo que falta.\n\n") + report + "\nGuarda la escena (Ctrl+S).",
            "Entendido");
    }

    /// <summary>Prefab + instancia en la escena abierta. Devuelve false si falta algo (queda en el reporte).</summary>
    public static bool BuildAndPlace(StringBuilder report)
    {
        GameObject prefab = BuildPrefab(report);
        return Place(prefab, report);
    }

    // ==================================================================
    // Prefab
    // ==================================================================

    private static GameObject BuildPrefab(StringBuilder report)
    {
        LevelBuilderUtils.EnsureFolder(PrefabFolder);
        CharacterSpriteSet set = CharacterSpriteSetBuilder.GetOrCreateSet(SpriteSetId);

        var root = new GameObject(InstanceName);
        try
        {
            // Voz 3D propia: el lip-sync la lee (GetOutputData) y suena con el juego en pausa (D5)
            AudioSource voice = root.AddComponent<AudioSource>();
            Configure3D(voice);
            voice.ignoreListenerPause = true;
            LipSyncDriver lipSync = root.AddComponent<LipSyncDriver>();
            CentavoController controller = root.AddComponent<CentavoController>();
            CentavoInterrogation interrogation = root.AddComponent<CentavoInterrogation>(); // escena (3C.3)

            // Dibujo
            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            placeholder.name = "Placeholder";
            Object.DestroyImmediate(placeholder.GetComponent<Collider>());
            placeholder.transform.SetParent(visual, false);
            placeholder.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            placeholder.transform.localScale = new Vector3(0.55f, 0.9f, 0.55f);
            Renderer placeholderRenderer = placeholder.GetComponent<Renderer>();
            placeholderRenderer.sharedMaterial = CartoonKit.M("PiedraOscura"); // gris azulado, como el lobo
            placeholderRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var holder = new GameObject("SpriteHolder");
            holder.transform.SetParent(visual, false);
            SpriteRenderer sprite = holder.AddComponent<SpriteRenderer>();
            sprite.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (set.TryGet("idle", out CharacterSpriteSet.AnimClip idle)) sprite.sprite = idle.frames[0]; // vista previa
            BillboardSprite billboard = holder.AddComponent<BillboardSprite>();
            var billboardSo = new SerializedObject(billboard);
            billboardSo.FindProperty("spriteSet").objectReferenceValue = set;
            SerializedProperty placeholders = billboardSo.FindProperty("placeholderRenderers");
            placeholders.arraySize = 1;
            placeholders.GetArrayElementAtIndex(0).objectReferenceValue = placeholderRenderer;
            billboardSo.ApplyModifiedPropertiesWithoutUndo();

            // Hitbox: la tamaña CentavoController según la pose (de pie, cubierto, sentado)
            var hitboxGo = new GameObject("Hitbox");
            hitboxGo.transform.SetParent(root.transform, false);
            CapsuleCollider hitbox = hitboxGo.AddComponent<CapsuleCollider>();
            hitbox.direction = 1;
            hitbox.radius = 0.3f;
            hitbox.height = 1.8f;
            hitbox.center = new Vector3(0f, 0.9f, 0f);

            // Disparo: sonido, fogonazo y revólver que cae
            var sfxGo = new GameObject("Sfx");
            sfxGo.transform.SetParent(root.transform, false);
            sfxGo.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            AudioSource sfx = sfxGo.AddComponent<AudioSource>();
            Configure3D(sfx);

            var flashGo = new GameObject("Fogonazo");
            flashGo.transform.SetParent(root.transform, false);
            flashGo.transform.localPosition = new Vector3(0f, 1.35f, 0.35f);
            Light flash = flashGo.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = new Color(1f, 0.78f, 0.4f);
            flash.range = 3f;
            flash.intensity = 4f;
            flash.shadows = LightShadows.None;
            flash.enabled = false;

            GameObject revolver = GameObject.CreatePrimitive(PrimitiveType.Cube);
            revolver.name = "Revolver_Suelto";
            revolver.layer = IgnoreRaycastLayer; // no tapa la vista ni los perdigones
            revolver.transform.SetParent(root.transform, false);
            revolver.transform.localPosition = new Vector3(0.3f, 1.3f, 0.3f);
            revolver.transform.localScale = new Vector3(0.05f, 0.12f, 0.22f);
            Renderer revolverRenderer = revolver.GetComponent<Renderer>();
            revolverRenderer.sharedMaterial = CartoonKit.M("Metal");
            revolverRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            revolver.SetActive(false);

            var lipSo = new SerializedObject(lipSync);
            lipSo.FindProperty("voiceSource").objectReferenceValue = voice;
            lipSo.ApplyModifiedPropertiesWithoutUndo();

            var so = new SerializedObject(controller);
            so.FindProperty("billboard").objectReferenceValue = billboard;
            so.FindProperty("visual").objectReferenceValue = visual;
            so.FindProperty("hitbox").objectReferenceValue = hitbox;
            so.FindProperty("voiceSource").objectReferenceValue = voice;
            so.FindProperty("sfxSource").objectReferenceValue = sfx;
            so.FindProperty("lipSync").objectReferenceValue = lipSync;
            so.FindProperty("muzzleFlash").objectReferenceValue = flash;
            so.FindProperty("droppedRevolver").objectReferenceValue = revolver;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Canvas del interrogatorio, si ya se creó (si no, lo enlaza "Crear UI de interrogatorio")
            var canvasPrefab = AssetDatabase.LoadAssetAtPath<InterrogationDirector>(InterrogationUIBuilder.PrefabPath);
            var interrogationSo = new SerializedObject(interrogation);
            interrogationSo.FindProperty("canvasPrefab").objectReferenceValue = canvasPrefab;
            interrogationSo.ApplyModifiedPropertiesWithoutUndo();
            if (canvasPrefab == null)
                report.AppendLine("⚠ Falta InterrogationCanvas: correr Plomo y Paja → Contador → Crear UI de interrogatorio.");

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            report.AppendLine(prefab != null ? $"✓ Prefab {PrefabPath}" : $"✗ No se pudo guardar {PrefabPath}");
            if (!set.HasAnyFrames)
                report.AppendLine("⚠ El set de sprites 'contador' está vacío: correr Plomo y Paja → Reconstruir Sets de Sprites (mientras, se ve la cápsula).");
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>Fuente 3D como la voz de los guardias (lineal, mín. 2 m), con máximo 25 m. No hay mixer en el proyecto.</summary>
    private static void Configure3D(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2f;
        source.maxDistance = 25f;
        source.dopplerLevel = 0f;
    }

    // ==================================================================
    // Escena
    // ==================================================================

    private static bool Place(GameObject prefab, StringBuilder report)
    {
        if (prefab == null) return false;
        bool ok = true;

        ContadorShed shed = Object.FindObjectOfType<ContadorShed>(true);
        if (shed == null || shed.StandAnchor == null)
        {
            report.AppendLine("✗ No hay ContadorShed (o le falta Anchor_CentavoStand): correr Construir patio y cuarto.");
            return false;
        }

        Scene scene = shed.gameObject.scene;
        Transform actors = FindOrCreateRoot(scene, ActorsRoot);

        // Conserva la primera instancia del prefab; borra duplicados y copias desempaquetadas
        GameObject keep = null;
        foreach (CentavoController existing in Object.FindObjectsOfType<CentavoController>(true))
        {
            GameObject go = existing.gameObject;
            bool isInstance = PrefabUtility.IsAnyPrefabInstanceRoot(go) &&
                              PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) == PrefabPath;
            if (isInstance && keep == null) keep = go;
            else Object.DestroyImmediate(go);
        }
        if (keep == null) keep = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);

        keep.name = InstanceName;
        keep.transform.SetParent(actors, false);
        keep.transform.SetPositionAndRotation(shed.StandAnchor.position, shed.StandAnchor.rotation);

        // Referencias de escena (overrides de la instancia): el cuarto y los guardias del patio
        var so = new SerializedObject(keep.GetComponent<CentavoController>());
        so.FindProperty("shed").objectReferenceValue = shed;
        SerializedProperty guards = so.FindProperty("yardGuards");
        guards.arraySize = 0;
        foreach (string guardName in YardGuardNames)
        {
            EnemyHealth guard = FindGuard(actors, guardName);
            if (guard == null)
            {
                report.AppendLine($"⚠ No se encontró {guardName}: Centavo no lo esperará (D2).");
                ok = false;
                continue;
            }
            guards.arraySize++;
            guards.GetArrayElementAtIndex(guards.arraySize - 1).objectReferenceValue = guard;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        report.AppendLine($"✓ Centavo en {ActorsRoot}/{InstanceName}, sobre Anchor_CentavoStand {shed.StandAnchor.position}");
        report.AppendLine($"✓ Espera a {guards.arraySize} guardia(s) del patio");
        if (shed.SeatedAnchor == null) { report.AppendLine("✗ Falta Anchor_CentavoSeated en el cuarto"); ok = false; }
        if (shed.ExitPath == null || shed.ExitPath.Length == 0) { report.AppendLine("✗ Falta Exit_Path en el cuarto"); ok = false; }
        if (shed.ActivationRadius <= 0f) { report.AppendLine("✗ Falta Trigger_Activacion en el cuarto"); ok = false; }
        return ok;
    }

    private static EnemyHealth FindGuard(Transform actors, string name)
    {
        Transform t = actors.Find(name);
        if (t == null)
        {
            GameObject go = GameObject.Find(name);
            if (go != null) t = go.transform;
        }
        return t != null ? t.GetComponent<EnemyHealth>() : null;
    }

    private static Transform FindOrCreateRoot(Scene scene, string name)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
            if (go.name == name) return go.transform;
        var root = new GameObject(name);
        SceneManager.MoveGameObjectToScene(root, scene);
        return root.transform;
    }
}
