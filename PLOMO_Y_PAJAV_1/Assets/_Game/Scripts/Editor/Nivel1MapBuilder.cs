using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static BoundaryKitBuilder;

/// <summary>
/// Fase N1.1: Nivel 1 más grande, con bordes que dejan ver. MENÚ: Plomo y Paja → Nivel 1 → Ampliar mapa
/// (también lo corre Generar Nivel Porky). Idempotente: "N1_Mapa", "N1_Luces", "--- VISTA ---" y "N1_RedSeguridad" se rehacen.
///
/// PLANO (mundo, N = +Z):
///   Calle x -44..44, z -20..-10 (al sur, en las puntas, el borde corre por z = -17; la acera de enfrente sigue siendo x ±20).
///   Punta oeste: barricada de Don Lobo. Punta este: auto chocado y farol roto. La calle sigue visible detrás de las dos.
///   Vecinos (fachada en z = -5, patio z -10..-5): C x -44..-29, A x -29..-13.6, B x 14..29. Porche con mesa en B.
///   Callejón lateral x -13.6..-10, z -10..4, junto a la casa de Porky; termina en una reja de hierro que da a la calle de atrás.
///   Lote vacío x 29..44, z -10..0, con escombros y un cartel sin nada.
///   Fondo: el rincón de Centavo sigue detrás del cuarto (z 21..25.5) hasta Gate_Bosque; detrás, el sendero (~30 m) y Anchor_LevelExit.
/// El sendero no entra al NavMesh: el portón está cerrado y por ahí no anda ningún guardia.
/// </summary>
public static class Nivel1MapBuilder
{
    public static readonly Vector3 ForestGatePosition = new Vector3(-6.4f, 0f, ContadorYardBuilder.RinconBackZ);
    public static readonly Vector3 LevelExitPosition = new Vector3(-6.2f, 0f, 55f);

    // N1.2 (PickupBuilder): superficies de los objetos en lo que arma este builder
    private const float PorchTop = 0.24f;
    private const float PorchTableOffsetX = 1.45f, PorchTableOffsetZ = -1.05f;
    private const float NeighbourBDoorX = 22f;
    private const float AlleyCrateSize = 0.8f;
    private static readonly Vector3 AlleyCratePosition = new Vector3(-13f, 0f, -1.2f);
    public static readonly Vector3 PorchTableSpot = new Vector3(NeighbourBDoorX + PorchTableOffsetX, PorchTop + 0.78f, NeighbourFacadeZ + PorchTableOffsetZ);
    public static readonly Vector3 AlleyCrateSpot = AlleyCratePosition + Vector3.up * AlleyCrateSize;

    private const float StreetEndX = 44f;
    private const float StreetSouthZ = -17f;
    private const float FacadeZ = -20f;
    private const float YardFrontZ = -10f;
    private const float NeighbourFacadeZ = -5f;
    private const float ClosureZ = -4.8f;
    private const float AlleyWestX = -13.6f;
    private const float AlleyEndZ = 4f;
    private const float LotBackZ = 0f;
    private const float FenceHeight = 1.6f;

    private const string EnvironmentRoot = "--- ENVIRONMENT ---";
    private const string LightingRoot = "--- LIGHTING ---";
    private const string MarkersRoot = "--- MARKERS ---";
    private const string MapGroup = "N1_Mapa";
    private const string LightsGroup = "N1_Luces";
    private const string VistaRoot = "--- VISTA ---";
    private const string SafetyRoot = "N1_RedSeguridad";
    private const string LevelExitAnchor = "Anchor_LevelExit";
    private const string GateAnchor = "Anchor_PortonBosque";

    private static readonly Color WarmLamp = new Color32(0xFF, 0xB0, 0x60, 0xFF);

    // Mezclas de piezas (regla N5). Se repiten ids para darle más peso a una pieza
    private static readonly string[] MixStreet = { Iron, Hedge, CarParked, Crates, Rubble, PicketBroken, Ditch, Barricade };
    private static readonly string[] MixLow = { Picket, PicketBroken, Hedge };
    private static readonly string[] MixAlley = { Picket, PicketBroken, Hedge, Iron };
    private static readonly string[] MixLot = { Rubble, PicketBroken, Crates, Iron, Ditch };
    private static readonly string[] MixForest = { TreeLine, TreeLine, TreeLine, Rubble, Hedge };

    // ==================================================================
    // Menú
    // ==================================================================

    [MenuItem("Plomo y Paja/Nivel 1/Ampliar mapa")]
    public static void BuildMenu()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        if (!File.Exists(LevelBuilder_NivelPorky.ScenePath))
        {
            EditorUtility.DisplayDialog("Ampliar mapa", $"No existe {LevelBuilder_NivelPorky.ScenePath}. Primero: Plomo y Paja → Generar Nivel Porky.", "Entendido");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != LevelBuilder_NivelPorky.ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(LevelBuilder_NivelPorky.ScenePath, OpenSceneMode.Single);
        }

        CartoonKit.BeginSession();
        Transform environment = FindOrCreateRoot(scene, EnvironmentRoot);
        Transform lighting = FindOrCreateRoot(scene, LightingRoot);
        Transform markers = FindOrCreateRoot(scene, MarkersRoot);

