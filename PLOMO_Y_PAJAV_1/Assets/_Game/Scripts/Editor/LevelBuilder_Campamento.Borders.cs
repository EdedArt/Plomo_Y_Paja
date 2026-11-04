using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KitMesh = CartoonKit.KitMesh;
using WallOpening = LevelBuilderUtils.WallOpening;

/// <summary>
/// Campamento: bordes y vista (reglas N1–N5 de Fase-N1-Mapa-Tutorial.md §1).
/// BORDES: árboles densos (bosque y costados de la guarida), cerca de tablas del aserradero, baranda sobre el río (oriente) y
/// baranda rota sobre el barranco (occidente). Altura ≥ 1.0 m: no hay salto y stepOffset = 0.3. Cada collider abraza a su pieza.
/// La baranda "rota" nunca deja un hueco abierto con collider: donde falta el pasamanos hay tablas en cruz que tapan el paso.
/// VISTA: árboles, río, barranco, luces de Porcópolis al otro lado del río (tarjetas), sin colliders y fuera del NavMesh.
/// </summary>
public static partial class LevelBuilder_Campamento
{
    private const float RavineDepth = 8f;
    private const float RiverTop = -1.1f;

    /// <summary>Contorno jugable (para la prueba de fugas), en sentido antihorario visto desde arriba.</summary>
    private static readonly Vector2[] PlayableOutline =
    {
        new Vector2(-5.5f, -50f), new Vector2(5.2f, -50f), new Vector2(5.8f, -38f), new Vector2(4.9f, -26f), new Vector2(5.5f, -16f),
        new Vector2(5f, -10f), new Vector2(10f, -10f), new Vector2(10f, 5f), new Vector2(18f, 5f), new Vector2(18f, 30f),
        new Vector2(14.4f, 40f), new Vector2(13f, 50f), new Vector2(11.2f, 54f), new Vector2(11f, 60f), new Vector2(9f, 60f),
        new Vector2(9f, 74f), new Vector2(-9f, 74f), new Vector2(-9f, 60f), new Vector2(-11f, 60f), new Vector2(-11.2f, 54f),
        new Vector2(-13f, 50f), new Vector2(-14f, 40f), new Vector2(-17.5f, 30f), new Vector2(-17.5f, 5f), new Vector2(-10f, 5f),
        new Vector2(-10f, -10f), new Vector2(-5.2f, -10f), new Vector2(-4.9f, -18f), new Vector2(-5.6f, -28f), new Vector2(-4.8f, -40f),
    };

    // ============ Bordes ============

    private static void BuildBorders(Transform env, Transform lighting)
    {
        Transform borders = LevelBuilderUtils.CreateGroup("Bordes", env);

        // Z0: árboles densos a los dos lados del sendero y detrás del inicio
        TreeLine("Arboles_Sendero_O", new[] { V(-5.2f, -10f), V(-4.9f, -18f), V(-5.6f, -28f), V(-4.8f, -40f), V(-5.5f, -50f) }, borders);
        TreeLine("Arboles_Sendero_E", new[] { V(5.2f, -50f), V(5.8f, -38f), V(4.9f, -26f), V(5.5f, -16f), V(5f, -10f) }, borders);
        TreeLine("Arboles_Inicio", new[] { V(-5.5f, -50f), V(5.2f, -50f) }, borders);

        // Z1 y sur de Z2: cerca de tablas del aserradero (1.6 m, se ve entre las tablas). Hueco de 4 m = el portón
        const float fence = 1.6f;
        CartoonKit.Fence("Cerca_Sur", new Vector3(-10.1f, 0f, -10f), new Vector3(10.1f, 0f, -10f), fence, borders, new WallOpening(10.1f, 4f, fence));
        CartoonKit.Fence("Cerca_Porton_O", new Vector3(-10f, 0f, -10f), new Vector3(-10f, 0f, 5.1f), fence, borders);
        CartoonKit.Fence("Cerca_Porton_E", new Vector3(10f, 0f, -10f), new Vector3(10f, 0f, 5.1f), fence, borders);
        CartoonKit.Fence("Cerca_Barracones_O", new Vector3(-17.7f, 0f, 5f), new Vector3(-10f, 0f, 5f), fence, borders);
        CartoonKit.Fence("Cerca_Barracones_E", new Vector3(10f, 0f, 5f), new Vector3(18.1f, 0f, 5f), fence, borders);

        // Oriente: el río, con baranda
        Railing("Baranda_Rio", new[] { V(18f, 5f), V(18f, 30f), V(14.4f, 40f), V(13f, 50f) }, false, borders);
        CartoonKit.StreetLamp("Farol_Rio_A", new Vector3(16.9f, 0f, 8.6f), -90f, borders, lighting, WarmLamp, 2.2f, 12f);
        CartoonKit.StreetLamp("Farol_Rio_B", new Vector3(13.2f, 0f, 37.4f), -90f, borders, lighting, WarmLamp, 2.2f, 12f);

        // Occidente: el barranco, con baranda rota
        Railing("Baranda_Barranco", new[] { V(-17.5f, 5f), V(-17.5f, 30f), V(-14f, 40f), V(-13f, 50f) }, true, borders);
        CartoonKit.StreetLamp("Farol_Barranco", new Vector3(-16.4f, 0f, 16f), 90f, borders, lighting, WarmLamp, 2.2f, 12f);

        // Árboles que cierran contra las esquinas de la cabaña
        TreeLine("Arboles_Guarida_O", new[] { V(-13f, 50f), V(-11.1f, 54.2f) }, borders);
        TreeLine("Arboles_Guarida_E", new[] { V(13f, 50f), V(11.1f, 54.2f) }, borders);
    }

