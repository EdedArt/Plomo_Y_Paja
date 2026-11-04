using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static LevelBuilderUtils;

/// <summary>
/// Genera Campamento.unity, el Nivel 2 (fase N2.1). MENÚ: Plomo y Paja → Nivel 2 → Generar Campamento.
///
/// PLANO (metros, +Z = norte, +X = oriente). Se recorre de sur a norte:
///   Z0 bosque     x -5..5      z -50..-10   sendero de 40 m, inicio del jugador
///   Z1 portón     x -10..10    z -10..5     portón del aserradero, camioneta, cajas
///   Z2 barracones x -17.5..18  z 5..30      3 barracones (uno entrable), carpas, fogata, gramófono
///   Z3 patio      x -13..13    z 30..50     pilas de troncos, sierra bajo su cobertizo
///   Z4 guarida    x -11..11    z 54..74     pasillo y oficina (z 54..60) → salón de 18×14 (z 60..74) → cuarto de atrás
///
/// IDEMPOTENTE: rehace todo lo generado, MENOS el grupo "--- ANCLAS DE PISTAS ---": la escena se abre, no se crea de cero, y
/// lo que Edgar haya puesto dentro de un ancla se queda como está. Los objetos salen de Data/PickupLayout_Campamento.asset
/// (se siembra una sola vez; después manda "Guardar posiciones de objetos").
/// Sin muros invisibles: cada collider que frena al jugador pertenece a un objeto visible en el mismo lugar (N1).
/// </summary>
public static partial class LevelBuilder_Campamento
{
    public const string ScenePath = BuildSceneOrder.CampamentoPath;

    private const string ClueAnchorsRoot = "--- ANCLAS DE PISTAS ---";
    private const string AutoCluePrefix = "Pista_Auto_";
    private const string LayoutPath = "Assets/_Game/Data/PickupLayout_Campamento.asset";
    private const string InterrogationPrefabPath = "Assets/_Game/Prefabs/UI/InterrogationCanvas.prefab";
    private const int IgnoreRaycastLayer = 2;

    private static readonly Color WarmLamp = new Color32(0xFF, 0xB0, 0x60, 0xFF);

    // Anclas del salón: las leen N2.2 y N2.3
    public static readonly Vector3 LoboStart = new Vector3(0f, 0f, 71.7f);
    public static readonly Vector3[] LoboCovers =
    {
        new Vector3(-1.4f, 0f, 71.6f),  // detrás del escritorio
        new Vector3(5f, 0f, 70.7f),     // detrás de la columna noreste
        new Vector3(-5f, 0f, 70.7f),    // detrás de la columna noroeste
        new Vector3(-7f, 0f, 66.4f),    // detrás de la mesa de billar
    };
    public static readonly Vector3 RefuerzoA = new Vector3(-7.9f, 0f, 70f);
    public static readonly Vector3 RefuerzoB = new Vector3(7.9f, 0f, 70f);
    public static readonly Vector3 ConfrontationCam = new Vector3(0f, 1.55f, 67.5f);
    private static readonly Vector3 PlayerStart = new Vector3(0f, 0f, -47.5f);
    public static readonly Vector3 GramophonePosition = new Vector3(4f, 1.1f, 20f);

    // ============ Guardias (L3): 2 patrulleros, 2 centinelas, 1 en la puerta, 1 rendible ============

    private class GuardDef
    {
        public string guardName;
        public EnemyAI.EnemyType type;
        public string id;
        public Vector3[] points;
        public float yaw;
        public string npcId;        // con npcId: Enemy_Interrogable (se rinde y abre su diálogo)
    }

