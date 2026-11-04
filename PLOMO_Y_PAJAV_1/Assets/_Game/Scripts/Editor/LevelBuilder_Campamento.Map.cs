using System;
using UnityEditor;
using UnityEngine;
using KitMesh = CartoonKit.KitMesh;

/// <summary>Campamento: suelo, caminos y zonas Z0–Z3, con sus muebles. Ver el plano en LevelBuilder_Campamento.cs.</summary>
public static partial class LevelBuilder_Campamento
{
    private static Material _forestFloor, _campFloor, _rock, _river, _shine, _canvas, _felt, _fire, _truck;

    private const float TableTopBarracks = 0.8f;

    /// <summary>Tapas y techos: el horneado no debe caminar encima. Generate() les quita el flag después de MarkStaticRecursive.</summary>
    private static readonly System.Collections.Generic.List<Transform> NotWalkable = new System.Collections.Generic.List<Transform>();

    private static void LoadMaterials()
    {
        _forestFloor = LevelBuilderUtils.GetOrCreateMaterial("Camp_SueloBosque", new Color32(0x3F, 0x5C, 0x38, 0xFF), 0.1f, true);
        _campFloor = LevelBuilderUtils.GetOrCreateMaterial("Camp_SueloTierra", new Color32(0x6E, 0x55, 0x3B, 0xFF), 0.1f, true);
        _rock = LevelBuilderUtils.GetOrCreateMaterial("Camp_Roca", new Color32(0x4A, 0x44, 0x4E, 0xFF), 0.1f, true);
        _river = LevelBuilderUtils.GetOrCreateMaterial("Camp_Rio", new Color32(0x2F, 0x55, 0x8C, 0xFF), 0.6f, true);
        _shine = LevelBuilderUtils.GetOrCreateEmissiveMaterial("Camp_RioBrillo", new Color32(0x40, 0x38, 0x30, 0xFF), new Color(0.75f, 0.45f, 0.2f));
        _canvas = LevelBuilderUtils.GetOrCreateMaterial("Camp_Lona", new Color32(0xB9, 0xA8, 0x7E, 0xFF), 0.05f, true);
        _felt = LevelBuilderUtils.GetOrCreateMaterial("Camp_PanoBillar", new Color32(0x1F, 0x6B, 0x45, 0xFF), 0.05f, true);
        _fire = LevelBuilderUtils.GetOrCreateEmissiveMaterial("Camp_Fuego", new Color32(0xFF, 0x8A, 0x2A, 0xFF), new Color(3.4f, 1.5f, 0.4f));
        _truck = LevelBuilderUtils.GetOrCreateMaterial("Camp_Camioneta", new Color32(0x7A, 0x3A, 0x2C, 0xFF), 0.3f, true);
    }

    // ============ Ayudas de geometría ============

    private static Material K(string name) => CartoonKit.M(name);
    private static string Cm(float v) => CartoonKit.Cm(v);

    private static GameObject MeshGo(string name, string key, Action<KitMesh> build, Transform parent, Vector3 position, float yaw,
        params Material[] materials)
    {
        Mesh mesh = CartoonKit.GetMesh(key, build);
        return CartoonKit.MeshObject(name, mesh, materials, parent, position, Quaternion.Euler(0f, yaw, 0f));
    }

    private static BoxCollider Hug(GameObject go, Vector3 center, Vector3 size)
    {
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.center = center;
        box.size = size;
        return box;
    }