    private static Vector3 V(float x, float z) => new Vector3(x, 0f, z);

    /// <summary>Línea de árboles por tramos de ~6 m, con variedad (N5). Collider del tronco denso, no del follaje.</summary>
    private static void TreeLine(string name, Vector3[] points, Transform parent)
    {
        int index = 0;
        for (int p = 0; p < points.Length - 1; p++)
        {
            Vector3 delta = points[p + 1] - points[p];
            float total = delta.magnitude;
            Vector3 dir = delta / total;
            int pieces = Mathf.Max(1, Mathf.RoundToInt(total / 6f));
            float length = total / pieces;
            float yaw = Quaternion.LookRotation(dir, Vector3.up).eulerAngles.y;
            for (int i = 0; i < pieces; i++, index++)
            {
                int variant = index % 3;
                string key = $"Camp_Arboles_{Cm(length)}_{variant}";
                GameObject go = MeshGo($"{name}_{index:00}", key, b => BuildTreeSegment(b, key, length, variant), parent,
                    points[p] + dir * (length * i), yaw, K("MaderaOscura"), K("Hoja"), K("PastoLejano"), _rock);
                // Un poco de más a lo largo: entre un tramo y el siguiente no queda rendija
                Hug(go, new Vector3(0f, 1.65f, length * 0.5f), new Vector3(1.4f, 3.7f, length + 0.4f));
            }
        }
    }

    private static void BuildTreeSegment(KitMesh b, string key, float length, int variant)
    {
        System.Random rng = CartoonKit.Rng(key);
        int row = 0;
        for (float z = 0.3f; z < length; z += CartoonKit.Range(rng, 0.7f, 1.05f), row++)
        {
            float x = (row % 2 == 0 ? -0.3f : 0.3f) + CartoonKit.Range(rng, -0.1f, 0.1f);
            AddTree(b, rng, new Vector3(x, 0f, z), CartoonKit.Range(rng, 6.5f, 8.5f), row % 2 == 0 ? 2 : 1, 6, 7, 0, 1, 2);
        }

        if (variant == 1)
        {
            // Matas bajas
            for (float z = 0.6f; z < length; z += 1.5f)
            {
                b.Submesh = 1;
                AddBlob(b, rng, new Vector3(CartoonKit.Range(rng, -0.45f, 0.45f), 0.45f, z), new Vector3(0.85f, 0.7f, 0.85f), 7);
            }
        }
        else if (variant == 2)
        {
            // Una roca y un tronco contra los árboles
            b.Submesh = 3;
            AddBlob(b, rng, new Vector3(-0.35f, 0.4f, length * 0.35f), new Vector3(1f, 0.75f, 0.9f), 6);
            b.Submesh = 0;
            AddLog(b, new Vector3(0.2f, 0.3f, length * 0.7f), 2.4f, 0.26f, 75f);
        }
    }

