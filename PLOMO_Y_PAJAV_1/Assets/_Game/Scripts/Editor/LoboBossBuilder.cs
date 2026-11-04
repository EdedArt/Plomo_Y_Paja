using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// MENÚ: Plomo y Paja → Nivel 2 → Colocar Don Lobo (fase N2.2). También lo llama Generar Campamento.
/// Crea/actualiza Prefabs/Level2/DonLobo.prefab y deja una sola instancia en Anchor_LoboStart, con sus anclas de la escena.
///
/// PREFAB (raíz a escala 1.2, L11):
///   DonLobo (LoboBossController, NavMeshAgent, AudioSource de voz 3D, LipSyncDriver, EnemyGunfire)
///   ├── Visual
///   │   ├── Placeholder (cápsula sin collider: se ve si faltan los sprites)
///   │   └── SpriteHolder (SpriteRenderer + BillboardSprite con el set "lobo")
///   ├── Hitbox (CapsuleCollider, capa Default)
///   └── Pistola_Suelta (apagada; adorno que cae; capa Ignore Raycast)
/// DIBUJOS: el set "lobo". Mientras esté vacío usa los dibujos viejos de "donlobo" (idle, shoot, surrender, talk) con aviso. Al
/// llegar el arte, basta correr Reconstruir Sets de Sprites y este menú.
/// </summary>
public static class LoboBossBuilder
{
    public const string PrefabFolder = "Assets/_Game/Prefabs/Level2";
    public const string PrefabPath = PrefabFolder + "/DonLobo.prefab";
    public const float RootScale = 1.2f;

    private const string InstanceName = "Don_Lobo";
    private const string ActorsRoot = "--- ACTORS ---";
    private const string MarkersRoot = "--- MARKERS ---";
    private const string SpriteSetId = "lobo";
    private const string OldSpriteSetId = "donlobo";
    private const int IgnoreRaycastLayer = 2;
    private const int CoverCount = 4;

    private static readonly string[] Frames =
    {
        "idle_01", "idle_02", "peek_01", "cover_01", "shoot_01", "call_01", "hit_01", "rodillas_01", "rodillas_02",
        "talk_01", "talk_02", "angry_01", "angry_02", "laugh_01", "laugh_02", "death_01",
    };

    private static readonly string[] Voices =
    {
        "lobo_combate_01", "lobo_combate_02", "lobo_combate_03", "lobo_combate_04", "lobo_combate_05", "lobo_cae_01", "lobo_rodillas_01",
    };

