using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static LevelBuilderUtils;

/// <summary>
/// Genera desde cero el greybox de Nivel Porky (casa destruida del hermano menor).
/// MENÚ: Plomo y Paja → Generar Nivel Porky
///
/// PLANO (metros, X = a lo largo de la calle, Z = hacia el fondo):
///   Calle 40x10 (z -20..-10) → patio delantero (z -10..-6) → casa 16x12 (z -6..6)
///   → patio trasero 20x15 (z 6..21) → callejón (z 21..33, salida al fondo)
///
///   Casa:  Sala (x -8..2, z -6..2) | Dormitorio (x 2..8, z -6..2)
///          Cocina (x -8..8, z 2..6) a lo ancho del fondo
///   Puerta delantera en la sala, puerta trasera en la cocina,
///   boquete de 3 m en la fachada del dormitorio, techo parcial.
///
/// Camino libre de escombros: puerta delantera → sala → cocina → puerta trasera → callejón.
/// </summary>
public static class LevelBuilder_NivelPorky
{
    private const string ScenePath = "Assets/Scenes/NivelPorky.unity";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    private const float WallHeight = 3.5f;
    private const float WallThickness = 0.2f;
    private const float DoorWidth = 1.6f;
    private const float DoorHeight = 2.4f;

    // Materiales (se crean en Assets/Materials/Greybox si faltan)
    private static Material _floor, _wall, _wood, _debris, _ground;

    [MenuItem("Plomo y Paja/Generar Nivel Porky")]
    public static void Generate()
    {
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Generar Nivel Porky",
                $"{ScenePath} ya existe y se va a regenerar desde cero.\nSe perderán los cambios hechos a mano en esa escena.",
                "Sobrescribir", "Cancelar"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // Player, IA y guardias salen de prefabs: crear los que falten antes de vaciar la escena
        TestPrefabGenerator.EnsurePrefabs(false);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        LoadMaterials();
        Transform environment = CreateGroup("--- ENVIRONMENT ---");
        Transform lighting = CreateGroup("--- LIGHTING ---");
        Transform markers = CreateGroup("--- MARKERS ---");

        BuildGround(environment);
        BuildHouse(environment);
        BuildBoundaries(environment);
        BuildDestruction(environment);
        BuildFurniture(environment);
        BuildStreetLamp(environment, lighting);
        BuildLighting(lighting);
        BuildMarkers(markers);
        NoirVolumeBuilder.CreateGlobalVolume(lighting);
        BuildActors();

        // Solo el entorno es estático; luces y marcadores no
        MarkStaticRecursive(environment.gameObject);

        // Guardar antes de hornear: el NavMesh se guarda junto a la escena
        EditorSceneManager.SaveScene(scene, ScenePath);
        BakeNavMesh();
        EditorSceneManager.SaveScene(scene, ScenePath);

        AddToBuildSettings();
        AssetDatabase.SaveAssets();

        Debug.Log($"[LevelBuilder] Nivel Porky generado en {ScenePath}");
    }

    private static void LoadMaterials()
    {
        _floor = GetOrCreateMaterial("Greybox_Suelo", new Color(0.42f, 0.42f, 0.42f));
        _wall = GetOrCreateMaterial("Greybox_Pared", new Color(0.26f, 0.26f, 0.27f));
        _wood = GetOrCreateMaterial("Greybox_Madera", new Color(0.36f, 0.24f, 0.15f), 0.2f);
        _debris = GetOrCreateMaterial("Greybox_Escombros", new Color(0.33f, 0.31f, 0.29f));
        _ground = GetOrCreateMaterial("Greybox_Exterior", new Color(0.17f, 0.17f, 0.19f));
    }

    // ============ Suelos ============

    private static void BuildGround(Transform env)
    {
        Transform ground = CreateGroup("Suelos", env);
        CreateBox("Calle", new Vector3(0f, -0.1f, -15f), new Vector3(40f, 0.2f, 10f), _ground, ground);
        // Patio delantero + pasillos laterales + patio trasero en una sola losa (z -10..21)
        CreateBox("Patios", new Vector3(0f, -0.1f, 5.5f), new Vector3(20f, 0.2f, 31f), _ground, ground);
        CreateBox("Callejon_Suelo", new Vector3(0f, -0.1f, 27f), new Vector3(4f, 0.2f, 12f), _ground, ground);
        // Piso de la casa 2 cm más alto para que no parpadee con el patio
        CreateBox("Casa_Piso", new Vector3(0f, -0.08f, 0f), new Vector3(16.2f, 0.2f, 12.2f), _floor, ground);
    }

