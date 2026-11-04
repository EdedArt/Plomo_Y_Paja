using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using static LevelBuilderUtils;

/// <summary>
/// Fase 3C.1 — Patio trasero de Porky con el cuarto de herramientas de Centavo.
/// MENÚ: Plomo y Paja → Contador → Construir patio y cuarto
///
/// - Crea/actualiza Prefabs/Level1/ToolShed_Contador.prefab (caseta de tablas torcidas, banco = cobertura,
///   herramientas, barriles, lámpara de aceite, anclas de Centavo, camino de huida y zona de activación).
/// - Lo coloca en la esquina del fondo a la izquierda del patio trasero, con la puerta mirando al portón.
/// - Cerca + portón que separan ese rincón del resto del patio (sin el portón no se llega al cuarto).
/// - Ruta del patrullero por el portón y centinela custodiándolo; corre los accesorios que quedaban dentro.
/// - Acento de luz de luna fría sobre el portón y el cuarto.
/// - Hornea el NavMesh y comprueba (con el portón tapado y destapado) que solo se entra por el portón.
///
/// Idempotente: correrlo dos veces no duplica nada. No guarda la escena (lo hace David con Ctrl+S).
/// "Generar Nivel Porky" también lo construye (llama a BuildInto), así regenerar el nivel no lo borra.
///
/// PLANO (mundo, mismo sistema que LevelBuilder_NivelPorky): patio trasero x -10..10, z 6..21.
///   Rincón del cuarto: x -10..-2.8, z 13.5..25.5 (N1.1: sigue detrás del cuarto hasta el portón del bosque, Nivel1MapBuilder).
///   Portón en la cerca z = 13.5, x -8..-6.
///   Cuarto 4 x 3 m: x -9..-5, z 17.5..20.5, puerta en z = 17.5 mirando a -Z (al portón).
/// </summary>
public static class ContadorYardBuilder
{
    public const string PrefabFolder = "Assets/_Game/Prefabs/Level1";
    public const string PrefabPath = PrefabFolder + "/ToolShed_Contador.prefab";

    private const string EnvironmentRoot = "--- ENVIRONMENT ---";
    private const string LightingRoot = "--- LIGHTING ---";
    private const string MarkersRoot = "--- MARKERS ---";
    private const string ShedInstanceName = "ToolShed_Contador";
    private const string YardGroupName = "Patio_Contador";
    private const string MoonAccentName = "Luz_Luna_Patio_Contador";

    // ============ Ubicación en Nivel Porky (mundo) ============

    /// <summary>Centro del piso del cuarto (a nivel del suelo). +Z local del prefab = hacia la puerta.</summary>
    public static readonly Vector3 ShedPosition = new Vector3(-7f, 0f, 19f);
    /// <summary>180°: la puerta mira a -Z, hacia el portón y la casa.</summary>
    public const float ShedYaw = 180f;

    public const float YardFenceZ = 13.5f;     // cerca que cruza el patio (de la valla izquierda a YardFenceX)
    public const float YardFenceX = -2.8f;     // cerca que baja hasta el fondo del rincón (N1.1: antes -3.6, ahora se pasa por el lado del cuarto)
    public const float RinconBackZ = 25.5f;    // N1.1: fondo del rincón, detrás del cuarto, donde está el portón del bosque
    public const float GateX = -7f;            // centro del portón (alineado con la puerta del cuarto)
    public const float GateWidth = 2f;
    private const float FenceHeight = 1.6f;    // igual que el resto de vallas del nivel
    private const float FenceSink = 0.18f;     // el terreno del patio ondula ±0.16 m: la cerca se entierra un poco

    /// <summary>Ruta del patrullero (Patrol_A_0..5): fachada → pasillo derecho → patio → portón → rincón → pasillo izquierdo.</summary>
    public static readonly Vector3[] PatrolRoute =
    {
        new Vector3(-9f, 0f, -8f), new Vector3(9f, 0f, -8f), new Vector3(9f, 0f, 9f),
        new Vector3(-7f, 0f, 11.2f),   // delante del portón (fuera)
        new Vector3(-5f, 0f, 15.8f),   // dentro del rincón, delante del cuarto (cruza el portón)
        new Vector3(-9f, 0f, 9f),      // vuelve a salir por el portón
    };

    /// <summary>Centinela al lado del portón, de espaldas a él, mirando a la puerta trasera de la casa.</summary>
    public static readonly Vector3 SentinelPosition = new Vector3(-5.4f, 0f, 12.4f);
    public const float SentinelYaw = 180f;

    // Accesorios del patio que caían dentro del cuarto: se corren (mismo nombre, otra posición)
    public static readonly Vector3 FardoPatioPosition = new Vector3(-3.5f, 0f, 15f);      // N1.1: antes tapaba el paso al lado del cuarto
    public const float FardoPatioYaw = -5f;
    public static readonly Vector3 Escombro09Position = new Vector3(-9.35f, 0f, 16.8f);   // y la pone Rubble (se conserva)

    /// <summary>Camino de huida (mundo): puerta → portón → patio → pasillo izquierdo, detrás de la casa.</summary>
    private static readonly Vector3[] ExitPathWorld =
    {
        new Vector3(-7f, 0f, 16.3f), new Vector3(-7f, 0f, 12.4f), new Vector3(-9f, 0f, 8.6f), new Vector3(-9f, 0f, -3f),
    };

    // ============ Medidas del cuarto (locales al prefab, y = 0 en el suelo) ============

    private const float HalfW = 2f, HalfD = 1.5f;              // 4 x 3 m
    private const float FrontHeight = 2.75f, BackHeight = 2.15f; // techo inclinado hacia atrás
    private const float FloorTop = 0.22f;                       // piso de tablas por encima del terreno irregular
    private const float BoardBottom = -0.15f;                   // las tablas bajan detrás del zócalo
    private const float BoardWidth = 0.22f, BoardThick = 0.045f;
    private const float DoorHalfWidth = 0.75f, DoorHeight = 2.1f;
    private const float BenchHeight = 1.0f;
    private static readonly Vector3 BenchPosition = new Vector3(0f, FloorTop, 0.4f);   // base del banco
    private static readonly Vector3 BenchSize = new Vector3(1.9f, BenchHeight, 0.62f);
    private const float ActivationRadius = 12f;

    private static readonly Color LampColor = new Color(1f, 0.58f, 0.24f);
    private static readonly Color MoonColor = new Color32(0x9E, 0xBD, 0xFF, 0xFF);

    // ==================================================================
    // Menú
    // ==================================================================

