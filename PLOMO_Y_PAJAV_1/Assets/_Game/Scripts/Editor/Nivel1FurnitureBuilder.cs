using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fase N1.2: los muebles que pide la distribución de objetos (regla N7) y la casa de Porky no tenía:
/// baño (no había), mostrador de cocina, mesita de noche con cajón abierto y mesa de sala con una lata abierta.
/// MENÚ: Plomo y Paja → Nivel 1 → Colocar muebles y objetos. También lo corren "Ampliar mapa" y "Generar Nivel Porky".
/// Idempotente: el grupo "N1_Muebles" se rehace. Los Spot son las superficies donde PickupBuilder pone cada objeto.
///
/// PLANO: baño x -8..-5.2, z 2..6 (la punta oeste de la cocina), puerta en z 3.4. Por eso el barril y la caja de ese rincón
/// pasaron al rincón este de la cocina. Nada queda en x -5..-2 (el paso sala → cocina → puerta trasera sigue libre).
/// </summary>
public static class Nivel1FurnitureBuilder
{
    private const string GroupName = "N1_Muebles";
    private const string LightsName = "N1_Muebles_Luces";
    private const float Floor = LevelBuilder_NivelPorky.FloorTop;
    private const float BathWallX = -5.2f;

    // Mostrador de la cocina, contra la pared del fondo
    private const float CounterHeight = 0.9f;
    private static readonly Vector3 CounterPosition = new Vector3(2f, Floor, 5.62f);
    private static readonly Vector3 CounterSize = new Vector3(2.2f, CounterHeight, 0.56f);

    // Mesita de noche: contra la pared este, al sur de la losa caída (debajo de la losa no se llega ni se ve)
    private static readonly Vector3 NightstandPosition = new Vector3(7.6f, Floor, -2.65f);
    private const float NightstandYaw = -90f; // el cajón sale hacia la habitación (-X)
    private const float DrawerFloorY = 0.35f, DrawerCenterZ = 0.34f;

    // Mesa de la sala, con la silla volcada al lado
    private static readonly Vector3 TablePosition = new Vector3(-1.2f, Floor, -3.5f);
    private const float TableTop = 0.78f;

    // Armario del baño, colgado en la pared del fondo
    private static readonly Vector3 CabinetPosition = new Vector3(-6.6f, 0f, 5.9f); // pegado a la pared (z = 6)
    private const float CabinetShelfY = 1.25f;

    /// <summary>Barril y caja que estaban donde ahora está el baño (LevelBuilder_NivelPorky los pone aquí).</summary>
    public static readonly Vector3 KitchenBarrelPosition = new Vector3(7.4f, Floor, 2.65f);
    public static readonly Vector3 KitchenCratePosition = new Vector3(6.55f, Floor, 2.6f);

    // Superficies de los objetos (PickupBuilder.NivelPorkySeed)
    public static readonly Vector3 CounterSpot = CounterPosition + new Vector3(-0.5f, CounterHeight, -0.02f);
    public static readonly Vector3 DrawerSpot = NightstandPosition + Quaternion.Euler(0f, NightstandYaw, 0f) * new Vector3(0f, DrawerFloorY, DrawerCenterZ);
    public static readonly Vector3 TinSpot = TablePosition + Vector3.up * TableTop;
    public static readonly Vector3 CabinetSpot = CabinetPosition + new Vector3(0f, CabinetShelfY, -0.16f);

    // ==================================================================
    // Menú
    // ==================================================================

    [MenuItem("Plomo y Paja/Nivel 1/Colocar muebles y objetos")]
    public static void BuildMenu()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        if (!File.Exists(LevelBuilder_NivelPorky.ScenePath))
        {
            EditorUtility.DisplayDialog("Colocar muebles y objetos", $"No existe {LevelBuilder_NivelPorky.ScenePath}. Primero: Plomo y Paja → Generar Nivel Porky.", "Entendido");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != LevelBuilder_NivelPorky.ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(LevelBuilder_NivelPorky.ScenePath, OpenSceneMode.Single);
        }

        GameObject environment = GameObject.Find("/--- ENVIRONMENT ---");
        GameObject lighting = GameObject.Find("/--- LIGHTING ---");
        if (environment == null || lighting == null)
        {
            EditorUtility.DisplayDialog("Colocar muebles y objetos", "La escena no tiene los grupos del nivel. Corre Plomo y Paja → Generar Nivel Porky.", "Entendido");
            return;
        }