    // ============ Casa ============

    private static void BuildHouse(Transform env)
    {
        Transform house = CreateGroup("Casa", env);
        float t = WallThickness;

        // Fachada (z = -6): puerta delantera en x = -5, boquete de 3 m en x = 4.5
        CreateWallWithOpenings("Pared_Fachada", new Vector3(-8f - t / 2f, 0f, -6f), new Vector3(8f + t / 2f, 0f, -6f),
            WallHeight, t, _wall, house,
            new WallOpening(3.1f, DoorWidth, DoorHeight),     // x = -5
            new WallOpening(12.6f, 3f, WallHeight));          // x = 4.5 (boquete)

        // Fondo (z = 6): puerta trasera en x = -4
        CreateWallWithDoorway("Pared_Fondo", new Vector3(-8f - t / 2f, 0f, 6f), new Vector3(8f + t / 2f, 0f, 6f),
            WallHeight, t, _wall, house, 4.1f, DoorWidth, DoorHeight);

        // Laterales sólidos
        CreateWallWithOpenings("Pared_Izquierda", new Vector3(-8f, 0f, -6f), new Vector3(-8f, 0f, 6f), WallHeight, t, _wall, house);
        CreateWallWithOpenings("Pared_Derecha", new Vector3(8f, 0f, -6f), new Vector3(8f, 0f, 6f), WallHeight, t, _wall, house);

        // Interiores
        CreateWallWithDoorway("Pared_Sala_Dormitorio", new Vector3(2f, 0f, -6f), new Vector3(2f, 0f, 2f),
            WallHeight, t, _wall, house, 4f, DoorWidth, DoorHeight);          // puerta en z = -2
        CreateWallWithDoorway("Pared_Sala_Cocina", new Vector3(-8f, 0f, 2f), new Vector3(2f, 0f, 2f),
            WallHeight, t, _wall, house, 5f, DoorWidth, DoorHeight);          // puerta en x = -3
        CreateWallWithDoorway("Pared_Dormitorio_Cocina", new Vector3(2f, 0f, 2f), new Vector3(8f, 0f, 2f),
            WallHeight, t, _wall, house, 3f, DoorWidth, DoorHeight);          // puerta en x = 5

        // Techo parcial: cocina completa y fondo de la sala; dormitorio y frente de la sala sin techo
        Transform roof = CreateGroup("Techo", house);
        CreateBox("Techo_Cocina", new Vector3(0f, WallHeight + 0.1f, 4.05f), new Vector3(16.2f, 0.2f, 4.1f), _wall, roof);
        CreateBox("Techo_Sala", new Vector3(-3f, WallHeight + 0.1f, -0.5f), new Vector3(10.2f, 0.2f, 5f), _wall, roof);
    }

    // ============ Límites: valla, callejón, edificios de enfrente ============

    private static void BuildBoundaries(Transform env)
    {
        Transform bounds = CreateGroup("Limites", env);
        const float fence = 2f;

        CreateWallWithOpenings("Valla_Izquierda", new Vector3(-10f, 0f, -10f), new Vector3(-10f, 0f, 21f), fence, 0.2f, _wood, bounds);
        CreateWallWithOpenings("Valla_Derecha", new Vector3(10f, 0f, -10f), new Vector3(10f, 0f, 21f), fence, 0.2f, _wood, bounds);
        // Valla del fondo con salida de 4 m al callejón (x -2..2)
        CreateWallWithOpenings("Valla_Fondo", new Vector3(-10.1f, 0f, 21f), new Vector3(10.1f, 0f, 21f), fence, 0.2f, _wood, bounds,
            new WallOpening(10.1f, 4f, fence));

        // Callejón
        CreateWallWithOpenings("Callejon_Muro_Izq", new Vector3(-2.1f, 0f, 21f), new Vector3(-2.1f, 0f, 33f), 4f, 0.2f, _wall, bounds);
        CreateWallWithOpenings("Callejon_Muro_Der", new Vector3(2.1f, 0f, 21f), new Vector3(2.1f, 0f, 33f), 4f, 0.2f, _wall, bounds);
        CreateWallWithOpenings("Callejon_Muro_Fondo", new Vector3(-2.2f, 0f, 33.1f), new Vector3(2.2f, 0f, 33.1f), 4f, 0.2f, _wall, bounds);

        // Calle: edificios de enfrente, muros en los extremos y bordes junto al patio
        CreateBox("Edificios_Enfrente", new Vector3(0f, 4f, -21f), new Vector3(40.4f, 8f, 2f), _wall, bounds);
        CreateWallWithOpenings("Calle_Muro_Izq", new Vector3(-20.1f, 0f, -20f), new Vector3(-20.1f, 0f, -10f), 3f, 0.2f, _wall, bounds);
        CreateWallWithOpenings("Calle_Muro_Der", new Vector3(20.1f, 0f, -20f), new Vector3(20.1f, 0f, -10f), 3f, 0.2f, _wall, bounds);
        CreateWallWithOpenings("Calle_Borde_Izq", new Vector3(-20.2f, 0f, -10f), new Vector3(-10f, 0f, -10f), fence, 0.2f, _wood, bounds);
        CreateWallWithOpenings("Calle_Borde_Der", new Vector3(10f, 0f, -10f), new Vector3(20.2f, 0f, -10f), fence, 0.2f, _wood, bounds);
    }