    /// <summary>Copia de la forma de árbol del kit de bordes de la Sesión A (sin depender de su rama). Submallas: tronco, follaje, follaje oscuro.</summary>
    private static void AddTree(KitMesh b, System.Random rng, Vector3 basePos, float height, int blobs, int trunkSides, int blobSides,
        int trunkSub, int leafSub, int darkSub)
    {
        b.Submesh = trunkSub;
        float r = height * 0.03f;
        float segment = height * 0.62f / 4f;
        Vector3 p = basePos - Vector3.up * 0.3f;
        Quaternion q = Quaternion.Euler(CartoonKit.Range(rng, -5f, 5f), CartoonKit.Range(rng, 0f, 360f), CartoonKit.Range(rng, -5f, 5f));
        for (int s = 0; s < 4; s++)
        {
            CartoonKit.AddLathe(b, p, q, new[] { 0f, segment }, new[] { r, r * 0.84f }, trunkSides, false, false);
            p += q * Vector3.up * segment;
            q *= Quaternion.Euler(CartoonKit.Range(rng, -11f, 11f), 0f, CartoonKit.Range(rng, -11f, 11f));
            r *= 0.84f;
        }
        for (int i = 0; i < blobs; i++)
        {
            float t = blobs > 1 ? i / (blobs - 1f) : 0f;
            b.Submesh = i % 2 == 0 ? darkSub : leafSub;
            float size = height * Mathf.Lerp(0.27f, 0.17f, t);
            Vector3 offset = new Vector3(CartoonKit.Range(rng, -0.8f, 0.8f), height * 0.32f * t, CartoonKit.Range(rng, -0.8f, 0.8f));
            AddBlob(b, rng, p + offset, new Vector3(size, size * 0.72f, size), blobSides);
        }
    }

    private static void AddBlob(KitMesh b, System.Random rng, Vector3 pos, Vector3 radii, int sides)
    {
        const int rings = 5;
        var ys = new float[rings];
        var rs = new float[rings];
        for (int i = 0; i < rings; i++)
        {
            float a = -Mathf.PI * 0.5f + Mathf.PI * i / (rings - 1f);
            ys[i] = Mathf.Sin(a);
            rs[i] = Mathf.Max(0f, Mathf.Cos(a));
        }
        Matrix4x4 saved = b.Matrix;
        b.Matrix = saved * Matrix4x4.TRS(pos, Quaternion.Euler(0f, CartoonKit.Range(rng, 0f, 360f), 0f), radii);
        b.Lathe(ys, rs, sides, false, false);
        b.Matrix = saved;
    }

    /// <summary>Baranda de madera por tramos de ~5 m; se ve a través. broken = pasamanos partido, tablas en cruz, postes torcidos.</summary>
    private static void Railing(string name, Vector3[] points, bool broken, Transform parent)
    {
        int index = 0;
        for (int p = 0; p < points.Length - 1; p++)
        {
            Vector3 delta = points[p + 1] - points[p];
            float total = delta.magnitude;
            Vector3 dir = delta / total;
            int pieces = Mathf.Max(1, Mathf.RoundToInt(total / 5f));
            float length = total / pieces;
            float yaw = Quaternion.LookRotation(dir, Vector3.up).eulerAngles.y;
            for (int i = 0; i < pieces; i++, index++)
            {
                int variant = broken ? index % 3 : 0;
                string key = $"Camp_Baranda_{Cm(length)}_{(broken ? "Rota" : "Sana")}{variant}";
                GameObject go = MeshGo($"{name}_{index:00}", key, b => BuildRailingPiece(b, key, length, broken, variant), parent,
                    points[p] + dir * (length * i), yaw, K("MaderaOscura"), K("MaderaClara"));
                Hug(go, new Vector3(0f, 0.6f, length * 0.5f), new Vector3(0.25f, 1.2f, length + 0.1f));
            }
        }
    }