    [MenuItem("Plomo y Paja/Nivel 2/Colocar Don Lobo")]
    public static void PlaceMenu()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != LevelBuilder_Campamento.ScenePath)
        {
            if (!File.Exists(LevelBuilder_Campamento.ScenePath))
            {
                EditorUtility.DisplayDialog("Colocar Don Lobo", "No existe " + LevelBuilder_Campamento.ScenePath +
                    ". Primero: Plomo y Paja → Nivel 2 → Generar Campamento.", "Entendido");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(LevelBuilder_Campamento.ScenePath, OpenSceneMode.Single);
        }

        CartoonKit.BeginSession();
        CharacterSpriteSetBuilder.RebuildAll();

        var report = new StringBuilder();
        bool ok = BuildAndPlace(report);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[Lobo] Colocar Don Lobo:\n" + report);
        EditorUtility.DisplayDialog("Colocar Don Lobo",
            (ok ? "Listo.\n\n" : "Revisa la consola: hay algo que falta.\n\n") + report + "\nGuarda la escena (Ctrl+S).", "Entendido");
    }

    /// <summary>Prefab + instancia en la escena abierta. No guarda ni llama a CartoonKit.BeginSession (lo hace quien llama).</summary>
    public static bool BuildAndPlace(StringBuilder report)
    {
        EnsureAssetFolders();
        GameObject prefab = BuildPrefab(report);
        bool ok = prefab != null && Place(prefab, report);
        ReportMissing(report);
        return ok;
    }

    private static void EnsureAssetFolders()
    {
        LevelBuilderUtils.EnsureFolder(PrefabFolder);
        LevelBuilderUtils.EnsureFolder(CharacterSpriteImporter.CharactersFolder + "/" + SpriteSetId);
        LevelBuilderUtils.EnsureFolder(GameAudioImporter.VoicesFolder + "/" + SpriteSetId);
    }

    // ==================================================================
    // Prefab
    // ==================================================================

    private static GameObject BuildPrefab(StringBuilder report)
    {
        CharacterSpriteSet set = CharacterSpriteSetBuilder.GetOrCreateSet(SpriteSetId);
        if (!set.HasAnyFrames)
        {
            CharacterSpriteSet old = CharacterSpriteSetBuilder.GetOrCreateSet(OldSpriteSetId);
            if (old.HasAnyFrames)
            {
                set = old;
                report.AppendLine("⚠ El set 'lobo' está vacío: usa los dibujos viejos de 'donlobo' (solo idle, shoot, surrender, talk).");
            }
            else report.AppendLine("⚠ Sin dibujos de Don Lobo: se ve la cápsula.");
        }

        var root = new GameObject("DonLobo");
        root.transform.localScale = Vector3.one * RootScale;
        try
        {
            AudioSource voice = root.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.loop = false;
            voice.spatialBlend = 1f;
            voice.rolloffMode = AudioRolloffMode.Linear;
            voice.minDistance = 3f;
            voice.maxDistance = 30f;
            voice.dopplerLevel = 0f;
            voice.ignoreListenerPause = true;
            LipSyncDriver lipSync = root.AddComponent<LipSyncDriver>();

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.9f;
            agent.speed = 2.5f;
            agent.acceleration = 12f;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = 0.1f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            placeholder.name = "Placeholder";
            Object.DestroyImmediate(placeholder.GetComponent<Collider>());
            placeholder.transform.SetParent(visual, false);
            placeholder.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            placeholder.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
            Renderer placeholderRenderer = placeholder.GetComponent<Renderer>();
            placeholderRenderer.sharedMaterial = CartoonKit.M("Sofa"); // morado, como su traje
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

            var hitboxGo = new GameObject("Hitbox");
            hitboxGo.transform.SetParent(root.transform, false);
            CapsuleCollider hitbox = hitboxGo.AddComponent<CapsuleCollider>();
            hitbox.direction = 1;
            hitbox.radius = 0.32f;
            hitbox.height = 1.8f;
            hitbox.center = new Vector3(0f, 0.9f, 0f);

            GameObject pistol = CartoonKit.Box("Pistola_Suelta", new Vector3(0.25f, 1.1f, 0f) * RootScale, new Vector3(0.34f, 0.1f, 0.07f),
                CartoonKit.M("Laton"), root.transform, default, 0.02f, false);
            pistol.layer = IgnoreRaycastLayer;
            pistol.AddComponent<BoxCollider>().size = new Vector3(0.34f, 0.1f, 0.07f);
            pistol.SetActive(false);

            EnemyGunfire gunfire = root.AddComponent<EnemyGunfire>();
            MuzzlePointsBuilder.ConfigureGunfire(gunfire, set.name.Contains(OldSpriteSetId) ? OldSpriteSetId : SpriteSetId);
            var gunfireSo = new SerializedObject(gunfire);
            gunfireSo.FindProperty("spriteRenderer").objectReferenceValue = sprite;
            gunfireSo.ApplyModifiedPropertiesWithoutUndo();

            LoboBossController controller = root.AddComponent<LoboBossController>();
            var lipSo = new SerializedObject(lipSync);
            lipSo.FindProperty("voiceSource").objectReferenceValue = voice;
            lipSo.ApplyModifiedPropertiesWithoutUndo();

            var so = new SerializedObject(controller);
            so.FindProperty("billboard").objectReferenceValue = billboard;
            so.FindProperty("visual").objectReferenceValue = visual;
            so.FindProperty("hitbox").objectReferenceValue = hitbox;
            so.FindProperty("voiceSource").objectReferenceValue = voice;
            so.FindProperty("lipSync").objectReferenceValue = lipSync;
            so.FindProperty("droppedPistol").objectReferenceValue = pistol;
            so.FindProperty("reinforcementPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabGenerator.GuardPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            report.AppendLine(prefab != null ? $"✓ Prefab {PrefabPath} (raíz ×{RootScale:0.0})" : "✗ No se pudo guardar " + PrefabPath);
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // ==================================================================
    // Escena
    // ==================================================================

    private static bool Place(GameObject prefab, StringBuilder report)
    {
        GameObject markersGo = GameObject.Find("/" + MarkersRoot);
        if (markersGo == null)
        {
            report.AppendLine("✗ La escena no tiene " + MarkersRoot + ": correr Generar Campamento.");
            return false;
        }

        Transform markers = markersGo.transform;
        var missing = new List<string>();
        Transform start = Required(markers, "Anchor_LoboStart", missing);
        Transform cam = Required(markers, "Anchor_ConfrontationCam", missing);
        Transform refuerzoA = Required(markers, "Anchor_Refuerzo_A", missing); // apagadas: Find las encuentra igual por ser hijas
        Transform refuerzoB = Required(markers, "Anchor_Refuerzo_B", missing);
        Transform triggerT = Required(markers, "Trigger_Salon", missing);
        var covers = new Transform[CoverCount];
        for (int i = 0; i < CoverCount; i++) covers[i] = Required(markers, "Anchor_LoboCover_" + i, missing);
        if (missing.Count > 0)
        {
            report.AppendLine("✗ Faltan anclas: " + string.Join(", ", missing) + ". Correr Generar Campamento.");
            return false;
        }

        foreach (LoboBossController existing in Object.FindObjectsOfType<LoboBossController>(true))
            Object.DestroyImmediate(existing.gameObject);

        GameObject actorsGo = GameObject.Find("/" + ActorsRoot);
        Transform actors = actorsGo != null ? actorsGo.transform : LevelBuilderUtils.CreateGroup(ActorsRoot);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = InstanceName;
        instance.transform.SetParent(actors, true);
        instance.transform.SetPositionAndRotation(start.position, start.rotation);

        var so = new SerializedObject(instance.GetComponent<LoboBossController>());
        so.FindProperty("startAnchor").objectReferenceValue = start;
        so.FindProperty("confrontationCam").objectReferenceValue = cam;
        so.FindProperty("salonTrigger").objectReferenceValue = triggerT.GetComponent<Collider>();
        SetArray(so.FindProperty("coverAnchors"), covers);
        SetArray(so.FindProperty("reinforcementAnchors"), new[] { refuerzoA, refuerzoB });
        so.ApplyModifiedPropertiesWithoutUndo();

        report.AppendLine("✓ Don Lobo en Anchor_LoboStart, con 4 cubiertas y 2 anclas de refuerzos.");
        return true;
    }

    private static Transform Required(Transform parent, string name, List<string> missing)
    {
        Transform t = parent.Find(name);
        if (t == null) missing.Add(name);
        return t;
    }

    private static void SetArray(SerializedProperty property, Transform[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    // ==================================================================
    // Lo que falta
    // ==================================================================

    private static void ReportMissing(StringBuilder report)
    {
        string folder = CharacterSpriteImporter.CharactersFolder + "/" + SpriteSetId + "/";
        var frames = new List<string>();
        foreach (string id in Frames)
            if (AssetDatabase.LoadAssetAtPath<Sprite>(folder + id + ".png") == null) frames.Add(id);
        if (frames.Count > 0) report.AppendLine($"ℹ Faltan {frames.Count} dibujos en {folder}: {string.Join(", ", frames)}");

        AudioLibrary.SetActive(AudioLibraryBuilder.GetOrCreateLibrary()); // en el Editor no hay loader
        var voices = new List<string>();
        foreach (string id in Voices)
            if (!AudioLibrary.HasVoice(id)) voices.Add(id);
        if (voices.Count > 0) report.AppendLine($"ℹ Faltan {voices.Count} voces de combate (no suenan): {string.Join(", ", voices)}");
    }
}