    /// <summary>Rectángulo de suelo con la cara de arriba en topY, en baldosas: las luces de URP se reparten por objeto.</summary>
    private static void GroundRect(string name, float x0, float z0, float x1, float z1, float topY, Material mat, Transform parent,
        bool collider, float tile = 12f)
    {
        const float thickness = 1.5f;
        int nx = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / tile)), nz = Mathf.Max(1, Mathf.RoundToInt((z1 - z0) / tile));
        float w = (x1 - x0) / nx, d = (z1 - z0) / nz;
        for (int i = 0; i < nx; i++)
        for (int j = 0; j < nz; j++)
        {
            var center = new Vector3(x0 + w * (i + 0.5f), topY - thickness * 0.5f, z0 + d * (j + 0.5f));
            CartoonKit.Box($"{name}_{i}_{j}", center, new Vector3(w, thickness, d), mat, parent, default, 0.01f, collider);
        }
    }

    /// <summary>Tronco caído o apilado, acostado sobre el eje X local.</summary>
    private static void AddLog(KitMesh b, Vector3 center, float length, float radius, float yaw = 0f)
    {
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, -90f);
        Vector3 start = center - Quaternion.Euler(0f, yaw, 0f) * Vector3.right * (length * 0.5f);
        CartoonKit.AddLathe(b, start, rot, new[] { 0f, length }, new[] { radius, radius * 0.92f }, 7, true, true);
    }

    // ============ Muebles (las alturas son exactas: las pistas y los objetos se apoyan en ellas) ============

    private static GameObject Table(string name, Vector3 ground, float w, float d, float h, float yaw, Transform parent, string top = "Madera")
    {
        string key = $"Camp_Mesa_{Cm(w)}x{Cm(d)}x{Cm(h)}";
        GameObject go = MeshGo(name, key, b =>
        {
            b.Submesh = 0;
            CartoonKit.AddBox(b, new Vector3(0f, h - 0.03f, 0f), new Vector3(w, 0.06f, d), Quaternion.identity, 0.015f);
            b.Submesh = 1;
            foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
                CartoonKit.AddBox(b, new Vector3(sx * (w * 0.5f - 0.07f), (h - 0.06f) * 0.5f, sz * (d * 0.5f - 0.07f)),
                    new Vector3(0.08f, h - 0.06f, 0.08f), Quaternion.identity, 0.015f);
        }, parent, ground, yaw, K(top), K("MaderaOscura"));
        Hug(go, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d));
        return go;
    }

    /// <summary>Mueble macizo (escritorio): bloquea balas a los dos lados.</summary>
    private static GameObject SolidDesk(string name, Vector3 ground, float w, float d, float h, float yaw, Transform parent)
    {
        string key = $"Camp_Escritorio_{Cm(w)}x{Cm(d)}x{Cm(h)}";
        GameObject go = MeshGo(name, key, b =>
        {
            b.Submesh = 0;
            CartoonKit.AddBox(b, new Vector3(0f, h - 0.04f, 0f), new Vector3(w + 0.12f, 0.08f, d + 0.12f), Quaternion.identity, 0.02f);
            b.Submesh = 1;
            CartoonKit.AddBox(b, new Vector3(0f, (h - 0.08f) * 0.5f, 0f), new Vector3(w, h - 0.08f, d), Quaternion.identity, 0.03f);
            // Cajones al frente y al fondo
            b.Submesh = 2;
            foreach (int side in new[] { -1, 1 })
            foreach (float x in new[] { -w * 0.32f, 0f, w * 0.32f })
                CartoonKit.AddBox(b, new Vector3(x, h * 0.48f, side * (d * 0.5f + 0.01f)), new Vector3(w * 0.26f, h * 0.55f, 0.02f), Quaternion.identity, 0.006f);
        }, parent, ground, yaw, K("MaderaClara"), K("MaderaOscura"), K("Madera"));
        Hug(go, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d));
        return go;
    }

    /// <summary>Estante abierto por delante (+Z local). Cada tabla tiene collider: los objetos se apoyan en la repisa.</summary>
    private static GameObject Cabinet(string name, Vector3 ground, float w, float d, float h, float yaw, Transform parent, params float[] shelves)
    {
        const float board = 0.05f;
        string key = $"Camp_Estante_{Cm(w)}x{Cm(d)}x{Cm(h)}_{shelves.Length}";
        GameObject go = MeshGo(name, key, b =>
        {
            b.Submesh = 0;
            CartoonKit.AddBox(b, new Vector3(-w * 0.5f + board * 0.5f, h * 0.5f, 0f), new Vector3(board, h, d), Quaternion.identity, 0.01f);
            CartoonKit.AddBox(b, new Vector3(w * 0.5f - board * 0.5f, h * 0.5f, 0f), new Vector3(board, h, d), Quaternion.identity, 0.01f);
            CartoonKit.AddBox(b, new Vector3(0f, h - board * 0.5f, 0f), new Vector3(w, board, d), Quaternion.identity, 0.01f);
            CartoonKit.AddBox(b, new Vector3(0f, h * 0.5f, -d * 0.5f + board * 0.5f), new Vector3(w, h, board), Quaternion.identity, 0.01f);
            b.Submesh = 1;
            foreach (float y in shelves)
                CartoonKit.AddBox(b, new Vector3(0f, y - board * 0.5f, 0f), new Vector3(w - board * 2f, board, d - board), Quaternion.identity, 0.01f);
        }, parent, ground, yaw, K("MaderaOscura"), K("Madera"));

        Hug(go, new Vector3(-w * 0.5f + board * 0.5f, h * 0.5f, 0f), new Vector3(board, h, d));
        Hug(go, new Vector3(w * 0.5f - board * 0.5f, h * 0.5f, 0f), new Vector3(board, h, d));
        Hug(go, new Vector3(0f, h - board * 0.5f, 0f), new Vector3(w, board, d));
        Hug(go, new Vector3(0f, h * 0.5f, -d * 0.5f + board * 0.5f), new Vector3(w, h, board));
        foreach (float y in shelves)
            Hug(go, new Vector3(0f, y - board * 0.5f, 0f), new Vector3(w - board * 2f, board, d - board));
        return go;
    }

    /// <summary>Cajón de madera abierto, con su fondo y sus cuatro paredes (collider por tabla). Un objeto cabe adentro.</summary>
    private static GameObject OpenCrate(string name, Vector3 ground, float size, float yaw, Transform parent)
    {
        const float h = 0.42f, t = 0.05f;
        string key = "Camp_CajonAbierto_" + Cm(size);
        GameObject go = MeshGo(name, key, b =>
        {
            b.Submesh = 0;
            CartoonKit.AddBox(b, new Vector3(0f, 0.04f, 0f), new Vector3(size, 0.08f, size), Quaternion.identity, 0.01f);
            foreach (int s in new[] { -1, 1 })
            {
                CartoonKit.AddBox(b, new Vector3(s * (size - t) * 0.5f, h * 0.5f, 0f), new Vector3(t, h, size), Quaternion.identity, 0.01f);
                CartoonKit.AddBox(b, new Vector3(0f, h * 0.5f, s * (size - t) * 0.5f), new Vector3(size, h, t), Quaternion.identity, 0.01f);
            }
        }, parent, ground, yaw, K("MaderaClara"));

        Hug(go, new Vector3(0f, 0.04f, 0f), new Vector3(size, 0.08f, size));
        foreach (int s in new[] { -1, 1 })
        {
            Hug(go, new Vector3(s * (size - t) * 0.5f, h * 0.5f, 0f), new Vector3(t, h, size));
            Hug(go, new Vector3(0f, h * 0.5f, s * (size - t) * 0.5f), new Vector3(size, h, t));
        }
        return go;
    }

    /// <summary>Pila de troncos para cubrirse (a lo largo de X local).</summary>
    private static GameObject LogPile(string name, Vector3 ground, float length, int rows, float yaw, Transform parent)
    {
        const float r = 0.24f;
        string key = $"Camp_Troncos_{Cm(length)}_{rows}";
        float width = rows * r * 2f, height = rows * r * 1.75f + r * 0.25f;
        GameObject go = MeshGo(name, key, b =>
        {
            for (int row = 0; row < rows; row++)
            {
                int count = rows - row;
                for (int i = 0; i < count; i++)
                {
                    float z = (i - (count - 1) * 0.5f) * r * 2f;
                    AddLog(b, new Vector3(0f, r + row * r * 1.75f, z), length, r);
                }
            }
        }, parent, ground, yaw, K("Madera"));
        Hug(go, new Vector3(0f, height * 0.5f, 0f), new Vector3(length, height, width));
        return go;
    }

    /// <summary>Techo a dos aguas, solo dibujo: el collider del techo es el cielorraso. center = centro a la altura del muro.</summary>
    private static void PitchedRoof(string name, Vector3 center, float w, float d, float rise, Transform parent)
    {
        string key = $"Camp_Techo_{Cm(w)}x{Cm(d)}x{Cm(rise)}";
        MeshGo(name, key, b =>
        {
            float half = d * 0.5f, slope = Mathf.Sqrt(half * half + rise * rise);
            float angle = Mathf.Atan2(rise, half) * Mathf.Rad2Deg;
            b.Submesh = 0;
            foreach (int s in new[] { -1, 1 })
                CartoonKit.AddBox(b, new Vector3(0f, rise * 0.5f, s * half * 0.5f), new Vector3(w + 0.7f, 0.14f, slope + 0.5f),
                    Quaternion.Euler(s * angle, 0f, 0f), 0.03f);
            b.Submesh = 1;
            foreach (int s in new[] { -1, 1 })
            {
                float x = s * w * 0.5f;
                b.Card(new[] { new Vector3(x, 0f, -half), new Vector3(x, 0f, half), new Vector3(x, rise, 0f) }, new Vector3(s, 0f, 0f));
            }
        }, parent, center, 0f, K("TechoEdificio"), K("MaderaOscura"));
    }

    private static void Ceiling(string name, Vector3 center, Vector2 size, Transform parent)
    {
        // Collider de techo: apaga la lluvia y los pasos de barro (RainEmitter). Se ve desde adentro
        GameObject go = CartoonKit.Box(name, center, new Vector3(size.x, 0.2f, size.y), K("Cielorraso"), parent);
        NotWalkable.Add(go.transform); // se aplica después de marcar todo como estático
    }

    // ============ Suelo y caminos ============

    private static void BuildGround(Transform env, Transform vista)
    {
        Transform walk = LevelBuilderUtils.CreateGroup("Suelo", env);
        GroundRect("Suelo_Sendero", -7f, -52f, 7f, -10f, 0f, _forestFloor, walk, true);
        GroundRect("Suelo_Porton", -11f, -10f, 11f, 5f, 0f, _campFloor, walk, true);
        GroundRect("Suelo_Campamento", -19.5f, 5f, 19.8f, 50f, 0f, _campFloor, walk, true);
        GroundRect("Suelo_Guarida", -13f, 50f, 13f, 80f, 0f, _campFloor, walk, true);

        // Más allá de los bordes: se ve, sin collider y fuera del NavMesh
        Transform far = LevelBuilderUtils.CreateGroup("Suelo_Vista", vista);
        GroundRect("Bosque_Oeste", -60f, -62f, -7f, -10f, 0f, _forestFloor, far, false, 16f);
        GroundRect("Bosque_Este", 7f, -62f, 19.8f, -10f, 0f, _forestFloor, far, false, 16f);
        GroundRect("Fuera_Oeste", -19.5f, -10f, -11f, 5f, 0f, _forestFloor, far, false, 16f);
        GroundRect("Fuera_Este", 11f, -10f, 19.8f, 5f, 0f, _forestFloor, far, false, 16f);
        GroundRect("Fuera_LejosOeste", -60f, -10f, -34f, 96f, 0f, _forestFloor, far, false, 16f);
        GroundRect("Fuera_NO", -34f, 56f, -13f, 96f, 0f, _forestFloor, far, false, 16f);
        GroundRect("Fuera_NO2", -19.5f, 50f, -13f, 56f, 0f, _forestFloor, far, false, 16f);
        GroundRect("Fuera_Norte", -13f, 80f, 13f, 96f, 0f, _forestFloor, far, false, 16f);
        GroundRect("Fuera_NE", 13f, 50f, 19.8f, 96f, 0f, _forestFloor, far, false, 16f);
    }

    private static void BuildPaths(Transform deco)
    {
        CartoonKit.DirtPath("Camp_Camino_Central", new[]
        {
            new Vector3(0f, 0f, -50f), new Vector3(-1f, 0f, -40f), new Vector3(1f, 0f, -30f), new Vector3(-0.5f, 0f, -20f),
            new Vector3(0f, 0f, -10f), new Vector3(0f, 0f, 5f), new Vector3(-3f, 0f, 12f), new Vector3(-3f, 0f, 24f),
            new Vector3(0f, 0f, 31f), new Vector3(0f, 0f, 53.9f),
        }, 3.4f, deco);
    }

    // ============ Z0: el bosque ============

    private static void BuildForest(Transform env, Transform lighting, Transform deco)
    {
        Transform z0 = LevelBuilderUtils.CreateGroup("Z0_Bosque", env);

        // Faroles de los hombres de Lobo: marcan el sendero sin aclarar el bosque
        CartoonKit.StreetLamp("Farol_Sendero_A", new Vector3(-3.7f, 0f, -38f), 90f, z0, lighting, WarmLamp, 2.2f, 12f);
        CartoonKit.StreetLamp("Farol_Sendero_B", new Vector3(3.7f, 0f, -24f), -90f, z0, lighting, WarmLamp, 2.2f, 12f);

        // Tronco caído (se rodea) y piedras
        string logKey = "Camp_TroncoCaido";
        GameObject log = MeshGo("Tronco_Caido", logKey, b => AddLog(b, new Vector3(0f, 0.28f, 0f), 4.2f, 0.3f), z0, new Vector3(-2.4f, 0f, -29f), 25f, K("MaderaOscura"));
        Hug(log, new Vector3(0f, 0.3f, 0f), new Vector3(4.2f, 0.6f, 0.6f));
        CartoonKit.Rubble("Piedra_Sendero_A", new Vector3(3.2f, 0f, -44f), 0.8f, 20f, _rock, z0, 11);
        CartoonKit.Rubble("Piedra_Sendero_B", new Vector3(-3.4f, 0f, -17f), 0.7f, 70f, _rock, z0, 12);
    }

    // ============ Z1: portón del aserradero ============

    private static void BuildGate(Transform env, Transform lighting, Transform deco)
    {
        Transform z1 = LevelBuilderUtils.CreateGroup("Z1_Porton", env);
        const float gateZ = -10f, postX = 2.2f;

        // Marco de portón: postes, dintel y dos hojas de tablas abiertas hacia adentro (se ve el campamento al fondo)
        foreach (int s in new[] { -1, 1 })
        {
            CartoonKit.Box("Porton_Poste_" + s, new Vector3(s * postX, 2f, gateZ), new Vector3(0.4f, 4f, 0.4f), K("MaderaOscura"), z1);
            CartoonKit.Box("Porton_Hoja_" + s, new Vector3(s * (postX + 0.1f), 1.2f, gateZ + 1.1f), new Vector3(0.12f, 2.4f, 2.1f), K("Madera"), z1);
            foreach (float y in new[] { 0.5f, 1.2f, 1.9f })
                CartoonKit.Box($"Porton_Travesano_{s}_{y:0.0}", new Vector3(s * (postX + 0.02f), y, gateZ + 1.1f), new Vector3(0.05f, 0.12f, 2.1f),
                    K("MaderaOscura"), deco, default, 0.01f, false);
        }
        CartoonKit.Box("Porton_Dintel", new Vector3(0f, 3.95f, gateZ), new Vector3(5.2f, 0.34f, 0.4f), K("MaderaOscura"), z1);

        // Dos faroles
        CartoonKit.StreetLamp("Farol_Porton_O", new Vector3(-3.3f, 0f, gateZ - 0.8f), 90f, z1, lighting, WarmLamp, 2.8f, 13f);
        CartoonKit.StreetLamp("Farol_Porton_E", new Vector3(3.3f, 0f, gateZ - 0.8f), -90f, z1, lighting, WarmLamp, 2.8f, 13f);
        CartoonKit.StreetLamp("Farol_Patio_Porton", new Vector3(-8.8f, 0f, 3.6f), 90f, z1, lighting, WarmLamp, 2.4f, 13f);

        BuildTruck(z1, new Vector3(7.2f, 0f, -2.6f));

        // Cajas: rompen el largo de la cerca y dan cubierta
        CartoonKit.Crate("Caja_Porton_A", new Vector3(-8.7f, 0f, -7.6f), 10f, z1, 1.1f);
        CartoonKit.Crate("Caja_Porton_B", new Vector3(-8.8f, 0f, -6.3f), -15f, z1, 0.9f);
        CartoonKit.Crate("Caja_Porton_C", new Vector3(-8.7f, 0f, 0.2f), 25f, z1, 1.1f);
        CartoonKit.Barrel("Barril_Porton_A", new Vector3(8.8f, 0f, 3.4f), 0f, z1, 1);
        CartoonKit.Barrel("Barril_Porton_B", new Vector3(8.7f, 0f, -7.8f), 40f, z1, 2);
    }

    private static void BuildTruck(Transform parent, Vector3 ground)
    {
        Vector3 size = new Vector3(2.1f, 2.2f, 5.2f);
        GameObject go = MeshGo("Camioneta_Vieja", "Camp_Camioneta", b =>
        {
            b.Submesh = 0;
            CartoonKit.AddBox(b, new Vector3(0f, 0.85f, 0.2f), new Vector3(2f, 0.5f, 4.8f), Quaternion.identity, 0.12f);   // chasis
            CartoonKit.AddBox(b, new Vector3(0f, 1.6f, 1.2f), new Vector3(1.8f, 1.1f, 1.5f), Quaternion.identity, 0.2f);    // cabina
            CartoonKit.AddBox(b, new Vector3(0f, 1.05f, 2.35f), new Vector3(1.6f, 0.5f, 1f), Quaternion.Euler(6f, 0f, 0f), 0.16f); // capó
            b.Submesh = 1;
            CartoonKit.AddBox(b, new Vector3(0f, 1.35f, -1.2f), new Vector3(1.95f, 0.5f, 2.3f), Quaternion.identity, 0.03f); // baranda de tablas
            b.Submesh = 2;
            CartoonKit.AddBox(b, new Vector3(0f, 1.75f, 1.97f), new Vector3(1.5f, 0.55f, 0.04f), Quaternion.identity, 0.02f); // parabrisas
            b.Submesh = 3;
            foreach (int sx in new[] { -1, 1 })
            foreach (float z in new[] { -1.4f, 1.9f })
                CartoonKit.AddLathe(b, new Vector3(sx * 0.82f, 0.42f, z), Quaternion.Euler(0f, 0f, 90f),
                    new[] { -0.17f, 0.17f }, new[] { 0.42f, 0.42f }, 10, true, true);
        }, parent, ground, 0f, _truck, K("Madera"), K("Ventana"), K("Metal"));
        Hug(go, new Vector3(0f, size.y * 0.5f, 0.2f), size);
    }

    // ============ Z2: barracones ============

    private static void BuildBarracks(Transform env, Transform lighting, Transform deco)
    {
        Transform z2 = LevelBuilderUtils.CreateGroup("Z2_Barracones", env);

        Barrack("Barracon_A", -16.5f, 20f, -7.5f, 26f, true, -12f, z2, lighting, deco);
        Barrack("Barracon_B", 7.5f, 21f, 16.5f, 27f, false, 12f, z2, lighting, deco);
        Barrack("Barracon_C", -16.5f, 7f, -7.5f, 12f, false, -12f, z2, lighting, deco);

        // Adentro del barracón A: la mesa de la foto, literas y la repisa con cartuchos
        Table("Mesa_Foto", new Vector3(-12f, 0f, 23.2f), 1.5f, 0.9f, TableTopBarracks, 0f, z2);
        CartoonKit.Chair("Silla_Foto", new Vector3(-13.4f, 0f, 23.1f), 95f, z2);
        Cabinet("Repisa_Barracon", new Vector3(-9.4f, 0f, 25.55f), 1.4f, 0.5f, 1.9f, 180f, z2, 0.45f, 1.05f);
        foreach (float z in new[] { 21.2f, 24.6f })
            CartoonKit.Box("Litera_" + z, new Vector3(-15.75f, 0.45f, z), new Vector3(1.2f, 0.9f, 2.1f), K("Colchon"), z2);

        Tent("Carpa_A", new Vector3(13.5f, 0f, 12f), 0f, z2, lighting);
        OpenCrate("Cajon_Carpa", new Vector3(13.5f, 0f, 9.3f), 0.7f, 8f, z2);
        Tent("Carpa_B", new Vector3(10.6f, 0f, 17.4f), 90f, z2, lighting);
        Tent("Carpa_C", new Vector3(-11.5f, 0f, 16.4f), 90f, z2, lighting);

        BuildCampfire(z2, lighting);

        // Gramófono sobre su mesa (de aquí sale amb_campamento, 3D)
        Table("Mesa_Gramofono", new Vector3(4f, 0f, 20f), 0.9f, 0.9f, 0.8f, 0f, z2);
        BuildGramophone(deco, new Vector3(4f, 0.8f, 20f));

        // La mesa del Contable, con sus libros
        Table("Mesa_Contable", new Vector3(-4.2f, 0f, 22.4f), 1.5f, 0.8f, 0.8f, 0f, z2);
        MeshGo("Libros_Contable", "Camp_Libros", b =>
        {
            b.Submesh = 0;
            CartoonKit.AddBox(b, new Vector3(-0.35f, 0.03f, 0f), new Vector3(0.3f, 0.06f, 0.4f), Quaternion.Euler(0f, 12f, 0f), 0.01f);
            CartoonKit.AddBox(b, new Vector3(-0.32f, 0.09f, 0.01f), new Vector3(0.28f, 0.05f, 0.38f), Quaternion.Euler(0f, -8f, 0f), 0.01f);
            b.Submesh = 1;
            CartoonKit.AddBox(b, new Vector3(0.25f, 0.01f, 0f), new Vector3(0.42f, 0.02f, 0.3f), Quaternion.Euler(0f, 18f, 0f), 0.004f);
        }, deco, new Vector3(-4.2f, 0.8f, 22.4f), 0f, K("Sofa"), K("Carta"));

        CartoonKit.StreetLamp("Farol_Barracones_O", new Vector3(-7.2f, 0f, 16f), 90f, z2, lighting, WarmLamp, 2.6f, 14f);
        CartoonKit.StreetLamp("Farol_Barracones_E", new Vector3(7.2f, 0f, 13.5f), -90f, z2, lighting, WarmLamp, 2.6f, 14f);
        CartoonKit.StreetLamp("Farol_Barracones_N", new Vector3(2.5f, 0f, 29.5f), 180f, z2, lighting, WarmLamp, 2.6f, 14f);

        CartoonKit.Barrel("Barril_Barracones_A", new Vector3(16.8f, 0f, 19.4f), 0f, z2, 0);
        CartoonKit.Crate("Caja_Barracones_A", new Vector3(16.6f, 0f, 16.6f), 15f, z2, 1f);
    }

    /// <summary>Barracón de madera con puerta al sur. El entrable tiene piso, marco abierto y lámpara; los otros, puerta cerrada y ventana encendida.</summary>
    private static void Barrack(string name, float x0, float z0, float x1, float z1, bool enterable, float doorX, Transform parent,
        Transform lighting, Transform deco)
    {
        const float h = 3f, t = 0.2f;
        const CartoonKit.WallFace In = CartoonKit.WallFace.Wainscot, Out = CartoonKit.WallFace.Plain;
        Transform root = LevelBuilderUtils.CreateGroup(name, parent);
        Material wood = K("Madera");
        var sw = new Vector3(x0, 0f, z0); var se = new Vector3(x1, 0f, z0);
        var nw = new Vector3(x0, 0f, z1); var ne = new Vector3(x1, 0f, z1);

        // A la derecha de start→end: oeste→este = sur (afuera), sur→norte = este, este→oeste = norte, norte→sur = oeste
        if (enterable) CartoonKit.Wall(name + "_Sur", sw, se, h, t, wood, Out, In, root, new LevelBuilderUtils.WallOpening(doorX - x0, 2.2f, 2.4f));
        else CartoonKit.Wall(name + "_Sur", sw, se, h, t, wood, Out, Out, root);
        CartoonKit.Wall(name + "_Oeste", sw, nw, h, t, wood, enterable ? In : Out, Out, root);
        CartoonKit.Wall(name + "_Norte", ne, nw, h, t, wood, Out, enterable ? In : Out, root);
        CartoonKit.Wall(name + "_Este", ne, se, h, t, wood, enterable ? In : Out, Out, root);

        float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f, w = x1 - x0, d = z1 - z0;
        Ceiling(name + "_Cielorraso", new Vector3(cx, h + 0.1f, cz), new Vector2(w + 0.2f, d + 0.2f), root);
        PitchedRoof(name + "_Techo", new Vector3(cx, h + 0.2f, cz), w, d, 1.5f, deco);

        foreach (float wx in new[] { x0 + w * 0.22f, x1 - w * 0.22f })
            CartoonKit.WindowFrame($"{name}_Ventana_{wx:0.0}", new Vector3(wx, 1.75f, z0 - t * 0.5f - 0.05f), Quaternion.LookRotation(Vector3.back), 1f, 0.9f, deco, true);

        if (enterable)
        {
            CartoonKit.PlankFloor(name + "_Piso", new Vector3(cx, 0.02f, cz), new Vector2(w - t, d - t), root, 0.8f);
            CartoonKit.HangingLamp(name + "_Lampara", new Vector3(cx, h, cz), 0.6f, root, lighting, WarmLamp, 2.6f, 10f);
        }
        else
        {
            CartoonKit.DoorLeaf(name + "_Puerta", new Vector3(doorX, 1.15f, z0 - t * 0.5f - 0.05f), Quaternion.identity, 1.6f, 2.3f, K("Puerta"), root);
        }
        CartoonKit.WallLamp(name + "_Aplique", new Vector3(doorX, 2.65f, z0 - t * 0.5f - 0.02f), 180f, root, lighting, WarmLamp, 1.8f, 8f);
    }

    /// <summary>Carpa de lona: la boca mira a -Z local. El collider abraza el bulto, hasta donde la lona se levanta del piso.</summary>
    private static void Tent(string name, Vector3 ground, float yaw, Transform parent, Transform lighting)
    {
        const float w = 3.4f, d = 4f, h = 2.3f;
        GameObject go = MeshGo(name, "Camp_Carpa", b =>
        {
            float half = w * 0.5f, slope = Mathf.Sqrt(half * half + h * h), angle = Mathf.Atan2(h, half) * Mathf.Rad2Deg;
            b.Submesh = 0;
            foreach (int s in new[] { -1, 1 })
                CartoonKit.AddBox(b, new Vector3(s * half * 0.5f, h * 0.5f, 0f), new Vector3(slope + 0.1f, 0.05f, d),
                    Quaternion.Euler(0f, 0f, -s * angle), 0.02f);
            // Fondo cerrado
            b.Card(new[] { new Vector3(-half, 0f, d * 0.5f), new Vector3(half, 0f, d * 0.5f), new Vector3(0f, h, d * 0.5f) }, Vector3.forward);
            b.Submesh = 1;
            CartoonKit.AddBox(b, new Vector3(0f, h, 0f), new Vector3(0.08f, 0.08f, d + 0.3f), Quaternion.identity, 0.02f);
            foreach (int s in new[] { -1, 1 })
                CartoonKit.AddBox(b, new Vector3(0f, h * 0.5f, s * d * 0.5f), new Vector3(0.08f, h, 0.08f), Quaternion.identity, 0.02f);
        }, parent, ground, yaw, _canvas, K("MaderaOscura"));
        Hug(go, new Vector3(0f, 1f, 0f), new Vector3(w, 2f, d));
    }

    private static void BuildCampfire(Transform parent, Transform lighting)
    {
        var center = new Vector3(0f, 0f, 17f);
        GameObject go = MeshGo("Fogata", "Camp_Fogata", b =>
        {
            b.Submesh = 0;
            for (int i = 0; i < 9; i++)
            {
                float a = i * Mathf.PI * 2f / 9f;
                CartoonKit.AddBox(b, new Vector3(Mathf.Cos(a) * 0.7f, 0.1f, Mathf.Sin(a) * 0.7f), new Vector3(0.32f, 0.24f, 0.28f),
                    Quaternion.Euler(0f, i * 40f, 0f), 0.08f, 1, 0.03f, i);
            }
            b.Submesh = 1;
            foreach (float yaw in new[] { 0f, 60f, 120f })
                AddLog(b, new Vector3(0f, 0.16f, 0f), 1f, 0.09f, yaw);
            b.Submesh = 2;
            CartoonKit.AddLathe(b, new Vector3(0f, 0.1f, 0f), Quaternion.identity, new[] { 0f, 0.3f, 0.8f, 1.15f }, new[] { 0.32f, 0.34f, 0.17f, 0f }, 8, false, false);
        }, parent, center, 0f, _rock, K("MaderaOscura"), _fire);
        Hug(go, new Vector3(0f, 0.3f, 0f), new Vector3(1.7f, 0.6f, 1.7f));

        Light light = LevelBuilderUtils.CreatePointLight("Luz_Fogata", center + Vector3.up * 1f, new Color32(0xFF, 0x96, 0x46, 0xFF), 3.6f, 13f, lighting);
        light.gameObject.AddComponent<LightFlicker>();

        foreach (int s in new[] { -1, 1 })
            CartoonKit.Box("Banca_Fogata_" + s, new Vector3(s * 2.4f, 0.22f, 17.3f), new Vector3(0.5f, 0.44f, 2.1f), K("Madera"), parent, new Vector3(0f, s * 12f, 0f));
    }

    private static void BuildGramophone(Transform parent, Vector3 onTable)
    {
        MeshGo("Gramofono", "Camp_Gramofono", b =>
        {
            b.Submesh = 0;
            CartoonKit.AddBox(b, new Vector3(0f, 0.08f, 0f), new Vector3(0.42f, 0.16f, 0.42f), Quaternion.identity, 0.02f);
            b.Submesh = 1;
            CartoonKit.AddLathe(b, new Vector3(0f, 0.17f, 0f), Quaternion.identity, new[] { 0f, 0.012f }, new[] { 0.17f, 0.17f }, 16, false, true);
            b.Submesh = 2;
            // Corneta dorada, inclinada, abriéndose hacia afuera
            CartoonKit.AddLathe(b, new Vector3(0.12f, 0.2f, 0.1f), Quaternion.Euler(-48f, 0f, 0f),
                new[] { 0f, 0.14f, 0.3f, 0.42f, 0.5f }, new[] { 0.025f, 0.035f, 0.08f, 0.17f, 0.27f }, 12, false, false);
        }, parent, onTable, 200f, K("MaderaOscura"), K("Metal"), K("Laton"));
    }

    // ============ Z3: patio de madera ============

    private static void BuildYard(Transform env, Transform lighting, Transform deco)
    {
        Transform z3 = LevelBuilderUtils.CreateGroup("Z3_Patio", env);

        LogPile("Pila_A", new Vector3(-8.2f, 0f, 35.2f), 5f, 3, 8f, z3);
        LogPile("Pila_B", new Vector3(4.4f, 0f, 34.4f), 4.4f, 3, -6f, z3);
        LogPile("Pila_C", new Vector3(-3.8f, 0f, 39.8f), 4.2f, 3, 80f, z3);
        LogPile("Pila_D", new Vector3(-9.2f, 0f, 44f), 4.6f, 4, 12f, z3);
        LogPile("Pila_E", new Vector3(3f, 0f, 44f), 3.6f, 3, 85f, z3);

        // Pilas de tablas
        CartoonKit.Box("Tablas_A", new Vector3(-4.6f, 0.55f, 33.6f), new Vector3(3.2f, 1.1f, 1.1f), K("MaderaClara"), z3, new Vector3(0f, 4f, 0f));
        CartoonKit.Box("Tablas_B", new Vector3(8f, 0.5f, 36.8f), new Vector3(1.1f, 1f, 2.8f), K("MaderaClara"), z3, new Vector3(0f, -7f, 0f));

        BuildSawShed(z3, lighting, deco);

        CartoonKit.StreetLamp("Farol_Patio_O", new Vector3(-11f, 0f, 38.6f), 90f, z3, lighting, WarmLamp, 2.6f, 14f);
        CartoonKit.StreetLamp("Farol_Patio_C", new Vector3(1.6f, 0f, 38.8f), 180f, z3, lighting, WarmLamp, 2.4f, 13f);
    }

    /// <summary>Cobertizo del aserradero: techo sobre postes, sierra grande apagada y el banco de trabajo.</summary>
    private static void BuildSawShed(Transform parent, Transform lighting, Transform deco)
    {
        const float x0 = 5.5f, x1 = 11.5f, z0 = 39f, z1 = 46f, h = 3.6f;
        Transform shed = LevelBuilderUtils.CreateGroup("Aserradero", parent);
        foreach (float x in new[] { x0, x1 })
        foreach (float z in new[] { z0, z1 })
            CartoonKit.Box($"Poste_{x:0}_{z:0}", new Vector3(x, h * 0.5f, z), new Vector3(0.3f, h, 0.3f), K("MaderaOscura"), shed);
        Ceiling("Aserradero_Cielorraso", new Vector3((x0 + x1) * 0.5f, h + 0.1f, (z0 + z1) * 0.5f), new Vector2(x1 - x0 + 0.3f, z1 - z0 + 0.3f), shed);
        PitchedRoof("Aserradero_Techo", new Vector3((x0 + x1) * 0.5f, h + 0.2f, (z0 + z1) * 0.5f), x1 - x0, z1 - z0, 1.2f, deco);

        CartoonKit.Box("Sierra_Banco", new Vector3(8.5f, 0.45f, 43.4f), new Vector3(1.4f, 0.9f, 4.2f), K("Madera"), shed);
        GameObject saw = MeshGo("Sierra_Disco", "Camp_Sierra", b =>
        {
            b.Submesh = 0;
            CartoonKit.AddLathe(b, Vector3.zero, Quaternion.Euler(0f, 0f, 90f), new[] { -0.02f, 0.02f }, new[] { 1.15f, 1.15f }, 22, true, true);
            b.Submesh = 1;
            CartoonKit.AddBox(b, new Vector3(0f, -0.3f, 0f), new Vector3(0.22f, 0.7f, 0.5f), Quaternion.identity, 0.03f);
        }, shed, new Vector3(8.5f, 1.45f, 43.4f), 0f, K("Cromo"), K("Metal"));
        Hug(saw, Vector3.zero, new Vector3(0.1f, 2.3f, 2.3f));

        Table("Banco_Trabajo", new Vector3(10.2f, 0f, 40.6f), 1.6f, 0.8f, 0.9f, 0f, shed);
        CartoonKit.HangingLamp("Aserradero_Lampara", new Vector3(8.5f, h, 41f), 0.7f, shed, lighting, WarmLamp, 2.8f, 12f);
    }
}