    private static void BuildRailingPiece(KitMesh b, string key, float length, bool broken, int variant)
    {
        System.Random rng = CartoonKit.Rng(key);
        float lean = broken ? 7f : 2f;

        b.Submesh = 0;
        int posts = Mathf.Max(2, Mathf.CeilToInt(length / 1.6f));
        for (int i = 0; i <= posts; i++)
            CartoonKit.AddBox(b, new Vector3(0f, 0.55f, length * i / posts), new Vector3(0.13f, 1.3f, 0.13f),
                Quaternion.Euler(CartoonKit.Range(rng, -lean, lean), 0f, CartoonKit.Range(rng, -lean, lean)), 0.03f);

        b.Submesh = 1;
        // El de abajo siempre está entero
        CartoonKit.AddBox(b, new Vector3(0.07f, 0.5f, length * 0.5f), new Vector3(0.05f, 0.1f, length),
            Quaternion.Euler(CartoonKit.Range(rng, -1.5f, 1.5f), 0f, 0f), 0.02f);

        if (variant == 1)
        {
            // Pasamanos partido en dos, caído en V; tablas en cruz cierran lo que falta
            CartoonKit.AddBox(b, new Vector3(0.07f, 0.98f, length * 0.2f), new Vector3(0.05f, 0.1f, length * 0.42f), Quaternion.Euler(-9f, 0f, 0f), 0.02f);
            CartoonKit.AddBox(b, new Vector3(0.07f, 0.92f, length * 0.8f), new Vector3(0.05f, 0.1f, length * 0.42f), Quaternion.Euler(14f, 0f, 0f), 0.02f);
            float cross = Mathf.Atan2(0.72f, length * 0.28f) * Mathf.Rad2Deg;
            float crossLength = Mathf.Sqrt(0.72f * 0.72f + length * 0.28f * length * 0.28f);
            foreach (int s in new[] { -1, 1 })
                CartoonKit.AddBox(b, new Vector3(0.09f, 0.68f, length * 0.5f), new Vector3(0.04f, 0.14f, crossLength),
                    Quaternion.Euler(s * cross, 0f, 0f), 0.015f);
        }
        else if (variant == 2)
        {
            // Pasamanos torcido y una tabla de refuerzo clavada encima
            CartoonKit.AddBox(b, new Vector3(0.07f, 1f, length * 0.5f), new Vector3(0.05f, 0.1f, length), Quaternion.Euler(-3f, 0f, 2f), 0.02f);
            CartoonKit.AddBox(b, new Vector3(0.1f, 0.78f, length * 0.35f), new Vector3(0.04f, 0.12f, length * 0.5f), Quaternion.Euler(16f, 0f, 0f), 0.015f);
        }
        else
        {
            CartoonKit.AddBox(b, new Vector3(0.07f, 1.02f, length * 0.5f), new Vector3(0.05f, 0.1f, length),
                Quaternion.Euler(CartoonKit.Range(rng, -1.5f, 1.5f), 0f, 0f), 0.02f);
        }
    }

    // ============ Vista: lo que se ve más allá de los bordes (N2, N4) ============

    private static void BuildVista(Transform vista)
    {
        BuildRiver(vista);
        BuildRavine(vista);
        BuildCityAcrossRiver(vista);

        Transform trees = LevelBuilderUtils.CreateGroup("Arboles_Vista", vista);
        ScatterTrees("Bosque_O", -34f, -60f, -8f, -11f, trees);
        ScatterTrees("Bosque_E", 7.5f, -60f, 18f, -11f, trees);
        ScatterTrees("Fuera_O", -18.5f, -9f, -11.5f, 3f, trees);
        ScatterTrees("Fuera_E", 11.5f, -9f, 18f, 3f, trees);
        ScatterTrees("LejosO", -58f, -8f, -36f, 94f, trees);
        ScatterTrees("NO", -32f, 57f, -14f, 94f, trees);
        ScatterTrees("Norte", -13f, 81f, 13f, 94f, trees);
        ScatterTrees("NE", 14f, 52f, 19f, 94f, trees);
        ScatterTrees("OtraOrilla", 47f, -50f, 58f, -5f, trees);
        ScatterTrees("OtraOrilla2", 47f, 55f, 58f, 92f, trees);
    }