    private static readonly GuardDef[] Guards =
    {
        new GuardDef { guardName = "Guard_Centinela_Porton", type = EnemyAI.EnemyType.Centinela, id = "Porton",
            points = new[] { new Vector3(-4f, 0f, -1.5f) }, yaw = 180f },
        new GuardDef { guardName = "Guard_Patrulla_Porton", type = EnemyAI.EnemyType.Patrullero, id = "Porton", points = new[]
            { new Vector3(4.2f, 0f, -7f), new Vector3(4.2f, 0f, 3f), new Vector3(-6.5f, 0f, 3f), new Vector3(-6.5f, 0f, -7.5f) } },
        new GuardDef { guardName = "Guard_Patrulla_Barracones", type = EnemyAI.EnemyType.Patrullero, id = "Barracones", points = new[]
            { new Vector3(-6f, 0f, 8f), new Vector3(6f, 0f, 8f), new Vector3(6f, 0f, 28f), new Vector3(-6f, 0f, 28f) } },
        new GuardDef { guardName = "Guard_Contable", type = EnemyAI.EnemyType.Centinela, id = "Contable", npcId = "guardia_registro",
            points = new[] { new Vector3(-4.2f, 0f, 21.1f) }, yaw = 180f },
        new GuardDef { guardName = "Guard_Centinela_Patio", type = EnemyAI.EnemyType.Centinela, id = "Patio",
            points = new[] { new Vector3(-3f, 0f, 46.5f) }, yaw = 180f },
        new GuardDef { guardName = "Guard_Centinela_Puerta", type = EnemyAI.EnemyType.Centinela, id = "Puerta",
            points = new[] { new Vector3(3.2f, 0f, 51.8f) }, yaw = 180f },
    };

    // ============ Objetos (L5, N7): siempre en un mueble o contenedor ============
    // Contrato de N1.2: con "container", position.y es la altura de la superficie (el piso del cajón, la repisa, la tapa).
    // Salen de las alturas de Cabinet (cada repisa), OpenCrate (fondo) y Table (h) en LevelBuilder_Campamento.Map/Lodge.

    private const float ShelfLow = 0.45f, ShelfHigh = 1.05f, CrateBottom = 0.08f, WorkbenchTop = 0.9f, ConsoleTop = 0.85f;

    private static PickupLayout.Entry[] PickupSeed() => new[]
    {
        // Campamento
        Seed("Cartuchos_Barracon", PickupKind.Cartuchos, new Vector3(-9.4f, ShelfHigh, 25.55f), "Barracón A: repisa alta del estante"),
        Seed("Botiquin_Carpa", PickupKind.Botiquin, new Vector3(13.5f, CrateBottom, 9.3f), "Carpa A: cajón abierto de la entrada"),
        Seed("Cartuchos_Aserradero", PickupKind.Cartuchos, new Vector3(10.2f, WorkbenchTop, 40.6f), "Aserradero: banco de trabajo junto a la sierra"),
        // Antes del salón (pasillo y oficina)
        Seed("Cartuchos_Pasillo", PickupKind.Cartuchos, new Vector3(-1.12f, ConsoleTop, 56.2f), "Pasillo: consola"),
        Seed("Botiquin_Oficina", PickupKind.Botiquin, new Vector3(10.55f, ShelfHigh, 57f), "Oficina: repisa alta del estante"),
        Seed("Balas_Oficina", PickupKind.BalasRevolver, new Vector3(3f, CrateBottom, 59f), "Oficina: cajón abierto"),
        // Dentro del salón
        Seed("Cartuchos_Gabinete", PickupKind.Cartuchos, new Vector3(-8.55f, ShelfHigh, 61.6f), "Salón: gabinete, muro oeste"),
        Seed("Balas_Barra", PickupKind.BalasRevolver, new Vector3(8.6f, ShelfLow, 62.6f), "Salón: repisa baja detrás de la barra"),
    };

    private static PickupLayout.Entry Seed(string id, PickupKind kind, Vector3 position, string container) =>
        new PickupLayout.Entry { id = id, kind = kind, position = position, container = container };

    // ============ Menú ============

    [MenuItem("Plomo y Paja/Nivel 2/Generar Campamento")]
    public static void Generate()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Generar Campamento",
                ScenePath + " ya existe y se va a regenerar.\nSolo se conserva lo que haya dentro de las anclas de pistas " +
                "(lo de Edgar); los demás cambios hechos a mano se pierden.", "Regenerar", "Cancelar"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var report = new StringBuilder();

        CartoonKit.BeginSession();
        TestPrefabGenerator.EnsurePrefabs(false);
        RenderingSetup.Configure();