    // ============ Destrucción: losas caídas y escombros ============
    // Nada en x -6..-2 de sala/cocina ni frente a ninguna puerta.

    private static void BuildDestruction(Transform env)
    {
        Transform ruins = CreateGroup("Escombros", env);

        // Pedazo de techo colgando dentro del dormitorio (apoyado en la pared derecha)
        CreateBox("Losa_Techo_Colgante", new Vector3(6.6f, 1.5f, -0.5f), new Vector3(3.6f, 0.2f, 3f), _wall, ruins,
            new Vector3(0f, 0f, 50f));
        // Pedazo de fachada caído en el patio delantero, al lado del boquete (deja libre x 3..5 y z < -8.3)
        CreateBox("Losa_Fachada_Caida", new Vector3(6.3f, 0.3f, -7.2f), new Vector3(2.6f, 0.2f, 1.8f), _wall, ruins,
            new Vector3(0f, 15f, 12f));
        // Viga caída en el patio trasero, lejos del camino a la valla
        CreateBox("Viga_Caida", new Vector3(7f, 0.15f, 16f), new Vector3(0.3f, 0.3f, 3.5f), _wood, ruins,
            new Vector3(0f, 25f, 0f));

        var cubes = new (Vector3 pos, float size, float yaw)[]
        {
            (new Vector3(7.2f, 0f, -4.6f), 0.5f, 20f),    // dormitorio
            (new Vector3(6.2f, 0f, -5.3f), 0.3f, 40f),
            (new Vector3(7.3f, 0f, -3.4f), 0.4f, 10f),
            (new Vector3(3.2f, 0f, -4.8f), 0.3f, 65f),
            (new Vector3(-8.5f, 0f, -9.2f), 0.4f, 30f),   // patio delantero
            (new Vector3(2.4f, 0f, -7.3f), 0.3f, 55f),
            (new Vector3(-7.3f, 0f, -5.3f), 0.4f, 15f),   // esquina de la sala
            (new Vector3(6f, 0f, 12f), 0.5f, 35f),        // patio trasero
            (new Vector3(7f, 0f, 13f), 0.3f, 70f),
            (new Vector3(-8f, 0f, 18f), 0.4f, 5f),
        };
        for (int i = 0; i < cubes.Length; i++)
        {
            var c = cubes[i];
            CreateBox($"Escombro_{i:00}", c.pos + Vector3.up * (c.size * 0.5f), Vector3.one * c.size, _debris, ruins,
                new Vector3(0f, c.yaw, 0f));
        }
    }

    // ============ Muebles ============

    private static void BuildFurniture(Transform env)
    {
        Transform furniture = CreateGroup("Muebles", env);
        CreateBox("Sofa", new Vector3(-7.3f, 0.45f, -1f), new Vector3(1f, 0.9f, 2.2f), _wood, furniture);
        CreateBox("Cajonera", new Vector3(2.5f, 0.5f, 0.8f), new Vector3(0.6f, 1f, 1.2f), _wood, furniture);
        CreateBox("Mesa", new Vector3(2f, 0.4f, 4f), new Vector3(1.6f, 0.8f, 1f), _wood, furniture);
        CreateBox("Nevera", new Vector3(7.4f, 0.9f, 5.2f), new Vector3(0.8f, 1.8f, 0.8f), _floor, furniture);
    }