    private static void ScatterTrees(string name, float x0, float z0, float x1, float z1, Transform parent)
    {
        const float cell = 13f;
        int nx = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / cell)), nz = Mathf.Max(1, Mathf.RoundToInt((z1 - z0) / cell));
        float w = (x1 - x0) / nx, d = (z1 - z0) / nz;
        for (int i = 0; i < nx; i++)
        for (int j = 0; j < nz; j++)
        {
            int variant = (i * 3 + j) % 4;
            string key = "Camp_Bosquecito_" + variant;
            MeshGo($"{name}_{i}_{j}", key, b =>
            {
                System.Random rng = CartoonKit.Rng(key);
                for (int t = 0; t < 8; t++)
                    AddTree(b, rng, new Vector3(CartoonKit.Range(rng, -6f, 6f), 0f, CartoonKit.Range(rng, -6f, 6f)),
                        CartoonKit.Range(rng, 7f, 10f), 2, 5, 6, 0, 1, 2);
            }, parent, new Vector3(x0 + w * (i + 0.5f), 0f, z0 + d * (j + 0.5f)), variant * 90f, K("MaderaOscura"), K("Hoja"), K("PastoLejano"));
        }
    }

    private static void BuildRiver(Transform vista)
    {
        Transform river = LevelBuilderUtils.CreateGroup("Rio", vista);
        const float x0 = 19.8f, x1 = 46f, thick = 1.5f;
        CartoonKit.Box("Rio_Agua", new Vector3((x0 + x1) * 0.5f, RiverTop - thick * 0.5f, 18f), new Vector3(x1 - x0, thick, 160f), _river, river, default, 0.01f, false);

        // La orilla de enfrente y el reflejo de las luces de la ciudad
        GroundRect("Rio_OrillaLejana", x1, -62f, 90f, 98f, -0.2f, _forestFloor, river, false, 20f);
        System.Random rng = CartoonKit.Rng("Camp_Reflejos");
        for (int i = 0; i < 14; i++)
        {
            float z = CartoonKit.Range(rng, -24f, 62f);
            var center = new Vector3(CartoonKit.Range(rng, x0 + 8f, x1 - 1f), RiverTop + 0.01f, z);
            CartoonKit.Box("Rio_Reflejo_" + i, center, new Vector3(CartoonKit.Range(rng, 6f, 13f), 0.01f, CartoonKit.Range(rng, 0.25f, 0.6f)), _shine, river, default, 0f, false);
        }
    }

    private static void BuildRavine(Transform vista)
    {
        Transform ravine = LevelBuilderUtils.CreateGroup("Barranco", vista);
        const float x0 = -34f, x1 = -19.5f, z0 = 2f, z1 = 56f, bottom = -RavineDepth + 1f, thick = 1.2f;
        float midX = (x0 + x1) * 0.5f, midZ = (z0 + z1) * 0.5f, height = -bottom - 0.02f; // sin llegar a y = 0: no pelea con el suelo
        CartoonKit.Box("Barranco_Fondo", new Vector3(midX, bottom - thick * 0.5f, midZ), new Vector3(x1 - x0, thick, z1 - z0), _forestFloor, ravine, default, 0.01f, false);
        // Paredes de roca (cierran hasta el borde de los suelos)
        float wallY = bottom + height * 0.5f;
        CartoonKit.Box("Barranco_Pared_E", new Vector3(x1, wallY, midZ), new Vector3(0.6f, height, z1 - z0), _rock, ravine, default, 0.05f, false, 0.08f, 3);
        CartoonKit.Box("Barranco_Pared_O", new Vector3(x0, wallY, midZ), new Vector3(0.6f, height, z1 - z0), _rock, ravine, default, 0.05f, false, 0.08f, 4);
        CartoonKit.Box("Barranco_Pared_S", new Vector3(midX, wallY, z0), new Vector3(x1 - x0, height, 0.6f), _rock, ravine, default, 0.05f, false, 0.08f, 5);
        CartoonKit.Box("Barranco_Pared_N", new Vector3(midX, wallY, z1), new Vector3(x1 - x0, height, 0.6f), _rock, ravine, default, 0.05f, false, 0.08f, 6);

        // Suelo entre el barranco y lo que queda del lado sur y norte (detrás del Suelo_Campamento)
        GroundRect("Barranco_Sur", x0, -10f, x1, z0, 0f, _forestFloor, ravine, false, 16f);
    }

    /// <summary>Porcópolis al otro lado del río: tarjetas planas (N4) con ventanas emisivas, sin colliders ni luces reales.</summary>
    private static void BuildCityAcrossRiver(Transform vista)
    {
        Transform city = LevelBuilderUtils.CreateGroup("Porcopolis_Luces", vista);
        System.Random place = CartoonKit.Rng("Camp_Ciudad");
        for (int i = 0; i < 9; i++)
        {
            int variant = i % 3;
            string key = "Camp_Tarjeta_Ciudad_" + variant;
            var position = new Vector3(CartoonKit.Range(place, 62f, 78f), -0.2f, -26f + i * 13f + CartoonKit.Range(place, -3f, 3f));
            MeshGo("Tarjeta_Ciudad_" + i, key, b => BuildCityCard(b, key), city, position, 90f + CartoonKit.Range(place, -6f, 6f),
                K("TechoEdificio"), K("VidrioCalido"));
        }
    }

    private static void BuildCityCard(KitMesh b, string key)
    {
        System.Random rng = CartoonKit.Rng(key);
        const float depth = 0.4f;
        float x = -11f;
        while (x < 11f)
        {
            float w = CartoonKit.Range(rng, 3f, 5.5f), h = CartoonKit.Range(rng, 8f, 24f);
            b.Submesh = 0;
            CartoonKit.AddBox(b, new Vector3(x + w * 0.5f, h * 0.5f, 0f), new Vector3(w, h, depth), Quaternion.identity, 0.02f);
            b.Submesh = 1;
            for (float wy = 2f; wy < h - 1f; wy += 2.2f)
            for (float wx = x + 0.8f; wx < x + w - 0.5f; wx += 1.3f)
            {
                if (rng.NextDouble() < 0.55) continue;
                CartoonKit.AddBox(b, new Vector3(wx, wy, 0f), new Vector3(0.6f, 0.9f, depth + 0.06f), Quaternion.identity, 0f, 0);
            }
            x += w + CartoonKit.Range(rng, 0.2f, 1.2f);
        }
    }

    // ============ Prueba de fugas: ¿se puede salir caminando? ============

    /// <summary>
    /// Recorre el nivel desde el inicio con la cápsula del jugador (radio 0.5) cada 0.5 m, como quien camina sin saltar. Avisa si
    /// llega a un punto fuera del contorno o sin piso. Solo usa colliders: no detecta muros invisibles, pero sí huecos en los bordes.
    /// </summary>
    private static void ValidateBorders(StringBuilder report)
    {
        // El jugador y los guardias no cuentan: sus cápsulas taparían el arranque de la prueba
        GameObject player = GameObject.FindWithTag("Player");
        GameObject actors = GameObject.Find("/--- ACTORS ---");
        if (player != null) player.SetActive(false);
        if (actors != null) actors.SetActive(false);
        Physics.SyncTransforms();

        const float step = 0.5f, radius = 0.5f;
        const int limit = 200000;
        int mask = Physics.DefaultRaycastLayers;

        var visited = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();
        var start = new Vector2Int(Mathf.RoundToInt(PlayerStart.x / step), Mathf.RoundToInt(PlayerStart.z / step));
        visited.Add(start);
        queue.Enqueue(start);

        var neighbours = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
        int leaks = 0;
        var examples = new StringBuilder();

        while (queue.Count > 0 && visited.Count < limit)
        {
            Vector2Int cell = queue.Dequeue();
            foreach (Vector2Int n in neighbours)
            {
                Vector2Int next = cell + n;
                if (!visited.Add(next)) continue;
                var feet = new Vector3(next.x * step, 0f, next.y * step);
                if (Physics.CheckCapsule(feet + Vector3.up * (radius + 0.32f), feet + Vector3.up * 1.5f, radius, mask, QueryTriggerInteraction.Ignore))
                    continue;

                bool outside = !InsideOutline(feet.x, feet.z);
                bool noFloor = !Physics.Raycast(feet + Vector3.up * 0.5f, Vector3.down, 1.2f, mask, QueryTriggerInteraction.Ignore);
                if (outside || noFloor)
                {
                    if (leaks < 5) examples.Append($" ({feet.x:0.0}, {feet.z:0.0}){(noFloor ? " sin piso" : "")}");
                    leaks++;
                    continue; // por ahí se sale: no se sigue contando desde afuera
                }
                queue.Enqueue(next);
            }
        }

        if (player != null) player.SetActive(true);
        if (actors != null) actors.SetActive(true);
        Physics.SyncTransforms();

        // Unos 4000 m² caminables a 4 puntos por m²: si recorrió mucho menos, el resultado no sirve
        if (visited.Count < 4000) report.AppendLine($"✗ Bordes: la prueba solo recorrió {visited.Count} puntos; algo tapa el inicio.");
        else if (leaks == 0) report.AppendLine($"✓ Bordes: {visited.Count} puntos recorridos, nadie se sale caminando.");
        else report.AppendLine($"✗ Bordes: {leaks} puntos fuera del contorno o sin piso. Ej.:{examples}");
    }

    private static bool InsideOutline(float x, float z)
    {
        bool inside = false;
        for (int i = 0, j = PlayableOutline.Length - 1; i < PlayableOutline.Length; j = i++)
        {
            Vector2 a = PlayableOutline[i], b = PlayableOutline[j];
            if (a.y > z != b.y > z && x < (b.x - a.x) * (z - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }
}