        Scene scene = OpenOrCreateScene();
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name != ClueAnchorsRoot) Object.DestroyImmediate(root);

        LoadMaterials();
        NotWalkable.Clear();
        Transform environment = CreateGroup("--- ENVIRONMENT ---");
        Transform vista = CreateGroup("--- VISTA ---");
        Transform lighting = CreateGroup("--- LIGHTING ---");
        Transform markers = CreateGroup("--- MARKERS ---");
        Transform deco = CreateGroup("Decoracion", environment); // sin colliders: no cuenta para el NavMesh

        BuildGround(environment, vista);
        BuildPaths(deco);
        BuildBorders(environment, lighting);
        BuildVista(vista);
        BuildForest(environment, lighting, deco);
        BuildGate(environment, lighting, deco);
        BuildBarracks(environment, lighting, deco);
        BuildYard(environment, lighting, deco);
        BuildLodge(environment, lighting, deco);

        Light moon = BuildMoonAndAmbient(lighting);
        NoirVolumeBuilder.CreateTechnicolorVolume(lighting);
        BuildSky(moon);

        MarkStaticRecursive(environment.gameObject);
        MarkStaticRecursive(vista.gameObject);
        CartoonKit.ExcludeFromNavMesh(vista);
        CartoonKit.ExcludeFromNavMesh(deco);
        foreach (Transform top in NotWalkable) CartoonKit.ExcludeFromNavMesh(top);

        BuildMarkers(markers);
        PlaceClueAnchors(report);
        BuildActors(markers, report);
        BuildSceneSystems(report);
        PlacePickups(report);

        // Guardar antes de hornear: el NavMesh se guarda junto a la escena
        EditorSceneManager.SaveScene(scene, ScenePath);