        CartoonKit.BeginSession();
        var report = new StringBuilder();
        BuildInto(environment.transform, lighting.transform);
        PickupBuilder.BuildPrefabs(report);
        PickupBuilder.PlaceInOpenScene(report);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        LevelBuilder_NivelPorky.BakeNavMesh(); // los muebles nuevos cambian el NavMesh
        bool ok = PickupBuilder.ValidateSpots(report);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[Nivel 1] Muebles y objetos:\n" + report);
        EditorUtility.DisplayDialog("Colocar muebles y objetos", (ok ? "Listo. Escena guardada." : "Listo, pero hay comprobaciones que fallaron (ver la consola).") +
                                                                 "\nEl reporte completo está en la consola.", "Entendido");
    }

    // ==================================================================
    // Construcción
    // ==================================================================

    /// <summary>Arma los muebles en la escena abierta. No llama a BeginSession ni coloca los objetos.</summary>
    public static void BuildInto(Transform environment, Transform lighting)
    {
        DestroyChild(environment, GroupName);
        DestroyChild(lighting, LightsName);
        Transform group = LevelBuilderUtils.CreateGroup(GroupName, environment);
        Transform lights = LevelBuilderUtils.CreateGroup(LightsName, lighting);

        BuildBathroom(group, lights);
        BuildCounter(group);
        BuildNightstand(group);
        GameObject tin = BuildLivingTable(group);
        MoveKitchenProps(environment);

        LevelBuilderUtils.MarkStaticRecursive(group.gameObject);
        CartoonKit.ExcludeFromNavMesh(tin.transform);
    }

    private static void DestroyChild(Transform parent, string name)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            if (parent.GetChild(i).name == name) Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }

    /// <summary>Solo cambia algo en una escena de antes de N1.2.</summary>
    private static void MoveKitchenProps(Transform environment)
    {
        Transform furniture = environment.Find("Muebles");
        if (furniture == null) return;
        Transform barrel = furniture.Find("Barril_Cocina");
        if (barrel != null) barrel.position = KitchenBarrelPosition;
        Transform crate = furniture.Find("Caja_Cocina");
        if (crate != null) crate.position = KitchenCratePosition;
    }

    // ------------------------------ Baño ------------------------------

    private static void BuildBathroom(Transform parent, Transform lights)
    {
        Transform bath = LevelBuilderUtils.CreateGroup("Bano", parent);
        const CartoonKit.WallFace Wood = CartoonKit.WallFace.Wainscot;

        // Mismo material y altura que las paredes de la casa, puerta en z = 3.4
        Material straw = LevelBuilderUtils.GetOrCreateMaterial("Greybox_Paja", new Color32(0xE8, 0xC3, 0x5A, 0xFF), 0.1f, true);
        CartoonKit.Wall("Pared_Bano", new Vector3(BathWallX, 0f, 2f), new Vector3(BathWallX, 0f, 6f), LevelBuilder_NivelPorky.WallHeight, 0.2f, straw,
            Wood, Wood, bath, new LevelBuilderUtils.WallOpening(1.4f, 1.6f, 2.4f));

        // Armario abierto: la caja no tiene collider; solo la repisa, para que el objeto apoye y se vea desde adelante
        Mesh cabinet = CartoonKit.GetMesh("Bano_Armario", b =>
        {
            const float w = 0.72f, h = 0.86f, d = 0.3f;
            System.Random rng = CartoonKit.Rng("Bano_Armario");
            b.Submesh = 0;
            CartoonKit.AddBox(b, new Vector3(0f, h * 0.5f, -0.012f), new Vector3(w, h, 0.024f), Quaternion.identity, 0.006f);          // fondo
            foreach (float y in new[] { 0f, h })
                CartoonKit.AddBox(b, new Vector3(0f, y, -d * 0.5f), new Vector3(w, 0.03f, d), Quaternion.identity, 0.008f);              // piso y tapa
            foreach (float x in new[] { -w * 0.5f, w * 0.5f })
                CartoonKit.AddBox(b, new Vector3(x, h * 0.5f, -d * 0.5f), new Vector3(0.03f, h, d), Quaternion.identity, 0.008f);        // lados
            b.Submesh = 1; // puerta abierta, colgando de una bisagra
            CartoonKit.AddBox(b, new Vector3(w * 0.5f + 0.2f, h * 0.5f, -d - 0.14f), new Vector3(0.03f, h - 0.04f, w * 0.72f),
                Quaternion.Euler(CartoonKit.Range(rng, -2f, 2f), 25f, 0f), 0.01f);
        });
        CartoonKit.MeshObject("Armario_Bano", cabinet, new[] { CartoonKit.M("Yeso"), CartoonKit.M("MaderaClara") }, bath,
            CabinetPosition + Vector3.up * (CabinetShelfY - 0.02f), Quaternion.identity);
        var shelf = new GameObject("Armario_Bano_Repisa");
        shelf.transform.SetParent(bath, false);
        shelf.transform.position = CabinetPosition + new Vector3(0f, CabinetShelfY - 0.015f, -0.15f);
        shelf.AddComponent<BoxCollider>().size = new Vector3(0.7f, 0.03f, 0.3f);

        // Lavabo debajo del armario y bañera de patas contra la pared oeste
        Mesh sink = CartoonKit.GetMesh("Bano_Lavabo", b =>
        {
            CartoonKit.AddLathe(b, Vector3.zero, Quaternion.identity, new[] { 0f, 0.06f, 0.62f }, new[] { 0.17f, 0.11f, 0.09f }, 12, false, false);
            CartoonKit.AddLathe(b, new Vector3(0f, 0.62f, 0f), Quaternion.identity, new[] { 0f, 0.12f, 0.2f }, new[] { 0.14f, 0.3f, 0.33f }, 14, true, true);
        });
        CartoonKit.FitCollider(CartoonKit.MeshObject("Lavabo", sink, new[] { CartoonKit.M("Plato") }, bath, new Vector3(CabinetPosition.x, Floor, 5.55f), Quaternion.identity));

        Mesh tub = CartoonKit.GetMesh("Bano_Banera", b =>
        {
            b.Submesh = 0;
            CartoonKit.AddBox(b, new Vector3(0f, 0.42f, 0f), new Vector3(0.78f, 0.5f, 1.65f), Quaternion.identity, 0.16f, 2);
            b.Submesh = 1; // agua sucia, y patas de metal
            CartoonKit.AddBox(b, new Vector3(0f, 0.665f, 0f), new Vector3(0.58f, 0.02f, 1.42f), Quaternion.identity, 0.008f);
            b.Submesh = 2;
            foreach (float x in new[] { -0.3f, 0.3f })
            foreach (float z in new[] { -0.65f, 0.65f })
                CartoonKit.AddLathe(b, new Vector3(x, 0f, z), Quaternion.identity, new[] { 0f, 0.2f }, new[] { 0.05f, 0.08f }, 8);
        });
        CartoonKit.FitCollider(CartoonKit.MeshObject("Banera", tub, new[] { CartoonKit.M("Plato"), CartoonKit.M("Charco"), CartoonKit.M("Metal") }, bath,
            new Vector3(-7.45f, Floor, 3.1f), Quaternion.identity));

        // Bombilla propia (las paredes nuevas le tapan la de la cocina), sin sombras
        Light bulb = CartoonKit.BareBulb("Bombilla_Bano", new Vector3(-6.6f, LevelBuilder_NivelPorky.WallHeight, 4.2f), 0.65f, bath, lights,
            new Color32(0xFF, 0xB8, 0x70, 0xFF), 1.5f, 5f);
        bulb.shadows = LightShadows.None;
    }

    // ------------------------------ Mostrador de la cocina ------------------------------

    private static void BuildCounter(Transform parent)
    {
        Mesh mesh = CartoonKit.GetMesh("Cocina_Mostrador", b =>
        {
            System.Random rng = CartoonKit.Rng("Cocina_Mostrador");
            float w = CounterSize.x, d = CounterSize.z;
            b.Submesh = 0;
            CartoonKit.AddBox(b, new Vector3(0f, (CounterHeight - 0.05f) * 0.5f, 0f), new Vector3(w - 0.06f, CounterHeight - 0.05f, d - 0.06f), Quaternion.identity, 0.03f);
            b.Submesh = 1; // tapa
            CartoonKit.AddBox(b, new Vector3(0f, CounterHeight - 0.025f, 0f), new Vector3(w, 0.05f, d), Quaternion.Euler(0f, 0f, 0.4f), 0.015f);
            b.Submesh = 2; // puertitas, una entreabierta
            for (int i = 0; i < 3; i++)
            {
                float x = -w * 0.33f + i * w * 0.33f;
                CartoonKit.AddBox(b, new Vector3(x, 0.44f, -d * 0.5f - 0.005f), new Vector3(w * 0.3f, 0.7f, 0.025f),
                    Quaternion.Euler(0f, i == 2 ? 24f : CartoonKit.Range(rng, -1f, 1f), 0f), 0.008f);
            }
            b.Submesh = 3; // platos sucios al otro extremo de los cartuchos
            foreach (float y in new[] { 0f, 0.025f, 0.05f })
                CartoonKit.AddLathe(b, new Vector3(0.6f, CounterHeight + y, 0.02f), Quaternion.identity, new[] { 0f, 0.022f }, new[] { 0.09f, 0.13f }, 12, true, true);
        });
        // Los platos no cuentan para la caja: la tapa es la superficie
        GameObject counter = CartoonKit.MeshObject("Mostrador_Cocina", mesh, new[] { CartoonKit.M("Madera"), CartoonKit.M("MaderaClara"), CartoonKit.M("MaderaOscura"), CartoonKit.M("Plato") },
            parent, CounterPosition, Quaternion.identity);
        BoxCollider box = counter.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, CounterHeight * 0.5f, 0f);
        box.size = CounterSize;
    }

    // ------------------------------ Mesita de noche con el cajón abierto ------------------------------

    private static void BuildNightstand(Transform parent)
    {
        const float w = 0.5f, h = 0.62f, d = 0.42f, drawerW = 0.44f, drawerLen = 0.3f;
        Mesh mesh = CartoonKit.GetMesh("Dormitorio_Mesita", b =>
        {
            b.Submesh = 0;
            CartoonKit.AddBox(b, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), Quaternion.identity, 0.02f);
            b.Submesh = 1; // cajón sacado: fondo, lados, frente y pomo
            CartoonKit.AddBox(b, new Vector3(0f, DrawerFloorY - 0.01f, DrawerCenterZ), new Vector3(drawerW, 0.02f, drawerLen), Quaternion.identity, 0.005f);
            foreach (float x in new[] { -drawerW * 0.5f, drawerW * 0.5f })
                CartoonKit.AddBox(b, new Vector3(x, DrawerFloorY + 0.05f, DrawerCenterZ), new Vector3(0.02f, 0.1f, drawerLen), Quaternion.identity, 0.005f);
            CartoonKit.AddBox(b, new Vector3(0f, DrawerFloorY + 0.06f, DrawerCenterZ + drawerLen * 0.5f), new Vector3(drawerW + 0.04f, 0.15f, 0.025f), Quaternion.identity, 0.008f);
            b.Submesh = 2;
            CartoonKit.AddLathe(b, new Vector3(0f, DrawerFloorY + 0.06f, DrawerCenterZ + drawerLen * 0.5f + 0.012f), Quaternion.Euler(90f, 0f, 0f), new[] { 0f, 0.02f, 0.04f }, new[] { 0.015f, 0.03f, 0f }, 8);
        });
        GameObject stand = CartoonKit.MeshObject("Mesita_Noche", mesh, new[] { CartoonKit.M("Madera"), CartoonKit.M("MaderaClara"), CartoonKit.M("Laton") }, parent,
            NightstandPosition, Quaternion.Euler(0f, NightstandYaw, 0f));

        // Dos cajas: el cuerpo y el fondo del cajón. El rayo de PickupSpot apoya en el fondo del cajón, no en la tapa
        BoxCollider body = stand.AddComponent<BoxCollider>();
        body.center = new Vector3(0f, h * 0.5f, 0f);
        body.size = new Vector3(w, h, d);
        BoxCollider drawer = stand.AddComponent<BoxCollider>();
        drawer.center = new Vector3(0f, DrawerFloorY - 0.01f, DrawerCenterZ);
        drawer.size = new Vector3(drawerW, 0.02f, drawerLen);
    }

    // ------------------------------ Mesa de la sala con la lata ------------------------------

    /// <summary>Devuelve la lata: no tiene collider y no cuenta para el NavMesh.</summary>
    private static GameObject BuildLivingTable(Transform parent)
    {
        CartoonKit.RoundTable("Mesa_Sala", TablePosition, parent, 0.42f);

        Mesh mesh = CartoonKit.GetMesh("Sala_Lata", b =>
        {
            b.Submesh = 0; // lata abierta (por dentro también se ve)
            b.Lathe(new[] { 0f, 0.075f }, new[] { 0.15f, 0.15f }, 16, true, false);
            b.Lathe(new[] { 0f, 0.075f }, new[] { 0.14f, 0.14f }, 16, false, false, true);
            b.Submesh = 1; // la tapa, apoyada al lado
            CartoonKit.AddLathe(b, new Vector3(0.22f, 0.02f, 0.14f), Quaternion.Euler(8f, 0f, 14f), new[] { 0f, 0.012f }, new[] { 0.155f, 0.155f }, 16, true, true);
        });
        return CartoonKit.MeshObject("Lata_Sala", mesh, new[] { CartoonKit.M("Metal"), CartoonKit.M("Laton") }, parent, TinSpot, Quaternion.identity);
    }
}