    // ============ Luces ============

    private static void BuildStreetLamp(Transform env, Transform lighting)
    {
        Transform lamp = CreateGroup("Farol_Roto", env);

        // Poste un poco torcido
        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Farol_Poste";
        pole.transform.SetParent(lamp, false);
        pole.transform.position = new Vector3(-7f, 2f, -10.6f);
        pole.transform.rotation = Quaternion.Euler(0f, 0f, 4f);
        pole.transform.localScale = new Vector3(0.15f, 2f, 0.15f);
        pole.GetComponent<MeshRenderer>().sharedMaterial = _wall;

        CreateBox("Farol_Brazo", new Vector3(-6.6f, 4f, -10.6f), new Vector3(1f, 0.08f, 0.08f), _wall, lamp);
        CreateBox("Farol_Cabeza", new Vector3(-6.15f, 3.9f, -10.6f), new Vector3(0.3f, 0.15f, 0.3f), _wall, lamp);

        Light light = CreatePointLight("Farol_Luz", new Vector3(-6.15f, 3.7f, -10.6f),
            new Color32(0xFF, 0xB0, 0x60, 0xFF), 2.5f, 12f, lighting);
        light.gameObject.AddComponent<LightFlicker>();
    }

    private static void BuildLighting(Transform lighting)
    {
        // Luna: direccional tenue y fría
        var moonGo = new GameObject("Luz_Luna");
        moonGo.transform.SetParent(lighting, false);
        moonGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        Light moon = moonGo.AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.6f, 0.7f, 1f);
        moon.intensity = 0.15f;
        moon.shadows = LightShadows.Soft;

        // Foco cálido y débil en la sala
        CreatePointLight("Luz_Sala", new Vector3(-3f, 2.9f, -2f), new Color(1f, 0.78f, 0.5f), 0.8f, 7f, lighting);