    [MenuItem("Plomo y Paja/Contador/Construir patio y cuarto")]
    public static void BuildMenu()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != LevelBuilder_NivelPorky.ScenePath)
        {
            if (!File.Exists(LevelBuilder_NivelPorky.ScenePath))
            {
                EditorUtility.DisplayDialog("Construir patio y cuarto",
                    $"No existe {LevelBuilder_NivelPorky.ScenePath}. Primero: Plomo y Paja → Generar Nivel Porky.", "Entendido");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(LevelBuilder_NivelPorky.ScenePath, OpenSceneMode.Single);
        }

        CartoonKit.BeginSession();
        Transform environment = FindOrCreateRoot(scene, EnvironmentRoot);
        Transform lighting = FindOrCreateRoot(scene, LightingRoot);
        Transform markers = FindOrCreateRoot(scene, MarkersRoot);

        BuildInto(environment, lighting);
        ApplyGuardRoute(markers);
        MoveYardProps();
        string extraGuards = LevelBuilder_NivelPorky.PlaceExtraGuards(markers, null); // fase 3C.4.2

        bool ok = BakeAndValidate(out string report);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();

        report = extraGuards + "\n" + report;
        Debug.Log("[Contador] Patio y cuarto construidos.\n" + report);
        EditorUtility.DisplayDialog("Construir patio y cuarto",
            (ok ? "Listo. " : "Listo, pero revisa la consola: hay comprobaciones que fallaron.\n\n") + report +
            "\n\nGuarda la escena (Ctrl+S).", "Entendido");
    }

    /// <summary>
    /// Crea/actualiza el prefab, lo coloca y arma la cerca, el portón y la luz; después coloca a Centavo
    /// (ContadorCentavoBuilder, fase 3C.2). Lo usa el menú y "Generar Nivel Porky".
    /// No llama a CartoonKit.BeginSession (lo hace quien llama).
    /// </summary>
    public static void BuildInto(Transform environment, Transform lighting)
    {
        GameObject prefab = BuildShedPrefab();
        PlaceShed(prefab, environment);
        BuildYard(environment, lighting);

        // Fase 3C.2: Centavo en su ancla (idempotente; así "Generar Nivel Porky" no lo pierde)
        var centavo = new StringBuilder();
        if (ContadorCentavoBuilder.BuildAndPlace(centavo)) Debug.Log("[Contador] Centavo:\n" + centavo);
        else Debug.LogWarning("[Contador] Centavo:\n" + centavo);
    }

    /// <summary>N1.1 ("Ampliar mapa" sobre una escena vieja): rehace las cercas del rincón y corre el fardo, sin tocar el cuarto ni a Centavo.</summary>
    public static void RefreshYardForMap(Transform environment, Transform lighting)
    {
        BuildYard(environment, lighting);
        MoveYardProps();
    }

    // ==================================================================
    // Prefab del cuarto
    // ==================================================================

    private static GameObject BuildShedPrefab()
    {
        // Se arma en el origen (posiciones de mundo = locales del prefab) y se guarda como prefab
        var root = new GameObject(ShedInstanceName);
        try
        {
            ContadorShed shed = root.AddComponent<ContadorShed>();
            Transform structure = CreateGroup("Estructura", root.transform);

            BuildFloor(structure);
            BuildWalls(structure);
            BuildRoof(structure);
            GameObject bench = BuildBench(structure);
            Transform tools = BuildTools(structure);
            BuildBarrels(structure);
            MarkStaticRecursive(structure.gameObject);
            CartoonKit.ExcludeFromNavMesh(tools);           // colgadas de la pared: no cuentan para el NavMesh

            Light lamp = BuildOilLamp(root.transform);

            Transform anchors = CreateGroup("Anclas", root.transform);
            LevelMarker stand = CreateMarker("Anchor_CentavoStand", MarkerType.Anchor, "Anchor_CentavoStand",
                new Vector3(0f, FloorTop, -0.35f), 0f, anchors);
            LevelMarker seated = CreateMarker("Anchor_CentavoSeated", MarkerType.Anchor, "Anchor_CentavoSeated",
                new Vector3(0f, FloorTop, 1.0f), 0f, anchors);
            LevelMarker cam = CreateMarker("Anchor_InterrogationCam", MarkerType.Anchor, "Anchor_InterrogationCam",
                new Vector3(0f, 0.9f, 2.5f), 0f, anchors);
            Vector3 lookAt = seated.transform.position + Vector3.up * 0.55f;   // pecho/cara de Centavo sentado
            cam.transform.rotation = Quaternion.LookRotation(lookAt - cam.transform.position, Vector3.up);

            Transform exitGroup = CreateGroup("Exit_Path", root.transform);
            var exitPoints = new Transform[ExitPathWorld.Length];
            for (int i = 0; i < ExitPathWorld.Length; i++)
            {
                Vector3 local = WorldToShed(ExitPathWorld[i]);
                Vector3 next = i + 1 < ExitPathWorld.Length ? WorldToShed(ExitPathWorld[i + 1]) : local + (local - WorldToShed(ExitPathWorld[i - 1]));
                Vector3 dir = next - local;
                float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                exitPoints[i] = CreateMarker($"Exit_Path_{i}", MarkerType.Anchor, $"Exit_Path_{i}", local, yaw, exitGroup).transform;
            }

            var trigger = new GameObject("Trigger_Activacion");
            trigger.transform.SetParent(root.transform, false);
            trigger.transform.localPosition = new Vector3(0f, 1f, HalfD);     // en la puerta
            trigger.layer = 2;                                                 // Ignore Raycast: la escopeta y la vista de los guardias lo atraviesan
            SphereCollider sphere = trigger.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = ActivationRadius;

            var so = new SerializedObject(shed);
            so.FindProperty("standAnchor").objectReferenceValue = stand.transform;
            so.FindProperty("seatedAnchor").objectReferenceValue = seated.transform;
            so.FindProperty("interrogationCamAnchor").objectReferenceValue = cam.transform;
            SerializedProperty path = so.FindProperty("exitPath");
            path.arraySize = exitPoints.Length;
            for (int i = 0; i < exitPoints.Length; i++) path.GetArrayElementAtIndex(i).objectReferenceValue = exitPoints[i];
            so.FindProperty("activationTrigger").objectReferenceValue = sphere;
            so.FindProperty("benchCover").objectReferenceValue = bench.GetComponent<BoxCollider>();
            so.FindProperty("oilLamp").objectReferenceValue = lamp;
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder(PrefabFolder);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success || saved == null) Debug.LogError($"[Contador] No se pudo guardar {PrefabPath}.");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>Altura del borde superior de las paredes según la profundidad (techo de una sola agua).</summary>
    private static float WallTop(float z) => BackHeight + (FrontHeight - BackHeight) * (z + HalfD) / (2f * HalfD);

    private static Vector3 WorldToShed(Vector3 world) => Quaternion.Inverse(Quaternion.Euler(0f, ShedYaw, 0f)) * (world - ShedPosition);
    private static Vector3 ShedToWorld(Vector3 local) => ShedPosition + Quaternion.Euler(0f, ShedYaw, 0f) * local;

    // ---- Piso: zócalo oscuro (tapa el terreno irregular) + tablas del kit

    private static void BuildFloor(Transform parent)
    {
        CartoonKit.Box("Zocalo_Cuarto", new Vector3(0f, 0f, 0f), new Vector3(2f * HalfW + 0.1f, 0.36f, 2f * HalfD + 0.1f),
            CartoonKit.M("MaderaOscura"), parent, default, 0.03f);
        CartoonKit.PlankFloor("Piso_Cuarto", new Vector3(0f, FloorTop, 0f), new Vector2(2f * HalfW - 0.1f, 2f * HalfD - 0.1f), parent, 1f);
        // Escalón suelto delante de la puerta
        CartoonKit.Box("Escalon_Cuarto", new Vector3(0f, 0.06f, HalfD + 0.24f), new Vector3(1.5f, 0.1f, 0.34f),
            CartoonKit.M("Madera"), parent, new Vector3(0f, 4f, 1.5f), 0.02f);
    }

    // ---- Paredes: tablas verticales torcidas (±3°, semilla fija), postes de esquina y travesaños. Colisión aparte.

    private static void BuildWalls(Transform parent)
    {
        Mesh mesh = CartoonKit.GetMesh("Cuarto_Contador_Paredes", b =>
        {
            System.Random rng = CartoonKit.Rng("Cuarto_Contador_Paredes");

            // Fondo (z = -HalfD) y frente (z = +HalfD, con la puerta)
            int countX = Mathf.RoundToInt(2f * HalfW / BoardWidth);
            float stepX = 2f * HalfW / countX;
            for (int i = 0; i < countX; i++)
            {
                float x = -HalfW + stepX * (i + 0.5f);
                float t = (i + 0.5f) / countX;
                Board(b, rng, new Vector3(x, BoardBottom, -HalfD), Vector3.back, BackHeight - BoardBottom, t);
                if (Mathf.Abs(x) < DoorHalfWidth + 0.06f)
                    Board(b, rng, new Vector3(x, DoorHeight, HalfD), Vector3.forward, FrontHeight - DoorHeight, t);   // sobre la puerta
                else
                    Board(b, rng, new Vector3(x, BoardBottom, HalfD), Vector3.forward, FrontHeight - BoardBottom, t);
            }

            // Laterales (x = ±HalfW): la altura sigue la caída del techo
            int countZ = Mathf.RoundToInt(2f * HalfD / BoardWidth);
            float stepZ = 2f * HalfD / countZ;
            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < countZ; i++)
            {
                float z = -HalfD + stepZ * (i + 0.5f);
                Board(b, rng, new Vector3(side * HalfW, BoardBottom, z), side * Vector3.right, WallTop(z) - BoardBottom + 0.03f, (i + 0.5f) / countZ);
            }

            // Postes de esquina, travesaños interiores y marco de la puerta (madera oscura)
            b.Submesh = 2;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                float z = sz * HalfD, h = WallTop(z) - BoardBottom;
                CartoonKit.AddBox(b, new Vector3(sx * HalfW, BoardBottom + h * 0.5f, z), new Vector3(0.13f, h, 0.13f),
                    Quaternion.Euler(CartoonKit.Range(rng, -2f, 2f), 0f, CartoonKit.Range(rng, -2.5f, 2.5f)), 0.03f, 1);
            }
            foreach (float y in new[] { 0.6f, 1.75f })
            {
                CartoonKit.AddBox(b, new Vector3(0f, y, -HalfD + 0.045f), new Vector3(2f * HalfW - 0.1f, 0.08f, 0.035f),
                    Quaternion.Euler(0f, 0f, CartoonKit.Range(rng, -1.2f, 1.2f)), 0.012f, 1);
                for (int side = -1; side <= 1; side += 2)
                {
                    CartoonKit.AddBox(b, new Vector3(side * (HalfW - 0.045f), y, 0f), new Vector3(0.035f, 0.08f, 2f * HalfD - 0.1f),
                        Quaternion.Euler(CartoonKit.Range(rng, -1.2f, 1.2f), 0f, 0f), 0.012f, 1);
                    float from = DoorHalfWidth + 0.1f, to = HalfW - 0.05f;
                    CartoonKit.AddBox(b, new Vector3(side * (from + to) * 0.5f, y, HalfD - 0.045f), new Vector3(to - from, 0.08f, 0.035f),
                        Quaternion.Euler(0f, 0f, CartoonKit.Range(rng, -1.5f, 1.5f)), 0.012f, 1);
                }
            }
            for (int side = -1; side <= 1; side += 2)
                CartoonKit.AddBox(b, new Vector3(side * (DoorHalfWidth + 0.05f), DoorHeight * 0.5f, HalfD + 0.03f),
                    new Vector3(0.1f, DoorHeight, 0.07f), Quaternion.Euler(0f, 0f, side * 1.5f), 0.02f, 1);
            CartoonKit.AddBox(b, new Vector3(0f, DoorHeight + 0.05f, HalfD + 0.035f), new Vector3(2f * DoorHalfWidth + 0.35f, 0.11f, 0.08f),
                Quaternion.Euler(0f, 0f, 3f), 0.02f, 1);   // dintel chueco
        });
        CartoonKit.MeshObject("Paredes_Cuarto", mesh,
            new[] { CartoonKit.M("Madera"), CartoonKit.M("MaderaClara"), CartoonKit.M("MaderaOscura") }, parent, Vector3.zero, Quaternion.identity);

        // Colisión: cajas invisibles, sin rendijas (la escopeta y el jugador no se cuelan entre tablas)
        Transform colliders = CreateGroup("Colisiones_Cuarto", parent);
        const float t = 0.14f;
        float sideH = BackHeight - BoardBottom, frontH = FrontHeight - BoardBottom;
        ColliderBox("Col_Fondo", new Vector3(0f, BoardBottom + sideH * 0.5f, -HalfD), new Vector3(2f * HalfW + t, sideH, t), colliders);
        ColliderBox("Col_Izquierda", new Vector3(-HalfW, BoardBottom + sideH * 0.5f, 0f), new Vector3(t, sideH, 2f * HalfD + t), colliders);
        ColliderBox("Col_Derecha", new Vector3(HalfW, BoardBottom + sideH * 0.5f, 0f), new Vector3(t, sideH, 2f * HalfD + t), colliders);
        float jambW = HalfW - DoorHalfWidth + t * 0.5f;
        ColliderBox("Col_Frente_Izq", new Vector3(-(DoorHalfWidth + jambW * 0.5f), BoardBottom + frontH * 0.5f, HalfD), new Vector3(jambW, frontH, t), colliders);
        ColliderBox("Col_Frente_Der", new Vector3(DoorHalfWidth + jambW * 0.5f, BoardBottom + frontH * 0.5f, HalfD), new Vector3(jambW, frontH, t), colliders);
        ColliderBox("Col_Dintel", new Vector3(0f, (DoorHeight + FrontHeight) * 0.5f, HalfD), new Vector3(2f * DoorHalfWidth, FrontHeight - DoorHeight, t), colliders);
    }

    /// <summary>
    /// Una tabla vertical: base en el plano de la pared, "outward" = normal hacia afuera, t = posición 0..1 a lo largo
    /// (la pared se abomba un poco hacia afuera en el centro: dibujo de goma).
    /// </summary>
    private static void Board(CartoonKit.KitMesh b, System.Random rng, Vector3 basePoint, Vector3 outward, float height, float t)
    {
        double roll = rng.NextDouble();
        b.Submesh = roll < 0.55 ? 0 : roll < 0.88 ? 1 : 2;
        float w = BoardWidth * CartoonKit.Range(rng, 0.86f, 0.97f);
        float h = height + CartoonKit.Range(rng, -0.05f, 0.04f);
        float bulge = 0.035f * Mathf.Sin(Mathf.PI * t);
        Quaternion wall = Quaternion.LookRotation(outward, Vector3.up);
        Quaternion crooked = Quaternion.Euler(CartoonKit.Range(rng, -1f, 1f), CartoonKit.Range(rng, -1.5f, 1.5f), CartoonKit.Range(rng, -3f, 3f));
        Vector3 center = basePoint + outward * bulge + Vector3.up * (h * 0.5f);
        CartoonKit.AddBox(b, center, new Vector3(w, h, BoardThick), wall * crooked, 0.012f, 1);
    }

    private static void ColliderBox(string name, Vector3 center, Vector3 size, Transform parent, Vector3 euler = default)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.AddComponent<BoxCollider>().size = size;
    }

    // ---- Techo de una sola agua: tablas de adelante hacia atrás, combado, dos parches de lata

    private static void BuildRoof(Transform parent)
    {
        const float overFront = 0.4f, overBack = 0.25f, overSide = 0.22f;
        float slope = Mathf.Atan2(FrontHeight - BackHeight, 2f * HalfD) * Mathf.Rad2Deg;
        float length = (2f * HalfD + overFront + overBack) / Mathf.Cos(slope * Mathf.Deg2Rad);
        float centerZ = (overFront - overBack) * 0.5f;
        float RoofY(float z) => WallTop(z) + 0.05f;

        Mesh mesh = CartoonKit.GetMesh("Cuarto_Contador_Techo", b =>
        {
            System.Random rng = CartoonKit.Rng("Cuarto_Contador_Techo");
            float width = 2f * (HalfW + overSide);
            int count = Mathf.RoundToInt(width / 0.26f);
            float step = width / count;
            for (int i = 0; i < count; i++)
            {
                float u = (i + 0.5f) / count;
                float x = -width * 0.5f + step * (i + 0.5f);
                float sag = -0.06f * Mathf.Sin(Mathf.PI * u);
                double roll = rng.NextDouble();
                b.Submesh = roll < 0.5 ? 0 : roll < 0.85 ? 2 : 1;
                CartoonKit.AddBox(b, new Vector3(x, RoofY(centerZ) + sag + CartoonKit.Range(rng, -0.015f, 0.015f), centerZ),
                    new Vector3(step * CartoonKit.Range(rng, 0.9f, 0.98f), 0.05f, length + CartoonKit.Range(rng, -0.08f, 0.08f)),
                    Quaternion.Euler(-slope + CartoonKit.Range(rng, -0.8f, 0.8f), CartoonKit.Range(rng, -1.2f, 1.2f), CartoonKit.Range(rng, -2f, 2f)),
                    0.012f, 1);
            }
            // Listones por encima
            b.Submesh = 2;
            foreach (float z in new[] { -1.05f, 0.1f, 1.25f })
                CartoonKit.AddBox(b, new Vector3(0f, RoofY(z) + 0.02f, z), new Vector3(width + 0.1f, 0.05f, 0.09f),
                    Quaternion.Euler(-slope, 0f, CartoonKit.Range(rng, -1.5f, 1.5f)), 0.015f, 1);
            // Parches de lata
            b.Submesh = 3;
            CartoonKit.AddBox(b, new Vector3(-0.9f, RoofY(0.6f) + 0.035f, 0.6f), new Vector3(0.9f, 0.015f, 0.7f),
                Quaternion.Euler(-slope, 8f, 1.5f), 0.006f, 1);
            CartoonKit.AddBox(b, new Vector3(1.1f, RoofY(-0.8f) + 0.035f, -0.8f), new Vector3(0.6f, 0.015f, 0.55f),
                Quaternion.Euler(-slope, -12f, -1f), 0.006f, 1);
        });
        GameObject roof = CartoonKit.MeshObject("Techo_Cuarto", mesh,
            new[] { CartoonKit.M("Madera"), CartoonKit.M("MaderaClara"), CartoonKit.M("MaderaOscura"), CartoonKit.M("Metal") },
            parent, Vector3.zero, Quaternion.identity);
        roof.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;

        // Collider del techo: tapa la lluvia (RainEmitter.ProbeUnderRoof) y las balas que van hacia arriba
        ColliderBox("Col_Techo", new Vector3(0f, RoofY(centerZ), centerZ), new Vector3(2f * (HalfW + overSide), 0.08f, length),
            parent, new Vector3(-slope, 0f, 0f));
    }

    // ---- Banco de trabajo: cobertura sólida de 1 m, frente tapado con tablas

    private static GameObject BuildBench(Transform parent)
    {
        Mesh mesh = CartoonKit.GetMesh("Cuarto_Contador_Banco", b =>
        {
            System.Random rng = CartoonKit.Rng("Cuarto_Contador_Banco");
            float hx = BenchSize.x * 0.5f, hz = BenchSize.z * 0.5f;

            b.Submesh = 0;                                                       // tablero
            CartoonKit.AddBox(b, new Vector3(0f, BenchHeight - 0.04f, 0f), new Vector3(BenchSize.x, 0.08f, BenchSize.z),
                Quaternion.Euler(0f, 0f, 1.2f), 0.02f, 1);

            b.Submesh = 1;                                                       // patas, estante y costados
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                CartoonKit.AddBox(b, new Vector3(sx * (hx - 0.09f), (BenchHeight - 0.08f) * 0.5f, sz * (hz - 0.07f)),
                    new Vector3(0.09f, BenchHeight - 0.08f, 0.09f),
                    Quaternion.Euler(CartoonKit.Range(rng, -2f, 2f), 0f, CartoonKit.Range(rng, -2.5f, 2.5f)), 0.02f, 1);
            CartoonKit.AddBox(b, new Vector3(0f, 0.32f, -0.03f), new Vector3(BenchSize.x - 0.12f, 0.04f, BenchSize.z - 0.12f),
                Quaternion.Euler(0f, 0f, -1f), 0.01f, 1);
            for (int sx = -1; sx <= 1; sx += 2)
                CartoonKit.AddBox(b, new Vector3(sx * (hx - 0.03f), 0.47f, 0f), new Vector3(0.035f, 0.86f, BenchSize.z - 0.08f),
                    Quaternion.Euler(CartoonKit.Range(rng, -1.5f, 1.5f), 0f, 0f), 0.01f, 1);

            b.Submesh = 3;                                                       // frente (lado de la puerta) tapado
            int boards = 8;
            for (int i = 0; i < boards; i++)
            {
                float x = -hx + 0.06f + (BenchSize.x - 0.12f) * (i + 0.5f) / boards;
                CartoonKit.AddBox(b, new Vector3(x, 0.47f, hz - 0.02f),
                    new Vector3((BenchSize.x - 0.12f) / boards * 0.94f, 0.86f + CartoonKit.Range(rng, -0.04f, 0.02f), 0.03f),
                    Quaternion.Euler(0f, 0f, CartoonKit.Range(rng, -3f, 3f)), 0.008f, 1);
            }

            b.Submesh = 2;                                                       // morsa y lata de clavos
            CartoonKit.AddBox(b, new Vector3(-hx + 0.12f, BenchHeight + 0.04f, -0.12f), new Vector3(0.18f, 0.08f, 0.14f), Quaternion.identity, 0.015f, 1);
            CartoonKit.AddBox(b, new Vector3(-hx + 0.12f, BenchHeight + 0.1f, -0.05f), new Vector3(0.16f, 0.1f, 0.04f), Quaternion.identity, 0.01f, 1);
            CartoonKit.AddBox(b, new Vector3(-hx + 0.12f, BenchHeight + 0.09f, 0.04f), new Vector3(0.025f, 0.025f, 0.24f), Quaternion.Euler(0f, 12f, 0f), 0.008f, 0);
            CartoonKit.AddLathe(b, new Vector3(-0.35f, BenchHeight, -0.12f), Quaternion.identity, new[] { 0f, 0.12f, 0.13f },
                new[] { 0.06f, 0.06f, 0.055f }, 10, true, true);
        });
        GameObject bench = CartoonKit.MeshObject("Banco_Trabajo", mesh,
            new[] { CartoonKit.M("MaderaClara"), CartoonKit.M("Madera"), CartoonKit.M("Metal"), CartoonKit.M("MaderaOscura") },
            parent, BenchPosition, Quaternion.identity);
        BoxCollider col = bench.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, BenchHeight * 0.5f, 0f);
        col.size = BenchSize;
        return bench;
    }

    // ---- Herramientas colgadas en la pared del fondo (decorativas, sin collider)

    private static Transform BuildTools(Transform parent)
    {
        Transform group = CreateGroup("Herramientas", parent);
        float wall = -HalfD + 0.075f;                                            // delante de los travesaños de la pared del fondo
        Mesh mesh = CartoonKit.GetMesh("Cuarto_Contador_Herramientas", b =>
        {
            // 0 Metal · 1 Madera · 2 Cuerda · 3 MaderaOscura
            b.Submesh = 3;                                                       // tablero
            CartoonKit.AddBox(b, new Vector3(0.1f, 1.72f, wall), new Vector3(2.5f, 0.85f, 0.03f), Quaternion.Euler(0f, 0f, -1.2f), 0.01f, 1);
            float z = wall + 0.03f;

            // Serrucho
            b.Submesh = 0;
            CartoonKit.AddBox(b, new Vector3(-0.85f, 1.82f, z), new Vector3(0.58f, 0.17f, 0.01f), Quaternion.Euler(0f, 0f, 9f), 0.004f, 1);
            b.Submesh = 1;
            CartoonKit.AddBox(b, new Vector3(-0.5f, 1.88f, z + 0.01f), new Vector3(0.13f, 0.12f, 0.035f), Quaternion.Euler(0f, 0f, 9f), 0.02f, 1);

            // Martillo
            CartoonKit.AddBox(b, new Vector3(-0.22f, 1.7f, z + 0.01f), new Vector3(0.035f, 0.34f, 0.035f), Quaternion.Euler(0f, 0f, -6f), 0.01f, 1);
            b.Submesh = 0;
            CartoonKit.AddBox(b, new Vector3(-0.24f, 1.89f, z + 0.02f), new Vector3(0.16f, 0.06f, 0.06f), Quaternion.Euler(0f, 0f, -6f), 0.012f, 1);

            // Llave inglesa
            CartoonKit.AddBox(b, new Vector3(0.12f, 1.72f, z), new Vector3(0.04f, 0.27f, 0.015f), Quaternion.Euler(0f, 0f, 4f), 0.006f, 1);
            CartoonKit.AddBox(b, new Vector3(0.125f, 1.87f, z), new Vector3(0.1f, 0.05f, 0.016f), Quaternion.Euler(0f, 0f, 4f), 0.006f, 1);

            // Pala (cuelga con la hoja arriba)
            b.Submesh = 1;
            CartoonKit.AddBox(b, new Vector3(0.6f, 1.25f, z + 0.02f), new Vector3(0.04f, 1.05f, 0.04f), Quaternion.Euler(0f, 0f, 3.5f), 0.012f, 1);
            b.Submesh = 0;
            CartoonKit.AddBox(b, new Vector3(0.62f, 1.92f, z + 0.02f), new Vector3(0.23f, 0.3f, 0.02f), Quaternion.Euler(0f, 0f, 3.5f), 0.04f, 1);

            // Rastrillo
            b.Submesh = 1;
            CartoonKit.AddBox(b, new Vector3(1.12f, 1.3f, z + 0.02f), new Vector3(0.04f, 1.1f, 0.04f), Quaternion.Euler(0f, 0f, -4f), 0.012f, 1);
            b.Submesh = 0;
            CartoonKit.AddBox(b, new Vector3(1.08f, 1.86f, z + 0.03f), new Vector3(0.34f, 0.04f, 0.03f), Quaternion.Euler(0f, 0f, -4f), 0.008f, 1);
            for (int i = 0; i < 5; i++)
                CartoonKit.AddBox(b, new Vector3(0.94f + i * 0.07f, 1.93f, z + 0.03f), new Vector3(0.015f, 0.13f, 0.015f), Quaternion.Euler(0f, 0f, -4f), 0.005f, 0);

            // Ganchos
            foreach (float x in new[] { -0.75f, -0.22f, 0.12f, 0.6f, 1.12f, -1.35f })
                CartoonKit.AddBox(b, new Vector3(x, 2.08f, z + 0.03f), new Vector3(0.025f, 0.025f, 0.08f), Quaternion.identity, 0.005f, 0);

            // Rollo de soga
            b.Submesh = 2;
            CartoonKit.AddLathe(b, new Vector3(-1.35f, 1.78f, z + 0.03f), Quaternion.Euler(90f, 0f, 0f), new[] { -0.035f, 0.035f },
                new[] { 0.17f, 0.17f }, 16, false, false);
            CartoonKit.AddLathe(b, new Vector3(-1.35f, 1.78f, z + 0.03f), Quaternion.Euler(90f, 0f, 0f), new[] { -0.036f, 0.036f },
                new[] { 0.115f, 0.115f }, 16, false, false);
        });
        GameObject tools = CartoonKit.MeshObject("Herramientas_Pared", mesh,
            new[] { CartoonKit.M("Metal"), CartoonKit.M("Madera"), CartoonKit.M("Cuerda"), CartoonKit.M("MaderaOscura") },
            group, Vector3.zero, Quaternion.identity);
        tools.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return group;
    }

    // ---- Barriles y caja: cierran los lados del banco (no se pasa por detrás) y llenan el fondo

    private static void BuildBarrels(Transform parent)
    {
        CartoonKit.Barrel("Barril_Cuarto_A", new Vector3(-1.45f, FloorTop, 0.35f), 20f, parent, 1);
        CartoonKit.Barrel("Barril_Cuarto_B", new Vector3(-1.5f, FloorTop, -1.05f), 55f, parent, 2);
        CartoonKit.Barrel("Barril_Cuarto_C", new Vector3(1.5f, FloorTop, -1.05f), -30f, parent, 3);
        CartoonKit.Crate("Caja_Cuarto", new Vector3(1.45f, FloorTop, 0.38f), 12f, parent, 0.78f);
    }

    // ---- Lámpara de aceite sobre el banco + luz naranja cálida que parpadea suave

    private static Light BuildOilLamp(Transform root)
    {
        Vector3 basePos = BenchPosition + new Vector3(0.62f, BenchHeight, 0.1f);
        Mesh mesh = CartoonKit.GetMesh("Cuarto_Contador_Lampara", b =>
        {
            b.Submesh = 0;                                                       // base y tapa
            CartoonKit.AddLathe(b, Vector3.zero, Quaternion.identity, new[] { 0f, 0.025f, 0.04f }, new[] { 0.08f, 0.075f, 0.05f }, 14, true, false);
            CartoonKit.AddLathe(b, new Vector3(0f, 0.26f, 0f), Quaternion.identity, new[] { 0f, 0.03f }, new[] { 0.05f, 0.02f }, 12, true, true);
            CartoonKit.AddBox(b, new Vector3(0f, 0.33f, 0f), new Vector3(0.012f, 0.06f, 0.1f), Quaternion.identity, 0.004f, 0);   // asa
            b.Submesh = 1;                                                       // depósito de latón
            CartoonKit.AddLathe(b, new Vector3(0f, 0.04f, 0f), Quaternion.identity, new[] { 0f, 0.04f, 0.08f, 0.1f },
                new[] { 0.05f, 0.085f, 0.06f, 0.03f }, 14, false, true);
            b.Submesh = 2;                                                       // tubo de vidrio encendido
            CartoonKit.AddLathe(b, new Vector3(0f, 0.1f, 0f), Quaternion.identity, new[] { 0f, 0.05f, 0.12f, 0.16f },
                new[] { 0.035f, 0.055f, 0.04f, 0.03f }, 14, false, false);
        });
        GameObject lamp = CartoonKit.MeshObject("Lampara_Aceite", mesh,
            new[] { CartoonKit.M("Metal"), CartoonKit.M("Laton"), CartoonKit.M("VidrioCalido") }, root, basePos, Quaternion.identity);
        lamp.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Light light = CreatePointLight("Lampara_Aceite_Luz", basePos + Vector3.up * 0.24f, LampColor, 2.4f, 6f, lamp.transform);
        light.shadows = LightShadows.Soft;
        LightFlicker flicker = light.gameObject.AddComponent<LightFlicker>();
        var so = new SerializedObject(flicker);
        so.FindProperty("noiseAmount").floatValue = 0.12f;
        so.FindProperty("noiseSpeed").floatValue = 3f;
        so.FindProperty("dipChancePerSecond").floatValue = 0.25f;
        so.FindProperty("dipMinMultiplier").floatValue = 0.75f;
        so.FindProperty("offChancePerSecond").floatValue = 0f;           // llama de aceite: tiembla, nunca se apaga
        so.ApplyModifiedPropertiesWithoutUndo();
        return light;
    }

    // ==================================================================
    // Escena: instancia del prefab, cerca, portón y luz
    // ==================================================================

    private static void PlaceShed(GameObject prefab, Transform environment)
    {
        if (prefab == null) return;

        // Conserva la primera instancia (y sus overrides de 3C.2+); borra duplicados y copias desempaquetadas
        GameObject keep = null;
        foreach (ContadorShed shed in Object.FindObjectsOfType<ContadorShed>(true))
        {
            GameObject go = shed.gameObject;
            bool isInstance = PrefabUtility.IsAnyPrefabInstanceRoot(go) &&
                              PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) == PrefabPath;
            if (isInstance && keep == null) keep = go;
            else Object.DestroyImmediate(go);
        }
        if (keep == null) keep = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        keep.name = ShedInstanceName;
        keep.transform.SetParent(environment, false);
        keep.transform.SetPositionAndRotation(ShedPosition, Quaternion.Euler(0f, ShedYaw, 0f));
    }

    private static void BuildYard(Transform environment, Transform lighting)
    {
        DestroyChild(environment, YardGroupName);
        DestroyChild(lighting, MoonAccentName);

        Transform yard = CreateGroup(YardGroupName, environment);
        float baseY = -FenceSink, height = FenceHeight + FenceSink;
        const float leftFence = -10.1f;   // pasa la valla izquierda (x = -10) para que las colisiones se toquen

        // Cerca de frente con el portón, y cerca lateral hasta el fondo del rincón (N1.1: z = 25.5, detrás del cuarto)
        CartoonKit.Fence("Valla_Cuarto_Frente", new Vector3(leftFence, baseY, YardFenceZ), new Vector3(YardFenceX + 0.1f, baseY, YardFenceZ),
            height, yard, new WallOpening(GateX - leftFence, GateWidth, height));
        CartoonKit.Fence("Valla_Cuarto_Lado", new Vector3(YardFenceX, baseY, YardFenceZ - 0.1f), new Vector3(YardFenceX, baseY, RinconBackZ + 0.1f),
            height, yard);

        // Portón: dos postes gruesos, viga chueca arriba y la hoja abierta hacia adentro (decorativa)
        Material dark = CartoonKit.M("MaderaOscura");
        float postH = 2.15f + FenceSink;
        CartoonKit.Box("Porton_Poste_Izq", new Vector3(GateX - GateWidth * 0.5f, baseY + postH * 0.5f, YardFenceZ), new Vector3(0.2f, postH, 0.2f),
            dark, yard, new Vector3(0f, 0f, -2f), 0.05f);
        CartoonKit.Box("Porton_Poste_Der", new Vector3(GateX + GateWidth * 0.5f, baseY + postH * 0.5f, YardFenceZ), new Vector3(0.2f, postH, 0.2f),
            dark, yard, new Vector3(0f, 0f, 2.5f), 0.05f);
        CartoonKit.Box("Porton_Viga", new Vector3(GateX, 2.3f, YardFenceZ), new Vector3(GateWidth + 0.6f, 0.16f, 0.18f),
            dark, yard, new Vector3(0f, 0f, 3f), 0.04f);

        Mesh leafMesh = CartoonKit.GetMesh("Porton_Contador_Hoja", b =>
        {
            System.Random rng = CartoonKit.Rng("Porton_Contador_Hoja");
            const float width = 1.8f, bottom = 0.15f, top = 1.45f;
            b.Submesh = 0;
            for (int i = 0; i < 5; i++)
            {
                float x = 0.05f + width * (i + 0.5f) / 5f;
                float h = top - bottom + CartoonKit.Range(rng, -0.06f, 0.04f);
                CartoonKit.AddBox(b, new Vector3(x, bottom + h * 0.5f, 0f), new Vector3(width / 5f * 0.93f, h, 0.04f),
                    Quaternion.Euler(0f, 0f, CartoonKit.Range(rng, -3f, 3f)), 0.012f, 1);
            }
            b.Submesh = 1;
            foreach (float y in new[] { 0.4f, 1.15f })
                CartoonKit.AddBox(b, new Vector3(0.05f + width * 0.5f, y, 0.04f), new Vector3(width, 0.09f, 0.035f),
                    Quaternion.Euler(0f, 0f, CartoonKit.Range(rng, -1.5f, 1.5f)), 0.012f, 1);
            float angle = Mathf.Atan2(0.75f, width - 0.2f) * Mathf.Rad2Deg;
            CartoonKit.AddBox(b, new Vector3(0.05f + width * 0.5f, 0.775f, 0.045f), new Vector3(Mathf.Sqrt(0.75f * 0.75f + (width - 0.2f) * (width - 0.2f)), 0.08f, 0.03f),
                Quaternion.Euler(0f, 0f, angle), 0.012f, 1);
        });
        GameObject leaf = CartoonKit.MeshObject("Porton_Hoja", leafMesh, new[] { CartoonKit.M("Madera"), dark }, yard,
            new Vector3(GateX - GateWidth * 0.5f + 0.1f, 0f, YardFenceZ + 0.05f), Quaternion.Euler(0f, -105f, 0f));   // abierta hacia el rincón

        MarkStaticRecursive(yard.gameObject);
        CartoonKit.ExcludeFromNavMesh(leaf.transform);   // decorativa y sin collider: no achica el paso del portón

        // Acento frío de luna sobre el portón y la fachada del cuarto (la lámpara de aceite pone el cálido)
        Vector3 from = new Vector3(-4.5f, 6.5f, 11f), to = new Vector3(-7f, 0f, 16f);
        var moonGo = new GameObject(MoonAccentName);
        moonGo.transform.SetParent(lighting, false);
        moonGo.transform.SetPositionAndRotation(from, Quaternion.LookRotation(to - from));
        Light moon = moonGo.AddComponent<Light>();
        moon.type = LightType.Spot;
        moon.color = MoonColor;
        moon.intensity = 8f;
        moon.range = 16f;
        moon.spotAngle = 80f;
        moon.shadows = LightShadows.Soft;
    }

    // ==================================================================
    // Guardias: ruta del patrullero por el portón y centinela custodiándolo
    // ==================================================================

    private static void ApplyGuardRoute(Transform markersRoot)
    {
        var markers = new List<LevelMarker>(Object.FindObjectsOfType<LevelMarker>(true));

        // Patrol_A_0..N: reubica los que existen, crea los que faltan y borra los que sobran
        var waypoints = new List<Transform>();
        for (int i = 0; i < PatrolRoute.Length; i++)
        {
            string name = $"Patrol_A_{i}";
            LevelMarker marker = markers.Find(m => m.Type == MarkerType.PatrolWaypoint && m.Id == "A" && m.name == name);
            if (marker == null) marker = CreateMarker(name, MarkerType.PatrolWaypoint, "A", PatrolRoute[i], 0f, markersRoot);
            marker.transform.position = PatrolRoute[i];
            waypoints.Add(marker.transform);
        }
        foreach (LevelMarker extra in markers.FindAll(m => m.Type == MarkerType.PatrolWaypoint && m.Id == "A" && !waypoints.Contains(m.transform)))
            Object.DestroyImmediate(extra.gameObject);

        // Solo el centinela del patio (3C.4.2: en la casa hay otros centinelas, Sentinel_Sala / Sentinel_Cocina)
        LevelMarker sentinel = markers.Find(m => m != null && m.Type == MarkerType.SentinelPoint &&
                                                 m.name == LevelBuilder_NivelPorky.YardSentinelMarker);
        if (sentinel == null)
            sentinel = CreateMarker(LevelBuilder_NivelPorky.YardSentinelMarker, MarkerType.SentinelPoint, "", SentinelPosition,
                SentinelYaw, markersRoot);
        sentinel.transform.SetPositionAndRotation(SentinelPosition, Quaternion.Euler(0f, SentinelYaw, 0f));

        bool patrolSet = false, sentinelSet = false;
        foreach (EnemyAI ai in Object.FindObjectsOfType<EnemyAI>(true))
        {
            var so = new SerializedObject(ai);
            SerializedProperty type = so.FindProperty("enemyType");
            SerializedProperty points = so.FindProperty("patrolPoints");

            bool isPatrol = ai.name == "Guard_Patrulla_A";
            for (int i = 0; !isPatrol && i < points.arraySize; i++)
            {
                Object element = points.GetArrayElementAtIndex(i).objectReferenceValue;
                isPatrol = element != null && waypoints.Contains(element as Transform);
            }
            if (isPatrol)
            {
                points.arraySize = waypoints.Count;
                for (int i = 0; i < waypoints.Count; i++) points.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                patrolSet = true;
                continue;
            }

            // Por nombre: los centinelas de la casa (3C.4.2) no se tocan
            bool isSentinel = ai.name == "Guard_Centinela";
            if (isSentinel)
            {
                ai.transform.SetPositionAndRotation(sentinel.transform.position, sentinel.transform.rotation);
                sentinelSet = true;
            }
        }
        if (!patrolSet) Debug.LogWarning("[Contador] No encontré al patrullero (Guard_Patrulla_A): la ruta nueva quedó solo en los marcadores.");
        if (!sentinelSet) Debug.LogWarning("[Contador] No encontré al centinela (Guard_Centinela): el marcador se movió, el guardia no.");
    }

    /// <summary>El fardo y el escombro del rincón quedaban dentro del cuarto: se corren a un lado.</summary>
    private static void MoveYardProps()
    {
        GameObject fardo = GameObject.Find("/" + EnvironmentRoot + "/Patio_Props/Fardo_Patio");
        if (fardo != null) fardo.transform.SetPositionAndRotation(FardoPatioPosition, Quaternion.Euler(0f, FardoPatioYaw, 0f));
        else Debug.LogWarning("[Contador] No encontré Patio_Props/Fardo_Patio (¿se renombró?).");

        GameObject rubble = GameObject.Find("/" + EnvironmentRoot + "/Escombros/Escombro_09");
        if (rubble != null) rubble.transform.position = new Vector3(Escombro09Position.x, rubble.transform.position.y, Escombro09Position.z);
        else Debug.LogWarning("[Contador] No encontré Escombros/Escombro_09 (¿se renombró?).");
    }

    // ==================================================================
    // NavMesh: horneado y comprobaciones
    // ==================================================================

    /// <summary>
    /// 1) Hornea con el portón tapado: desde la casa NO debe haber camino al cuarto (la cerca está cerrada).
    /// 2) Quita la tapa, hornea de nuevo y comprueba que los caminos entran por el portón y que la patrulla es transitable.
    /// </summary>
    private static bool BakeAndValidate(out string report)
    {
        var sb = new StringBuilder();
        bool ok = true;

        GameObject plug = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plug.name = "__TapaPorton_Temporal";
        try
        {
            plug.transform.position = new Vector3(GateX, 1f, YardFenceZ);
            plug.transform.localScale = new Vector3(GateWidth + 0.3f, 2.2f, 0.4f);
#pragma warning disable 618
            GameObjectUtility.SetStaticEditorFlags(plug, StaticEditorFlags.NavigationStatic);
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
#pragma warning restore 618
            if (!NavMesh.SamplePosition(new Vector3(-4f, 0f, 7.5f), out NavMeshHit _, 1.5f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(ShedFront(), out NavMeshHit _, 1.5f, NavMesh.AllAreas))
            {
                ok = false;
                sb.AppendLine("✗ No se pudo comprobar la cerca: no hay NavMesh en la puerta trasera o delante del cuarto.");
            }
            else if (TryPath(new Vector3(-4f, 0f, 7.5f), ShedFront(), out NavMeshPath closedPath))
            {
                ok = false;
                sb.AppendLine("✗ Con el portón tapado se llega igual al cuarto: hay un hueco en la cerca.");
            }
            else sb.AppendLine($"✓ Con el portón tapado no hay camino al cuarto ({closedPath.status}).");
        }
        finally
        {
            Object.DestroyImmediate(plug);
        }

#pragma warning disable 618
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
#pragma warning restore 618
        ok &= ValidatePaths(sb);

        float radius = NavMesh.GetSettingsByIndex(0).agentRadius;
        if (radius > 0.5f) sb.AppendLine($"! Radio del agente = {radius}: con el portón de {GateWidth} m conviene 0.5 o menos.");
        report = sb.ToString().TrimEnd();
        return ok;
    }

    /// <summary>Comprobaciones con el NavMesh actual (también las corre "Generar Nivel Porky" después de hornear).</summary>
    public static bool ValidatePaths(StringBuilder sb)
    {
        bool ok = true;
        var sources = new (string label, Vector3 pos)[]
        {
            ("puerta trasera", new Vector3(-4f, 0f, 7.5f)), ("callejón lateral", new Vector3(-11.8f, 0f, 0f)),
            ("pasillo derecho", new Vector3(9f, 0f, 0f)), ("calle", new Vector3(0f, 0f, -15f)),
        };
        foreach (var (label, pos) in sources)
        {
            if (!TryPath(pos, ShedFront(), out NavMeshPath path))
            {
                ok = false;
                sb.AppendLine($"✗ Desde {label} no hay camino completo al cuarto ({path.status}).");
                continue;
            }
            if (GateOnly(path.corners, out string why)) sb.AppendLine($"✓ Desde {label}: entra por el portón.");
            else { ok = false; sb.AppendLine($"✗ Desde {label}: {why}"); }
        }

        bool legsOk = true;
        for (int i = 0; i < PatrolRoute.Length; i++)
        {
            Vector3 a = PatrolRoute[i], b = PatrolRoute[(i + 1) % PatrolRoute.Length];
            if (!TryPath(a, b, out NavMeshPath leg))
            {
                legsOk = false;
                sb.AppendLine($"✗ Patrulla: el tramo Patrol_A_{i} → Patrol_A_{(i + 1) % PatrolRoute.Length} no es transitable ({leg.status}).");
            }
        }
        if (legsOk) sb.AppendLine($"✓ Patrulla: los {PatrolRoute.Length} tramos son transitables.");
        ok &= legsOk;

        if (!NavMesh.SamplePosition(SentinelPosition, out NavMeshHit _, 0.6f, NavMesh.AllAreas))
        {
            ok = false;
            sb.AppendLine("✗ El centinela queda fuera del NavMesh.");
        }

        // Fase 3C.4.2: guardias de la calle y la casa
        ok &= LevelBuilder_NivelPorky.ValidateExtraGuards(sb);
        return ok;
    }

    private static Vector3 ShedFront() => ShedToWorld(new Vector3(0f, 0f, HalfD + 1.2f));

    private static bool TryPath(Vector3 from, Vector3 to, out NavMeshPath path)
    {
        path = new NavMeshPath();
        if (!NavMesh.SamplePosition(from, out NavMeshHit a, 1.5f, NavMesh.AllAreas)) return false;
        if (!NavMesh.SamplePosition(to, out NavMeshHit b, 1.5f, NavMesh.AllAreas)) return false;
        NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path);
        return path.status == NavMeshPathStatus.PathComplete;
    }

    /// <summary>¿El camino cruza la cerca del rincón solo por el portón?</summary>
    private static bool GateOnly(Vector3[] corners, out string why)
    {
        bool throughGate = false;
        float gateMin = GateX - GateWidth * 0.5f, gateMax = GateX + GateWidth * 0.5f;
        for (int i = 0; i + 1 < corners.Length; i++)
        {
            Vector3 p = corners[i], q = corners[i + 1];
            if ((p.z - YardFenceZ) * (q.z - YardFenceZ) < 0f)
            {
                float x = Mathf.Lerp(p.x, q.x, (YardFenceZ - p.z) / (q.z - p.z));
                if (x >= gateMin && x <= gateMax) throughGate = true;
                else if (x < YardFenceX) { why = $"cruza la cerca en x = {x:0.0}, fuera del portón."; return false; }
            }
            if ((p.x - YardFenceX) * (q.x - YardFenceX) < 0f)
            {
                float z = Mathf.Lerp(p.z, q.z, (YardFenceX - p.x) / (q.x - p.x));
                if (z > YardFenceZ && z < RinconBackZ) { why = $"cruza la cerca lateral en z = {z:0.0}."; return false; }
            }
        }
        why = throughGate ? "" : "no pasa por el portón.";
        return throughGate;
    }

    // ==================================================================
    // Utilidades
    // ==================================================================

    private static Transform FindOrCreateRoot(Scene scene, string name)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
            if (go.name == name) return go.transform;
        return CreateGroup(name);
    }

    private static void DestroyChild(Transform parent, string name)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            if (parent.GetChild(i).name == name) Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }
}