        var report = new StringBuilder();
        RemoveLegacy(environment, lighting, markers, report);
        LevelBuilder_NivelPorky.RefreshYardGround(environment);
        ContadorYardBuilder.RefreshYardForMap(environment, lighting);
        BuildInto(environment, lighting, markers, report);
        PickupBuilder.BuildPrefabs(report); // N1.2: los objetos van sobre los muebles que se acaban de rehacer
        PickupBuilder.PlaceInOpenScene(report);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        LevelBuilder_NivelPorky.BakeNavMesh();
        bool ok = Validate(report);
        ok &= ContadorYardBuilder.ValidatePaths(report);
        ok &= PickupBuilder.ValidateSpots(report);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[Nivel 1] Ampliar mapa:\n" + report);
        EditorUtility.DisplayDialog("Ampliar mapa", (ok ? "Listo. Escena guardada." : "Listo, pero hay comprobaciones que fallaron (ver la consola).") +
                                                    "\nEl reporte completo está en la consola.", "Entendido");
    }

    private static Transform FindOrCreateRoot(Scene scene, string name)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
            if (go.name == name) return go.transform;
        return LevelBuilderUtils.CreateGroup(name);
    }

    /// <summary>Lo que construía el nivel antes de N1.1. Solo hace falta al correr el menú sobre una escena vieja.</summary>
    private static void RemoveLegacy(Transform environment, Transform lighting, Transform markers, StringBuilder report)
    {
        int removed = 0;
        void Remove(Transform parent, string name)
        {
            if (parent == null) return;
            Transform child = parent.Find(name);
            if (child == null) return;
            Object.DestroyImmediate(child.gameObject);
            removed++;
        }

        Transform limits = environment.Find("Limites");
        foreach (string name in new[] { "Callejon_Muro_Izq", "Callejon_Muro_Der", "Callejon_Muro_Fondo", "Edificios_Enfrente", "Calle_Muro_Izq", "Calle_Muro_Der",
                     "Calle_Borde_Izq", "Calle_Borde_Der", "Valla_Fondo" })
            Remove(limits, name);
        Remove(environment.Find("Suelos"), "Callejon_Suelo");
        Transform props = environment.Find("Patio_Props");
        foreach (string name in new[] { "Barril_Callejon_A", "Barril_Callejon_B", "Caja_Callejon" }) Remove(props, name);
        Transform lamps = environment.Find("Faroles");
        foreach (string name in new[] { "Aplique_Callejon_A", "Aplique_Callejon_B" })
        {
            Remove(lamps, name);
            Remove(lighting, name + "_Luz");
            Remove(lighting, name + "_Haz");
        }
        Remove(markers, "LevelExit"); // marcador sin lógica en el callejón viejo; ahora manda Anchor_LevelExit
        report.AppendLine($"✓ Quitadas {removed} piezas viejas (muros en blanco y callejón del fondo)");
    }

    // ==================================================================
    // Construcción
    // ==================================================================

    /// <summary>Arma todo en la escena abierta. Lo llaman el menú y Generar Nivel Porky (antes de hornear). No llama a BeginSession.</summary>
    public static void BuildInto(Transform environment, Transform lighting, Transform markers, StringBuilder report)
    {
        BuildAll(report);

        DestroyChild(environment, MapGroup);
        DestroyChild(lighting, LightsGroup);
        foreach (string rootName in new[] { VistaRoot, SafetyRoot })
        {
            GameObject old = GameObject.Find("/" + rootName);
            if (old != null) Object.DestroyImmediate(old);
        }

        Transform map = LevelBuilderUtils.CreateGroup(MapGroup, environment);
        Transform lights = LevelBuilderUtils.CreateGroup(LightsGroup, lighting);

        Transform walkable = LevelBuilderUtils.CreateGroup("Transitable", map);
        BuildGrounds(walkable);
        BuildFacades(walkable);
        BuildNeighbours(walkable, lights);
        BuildSideAlley(walkable, lights);
        BuildEmptyLot(walkable, lights);
        BuildStreetLamps(walkable, lights);
        BuildYardBack(walkable, lights);
        LevelBuilderUtils.MarkStaticRecursive(walkable.gameObject);

        Transform borders = LevelBuilderUtils.CreateGroup("Bordes", map);
        BuildBorders(borders, report);
        LevelBuilderUtils.MarkStaticRecursive(borders.gameObject);

        // Portón aparte: las hojas, la cadena y el candado no son estáticos (N1.4 los mueve)
        Transform gateGroup = LevelBuilderUtils.CreateGroup("Porton", map);
        BuildGate(gateGroup, report);

        Transform forest = LevelBuilderUtils.CreateGroup("Bosque", map);
        BuildForestPath(forest, lights, report);
        LevelBuilderUtils.MarkStaticRecursive(forest.gameObject);
        CartoonKit.ExcludeFromNavMesh(forest);

        BuildVista(report);
        PlaceAnchors(markers);
        BuildSafetyCatch(markers);
        ApplyReadableLighting(report);
        Nivel1FurnitureBuilder.BuildInto(environment, lighting); // N1.2: los muebles donde van los objetos

        report.AppendLine($"✓ Calle x ±{StreetEndX}, vecinos, callejón lateral, lote vacío, portón del bosque en {ForestGatePosition}, salida en {LevelExitPosition}");
    }

    private static Vector3 V(float x, float z) => new Vector3(x, 0f, z);

    private static void DestroyChild(Transform parent, string name)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            if (parent.GetChild(i).name == name) Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }

    // ------------------------------ Suelos ------------------------------

    private static void BuildGrounds(Transform parent)
    {
        // La calle se parte en mallas de 24-40 m: URP da 8 luces adicionales por objeto y una sola de 88 m se quedaría a oscuras
        CartoonKit.Cobblestones("Calle_Oeste", V(-32f, -15f), new Vector2(24f, 10f), parent);
        CartoonKit.Cobblestones("Calle_Este", V(32f, -15f), new Vector2(24f, 10f), parent);
        CartoonKit.Cobblestones("Callejon_Lateral_Suelo", V((AlleyWestX - 10f) * 0.5f, (YardFrontZ + AlleyEndZ) * 0.5f), new Vector2(-10f - AlleyWestX, AlleyEndZ - YardFrontZ + 0.2f), parent);

        Material grass = CartoonKit.M("Pasto");
        CartoonKit.Box("Suelo_Vecinos_Oeste", new Vector3(-28.8f, -0.1f, -7.4f), new Vector3(30.4f, 0.2f, 5.4f), grass, parent, default, 0f);
        CartoonKit.Box("Suelo_Vecinos_Este", new Vector3(19.5f, -0.1f, -7.4f), new Vector3(19f, 0.2f, 5.4f), grass, parent, default, 0f);
        CartoonKit.Box("Suelo_Lote", new Vector3(36.5f, -0.1f, -5f), new Vector3(15f, 0.2f, 10.2f), CartoonKit.M("Tierra"), parent, default, 0f);
        CartoonKit.Box("Suelo_Rincon_Bosque", new Vector3(-6.4f, -0.1f, 23.3f), new Vector3(7.4f, 0.2f, 4.6f), grass, parent, default, 0f);
    }

    // ------------------------------ Fachadas de enfrente, con huecos que dejan ver (N2) ------------------------------

    private static void BuildFacades(Transform parent)
    {
        CartoonKit.Facades("Edificios_Enfrente_A", -20.2f, -7.5f, FacadeZ, 2f, parent);
        CartoonKit.Facades("Edificios_Enfrente_B", -5f, 6.5f, FacadeZ, 2f, parent);
        CartoonKit.Facades("Edificios_Enfrente_C", 9f, 20.2f, FacadeZ, 2f, parent);
    }

    // ------------------------------ Bordes ------------------------------

    private static void Path(Transform parent, string name, string[] pieces, bool inOrder, int seed, ref int total, StringBuilder report, params Vector3[] points)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        BorderPath path = go.AddComponent<BorderPath>();
        path.Setup(points, pieces, inOrder, seed);
        total += BorderPathFiller.Fill(path, report);
    }

    private static void BuildBorders(Transform parent, StringBuilder report)
    {
        int n = 0;
        string[] gate = { Iron };
        string[] ends = { Barricade, Barricade };
        string[] crash = { CarCrashed, Rubble };

        // Acera de enfrente: huecos entre fachadas
        Path(parent, "Hueco_Enfrente_A", gate, true, 1, ref n, report, V(-7.6f, FacadeZ), V(-4.9f, FacadeZ));
        Path(parent, "Hueco_Enfrente_B", gate, true, 2, ref n, report, V(6.4f, FacadeZ), V(9.1f, FacadeZ));

        // Calle: borde sur de las extensiones y las dos puntas
        Path(parent, "Calle_Sur_Oeste", MixStreet, false, 11, ref n, report, V(-20.2f, FacadeZ), V(-20.2f, StreetSouthZ), V(-StreetEndX, StreetSouthZ));
        // (el sentido importa: la zanja de Ditch_Railing cae del lado +Z local; estos caminos la dejan afuera)
        Path(parent, "Calle_Sur_Este", MixStreet, false, 12, ref n, report, V(StreetEndX, StreetSouthZ), V(20.2f, StreetSouthZ), V(20.2f, FacadeZ));
        Path(parent, "Punta_Oeste_Barricada", ends, true, 13, ref n, report, V(-StreetEndX, StreetSouthZ - 0.1f), V(-StreetEndX, YardFrontZ + 0.1f));
        Path(parent, "Punta_Este_Choque", crash, true, 14, ref n, report, V(StreetEndX, StreetSouthZ - 0.1f), V(StreetEndX, YardFrontZ + 0.1f));

        // Vecinos del oeste (aberturas de 2 m en x -38..-36 y x -22..-20) y su cierre contra las casas
        Path(parent, "Frente_C", MixLow, false, 21, ref n, report, V(-StreetEndX, YardFrontZ), V(-38f, YardFrontZ));
        Path(parent, "Frente_C_A", MixLow, false, 22, ref n, report, V(-36f, YardFrontZ), V(-22f, YardFrontZ));
        Path(parent, "Frente_A", MixLow, false, 23, ref n, report, V(-20f, YardFrontZ), V(AlleyWestX, YardFrontZ));
        Path(parent, "Lado_C", MixLow, false, 24, ref n, report, V(-StreetEndX, YardFrontZ), V(-StreetEndX, ClosureZ), V(-42.9f, ClosureZ));
        Path(parent, "Divisoria_C_A", MixLow, false, 25, ref n, report, V(-29f, YardFrontZ), V(-29f, ClosureZ));
        Path(parent, "Cierre_C_A", gate, true, 26, ref n, report, V(-30.1f, ClosureZ), V(-27.9f, ClosureZ));
        Path(parent, "Cierre_A_Callejon", MixLow, false, 27, ref n, report, V(-15.1f, ClosureZ), V(AlleyWestX, ClosureZ));

        // Callejón lateral: lado oeste y la reja del fondo (se ve la calle de atrás)
        Path(parent, "Callejon_Oeste", MixAlley, false, 31, ref n, report, V(AlleyWestX, YardFrontZ), V(AlleyWestX, AlleyEndZ + 0.1f));
        Path(parent, "Callejon_Reja", gate, true, 32, ref n, report, V(AlleyWestX - 0.1f, AlleyEndZ), V(-9.9f, AlleyEndZ));

        // Vecino B y el tramo entre el camión y su patio
        Path(parent, "Frente_Junto_Camion", MixLow, false, 41, ref n, report, V(10f, YardFrontZ), V(14f, YardFrontZ));
        Path(parent, "Frente_B_Oeste", MixLow, false, 42, ref n, report, V(14f, YardFrontZ), V(21f, YardFrontZ));
        Path(parent, "Frente_B_Este", MixLow, false, 43, ref n, report, V(23f, YardFrontZ), V(29f, YardFrontZ));
        Path(parent, "Lado_B", MixLow, false, 44, ref n, report, V(14f, YardFrontZ), V(14f, ClosureZ), V(15.1f, ClosureZ));
        Path(parent, "Cierre_B_Lote", gate, true, 45, ref n, report, V(27.9f, ClosureZ), V(29f, ClosureZ));

        // Lote vacío: entrada ancha (x 33..39), lados y fondo con vista detrás
        Path(parent, "Lote_Divisoria", MixLow, false, 51, ref n, report, V(29f, YardFrontZ), V(29f, LotBackZ + 0.1f));
        Path(parent, "Lote_Frente_A", new[] { PicketBroken }, true, 52, ref n, report, V(29f, YardFrontZ), V(33f, YardFrontZ));
        Path(parent, "Lote_Frente_B", new[] { PicketBroken, Rubble }, true, 53, ref n, report, V(39f, YardFrontZ), V(StreetEndX, YardFrontZ));
        Path(parent, "Lote_Este", MixLot, false, 54, ref n, report, V(StreetEndX, LotBackZ), V(StreetEndX, YardFrontZ));
        Path(parent, "Lote_Fondo", MixLot, false, 55, ref n, report, V(29f, LotBackZ), V(StreetEndX, LotBackZ));

        report.AppendLine($"✓ Bordes: {n} piezas colocadas por BorderPath");
    }

    private static void BuildGate(Transform parent, StringBuilder report)
    {
        GameObject prefab = Load(GateForest);
        if (prefab == null)
        {
            report.AppendLine("✗ Falta Gate_Bosque (Crear kit de bordes).");
            return;
        }
        var gate = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        gate.name = GateForest;
        gate.transform.SetPositionAndRotation(ForestGatePosition, Quaternion.identity);
    }

    // ------------------------------ Vecinos ------------------------------

    private static void BuildNeighbours(Transform parent, Transform lights)
    {
        Neighbour("Vecino_C", -43f, -30f, 5.6f, "RevoqueCalido", -37f, false, parent, lights);
        Neighbour("Vecino_A", -28f, -15f, 6.2f, "Ladrillo", -21f, false, parent, lights);
        Neighbour("Vecino_B", 15f, 28f, 5.2f, "Revoque", NeighbourBDoorX, true, parent, lights);

        // El vecino A da al callejón: ventanas de ese lado, para que no sea una pared en blanco
        Transform a = parent.Find("Vecino_A");
        foreach (float z in new[] { -2.6f, 0.6f })
            CartoonKit.WindowFrame($"Vecino_A_Ventana_Lado_{z:0}", new Vector3(-14.93f, 2.1f, z), Quaternion.Euler(0f, 90f, 0f), 1f, 1.2f, a, true);
    }

    /// <summary>Casa no enterable con fachada en z = -5: porche, escalón, ventanas encendidas, techo a dos aguas, buzón y aplique.</summary>
    private static void Neighbour(string name, float xMin, float xMax, float height, string wall, float doorX, bool porchTable, Transform parent, Transform lights)
    {
        const float depth = 9f, zFront = NeighbourFacadeZ;
        Transform root = LevelBuilderUtils.CreateGroup(name, parent);
        float cx = (xMin + xMax) * 0.5f, w = xMax - xMin;

        CartoonKit.Box(name + "_Cuerpo", new Vector3(cx, height * 0.5f - 0.1f, zFront + depth * 0.5f), new Vector3(w, height + 0.2f, depth), CartoonKit.M(wall), root, default, 0.12f);

        Mesh roof = CartoonKit.GetMesh($"Vecino_Techo_{CartoonKit.Cm(w)}x{CartoonKit.Cm(depth)}", b =>
        {
            float half = w * 0.5f + 0.35f;
            const float rise = 1.8f;
            float slope = Mathf.Sqrt(depth * depth * 0.25f + rise * rise) + 0.35f;
            float angle = Mathf.Atan2(rise, depth * 0.5f) * Mathf.Rad2Deg;
            b.Submesh = 0;
            CartoonKit.AddBox(b, new Vector3(0f, rise * 0.5f, -depth * 0.25f), new Vector3(half * 2f, 0.16f, slope), Quaternion.Euler(-angle, 0f, 0f), 0.05f);
            CartoonKit.AddBox(b, new Vector3(0f, rise * 0.5f, depth * 0.25f), new Vector3(half * 2f, 0.16f, slope), Quaternion.Euler(angle, 0f, 0f), 0.05f);
            b.Submesh = 1;
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * w * 0.5f;
                Vector3 p0 = new Vector3(x, 0f, -depth * 0.5f), p1 = new Vector3(x, 0f, depth * 0.5f), p2 = new Vector3(x, rise, 0f);
                b.Card(new[] { p0, p2, p1 }, Vector3.right * s);
                b.Card(new[] { p1, p2, p0 }, Vector3.right * s);
            }
        });
        CartoonKit.MeshObject(name + "_Techo", roof, new[] { CartoonKit.M("TechoEdificio"), CartoonKit.M(wall) }, root, new Vector3(cx, height, zFront + depth * 0.5f), Quaternion.identity);

        // Porche: piso de 0.24 m (se sube sin saltar, stepOffset 0.3), escalón, postes y alero
        const float porchTop = PorchTop, porchDepth = 2f, porchWidth = 5f;
        Material wood = CartoonKit.M("Madera"), dark = CartoonKit.M("MaderaOscura");
        CartoonKit.Box(name + "_Porche", new Vector3(doorX, porchTop * 0.5f - 0.05f, zFront - porchDepth * 0.5f), new Vector3(porchWidth, porchTop + 0.1f, porchDepth), wood, root, default, 0.03f);
        CartoonKit.Box(name + "_Escalon", new Vector3(doorX, 0.05f, zFront - porchDepth - 0.25f), new Vector3(2f, 0.12f, 0.5f), dark, root, default, 0.03f);
        foreach (float s in new[] { -1f, 1f })
            CartoonKit.Box($"{name}_Poste_{(s < 0f ? "Izq" : "Der")}", new Vector3(doorX + s * 2.3f, 1.45f, zFront - porchDepth + 0.15f), new Vector3(0.16f, 2.5f, 0.16f), dark, root,
                new Vector3(0f, 0f, s * 1.2f), 0.04f);
        CartoonKit.Box(name + "_Alero", new Vector3(doorX, 2.76f, zFront - porchDepth * 0.5f), new Vector3(porchWidth + 0.5f, 0.14f, porchDepth + 0.3f), CartoonKit.M("TechoEdificio"), root,
            new Vector3(3f, 0f, 0f), 0.04f, false);

        // Puerta cerrada: no se entra. +Z local de puerta y ventanas = hacia afuera (la calle está en -Z)
        Quaternion outward = Quaternion.Euler(0f, 180f, 0f);
        CartoonKit.DoorLeaf(name + "_Puerta", new Vector3(doorX, porchTop + 1.1f, zFront - 0.04f), outward, 1.1f, 2.2f, CartoonKit.M("Puerta"), root);
        int window = 0;
        for (float x = xMin + 1.6f; x < xMax - 1f; x += 2.6f)
        {
            bool nearDoor = Mathf.Abs(x - doorX) < 1.4f;
            if (!nearDoor)
                CartoonKit.WindowFrame($"{name}_Ventana_{window++}", new Vector3(x, 1.75f, zFront - 0.07f), outward, 1f, 1.2f, root, window % 3 != 2);
            if (height > 5f)
                CartoonKit.WindowFrame($"{name}_Ventana_{window++}", new Vector3(x, 4f, zFront - 0.07f), outward, 0.9f, 1f, root, window % 2 == 0);
        }

        // Buzón junto a la entrada del patio y aplique en la fachada (luz chica, sin sombras)
        Vector3 mail = new Vector3(doorX + 1.5f, 0f, YardFrontZ + 0.45f);
        CartoonKit.Box(name + "_Buzon_Poste", mail + Vector3.up * 0.52f, new Vector3(0.09f, 1.04f, 0.09f), dark, root, default, 0.02f);
        CartoonKit.Box(name + "_Buzon", mail + Vector3.up * 1.14f, new Vector3(0.24f, 0.22f, 0.42f), CartoonKit.M("Metal"), root, new Vector3(0f, 4f, 0f), 0.05f);

        Light lamp = CartoonKit.WallLamp(name + "_Aplique", new Vector3(doorX + 1.2f, 2.4f, zFront - 0.02f), 180f, root, lights, WarmLamp, 1.7f, 7.5f);
        lamp.shadows = LightShadows.None;

        if (porchTable)
        {
            // Para N1.2: mesa del porche del vecino
            CartoonKit.RoundTable(name + "_Mesa_Porche", new Vector3(doorX + PorchTableOffsetX, porchTop, zFront + PorchTableOffsetZ), root, 0.42f);
            CartoonKit.Chair(name + "_Silla_Porche", new Vector3(doorX - 1.5f, porchTop, zFront - 1f), 100f, root);
        }
    }

    // ------------------------------ Callejón lateral ------------------------------

    private static void BuildSideAlley(Transform parent, Transform lights)
    {
        Transform alley = LevelBuilderUtils.CreateGroup("Callejon_Lateral", parent);

        Mesh can = CartoonKit.GetMesh("BoteBasura", b =>
        {
            b.Submesh = 0;
            b.Lathe(new[] { 0f, 0.72f }, new[] { 0.24f, 0.29f }, 12, true, false);
            b.Submesh = 1;
            b.Lathe(new[] { 0.72f, 0.78f, 0.84f }, new[] { 0.32f, 0.3f, 0.08f }, 12, false, true);
        });
        Material[] canMats = { CartoonKit.M("Metal"), CartoonKit.M("PiedraOscura") };
        var cans = new[] { new Vector3(-13.05f, 0f, -6.6f), new Vector3(-12.95f, 0f, -5.8f), new Vector3(-10.55f, 0f, 1.1f) };
        for (int i = 0; i < cans.Length; i++)
            CartoonKit.FitCollider(CartoonKit.MeshObject($"Bote_Basura_{i}", can, canMats, alley, cans[i], Quaternion.Euler(0f, i * 40f, 0f)));

        CartoonKit.Crate("Caja_Callejon_Lateral_A", AlleyCratePosition, 8f, alley, AlleyCrateSize);
        CartoonKit.Crate("Caja_Callejon_Lateral_B", new Vector3(-10.6f, 0f, 2.7f), -12f, alley, 0.7f);
        CartoonKit.Barrel("Barril_Callejon_Lateral", new Vector3(-10.55f, 0f, -2.2f), 20f, alley, 3);

        // Tendedero de lado a lado, por encima de la cabeza (sin collider)
        Mesh line = CartoonKit.GetMesh("Tendedero", b =>
        {
            System.Random rng = CartoonKit.Rng("Tendedero");
            b.Submesh = 0;
            foreach (float x in new[] { -1.98f, 2.02f })
                CartoonKit.AddBox(b, new Vector3(x, 1.7f, 0f), new Vector3(0.09f, 3.6f, 0.09f), Quaternion.Euler(0f, 0f, CartoonKit.Range(rng, -1.5f, 1.5f)), 0.02f);
            b.Submesh = 1;
            CartoonKit.AddBox(b, new Vector3(0f, 3.14f, 0f), new Vector3(4f, 0.02f, 0.02f), Quaternion.identity, 0.005f);
            for (int i = 0; i < 5; i++)
            {
                b.Submesh = 2 + i % 3;
                float x = -1.5f + i * 0.72f;
                float h = CartoonKit.Range(rng, 0.45f, 0.8f);
                CartoonKit.AddBox(b, new Vector3(x, 3.12f - h * 0.5f, 0f), new Vector3(CartoonKit.Range(rng, 0.42f, 0.6f), h, 0.02f),
                    Quaternion.Euler(CartoonKit.Range(rng, -6f, 6f), 0f, CartoonKit.Range(rng, -5f, 5f)), 0.008f);
            }
        });
        CartoonKit.MeshObject("Tendedero", line, new[] { CartoonKit.M("MaderaOscura"), CartoonKit.M("Cuerda"), CartoonKit.M("Yeso"), CartoonKit.M("Manta"), CartoonKit.M("AlfombraBorde") },
            alley, new Vector3((AlleyWestX - 10f) * 0.5f, 0f, -3.6f), Quaternion.identity);

        Lamp("Farol_Callejon_Lateral", new Vector3(AlleyWestX - 0.25f, 0f, 0.6f), 90f, alley, lights, 2.2f, 10f);
    }

    // ------------------------------ Lote vacío ------------------------------

    private static void BuildEmptyLot(Transform parent, Transform lights)
    {
        Transform lot = LevelBuilderUtils.CreateGroup("Lote_Vacio", parent);

        foreach ((string id, Vector3 pos, float yaw) in new[]
                 {
                     ("Rubble_Lote_A", new Vector3(33.4f, 0f, -3.6f), 24f),
                     ("Rubble_Lote_B", new Vector3(41f, 0f, -6.2f), -38f),
                 })
        {
            GameObject prefab = Load(Rubble);
            if (prefab == null) continue;
            var pile = (GameObject)PrefabUtility.InstantiatePrefab(prefab, lot);
            pile.name = id;
            pile.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        }
        Material stone = CartoonKit.M("PiedraOscura");
        var rocks = new[] { new Vector3(36.6f, 0f, -7.4f), new Vector3(38.2f, 0f, -3.8f), new Vector3(31.2f, 0f, -7.8f), new Vector3(42.6f, 0f, -2.2f) };
        for (int i = 0; i < rocks.Length; i++)
            CartoonKit.Rubble($"Lote_Escombro_{i}", rocks[i], 0.45f + i * 0.08f, i * 47f, stone, lot, i + 40);

        // Cartel vacío: marco sobre dos postes, sin letras (la caja solo en los postes: se ve y se dispara a través del marco)
        Mesh frame = CartoonKit.GetMesh("Cartel_Vacio", b =>
        {
            System.Random rng = CartoonKit.Rng("Cartel_Vacio");
            b.Submesh = 0;
            foreach (float x in new[] { -3.6f, 3.6f })
            {
                CartoonKit.AddBox(b, new Vector3(x, 2.65f, 0f), new Vector3(0.24f, 5.7f, 0.24f), Quaternion.Euler(0f, 0f, CartoonKit.Range(rng, -1.5f, 1.5f)), 0.05f);
                CartoonKit.AddBox(b, new Vector3(x, 1.15f, 0.55f), new Vector3(0.12f, 2.7f, 0.12f), Quaternion.Euler(24f, 0f, 0f), 0.03f);
            }
            foreach (float y in new[] { 2.5f, 5.3f })
                CartoonKit.AddBox(b, new Vector3(0f, y, -0.1f), new Vector3(8f, 0.22f, 0.14f), Quaternion.Euler(0f, 0f, CartoonKit.Range(rng, -1f, 1f)), 0.03f);
            foreach (float x in new[] { -3.9f, 3.9f })
                CartoonKit.AddBox(b, new Vector3(x, 3.9f, -0.1f), new Vector3(0.2f, 3f, 0.14f), Quaternion.identity, 0.03f);
            b.Submesh = 1;
            CartoonKit.AddBox(b, new Vector3(0f, 3.9f, -0.04f), new Vector3(7.8f, 0.1f, 0.05f), Quaternion.Euler(0f, 0f, 1.2f), 0.012f);
            b.Submesh = 2; // restos de papel despegado, sin texto
            for (int i = 0; i < 5; i++)
                CartoonKit.AddBox(b, new Vector3(CartoonKit.Range(rng, -3.5f, 3.5f), i % 2 == 0 ? CartoonKit.Range(rng, 4.6f, 5.1f) : CartoonKit.Range(rng, 2.7f, 3.2f), -0.13f),
                    new Vector3(CartoonKit.Range(rng, 0.3f, 0.9f), CartoonKit.Range(rng, 0.25f, 0.6f), 0.01f), Quaternion.Euler(0f, 0f, CartoonKit.Range(rng, -18f, 18f)), 0.004f);
        });
        GameObject billboard = CartoonKit.MeshObject("Cartel_Vacio", frame, new[] { CartoonKit.M("MaderaOscura"), CartoonKit.M("Madera"), CartoonKit.M("Factura") }, lot,
            new Vector3(36.5f, 0f, -1.1f), Quaternion.identity);
        foreach (float x in new[] { -3.6f, 3.6f })
        {
            BoxCollider box = billboard.AddComponent<BoxCollider>();
            box.center = new Vector3(x, 2.65f, 0f);
            box.size = new Vector3(0.3f, 5.7f, 0.3f);
        }

        Lamp("Farol_Lote", new Vector3(29.3f, 0f, -2.4f), 90f, lot, lights, 2.4f, 11f);
    }

    // ------------------------------ Faroles (luz real solo dentro del área jugable, regla N4; sin sombras) ------------------------------

    private static void Lamp(string name, Vector3 position, float armYaw, Transform parent, Transform lights, float intensity, float range, float tilt = 0f, bool broken = false)
    {
        Light light = CartoonKit.StreetLamp(name, position, armYaw, parent, lights, WarmLamp, intensity, range, tilt, broken);
        light.shadows = LightShadows.None;

        Transform model = parent.Find(name);
        if (model != null && !model.TryGetComponent(out Collider _))
        {
            BoxCollider box = model.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 2f, 0f);
            box.size = new Vector3(0.3f, 4f, 0.3f);
        }

        if (broken)
        {
            var so = new SerializedObject(light.gameObject.AddComponent<LightFlicker>());
            so.FindProperty("noiseAmount").floatValue = 0.3f;
            so.FindProperty("dipChancePerSecond").floatValue = 1.6f;
            so.FindProperty("offChancePerSecond").floatValue = 0.4f;
            so.FindProperty("maxBlinks").intValue = 4;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void BuildStreetLamps(Transform parent, Transform lights)
    {
        Transform lamps = LevelBuilderUtils.CreateGroup("Faroles_Calle", parent);
        const float north = -10.55f, south = StreetSouthZ - 1.3f; // los del sur, detrás del borde: el brazo cae sobre la calle
        Lamp("Farol_Oeste_1", new Vector3(-24f, 0f, north), 90f, lamps, lights, 2.6f, 12.5f);
        Lamp("Farol_Oeste_2", new Vector3(-33f, 0f, south), 0f, lamps, lights, 2.6f, 12.5f);
        Lamp("Farol_Oeste_3", new Vector3(-41f, 0f, north), 90f, lamps, lights, 2.6f, 12.5f);
        Lamp("Farol_Este_1", new Vector3(24f, 0f, north), -90f, lamps, lights, 2.6f, 12.5f);
        Lamp("Farol_Este_2", new Vector3(33f, 0f, south), 0f, lamps, lights, 2.6f, 12.5f);
        Lamp("Farol_Roto_Este", new Vector3(41.6f, 0f, -15.9f), -90f, lamps, lights, 2.4f, 11f, 9f, true);
    }

    // ------------------------------ Fondo del patio: cerca nueva, rincón de Centavo hasta el portón ------------------------------

    private static void BuildYardBack(Transform parent, Transform lights)
    {
        Transform back = LevelBuilderUtils.CreateGroup("Fondo_Patio", parent);
        float z = ContadorYardBuilder.RinconBackZ, gateHalf = Length(GateForest) * 0.5f;
        float gateX = ForestGatePosition.x;

        // z = 21: desde la cerca del rincón hasta la valla derecha (el resto de z = 21 quedó abierto, es el rincón que sigue)
        CartoonKit.Fence("Valla_Fondo", new Vector3(ContadorYardBuilder.YardFenceX - 0.1f, 0f, 21f), new Vector3(10.1f, 0f, 21f), FenceHeight, back);

        CartoonKit.Fence("Valla_Rincon_Oeste", new Vector3(-10f, 0f, 20.9f), new Vector3(-10f, 0f, z + 0.1f), FenceHeight, back);
        CartoonKit.Fence("Valla_Rincon_Fondo_Izq", new Vector3(-10.1f, 0f, z), new Vector3(gateX - gateHalf + 0.1f, 0f, z), FenceHeight, back);
        CartoonKit.Fence("Valla_Rincon_Fondo_Der", new Vector3(gateX + gateHalf - 0.1f, 0f, z), new Vector3(ContadorYardBuilder.YardFenceX + 0.1f, 0f, z), FenceHeight, back);

        Lamp("Farol_Porton_Bosque", new Vector3(ContadorYardBuilder.YardFenceX - 0.3f, 0f, z - 0.9f), -90f, back, lights, 1.9f, 9f);
    }

    // ------------------------------ Sendero del bosque (detrás del portón) ------------------------------

    private static void BuildForestPath(Transform parent, Transform lights, StringBuilder report)
    {
        float z = ContadorYardBuilder.RinconBackZ;
        CartoonKit.Box("Suelo_Bosque", new Vector3(-6.4f, -0.1f, (z + 58.5f) * 0.5f), new Vector3(13f, 0.2f, 58.5f - z), CartoonKit.M("PastoLejano"), parent, default, 0f);
        CartoonKit.DirtPath("Camino_Bosque", new[] { V(-6.4f, z), V(-7.4f, 33f), V(-5f, 41f), V(-6.7f, 49f), V(-6.2f, 57f) }, 2f, parent);

        int n = 0;
        Path(parent, "Bosque_Oeste", MixForest, false, 61, ref n, report, V(-9f, z + 0.2f), V(-10f, 33f), V(-7.5f, 41f), V(-9.2f, 49f), V(-9f, 58f));
        Path(parent, "Bosque_Este", MixForest, false, 62, ref n, report, V(-3.8f, z + 0.2f), V(-4.8f, 33f), V(-2.5f, 41f), V(-4.2f, 49f), V(-3.5f, 58f));
        Path(parent, "Bosque_Final", new[] { TreeLine }, true, 63, ref n, report, V(-9.1f, 58f), V(-3.4f, 58f));

        Lamp("Farol_Bosque_1", new Vector3(-8.3f, 0f, 37f), 90f, parent, lights, 1.6f, 9f);
        Lamp("Farol_Bosque_2", new Vector3(-3.5f, 0f, 47f), -90f, parent, lights, 1.6f, 9f);
    }

    // ==================================================================
    // Vista: cerca (10-80 m) en mallas únicas sin collider; lejos, tarjetas (N4)
    // ==================================================================

    private const int SubBrick = 0, SubPlaster = 1, SubWarm = 2, SubRoof = 3, SubLit = 4, SubDark = 5, SubWood = 6, SubMetal = 7, SubGlow = 8;
    private const float HorizontalLimit = 86f; // el anillo de colinas cercanas de NightSkyBuilder está a 92 m

    private static void BuildVista(StringBuilder report)
    {
        Transform vista = LevelBuilderUtils.CreateGroup(VistaRoot);

        // La calle sigue después de las puntas; la de atrás del callejón y la de atrás de las fachadas
        CobblesNoCollider("Calle_Vista_Oeste", V(-StreetEndX - 12f, -15f), new Vector2(24f, 10f), vista);
        CobblesNoCollider("Calle_Vista_Este", V(StreetEndX + 12f, -15f), new Vector2(24f, 10f), vista);
        CobblesNoCollider("Calle_De_Atras", V(-38f, 8f), new Vector2(56f, 6f), vista);
        Material stone = CartoonKit.M("PiedraOscura");
        CartoonKit.Box("Calle_Lejos_Oeste", new Vector3(-77f, -0.12f, -15f), new Vector3(18f, 0.2f, 10f), stone, vista, default, 0f, false);
        CartoonKit.Box("Calle_Lejos_Este", new Vector3(77f, -0.12f, -15f), new Vector3(18f, 0.2f, 10f), stone, vista, default, 0f, false);
        CartoonKit.Box("Calle_Tras_Fachadas", new Vector3(0f, -0.12f, -25f), new Vector3(2f * HorizontalLimit, 0.2f, 5f), stone, vista, default, 0f, false);

        Mesh houses = CartoonKit.GetMesh("Vista_Casas", BuildVistaHouses);
        GameObject housesGo = CartoonKit.MeshObject("Vista_Casas", houses, new[]
            {
                CartoonKit.M("Ladrillo"), CartoonKit.M("Revoque"), CartoonKit.M("RevoqueCalido"), CartoonKit.M("TechoEdificio"), CartoonKit.M("VidrioCalido"),
                CartoonKit.M("VentanaApagada"), CartoonKit.M("MaderaOscura"), CartoonKit.M("Metal"), CartoonKit.M("Bombilla"),
            }, vista, Vector3.zero, Quaternion.identity);
        VistaFlags(housesGo);

        Mesh trees = CartoonKit.GetMesh("Vista_Bosque", BuildVistaForest);
        GameObject treesGo = CartoonKit.MeshObject("Vista_Bosque", trees, new[] { CartoonKit.M("MaderaOscura"), CartoonKit.M("Hoja"), CartoonKit.M("PastoLejano") },
            vista, Vector3.zero, Quaternion.identity);
        VistaFlags(treesGo);

        BuildBackdrops(vista, report);
        CartoonKit.MakeNonStatic(vista.gameObject);
        foreach (MeshRenderer r in vista.GetComponentsInChildren<MeshRenderer>(true))
            GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
    }

    private static void CobblesNoCollider(string name, Vector3 center, Vector2 size, Transform parent)
    {
        GameObject root = CartoonKit.Cobblestones(name, center, size, parent);
        if (root.TryGetComponent(out BoxCollider box)) Object.DestroyImmediate(box);
    }

    private static void VistaFlags(GameObject go)
    {
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = ShadowCastingMode.Off;
    }

    private static void BuildVistaHouses(CartoonKit.KitMesh b)
    {
        System.Random rng = CartoonKit.Rng("Vista_Casas");

        // Al sur de la calle (miran al norte) y a los lados de la calle que sigue
        HouseRow(b, rng, -HorizontalLimit, HorizontalLimit, -31f, 0f, 5f, 8.5f);
        HouseRow(b, rng, -80f, 80f, -49f, 0f, 5f, 9f);
        HouseRow(b, rng, -66f, 66f, -67f, 0f, 6f, 10f);
        HouseRow(b, rng, -HorizontalLimit, -48f, -3f, 180f, 5f, 8f);
        HouseRow(b, rng, 50f, HorizontalLimit, -3f, 180f, 5f, 8f);

        // Al norte de la calle de atrás (la que se ve por la reja del callejón)
        HouseRow(b, rng, -80f, -16f, 15.5f, 180f, 4.5f, 7f);
        HouseRow(b, rng, -76f, -18f, 33f, 180f, 5f, 8f);

        // Al este del patio: bajas, no tapan la colina de las casas
        HouseRow(b, rng, 18f, 46f, 11f, 180f, 4.2f, 5.2f);
        HouseRow(b, rng, 50f, 80f, 12f, 180f, 4.5f, 6f);

        WaterTower(b, new Vector3(-62f, 0f, -58f), 15f);
        WaterTower(b, new Vector3(66f, 0f, 38f), 13f);

        // Faroles solo emisivos, en la calle que sigue y en la de atrás
        foreach (float x in new[] { 50f, 62f, 74f })
        {
            AddLampPost(b, new Vector3(x, 0f, -9.7f), SubMetal, SubGlow);
            AddLampPost(b, new Vector3(-x, 0f, -9.7f), SubMetal, SubGlow);
            AddLampPost(b, new Vector3(x + 6f, 0f, -20.3f), SubMetal, SubGlow);
            AddLampPost(b, new Vector3(-x - 6f, 0f, -20.3f), SubMetal, SubGlow);
        }
        foreach (float x in new[] { -18f, -32f, -46f, -60f }) AddLampPost(b, new Vector3(x, 0f, 11.8f), SubMetal, SubGlow);
        foreach (float x in new[] { -60f, -30f, 0f, 30f, 60f }) AddLampPost(b, new Vector3(x, 0f, -27.8f), SubMetal, SubGlow);
    }

    private static void HouseRow(CartoonKit.KitMesh b, System.Random rng, float from, float to, float z, float yaw, float minH, float maxH)
    {
        float x = from;
        while (x < to)
        {
            float w = CartoonKit.Range(rng, 8f, 12f);
            AddVistaHouse(b, rng, new Vector3(x + w * 0.5f, -0.35f, z + CartoonKit.Range(rng, -1.5f, 1.5f)), yaw + CartoonKit.Range(rng, -2f, 2f), w, CartoonKit.Range(rng, minH, maxH),
                CartoonKit.Range(rng, 6f, 8f));
            x += w + CartoonKit.Range(rng, 2f, 5f);
        }
    }

    /// <summary>Casa de la vista: +Z local = frente; el cuerpo crece hacia -Z local.</summary>
    private static void AddVistaHouse(CartoonKit.KitMesh b, System.Random rng, Vector3 pos, float yaw, float w, float h, float d)
    {
        Matrix4x4 saved = b.Matrix;
        b.Matrix = saved * Matrix4x4.TRS(pos, Quaternion.Euler(0f, yaw, CartoonKit.Range(rng, -1f, 1f)), Vector3.one);

        int wall = rng.Next(3);
        b.Submesh = wall;
        CartoonKit.AddBox(b, new Vector3(0f, h * 0.5f, -d * 0.5f), new Vector3(w, h, d), Quaternion.identity, 0.08f);
        b.Submesh = SubRoof;
        if (rng.NextDouble() < 0.6)
        {
            const float rise = 1.6f;
            float slope = Mathf.Sqrt(d * d * 0.25f + rise * rise) + 0.3f;
            float angle = Mathf.Atan2(rise, d * 0.5f) * Mathf.Rad2Deg;
            CartoonKit.AddBox(b, new Vector3(0f, h + rise * 0.5f, -d * 0.25f), new Vector3(w + 0.5f, 0.14f, slope), Quaternion.Euler(-angle, 0f, 0f), 0.04f);
            CartoonKit.AddBox(b, new Vector3(0f, h + rise * 0.5f, -d * 0.75f), new Vector3(w + 0.5f, 0.14f, slope), Quaternion.Euler(angle, 0f, 0f), 0.04f);
        }
        else CartoonKit.AddBox(b, new Vector3(0f, h + 0.08f, -d * 0.5f), new Vector3(w + 0.3f, 0.22f, d + 0.3f), Quaternion.identity, 0.05f);

        // Ventanas: encendidas o apagadas (emisivas, sin luz real) y una puerta
        int cols = Mathf.Max(2, Mathf.FloorToInt(w / 2.4f));
        int floors = Mathf.Max(1, Mathf.FloorToInt((h - 1.2f) / 2.5f));
        for (int f = 0; f < floors; f++)
        for (int c = 0; c < cols; c++)
        {
            float x = -w * 0.5f + w * (c + 0.5f) / cols;
            if (f == 0 && c == cols / 2)
            {
                b.Submesh = SubWood;
                CartoonKit.AddBox(b, new Vector3(x, 1.25f, 0.03f), new Vector3(1f, 2.2f, 0.06f), Quaternion.identity, 0.01f);
                continue;
            }
            b.Submesh = rng.NextDouble() < 0.5 ? SubLit : SubDark;
            CartoonKit.AddBox(b, new Vector3(x, 1.9f + f * 2.5f, 0.03f), new Vector3(0.9f, 1.2f, 0.06f), Quaternion.identity, 0.01f);
        }

        b.Matrix = saved;
    }

    private static void WaterTower(CartoonKit.KitMesh b, Vector3 pos, float height)
    {
        b.Submesh = SubMetal;
        foreach (float x in new[] { -2f, 2f })
        foreach (float z in new[] { -2f, 2f })
            CartoonKit.AddBox(b, pos + new Vector3(x, height * 0.5f - 0.3f, z), new Vector3(0.28f, height, 0.28f), Quaternion.Euler(z * 1.2f, 0f, -x * 1.2f), 0.04f);
        b.Submesh = SubWood;
        CartoonKit.AddLathe(b, pos + Vector3.up * height, Quaternion.identity, new[] { 0f, 4.2f }, new[] { 3.1f, 3f }, 14);
        b.Submesh = SubRoof;
        CartoonKit.AddLathe(b, pos + Vector3.up * (height + 4.2f), Quaternion.identity, new[] { 0f, 2f }, new[] { 3.4f, 0.1f }, 14, false, false);
    }

    private static void BuildVistaForest(CartoonKit.KitMesh b)
    {
        System.Random rng = CartoonKit.Rng("Vista_Bosque");
        for (float z = 26f; z < 88f; z += 6f)
        for (float x = -70f; x < 44f; x += 6f)
        {
            float px = x + CartoonKit.Range(rng, -2.2f, 2.2f), pz = z + CartoonKit.Range(rng, -2.2f, 2.2f);
            if (pz < 60f && Mathf.Abs(px + 6.4f) < 6.5f) continue; // el sendero y sus bordes
            if (pz < 44f && px < -14f) continue;                   // las casas de la calle de atrás
            if (pz < 26f) continue;                                // detrás de la cerca del patio
            AddTree(b, rng, new Vector3(px, -0.35f, pz), CartoonKit.Range(rng, 6.5f, 11f), 3, 6, 7, 0, 1, 2);
        }
    }

    private static void BuildBackdrops(Transform vista, StringBuilder report)
    {
        Transform far = LevelBuilderUtils.CreateGroup("Fondos", vista);
        Vector3 center = new Vector3(0f, 0f, 5f); // el del anillo de NightSkyBuilder
        Texture2D city = BackdropTextureGenerator.Get(BackdropTextureGenerator.City, report);
        Texture2D hills = BackdropTextureGenerator.Get(BackdropTextureGenerator.Hills, report);
        Texture2D forest = BackdropTextureGenerator.Get(BackdropTextureGenerator.Forest, report);
        Texture2D houses = BackdropTextureGenerator.Get(BackdropTextureGenerator.Houses, report);
        Texture2D glow = BackdropTextureGenerator.Get(BackdropTextureGenerator.CampLight, report);

        PlaceCard("Tarjeta_Ciudad", city, new Vector3(0f, -1f, -84f), center, 176f, 40f, 0.3f, false, 0, far);
        PlaceCard("Tarjeta_Bosque", forest, new Vector3(-8f, -1f, 91f), center, 180f, 34f, 0.26f, false, 1, far);
        PlaceCard("Tarjeta_Colinas_Oeste", hills, new Vector3(-88f, -1f, 2f), center, 150f, 30f, 0.2f, false, 2, far);
        // La colina del libro: bien a la vista desde la calle (mirando al este) y desde el patio trasero
        PlaceCard("Tarjeta_Casas_Wilbur_Max", houses, new Vector3(105f, -1f, 22f), center, 92f, 42f, 0.1f, false, 3, far);
        // La luz cálida muy lejos, al final del sendero (el campamento, N1.4)
        PlaceCard("Tarjeta_Luz_Campamento", glow, new Vector3(-6f, 4f, 90f), center, 15f, 11f, 0f, true, 0, far);
    }

    // ==================================================================
    // Anclas, red de seguridad y luz
    // ==================================================================

    private static void PlaceAnchors(Transform markers)
    {
        foreach ((string name, Vector3 position, string id) in new[]
                 {
                     (LevelExitAnchor, LevelExitPosition, "nivel_porky_salida"),
                     (GateAnchor, ForestGatePosition, "porton_bosque"),
                 })
        {
            DestroyChild(markers, name);
            LevelBuilderUtils.CreateMarker(name, MarkerType.Anchor, id, position, 0f, markers);
        }
    }

    /// <summary>N1: red a ≥ 10 m detrás de los bordes (x ±44, z -17..58) + piso de abajo. Capa Ignore Raycast: no tapa balas ni vistas.</summary>
    private static void BuildSafetyCatch(Transform markers)
    {
        var go = new GameObject(SafetyRoot) { layer = 2 };
        Vector3 fallback = IntroTaxiBuilder.CurbPosition;
        foreach (LevelMarker marker in markers.GetComponentsInChildren<LevelMarker>(true))
            if (marker.Type == MarkerType.PlayerSpawn) fallback = marker.transform.position;
        go.AddComponent<SafetyCatchVolume>().Setup(fallback);

        const float wall = 4f, tall = 40f;
        var boxes = new (Vector3 center, Vector3 size)[]
        {
            (new Vector3(-60f, 15f, 20f), new Vector3(wall, tall, 120f)),
            (new Vector3(60f, 15f, 20f), new Vector3(wall, tall, 120f)),
            (new Vector3(0f, 15f, -36f), new Vector3(124f, tall, wall)),
            (new Vector3(0f, 15f, 74f), new Vector3(124f, tall, wall)),
            (new Vector3(0f, -8f, 20f), new Vector3(124f, wall, 120f)),
        };
        foreach ((Vector3 center, Vector3 size) in boxes)
        {
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = center;
            box.size = size;
        }
    }

    /// <summary>Antes el nivel se veía demasiado oscuro: un poco más de luna y la niebla empieza más lejos (el Volume global no se toca).</summary>
    private static void ApplyReadableLighting(StringBuilder report)
    {
        RenderSettings.fogStartDistance = 30f;
        RenderSettings.fogEndDistance = 190f;

        GameObject moon = GameObject.Find("/" + LightingRoot + "/Luz_Luna");
        if (moon != null && moon.TryGetComponent(out Light light)) light.intensity = 0.75f;
        else report.AppendLine("⚠ No encontré Luz_Luna: no se subió la luna.");
    }

    // ==================================================================
    // Comprobaciones (con el NavMesh horneado)
    // ==================================================================

    /// <summary>Todo lo nuevo se alcanza desde la calle y, del otro lado de cada borde, no hay camino (el mapa no tiene fugas).</summary>
    public static bool Validate(StringBuilder sb)
    {
        bool ok = true;
        Vector3 origin = V(0f, -15f);

        var inside = new (string label, Vector3 pos)[]
        {
            ("punta oeste", V(-41f, -14f)), ("punta este", V(40f, -14f)), ("patio vecino C", V(-37f, -7f)), ("patio vecino A", V(-21f, -8f)), ("patio vecino B", V(22f, -8.4f)),
            ("porche vecino B", V(23.4f, -6f)), ("callejón lateral", V(-11.8f, 2f)), ("lote vacío", V(36.5f, -5f)), ("detrás del cuarto", V(-6.4f, 23.5f)),
            ("acera del taxi", IntroTaxiBuilder.CurbPosition),
        };
        foreach ((string label, Vector3 pos) in inside)
        {
            if (Reachable(origin, pos)) continue;
            ok = false;
            sb.AppendLine($"✗ No se llega a {label} {pos}.");
        }

        var outside = new (string label, Vector3 pos)[]
        {
            ("detrás de la barricada", V(-47f, -14f)), ("detrás del auto chocado", V(47f, -14f)), ("sur de la calle, oeste", V(-32f, -19f)), ("sur de la calle, este", V(32f, -19f)),
            ("calle de atrás", V(-12f, 7f)), ("entre vecinos C y A", V(-29f, 1f)), ("detrás del lote", V(36f, 3f)), ("junto al vecino B", V(12f, -6f)), ("sendero del bosque", V(-6.4f, 30f)),
        };
        foreach ((string label, Vector3 pos) in outside)
        {
            if (!Reachable(origin, pos)) continue;
            ok = false;
            sb.AppendLine($"✗ Fuga: se llega a {label} {pos}. Hay un hueco en un borde.");
        }

        if (ok) sb.AppendLine($"✓ Mapa: {inside.Length} zonas alcanzables y {outside.Length} puntos del otro lado de los bordes sin camino.");
        return ok;
    }

    public static bool Reachable(Vector3 from, Vector3 to)
    {
        if (!NavMesh.SamplePosition(from, out NavMeshHit a, 1f, NavMesh.AllAreas)) return false;
        if (!NavMesh.SamplePosition(to, out NavMeshHit b, 1f, NavMesh.AllAreas)) return false;
        var path = new NavMeshPath();
        NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path);
        return path.status == NavMeshPathStatus.PathComplete;
    }
}