        // Ambiente nocturno
        RenderSettings.sun = moon;
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.07f, 0.08f, 0.11f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.05f, 0.06f, 0.08f);
        RenderSettings.fogDensity = 0.025f;
    }

    // ============ Marcadores ============

    private static void BuildMarkers(Transform markers)
    {
        CreateMarker("PlayerSpawn", MarkerType.PlayerSpawn, "", new Vector3(0f, 0f, -15f), 0f, markers);

        CreateMarker("Clue_nota_porky_01", MarkerType.Clue, "nota_porky_01", new Vector3(2.5f, 1.1f, 0.8f), 90f, markers);
        CreateMarker("Clue_contrato_01", MarkerType.Clue, "contrato_01", new Vector3(-6.5f, 0.3f, -2.5f), 0f, markers);

        // Patrulla alrededor del exterior de la casa (pasillos laterales de 1.8 m)
        Vector3[] patrol =
        {
            new Vector3(-9f, 0f, -8f), new Vector3(9f, 0f, -8f), new Vector3(9f, 0f, 9f), new Vector3(-9f, 0f, 9f)
        };
        for (int i = 0; i < patrol.Length; i++)
            CreateMarker($"Patrol_A_{i}", MarkerType.PatrolWaypoint, "A", patrol[i], 0f, markers);

        // Centinela en el patio trasero mirando a la puerta trasera (x -4, z 6)
        CreateMarker("Sentinel_PatioTrasero", MarkerType.SentinelPoint, "", new Vector3(-4f, 0f, 13f), 180f, markers);

        CreateMarker("Guard_guardia_porky", MarkerType.InterrogableGuard, "guardia_porky", new Vector3(5.5f, 0f, 4.3f), -90f, markers);

        LevelMarker exit = CreateMarker("LevelExit", MarkerType.LevelExit, "nivel_porky_salida", new Vector3(0f, 0f, 32f), 0f, markers);
        BoxCollider trigger = exit.gameObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 1.5f, 0f);
        trigger.size = new Vector3(3f, 3f, 1f);
    }

    // ============ Actores: jugador, IA y guardias (se colocan sobre los marcadores) ============

    private static void BuildActors()
    {
        var markers = new List<LevelMarker>(Object.FindObjectsOfType<LevelMarker>());
        markers.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        // El Player trae su propia cámara y AudioListener
        LevelMarker spawn = markers.Find(m => m.Type == MarkerType.PlayerSpawn);
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabGenerator.PlayerPath);
        if (playerPrefab != null && spawn != null)
        {
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
        }
        else
        {
            Debug.LogWarning("[LevelBuilder] Sin Player.prefab o PlayerSpawn: el nivel queda sin jugador.");
        }

        // Debe ser raíz de la escena para que DontDestroyOnLoad funcione
        PlaceRoot(TestPrefabGenerator.AICompanionPath);

        Transform actors = CreateGroup("--- ACTORS ---");
        GameObject guardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabGenerator.GuardPath);
        GameObject interrogablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabGenerator.InterrogablePath);

        // Patrullero: ruta Patrol_A_0..3
        var waypoints = markers.FindAll(m => m.Type == MarkerType.PatrolWaypoint && m.Id == "A");
        if (guardPrefab != null && waypoints.Count > 0)
        {
            GameObject patrol = SpawnAt(guardPrefab, "Guard_Patrulla_A", waypoints[0].transform, actors);
            var so = new SerializedObject(patrol.GetComponent<EnemyAI>());
            so.FindProperty("enemyType").enumValueIndex = (int)EnemyAI.EnemyType.Patrullero;
            SerializedProperty points = so.FindProperty("patrolPoints");
            points.arraySize = waypoints.Count;
            for (int i = 0; i < waypoints.Count; i++)
                points.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i].transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Centinela: quieto mirando hacia donde apunta el marcador
        LevelMarker sentinel = markers.Find(m => m.Type == MarkerType.SentinelPoint);
        if (guardPrefab != null && sentinel != null)
        {
            GameObject guard = SpawnAt(guardPrefab, "Guard_Centinela", sentinel.transform, actors);
            var so = new SerializedObject(guard.GetComponent<EnemyAI>());
            so.FindProperty("enemyType").enumValueIndex = (int)EnemyAI.EnemyType.Centinela;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Guardias interrogables: npcId = id del marcador
        foreach (LevelMarker marker in markers.FindAll(m => m.Type == MarkerType.InterrogableGuard))
        {
            if (interrogablePrefab == null) break;
            GameObject guard = SpawnAt(interrogablePrefab, $"Guard_Interrogable_{marker.Id}", marker.transform, actors);
            var so = new SerializedObject(guard.GetComponent<EnemyHealth>());
            so.FindProperty("npcId").stringValue = marker.Id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        RemoveStrayCameras();
    }

    private static GameObject SpawnAt(GameObject prefab, string name, Transform marker, Transform parent)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = name;
        instance.transform.SetParent(parent, false);
        instance.transform.SetPositionAndRotation(marker.position, marker.rotation);
        return instance;
    }

    private static void PlaceRoot(string prefabPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[LevelBuilder] Falta {prefabPath}.");
            return;
        }
        PrefabUtility.InstantiatePrefab(prefab);
    }

    /// <summary>Solo debe haber la cámara y el AudioListener del Player.</summary>
    private static void RemoveStrayCameras()
    {
        foreach (Camera cam in Object.FindObjectsOfType<Camera>(true))
            if (cam.GetComponentInParent<PlayerController>() == null)
                Object.DestroyImmediate(cam.gameObject);

        foreach (AudioListener listener in Object.FindObjectsOfType<AudioListener>(true))
            if (listener.GetComponentInParent<PlayerController>() == null)
                Object.DestroyImmediate(listener);
    }

    // ============ NavMesh y Build Settings ============

    private static void BakeNavMesh()
    {
        // com.unity.ai.navigation no está instalado: se usa el baker integrado
#pragma warning disable 618
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
#pragma warning restore 618

        float radius = UnityEngine.AI.NavMesh.GetSettingsByIndex(0).agentRadius;
        if (radius > 0.5f)
            Debug.LogWarning($"[LevelBuilder] Radio del agente = {radius}. Con puertas de {DoorWidth} m conviene 0.5 o menos.");
    }

    private static void AddToBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == ScenePath)) return;

        int sampleIndex = scenes.FindIndex(s => s.path == SampleScenePath);
        if (sampleIndex < 0 && File.Exists(SampleScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(SampleScenePath, true));
            sampleIndex = scenes.Count - 1;
        }

        scenes.Insert(sampleIndex + 1, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