#pragma warning disable 618 // com.unity.ai.navigation no está instalado: se usa el baker integrado, como Nivel Porky
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
#pragma warning restore 618
        ValidateRoutes(report);
        ValidateBorders(report);
        PickupBuilder.ValidateSpots(report); // N7: con el NavMesh horneado. Su punto de partida (0, 0, -15) cae en el sendero
        ReportMissingAudio(report);
        EditorSceneManager.SaveScene(scene, ScenePath);

        BuildSceneOrder.Ensure(report);
        AssetDatabase.SaveAssets();

        Debug.Log("[Campamento] Generado en " + ScenePath + ":\n" + report);
        EditorUtility.DisplayDialog("Generar Campamento", "Listo. El detalle está en la consola.\n\n" + report, "Entendido");
    }

    /// <summary>Con la escena ya guardada: scene.name lo necesita PickupBuilder para hallar su PickupLayout.</summary>
    private static Scene OpenOrCreateScene()
    {
        if (File.Exists(ScenePath)) return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);
        return scene;
    }

    // ============ Luz y cielo: de noche, pero se lee todo (L2) ============

    private const float FogStart = 20f, FogEnd = 125f;

    private static Light BuildMoonAndAmbient(Transform lighting)
    {
        var moonGo = new GameObject("Luz_Luna");
        moonGo.transform.SetParent(lighting, false);
        moonGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        Light moon = moonGo.AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color32(0xBF, 0xD4, 0xFF, 0xFF);
        moon.intensity = 0.8f;
        moon.shadows = LightShadows.Soft;

        // Más claro que Nivel Porky (sol 0.5): en esta escena casi todo es madera oscura
        RenderSettings.sun = moon;
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color32(0x4E, 0x55, 0xA8, 0xFF);
        RenderSettings.ambientEquatorColor = new Color32(0x80, 0x60, 0x90, 0xFF);
        RenderSettings.ambientGroundColor = new Color32(0x60, 0x4A, 0x3A, 0xFF);
        return moon;
    }

    private static void BuildSky(Light moon)
    {
        Transform sky = NightSkyBuilder.Build(new Vector3(0f, 0f, 12f), moon.transform.forward);

        // El disco lejano del cielo está en y = -0.35 y taparía el río y el barranco: se baja por debajo de los dos
        Transform farGround = sky.Find("Suelo_Lejano");
        if (farGround != null) farGround.position += Vector3.down * RavineDepth;
        else Debug.LogWarning("[Campamento] NightSkyBuilder ya no crea Suelo_Lejano: revisar que nada tape el río y el barranco.");

        // Niebla suave (NightSkyBuilder deja 22..170)
        RenderSettings.fogStartDistance = FogStart;
        RenderSettings.fogEndDistance = FogEnd;
    }

    // ============ Marcadores y anclas ============

    private static void BuildMarkers(Transform markers)
    {
        CreateMarker("PlayerSpawn", MarkerType.PlayerSpawn, "", PlayerStart, 0f, markers);

        CreateMarker("Anchor_LoboStart", MarkerType.Anchor, "", LoboStart, 180f, markers);
        for (int i = 0; i < LoboCovers.Length; i++)
            CreateMarker("Anchor_LoboCover_" + i, MarkerType.Anchor, "", LoboCovers[i], 180f, markers);
        CreateMarker("Anchor_ConfrontationCam", MarkerType.Anchor, "", ConfrontationCam, 0f, markers);

        // Refuerzos: inactivos hasta la pelea (N2.2). El Transform sirve igual con el objeto apagado
        CreateMarker("Anchor_Refuerzo_A", MarkerType.Anchor, "", RefuerzoA, 90f, markers).gameObject.SetActive(false);
        CreateMarker("Anchor_Refuerzo_B", MarkerType.Anchor, "", RefuerzoB, -90f, markers).gameObject.SetActive(false);

        // Entrada del salón. En Ignore Raycast: no frena perdigones ni tapa la vista
        var trigger = new GameObject("Trigger_Salon") { layer = IgnoreRaycastLayer };
        trigger.transform.SetParent(markers, false);
        trigger.transform.position = new Vector3(0f, 1.5f, 61.2f);
        BoxCollider box = trigger.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(5f, 3f, 2.4f);
    }

    /// <summary>L4: ancla vacía → CluePickup con los datos de la pista. Con algo que no sea nuestro (Edgar): no se toca.</summary>
    private static void PlaceClueAnchors(StringBuilder report)
    {
        GameObject found = GameObject.Find("/" + ClueAnchorsRoot);
        Transform root = found != null ? found.transform : CreateGroup(ClueAnchorsRoot);

        PlaceClueAnchor(root, "foto_reunion_01", "Foto de reunión", "Porky y el Lobo reuniéndose antes",
            "Una foto de Porky y Don Lobo brindando en una mesa de póker, muy sonrientes.\nAl reverso, a lápiz: «Socios. Mayo.»",
            new Vector3(-12f, TableTopBarracks + 0.02f, 23.2f), 20f, true, report);

        PlaceClueAnchor(root, "carta_lobo_01", "Carta de Don Lobo", "El Lobo explica su versión",
            "Monsieur Wilbur: si lees esto, entraste a mi casa a balazos. Tu hermano no está secuestrado: está pagando. " +
            "Me robó y me humilló delante de mi familia, y aun así no le toqué un pelo. Pregúntale a Max. Él lo vio todo. — D. L.",
            new Vector3(7.4f, DeskTopOffice + 0.02f, 57f), -15f, false, report);
    }

    private static void PlaceClueAnchor(Transform root, string clueId, string displayName, string shortDescription, string fullText,
        Vector3 position, float yaw, bool isPhoto, StringBuilder report)
    {
        string anchorName = "Anchor_Clue_" + clueId;
        string autoName = AutoCluePrefix + clueId;

        Transform anchor = root.Find(anchorName);
        if (anchor == null) anchor = CreateMarker(anchorName, MarkerType.Clue, clueId, position, yaw, root).transform;

        Transform auto = anchor.Find(autoName);
        bool hasForeign = anchor.childCount > (auto != null ? 1 : 0);
        if (auto != null) Object.DestroyImmediate(auto.gameObject);
        if (hasForeign)
        {
            report.AppendLine("✓ " + anchorName + ": tiene lo de Edgar, no se tocó.");
            return;
        }

        anchor.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        var go = new GameObject(autoName) { layer = IgnoreRaycastLayer };
        go.transform.SetParent(anchor, false);
        BoxCollider trigger = go.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 0.1f, 0f);
        trigger.size = new Vector3(0.6f, 0.4f, 0.6f);

        var so = new SerializedObject(go.AddComponent<CluePickup>());
        so.FindProperty("clueId").stringValue = clueId;
        so.FindProperty("displayName").stringValue = displayName;
        so.FindProperty("shortDescription").stringValue = shortDescription;
        so.FindProperty("fullText").stringValue = fullText;
        so.ApplyModifiedPropertiesWithoutUndo();

        // El dibujo: una hoja o una foto sobre el mueble (PickupPresentation la mueve y le da contorno)
        Vector3 size = isPhoto ? new Vector3(0.2f, 0.01f, 0.15f) : new Vector3(0.24f, 0.01f, 0.32f);
        GameObject paper = CartoonKit.Box("Papel", position + Vector3.up * 0.02f, size, CartoonKit.M(isPhoto ? "Factura" : "Carta"),
            go.transform, new Vector3(0f, yaw, 0f), 0.002f, false);
        paper.layer = IgnoreRaycastLayer;

        report.AppendLine("✓ " + anchorName + ": CluePickup con los datos de la pista (" + clueId + ").");
    }

    // ============ Actores ============

    private static void BuildActors(Transform markers, StringBuilder report)
    {
        // El Player trae su cámara, AudioListener, AudioLibraryLoader, lluvia, armas, mira y munición
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabGenerator.PlayerPath);
        if (playerPrefab != null)
        {
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.SetPositionAndRotation(PlayerStart, Quaternion.identity);
        }
        else report.AppendLine("✗ Falta Player.prefab (Crear Prefabs de Prueba): el nivel queda sin jugador.");

        // Raíces de la escena (DontDestroyOnLoad): si ya vienen de otra escena, estas copias se destruyen solas al cargar
        var companionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabGenerator.AICompanionPath);
        if (companionPrefab != null) PrefabUtility.InstantiatePrefab(companionPrefab);
        else report.AppendLine("✗ Falta AICompanion.prefab: sin pensamientos.");

        // Los crea GameManagersBootstrap antes de cargar la escena (N2.1.1): esta copia se destruye sola
        var managers = new GameObject("GameManagers");
        managers.AddComponent<GameManager>();
        managers.AddComponent<GameStateTracker>();

        Transform actors = CreateGroup("--- ACTORS ---");
        foreach (GuardDef guard in Guards) SpawnGuard(guard, markers, actors, report);
    }

    private static void SpawnGuard(GuardDef g, Transform markers, Transform actors, StringBuilder report)
    {
        bool interrogable = !string.IsNullOrEmpty(g.npcId);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(interrogable ? TestPrefabGenerator.InterrogablePath : TestPrefabGenerator.GuardPath);
        if (prefab == null)
        {
            report.AppendLine("✗ Falta el prefab de " + g.guardName + " (Crear Prefabs de Prueba).");
            return;
        }

        bool patrol = g.type == EnemyAI.EnemyType.Patrullero;
        var route = new List<Transform>();
        for (int i = 0; i < g.points.Length; i++)
        {
            string markerName = patrol ? $"Patrol_{g.id}_{i}" : interrogable ? "Guard_" + g.npcId : "Sentinel_" + g.id;
            MarkerType type = patrol ? MarkerType.PatrolWaypoint : interrogable ? MarkerType.InterrogableGuard : MarkerType.SentinelPoint;
            route.Add(CreateMarker(markerName, type, interrogable ? g.npcId : g.id, g.points[i], g.yaw, markers).transform);
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = g.guardName;
        instance.transform.SetParent(actors, false);
        instance.transform.SetPositionAndRotation(route[0].position, route[0].rotation);

        var ai = new SerializedObject(instance.GetComponent<EnemyAI>());
        ai.FindProperty("enemyType").enumValueIndex = (int)g.type;
        SerializedProperty points = ai.FindProperty("patrolPoints");
        points.arraySize = patrol ? route.Count : 0;
        for (int i = 0; patrol && i < route.Count; i++) points.GetArrayElementAtIndex(i).objectReferenceValue = route[i];
        ai.ApplyModifiedPropertiesWithoutUndo();

        if (interrogable)
        {
            var health = new SerializedObject(instance.GetComponent<EnemyHealth>());
            health.FindProperty("npcId").stringValue = g.npcId;
            health.ApplyModifiedPropertiesWithoutUndo();
            if (DialogueDatabase.Get(g.npcId) == null)
                report.AppendLine("✗ " + g.npcId + " no está en DialogueDatabase: no abrirá diálogo.");
        }
        report.AppendLine($"✓ {g.guardName} ({g.type}{(interrogable ? ", se rinde: " + g.npcId : "")})");
    }

    // ============ Lo que toda escena de juego necesita ============

    private static void BuildSceneSystems(StringBuilder report)
    {
        HudBuilder.PlaceInOpenScene(report);

        // El interrogatorio de Don Lobo (N2.3) usa este canvas. InterrogationCanvas se cierra solo en Awake
        var canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(InterrogationPrefabPath);
        if (canvasPrefab != null) PrefabUtility.InstantiatePrefab(canvasPrefab);
        else report.AppendLine("⚠ Falta " + InterrogationPrefabPath + ": correr Plomo y Paja → Contador → Crear UI de interrogatorio.");

        Transform root = CreateGroup("--- INICIO ---");
        BuildLevelStart(root, report);
        BuildAmbience(root);
    }

    private static void BuildLevelStart(Transform root, StringBuilder report)
    {
        TMP_FontAsset font = InterrogationUIBuilder.EnsureFont(report);

        // Sin franjas de cine en esta llegada: el subtítulo va sobre el mundo, un poco más arriba, para no pisar el HUD
        Canvas textCanvas = IntroBookBuilder.OverlayCanvas("UI_Campamento_Texto", 45);
        textCanvas.transform.SetParent(root, false);
        RectTransform subRect = IntroBookBuilder.UiChild("Subtitulos", textCanvas.transform, new Vector2(0.15f, 0.14f), new Vector2(0.85f, 0.25f), Vector2.zero, Vector2.zero);
        CanvasGroup subGroup = subRect.gameObject.AddComponent<CanvasGroup>();
        subGroup.blocksRaycasts = false;
        TMP_Text speaker = IntroBookBuilder.Label("Nombre", subRect, new Vector2(0f, 0.62f), Vector2.one, font, 26f, new Color32(0xE8, 0xB0, 0x4A, 0xFF));
        TMP_Text line = IntroBookBuilder.Label("Texto", subRect, Vector2.zero, new Vector2(1f, 0.66f), font, 30f, new Color32(0xF2, 0xE6, 0xC8, 0xFF));
        line.enableWordWrapping = true;
        CinematicSubtitles subtitles = subRect.gameObject.AddComponent<CinematicSubtitles>();
        IntroBookBuilder.SetRef(subtitles, "group", subGroup);
        IntroBookBuilder.SetRef(subtitles, "speaker", speaker);
        IntroBookBuilder.SetRef(subtitles, "line", line);

        ScreenFade fade = IntroBookBuilder.BuildFade();
        fade.transform.root.SetParent(root, false);

        var go = new GameObject("Inicio_Nivel");
        go.transform.SetParent(root, false);
        CampamentoLevelStart start = go.AddComponent<CampamentoLevelStart>();
        AudioSource voice = AudioChild("Voz_Wilbur", go.transform, Vector3.zero, false);
        IntroBookBuilder.SetRef(start, "fade", fade);
        IntroBookBuilder.SetRef(start, "subtitles", subtitles);
        IntroBookBuilder.SetRef(start, "voice", voice);
    }

    private static void BuildAmbience(Transform root)
    {
        var go = new GameObject("Ambiente");
        go.transform.SetParent(root, false);
        CampamentoAmbience ambience = go.AddComponent<CampamentoAmbience>();
        IntroBookBuilder.SetRef(ambience, "forestSource", AudioChild("Ambiente_Bosque", go.transform, Vector3.zero, false));

        AudioSource gramophone = AudioChild("Gramofono_Altavoz", go.transform, GramophonePosition, true);
        gramophone.minDistance = 4f;
        gramophone.maxDistance = 38f;
        gramophone.rolloffMode = AudioRolloffMode.Linear;
        IntroBookBuilder.SetRef(ambience, "gramophoneSource", gramophone);
    }

    private static AudioSource AudioChild(string name, Transform parent, Vector3 position, bool spatial)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = spatial ? 1f : 0f;
        source.dopplerLevel = 0f;
        return source;
    }

    // ============ Objetos ============

    private static void PlacePickups(StringBuilder report)
    {
        var layout = AssetDatabase.LoadAssetAtPath<PickupLayout>(LayoutPath);
        if (layout == null)
        {
            EnsureFolder(Path.GetDirectoryName(LayoutPath).Replace('\\', '/'));
            layout = ScriptableObject.CreateInstance<PickupLayout>();
            layout.entries.AddRange(PickupSeed());
            AssetDatabase.CreateAsset(layout, LayoutPath);
            AssetDatabase.SaveAssets();
        }
        else if (FillMissingContainers(layout, report))
        {
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();
        }

        PickupBuilder.BuildPrefabs(report);
        // N7: sin drops de guardias. PlaceInOpenScene deja el spawner con dropsEnabled apagado (N1.2), también desde
        // "HUD → Colocar objetos": nada los vuelve a prender
        PickupBuilder.PlaceInOpenScene(report);

        // N7: ningún objeto en el piso
        foreach (Pickup pickup in Object.FindObjectsOfType<Pickup>(true))
            if (pickup.transform.position.y < 0.05f)
                report.AppendLine("⚠ " + pickup.name + " quedó en el piso: no encontró su mueble en " + pickup.transform.position);
    }

    /// <summary>
    /// Un asset de antes de N2.1.1 (o creado vacío por "Colocar objetos"): sin container, N1.2 baja el objeto al piso. Solo se
    /// completa lo que no tiene mueble; lo guardado con su container en "Guardar posiciones de objetos" no se toca.
    /// </summary>
    private static bool FillMissingContainers(PickupLayout layout, StringBuilder report)
    {
        PickupLayout.Entry[] seed = PickupSeed();
        if (layout.entries.Count == 0)
        {
            layout.entries.AddRange(seed);
            return true;
        }

        bool changed = false;
        foreach (PickupLayout.Entry entry in layout.entries)
        {
            if (!string.IsNullOrEmpty(entry.container)) continue;
            PickupLayout.Entry known = System.Array.Find(seed, s => s.id == entry.id);
            if (known == null)
            {
                report.AppendLine("⚠ " + entry.id + " no tiene mueble y no es de la distribución del nivel: va a bajar al piso.");
                continue;
            }
            entry.position = known.position;
            entry.container = known.container;
            changed = true;
        }
        return changed;
    }

    // ============ Validación (en el Editor, al generar) ============

    private static void ValidateRoutes(StringBuilder report)
    {
        foreach (GuardDef g in Guards)
        {
            bool ok = true;
            foreach (Vector3 p in g.points)
                if (!NavMesh.SamplePosition(p, out NavMeshHit _, 0.6f, NavMesh.AllAreas))
                {
                    ok = false;
                    report.AppendLine($"✗ {g.guardName}: {p} queda fuera del NavMesh.");
                }
            for (int i = 0; ok && g.points.Length > 1 && i < g.points.Length; i++)
                if (!Connected(g.points[i], g.points[(i + 1) % g.points.Length]))
                {
                    ok = false;
                    report.AppendLine($"✗ {g.guardName}: el tramo {i} de la ronda no es transitable.");
                }
            if (ok) report.AppendLine($"✓ {g.guardName}: en el NavMesh" + (g.points.Length > 1 ? ", ronda transitable." : "."));
        }

        var targets = new List<KeyValuePair<string, Vector3>>
        {
            new KeyValuePair<string, Vector3>("Anchor_LoboStart", LoboStart),
            new KeyValuePair<string, Vector3>("Anchor_Refuerzo_A", RefuerzoA),
            new KeyValuePair<string, Vector3>("Anchor_Refuerzo_B", RefuerzoB),
        };
        for (int i = 0; i < LoboCovers.Length; i++)
            targets.Add(new KeyValuePair<string, Vector3>("Anchor_LoboCover_" + i, LoboCovers[i]));
        foreach (KeyValuePair<string, Vector3> target in targets)
            report.AppendLine(Connected(PlayerStart, target.Value)
                ? $"✓ Del inicio a {target.Key}: hay camino."
                : $"✗ Del inicio a {target.Key}: no hay camino en el NavMesh.");
    }

    private static bool Connected(Vector3 a, Vector3 b)
    {
        var path = new NavMeshPath();
        return NavMesh.SamplePosition(a, out NavMeshHit ha, 0.6f, NavMesh.AllAreas) &&
               NavMesh.SamplePosition(b, out NavMeshHit hb, 0.6f, NavMesh.AllAreas) &&
               NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, path) &&
               path.status == NavMeshPathStatus.PathComplete;
    }

    private static void ReportMissingAudio(StringBuilder report)
    {
        AudioLibrary.SetActive(AudioLibraryBuilder.GetOrCreateLibrary()); // en el Editor no hay loader
        foreach (string id in new[] { "amb_bosque", "amb_campamento" })
            if (AudioLibrary.GetSfx(id) == null) report.AppendLine("ℹ Falta el ambiente " + id + " (sin él, no suena).");
        if (!AudioLibrary.HasVoice("wilbur_bosque_01")) report.AppendLine("ℹ Falta la voz wilbur_bosque_01 (se ve solo el subtítulo).");
    }
}
