using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Fase N1.1: kit de bordes del Nivel 1. MENÚ: Plomo y Paja → Nivel 1 → Crear kit de bordes (también lo corren "Ampliar mapa"
/// y "Generar Nivel Porky"). Idempotente: reescribe los prefabs de Prefabs/Level/Boundary/.
/// Todas las piezas: con semilla, gruesas y un poco torcidas, X local = largo, y = 0 en el suelo. Colores del kit (CartoonKit.M).
/// Altura: el jugador no salta (jumpForce 0) y stepOffset = 0.3, así que N3 pide ≥ 1.0 m. Una sola caja por pieza.
/// </summary>
public static class BoundaryKitBuilder
{
    public const string PrefabFolder = "Assets/_Game/Prefabs/Level/Boundary";
    public const float MinBorderHeight = 1.0f; // N3: sin salto, max(1.0, stepOffset 0.3 + 0.4)

    public const string Picket = "Fence_Picket", PicketBroken = "Fence_PicketBroken", Iron = "Fence_Iron", Hedge = "Hedge_Low",
        Barricade = "Barricade_Lobo", CarParked = "Car_Parked_30s", CarCrashed = "Car_Crashed_30s", Rubble = "Rubble_Pile",
        Crates = "Crate_Stack", Ditch = "Ditch_Railing", TreeLine = "TreeLine_Segment", LampPost = "Lamp_Post",
        GateForest = "Gate_Bosque", BackdropCard = "Backdrop_Card";

    private const int NotWalkableArea = 1;
    private const int CardQueue = 2453;

    private class Piece
    {
        public string id;
        public float length;
        public Action<CartoonKit.KitMesh> build;
        public string[] materials;
        public Bounds? collider; // null = los bounds de la malla
    }

    private static readonly Dictionary<string, float> Lengths = new Dictionary<string, float>
    {
        { Picket, 3f }, { PicketBroken, 3f }, { Iron, 3f }, { Hedge, 3f }, { Barricade, 4f }, { CarParked, 4.8f }, { CarCrashed, 5f },
        { Rubble, 3f }, { Crates, 2.4f }, { Ditch, 3f }, { TreeLine, 6f }, { LampPost, 0.6f }, { GateForest, 3.4f },
    };

    public static float Length(string id) => Lengths.TryGetValue(id, out float l) ? l : 3f;
    public static GameObject Load(string id) => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{id}.prefab");

    // ==================================================================
    // Menú
    // ==================================================================

    [MenuItem("Plomo y Paja/Nivel 1/Crear kit de bordes")]
    public static void BuildMenu()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        CartoonKit.BeginSession();
        var report = new StringBuilder();
        BuildAll(report);
        AssetDatabase.SaveAssets();
        Debug.Log("[Nivel 1] Kit de bordes:\n" + report);
        EditorUtility.DisplayDialog("Crear kit de bordes", report.ToString(), "Entendido");
    }

    /// <summary>Crea o actualiza todos los prefabs. No llama a CartoonKit.BeginSession (lo hace quien llama).</summary>
    public static void BuildAll(StringBuilder report)
    {
        LevelBuilderUtils.EnsureFolder(PrefabFolder);
        foreach (Piece piece in Pieces()) SavePiece(piece, report);
        SaveGate(report);
        SaveCard(report);
    }

    private static IEnumerable<Piece> Pieces()
    {
        yield return new Piece { id = Picket, length = Length(Picket), build = b => BuildPicket(b, Picket, false), materials = new[] { "MaderaOscura", "Yeso" } };
        yield return new Piece { id = PicketBroken, length = Length(PicketBroken), build = b => BuildPicket(b, PicketBroken, true), materials = new[] { "MaderaOscura", "Yeso" } };
        yield return new Piece { id = Iron, length = Length(Iron), build = BuildIron, materials = new[] { "Metal" } };
        yield return new Piece { id = Hedge, length = Length(Hedge), build = BuildHedge, materials = new[] { "Hoja", "PastoLejano" } };
        yield return new Piece { id = Barricade, length = Length(Barricade), build = BuildBarricade, materials = new[] { "MaderaOscura", "Puerta", "Yeso", "Metal" } };
        yield return new Piece { id = CarParked, length = Length(CarParked), build = b => BuildCar(b, false), materials = new[] { "Ventana", "Metal", "Yeso", "Cromo", "VentanaApagada" } };
        yield return new Piece { id = CarCrashed, length = Length(CarCrashed), build = b => BuildCar(b, true), materials = new[] { "Puerta", "Metal", "Yeso", "Cromo", "VentanaApagada" } };
        yield return new Piece { id = Rubble, length = Length(Rubble), build = BuildRubble, materials = new[] { "PiedraOscura", "Ladrillo", "Madera" } };
        yield return new Piece { id = Crates, length = Length(Crates), build = BuildCrates, materials = new[] { "MaderaClara", "MaderaOscura" } };
        yield return new Piece
        {
            id = Ditch, length = Length(Ditch), build = BuildDitch, materials = new[] { "MaderaOscura", "Barro" },
            collider = new Bounds(new Vector3(0f, 0.45f, 0f), new Vector3(Length(Ditch), 1.3f, 0.2f)),
        };
        yield return new Piece
        {
            id = TreeLine, length = Length(TreeLine), build = BuildTreeLine, materials = new[] { "MaderaOscura", "Hoja", "PastoLejano" },
            collider = new Bounds(new Vector3(0f, 1.65f, 0f), new Vector3(Length(TreeLine), 3.7f, 1.4f)), // tronco denso, no el follaje
        };
        yield return new Piece
        {
            id = LampPost, length = Length(LampPost), build = b => AddLampPost(b, Vector3.zero), materials = new[] { "Metal", "Bombilla" },
            collider = new Bounds(new Vector3(0f, 2f, 0f), new Vector3(0.3f, 4f, 0.3f)),
        };
    }

    private static Material[] Mats(string[] names)
    {
        var mats = new Material[names.Length];
        for (int i = 0; i < names.Length; i++) mats[i] = CartoonKit.M(names[i]);
        return mats;
    }

    private static void SavePiece(Piece piece, StringBuilder report)
    {
        Mesh mesh = CartoonKit.GetMesh("Borde_" + piece.id, piece.build);
        GameObject root = CartoonKit.MeshObject(piece.id, mesh, Mats(piece.materials), null, Vector3.zero, Quaternion.identity);
        try
        {
            Bounds box = piece.collider ?? mesh.bounds;
            // Abajo la caja entra en el suelo: sin rendija aunque el terreno ondule
            box.SetMinMax(new Vector3(box.min.x, -0.3f, box.min.z), box.max);
            float top = box.max.y;
            root.AddComponent<BorderSegment>().Configure(piece.id, piece.length, box);

            if (top < MinBorderHeight) report.AppendLine($"✗ {piece.id}: mide {top:0.00} m, N3 pide ≥ {MinBorderHeight} m.");
            MarkBorderStatic(root);
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{piece.id}.prefab");
            report.AppendLine($"✓ {piece.id}: largo {piece.length} m, alto {top:0.00} m");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>Estático y "Not Walkable": nadie camina por encima, el NavMesh se corta alrededor.</summary>
    private static void MarkBorderStatic(GameObject root)
    {
        LevelBuilderUtils.MarkStaticRecursive(root);
#pragma warning disable 618
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetNavMeshArea(t.gameObject, NotWalkableArea);
#pragma warning restore 618
    }

    // ==================================================================
    // Portón del bosque. N1.4 lo abre: Hoja_Izq y Hoja_Der giran en su bisagra, Cadena y Candado se apagan,
    // la caja de la raíz se apaga (los postes conservan la suya)
    // ==================================================================

    private static void SaveGate(StringBuilder report)
    {
        const float len = 3.4f, height = 2.2f;
        Mesh posts = CartoonKit.GetMesh("Borde_Gate_Postes", b =>
        {
            System.Random rng = CartoonKit.Rng("Gate_Postes");
            CartoonKit.AddBox(b, new Vector3(-len * 0.5f, 1.15f, 0f), new Vector3(0.26f, 2.6f, 0.26f), Quaternion.Euler(0f, 0f, -1.5f), 0.05f);
            CartoonKit.AddBox(b, new Vector3(len * 0.5f, 1.15f, 0f), new Vector3(0.26f, 2.6f, 0.26f), Quaternion.Euler(0f, 0f, 2f), 0.05f);
            CartoonKit.AddBox(b, new Vector3(0f, 2.5f, 0f), new Vector3(len + 0.7f, 0.2f, 0.22f), Quaternion.Euler(0f, 0f, CartoonKit.Range(rng, 1f, 3f)), 0.05f);
        });
        GameObject root = CartoonKit.MeshObject(GateForest, posts, new[] { CartoonKit.M("MaderaOscura") }, null, Vector3.zero, Quaternion.identity);
        try
        {
            var box = new Bounds(new Vector3(0f, (height - 0.3f) * 0.5f, 0f), new Vector3(len, height + 0.3f, 0.3f));
            root.AddComponent<BorderSegment>().Configure(GateForest, len, box);
            MarkBorderStatic(root);

            foreach (float side in new[] { -1f, 1f })
            {
                string name = side < 0f ? "Hoja_Izq" : "Hoja_Der";
                Mesh leafMesh = CartoonKit.GetMesh("Borde_Gate_" + name, b => BuildGateLeaf(b, name, side, len * 0.5f - 0.12f, height));
                CartoonKit.MeshObject(name, leafMesh, new[] { CartoonKit.M("Madera"), CartoonKit.M("MaderaOscura") }, root.transform,
                    new Vector3(side * (len * 0.5f - 0.1f), 0f, 0f), Quaternion.identity);

                // Los postes siguen chocando cuando el portón se abre
                var post = new GameObject("Poste_Collider" + (side < 0f ? "_Izq" : "_Der"));
                post.transform.SetParent(root.transform, false);
                post.transform.localPosition = new Vector3(side * len * 0.5f, 1f, 0f);
                post.AddComponent<BoxCollider>().size = new Vector3(0.3f, 2.6f, 0.3f);
                LevelBuilderUtils.MarkStaticRecursive(post);
            }

            Mesh chain = CartoonKit.GetMesh("Borde_Gate_Cadena", b =>
            {
                System.Random rng = CartoonKit.Rng("Gate_Cadena");
                for (int i = 0; i < 9; i++)
                {
                    float x = -0.4f + i * 0.1f;
                    float y = 1.12f - 0.1f * Mathf.Sin(Mathf.PI * i / 8f) + CartoonKit.Range(rng, -0.006f, 0.006f);
                    CartoonKit.AddBox(b, new Vector3(x, y, -0.09f), new Vector3(0.1f, 0.035f, 0.035f), Quaternion.Euler(45f * (i % 2), 0f, 0f), 0.012f);
                }
            });
            CartoonKit.MeshObject("Cadena", chain, new[] { CartoonKit.M("Metal") }, root.transform, Vector3.zero, Quaternion.identity);
            Mesh padlock = CartoonKit.GetMesh("Borde_Gate_Candado", b =>
            {
                b.Submesh = 0;
                CartoonKit.AddBox(b, new Vector3(0f, 0.92f, -0.11f), new Vector3(0.14f, 0.14f, 0.06f), Quaternion.Euler(0f, 0f, 5f), 0.02f);
                b.Submesh = 1;
                CartoonKit.AddBox(b, new Vector3(-0.04f, 1.01f, -0.11f), new Vector3(0.02f, 0.09f, 0.02f), Quaternion.identity, 0.006f);
                CartoonKit.AddBox(b, new Vector3(0.04f, 1.01f, -0.11f), new Vector3(0.02f, 0.09f, 0.02f), Quaternion.identity, 0.006f);
                CartoonKit.AddBox(b, new Vector3(0f, 1.05f, -0.11f), new Vector3(0.1f, 0.02f, 0.02f), Quaternion.identity, 0.006f);
            });
            CartoonKit.MeshObject("Candado", padlock, new[] { CartoonKit.M("Laton"), CartoonKit.M("Metal") }, root.transform, Vector3.zero, Quaternion.identity);

            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{GateForest}.prefab");
            report.AppendLine($"✓ {GateForest}: largo {len} m, alto {height} m, cerrado (caja en la raíz), con cadena y candado");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void BuildGateLeaf(CartoonKit.KitMesh b, string key, float side, float width, float height)
    {
        System.Random rng = CartoonKit.Rng("Gate_" + key);
        float dir = -side; // la hoja crece desde la bisagra hacia el centro
        b.Submesh = 0;
        const int planks = 6;
        for (int i = 0; i < planks; i++)
        {
            float x = dir * width * (i + 0.5f) / planks;
            float h = height - 0.15f + CartoonKit.Range(rng, -0.08f, 0.08f);
            CartoonKit.AddBox(b, new Vector3(x, 0.1f + h * 0.5f, 0f), new Vector3(width / planks * 0.92f, h, 0.05f),
                Quaternion.Euler(0f, 0f, CartoonKit.Range(rng, -2f, 2f)), 0.012f);
        }
        b.Submesh = 1;
        foreach (float y in new[] { 0.45f, 1.75f })
            CartoonKit.AddBox(b, new Vector3(dir * width * 0.5f, y, -0.04f), new Vector3(width, 0.1f, 0.035f),
                Quaternion.Euler(0f, 0f, CartoonKit.Range(rng, -1.5f, 1.5f)), 0.012f);
        float angle = Mathf.Atan2(1.3f, width) * Mathf.Rad2Deg;
        CartoonKit.AddBox(b, new Vector3(dir * width * 0.5f, 1.1f, -0.045f), new Vector3(Mathf.Sqrt(width * width + 1.69f), 0.09f, 0.03f),
            Quaternion.Euler(0f, 0f, dir * angle), 0.012f);
    }

    // ==================================================================
    // Tarjeta de fondo: plano sin luz, silueta con alpha, teñido por la niebla (regla N4: sin collider ni BorderSegment)
    // ==================================================================

    private static void SaveCard(StringBuilder report)
    {
        Material mat = CardMaterial("Fondo_Tarjeta");
        if (mat == null)
        {
            report.AppendLine("✗ Falta el shader PlomoYPaja/BackdropCard: no se creó " + BackdropCard);
            return;
        }
        GameObject root = CartoonKit.MeshObject(BackdropCard, Resources.GetBuiltinResource<Mesh>("Quad.fbx"), new[] { mat }, null, Vector3.zero, Quaternion.identity);
        try
        {
            MeshRenderer mr = root.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{BackdropCard}.prefab");
            report.AppendLine($"✓ {BackdropCard}: plano sin luz, sin collider");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static Material CardMaterial(string name)
    {
        Shader shader = Shader.Find("PlomoYPaja/BackdropCard");
        if (shader == null) return null;
        string path = $"{LevelBuilderUtils.MaterialFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            LevelBuilderUtils.EnsureFolder(LevelBuilderUtils.MaterialFolder);
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>Coloca una tarjeta de fondo que mira a lookAt. blended = alpha suave (luz lejana); si no, recorte.</summary>
    public static GameObject PlaceCard(string name, Texture2D texture, Vector3 position, Vector3 lookAt, float width, float height,
        float fogTint, bool blended, int queueOffset, Transform parent)
    {
        Material mat = CardMaterial("Fondo_" + name);
        GameObject prefab = Load(BackdropCard);
        if (mat == null || prefab == null || texture == null) return null;

        mat.SetTexture("_MainTex", texture);
        mat.SetColor("_HorizonColor", NightSkyBuilder.Horizon);
        mat.SetFloat("_FogTint", fogTint);
        mat.SetFloat("_SrcBlend", (float)(blended ? BlendMode.SrcAlpha : BlendMode.One));
        mat.SetFloat("_DstBlend", (float)(blended ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
        mat.SetFloat("_ZWrite", blended ? 0f : 1f);
        mat.SetFloat("_Cutoff", blended ? 0f : 0.5f);
        mat.renderQueue = blended ? 2910 + queueOffset : CardQueue + queueOffset;

        var card = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        card.name = name;
        card.transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - lookAt));
        card.transform.localScale = new Vector3(width, height, 1f);
        card.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return card;
    }

    // ==================================================================
    // Mallas de las piezas
    // ==================================================================

    private static void BuildPicket(CartoonKit.KitMesh b, string key, bool broken)
    {
        System.Random rng = CartoonKit.Rng(key);
        float len = Length(key);
        const float h = 1.12f;
        b.Submesh = 0;
        foreach (float x in new[] { -len * 0.5f + 0.06f, len * 0.5f - 0.06f })
            CartoonKit.AddBox(b, new Vector3(x, (h - 0.2f) * 0.5f, -0.03f), new Vector3(0.13f, h + 0.2f, 0.13f),
                Quaternion.Euler(CartoonKit.Range(rng, -2f, 2f), 0f, CartoonKit.Range(rng, -2f, 2f)), 0.04f);
        foreach (float y in new[] { 0.28f, 0.82f })
            CartoonKit.AddBox(b, new Vector3(0f, y, -0.05f), new Vector3(len, 0.08f, 0.04f), Quaternion.Euler(0f, 0f, CartoonKit.Range(rng, -1f, 1f)), 0.015f);

        b.Submesh = 1;
        for (float x = -len * 0.5f + 0.16f; x < len * 0.5f - 0.1f; x += 0.24f)
        {
            double roll = rng.NextDouble();
            if (broken && roll < 0.3) continue;                                      // tabla que falta
            float top = h * CartoonKit.Range(rng, 0.88f, 0.98f);
            if (broken && roll > 0.72) top = h * CartoonKit.Range(rng, 0.45f, 0.7f); // tabla rota
            float tilt = broken && roll > 0.5 ? CartoonKit.Range(rng, -13f, 13f) : CartoonKit.Range(rng, -3f, 3f);
            CartoonKit.AddBox(b, new Vector3(x, top * 0.5f, 0f), new Vector3(0.14f, top, 0.03f), Quaternion.Euler(0f, 0f, tilt), 0.012f);
            Vector3 tip = new Vector3(x, top, 0f);
            b.Card(new[] { tip + new Vector3(-0.07f, -0.01f, -0.016f), tip + new Vector3(0f, 0.09f, -0.016f), tip + new Vector3(0.07f, -0.01f, -0.016f) }, Vector3.back);
            b.Card(new[] { tip + new Vector3(-0.07f, -0.01f, 0.016f), tip + new Vector3(0f, 0.09f, 0.016f), tip + new Vector3(0.07f, -0.01f, 0.016f) }, Vector3.forward);
        }
    }

    private static void BuildIron(CartoonKit.KitMesh b)
    {
        System.Random rng = CartoonKit.Rng(Iron);
        float len = Length(Iron);
        const float h = 1.6f;
        foreach (float x in new[] { -len * 0.5f + 0.05f, len * 0.5f - 0.05f })
        {
            CartoonKit.AddBox(b, new Vector3(x, (h - 0.2f) * 0.5f, 0f), new Vector3(0.1f, h + 0.2f, 0.1f), Quaternion.Euler(0f, 0f, CartoonKit.Range(rng, -1f, 1f)), 0.02f);
            CartoonKit.AddLathe(b, new Vector3(x, h, 0f), Quaternion.identity, new[] { 0f, 0.06f, 0.14f }, new[] { 0.05f, 0.08f, 0f }, 8, false, false);
        }
        foreach (float y in new[] { 0.22f, h - 0.22f })
            CartoonKit.AddBox(b, new Vector3(0f, y, 0f), new Vector3(len, 0.05f, 0.05f), Quaternion.Euler(0f, 0f, CartoonKit.Range(rng, -0.8f, 0.8f)), 0.012f);
        for (float x = -len * 0.5f + 0.2f; x < len * 0.5f - 0.1f; x += 0.17f)
        {
            float top = h - 0.02f + CartoonKit.Range(rng, -0.03f, 0.03f);
            CartoonKit.AddBox(b, new Vector3(x, top * 0.5f, 0f), new Vector3(0.03f, top, 0.03f), Quaternion.Euler(CartoonKit.Range(rng, -1.5f, 1.5f), 0f, CartoonKit.Range(rng, -1.5f, 1.5f)), 0.008f);
            CartoonKit.AddBox(b, new Vector3(x, top + 0.02f, 0f), new Vector3(0.06f, 0.06f, 0.015f), Quaternion.Euler(0f, 0f, 45f), 0.004f);
        }
    }

    private static void BuildHedge(CartoonKit.KitMesh b)
    {
        System.Random rng = CartoonKit.Rng(Hedge);
        float len = Length(Hedge);
        for (float x = -len * 0.5f + 0.3f; x < len * 0.5f; x += 0.45f)
        {
            b.Submesh = rng.NextDouble() < 0.25 ? 1 : 0;
            AddBlob(b, rng, new Vector3(x, 0.52f + CartoonKit.Range(rng, -0.04f, 0.05f), CartoonKit.Range(rng, -0.08f, 0.08f)),
                new Vector3(0.44f, 0.56f, 0.45f), 8);
        }
    }

    private static void BuildBarricade(CartoonKit.KitMesh b)
    {
        System.Random rng = CartoonKit.Rng(Barricade);
        float len = Length(Barricade);
        b.Submesh = 0; // caballetes
        foreach (float x in new[] { -len * 0.36f, len * 0.36f })
        {
            CartoonKit.AddBox(b, new Vector3(x, 1.02f, 0f), new Vector3(0.14f, 0.12f, 0.95f), Quaternion.Euler(CartoonKit.Range(rng, -2f, 2f), 0f, 0f), 0.03f);
            foreach (float z in new[] { -0.34f, 0.34f })
            {
                CartoonKit.AddBox(b, new Vector3(x - 0.2f, 0.5f, z), new Vector3(0.09f, 1.1f, 0.09f), Quaternion.Euler(0f, 0f, -20f), 0.02f);
                CartoonKit.AddBox(b, new Vector3(x + 0.2f, 0.5f, z), new Vector3(0.09f, 1.1f, 0.09f), Quaternion.Euler(0f, 0f, 20f), 0.02f);
            }
        }
        // Tablones pintados a franjas (rojo Don Lobo y crema)
        foreach (float y in new[] { 0.62f, 1.18f })
        {
            const int stripes = 8;
            float tilt = CartoonKit.Range(rng, -1.6f, 1.6f);
            Quaternion q = Quaternion.Euler(0f, 0f, tilt);
            for (int i = 0; i < stripes; i++)
            {
                b.Submesh = i % 2 == 0 ? 1 : 2;
                float x = -len * 0.5f + len * (i + 0.5f) / stripes;
                CartoonKit.AddBox(b, q * new Vector3(x, 0f, 0f) + new Vector3(0f, y, -0.5f), new Vector3(len / stripes, 0.2f, 0.05f), q, 0.012f);
            }
        }
        // Barriles entre los caballetes
        b.Submesh = 3;
        foreach (float x in new[] { -0.45f, 0.42f })
        {
            float yaw = CartoonKit.Range(rng, 0f, 360f);
            CartoonKit.AddLathe(b, new Vector3(x, 0f, 0.12f), Quaternion.Euler(0f, yaw, 0f), new[] { 0f, 0.5f, 0.98f }, new[] { 0.3f, 0.37f, 0.3f }, 12, false, true);
        }
    }

    private static void BuildCar(CartoonKit.KitMesh b, bool crashed)
    {
        System.Random rng = CartoonKit.Rng(crashed ? CarCrashed : CarParked);
        // X local = largo del auto. Chocado: la trompa hundida y una rueda menos
        Quaternion q = Quaternion.Euler(0f, 90f, 0f) * (crashed ? Quaternion.Euler(11f, 0f, 7f) : Quaternion.Euler(0f, 0f, CartoonKit.Range(rng, -1f, 1f)));
        b.Matrix = Matrix4x4.TRS(new Vector3(0f, crashed ? 0.18f : 0f, 0f), q, Vector3.one);

        IntroTaxiBuilder.AddCarBody(b, rng, false);
        b.Submesh = 4; // vidrios oscuros
        CartoonKit.AddBox(b, new Vector3(0f, 1.42f, -0.37f), new Vector3(1.52f, 0.56f, 2.1f), Quaternion.identity, 0.03f);

        foreach (float sx in new[] { -0.84f, 0.84f })
        foreach (float sz in new[] { -1.4f, 1.4f })
        {
            if (crashed && sx > 0f && sz > 0f) continue; // la rueda que se soltó
            b.Submesh = 1;
            CartoonKit.AddLathe(b, new Vector3(sx, 0.38f, sz), Quaternion.Euler(0f, 0f, 90f), new[] { -0.13f, -0.1f, 0.1f, 0.13f }, new[] { 0.33f, 0.38f, 0.38f, 0.33f }, 14);
            b.Submesh = 3;
            CartoonKit.AddLathe(b, new Vector3(sx, 0.38f, sz), Quaternion.Euler(0f, 0f, 90f), new[] { -0.15f, 0.15f }, new[] { 0.15f, 0.15f }, 8);
        }

        if (crashed)
        {
            // Capó arrugado y puerta abierta
            b.Submesh = 0;
            CartoonKit.AddBox(b, new Vector3(0.2f, 1.2f, 1.5f), new Vector3(1.05f, 0.1f, 0.9f), Quaternion.Euler(-24f, 8f, 10f), 0.03f);
            CartoonKit.AddBox(b, new Vector3(1.1f, 0.98f, 0.2f), new Vector3(0.07f, 0.34f, 1.0f), Quaternion.Euler(0f, 38f, 0f), 0.02f);
            b.ResetMatrix();
            // La rueda suelta en el piso y unos pedazos
            b.Submesh = 1;
            CartoonKit.AddLathe(b, new Vector3(1.95f, 0.14f, 0.95f), Quaternion.identity, new[] { -0.13f, -0.1f, 0.1f, 0.13f }, new[] { 0.33f, 0.38f, 0.38f, 0.33f }, 14);
            b.Submesh = 3;
            for (int i = 0; i < 4; i++)
                CartoonKit.AddBox(b, new Vector3(CartoonKit.Range(rng, 1.4f, 2.4f), 0.04f, CartoonKit.Range(rng, -0.9f, 0.5f)), new Vector3(0.22f, 0.04f, 0.14f),
                    Quaternion.Euler(0f, CartoonKit.Range(rng, 0f, 180f), 0f), 0.01f);
        }
        b.ResetMatrix();
    }

    private static void BuildRubble(CartoonKit.KitMesh b)
    {
        System.Random rng = CartoonKit.Rng(Rubble);
        float len = Length(Rubble);
        // Núcleo para que la pila llegue a la altura de N3
        b.Submesh = 0;
        CartoonKit.AddBox(b, new Vector3(0f, 0.55f, 0f), new Vector3(len * 0.72f, 1.15f, 1.15f), Quaternion.Euler(0f, 4f, 3f), 0.22f, 2, 0.1f, 11);
        for (int i = 0; i < 9; i++)
        {
            float size = CartoonKit.Range(rng, 0.35f, 0.72f);
            b.Submesh = i % 3 == 1 ? 1 : 0;
            Vector3 pos = new Vector3(CartoonKit.Range(rng, -len * 0.5f + 0.3f, len * 0.5f - 0.3f), CartoonKit.Range(rng, 0.1f, 0.5f), CartoonKit.Range(rng, -0.5f, 0.5f));
            CartoonKit.AddBox(b, pos, new Vector3(size * 1.2f, size * 0.7f, size), Quaternion.Euler(CartoonKit.Range(rng, -15f, 15f), CartoonKit.Range(rng, 0f, 180f), CartoonKit.Range(rng, -15f, 15f)),
                size * 0.2f, 2, size * 0.08f, i);
        }
        // Una tabla asomando
        b.Submesh = 2;
        CartoonKit.AddBox(b, new Vector3(0.5f, 1.02f, 0.2f), new Vector3(1.5f, 0.07f, 0.2f), Quaternion.Euler(10f, 25f, 22f), 0.02f);
    }

    private static void BuildCrates(CartoonKit.KitMesh b)
    {
        System.Random rng = CartoonKit.Rng(Crates);
        void Crate(Vector3 pos, float size, float yaw)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, CartoonKit.Range(rng, -1.5f, 1.5f));
            b.Submesh = 0;
            CartoonKit.AddBox(b, pos + Vector3.up * size * 0.5f, Vector3.one * size, q, size * 0.07f);
            b.Submesh = 1;
            foreach (float y in new[] { 0.33f, 0.66f })
                CartoonKit.AddBox(b, pos + Vector3.up * size * y + q * new Vector3(0f, 0f, -size * 0.505f), new Vector3(size * 0.9f, 0.03f, 0.012f), q, 0.006f);
        }
        Crate(new Vector3(-0.8f, 0f, 0.02f), 0.8f, 4f);
        Crate(new Vector3(0f, 0f, -0.03f), 0.8f, -5f);
        Crate(new Vector3(0.8f, 0f, 0.03f), 0.78f, 3f);
        Crate(new Vector3(-0.35f, 0.8f, 0f), 0.8f, 11f);
    }

    private static void BuildDitch(CartoonKit.KitMesh b)
    {
        System.Random rng = CartoonKit.Rng(Ditch);
        float len = Length(Ditch);
        b.Submesh = 0;
        for (int i = 0; i <= 3; i++)
        {
            float x = -len * 0.5f + len * i / 3f;
            CartoonKit.AddBox(b, new Vector3(x, 0.45f, 0f), new Vector3(0.11f, 1.3f, 0.11f), Quaternion.Euler(CartoonKit.Range(rng, -4f, 4f), 0f, CartoonKit.Range(rng, -4f, 4f)), 0.03f);
        }
        foreach (float y in new[] { 0.55f, 1.02f })
            CartoonKit.AddBox(b, new Vector3(0f, y, 0.02f), new Vector3(len, 0.09f, 0.06f), Quaternion.Euler(0f, 0f, CartoonKit.Range(rng, -1.5f, 1.5f)), 0.02f);
        // La zanja: barro oscuro del otro lado de la baranda
        b.Submesh = 1;
        CartoonKit.AddBox(b, new Vector3(0f, -0.03f, 0.95f), new Vector3(len, 0.06f, 1.5f), Quaternion.identity, 0.02f);
    }

    private static void BuildTreeLine(CartoonKit.KitMesh b)
    {
        System.Random rng = CartoonKit.Rng(TreeLine);
        float len = Length(TreeLine);
        int row = 0;
        for (float x = -len * 0.5f + 0.35f; x < len * 0.5f; x += CartoonKit.Range(rng, 0.7f, 1.05f))
        {
            float z = (row % 2 == 0 ? -0.3f : 0.3f) + CartoonKit.Range(rng, -0.1f, 0.1f);
            AddTree(b, rng, new Vector3(x, 0f, z), CartoonKit.Range(rng, 6.5f, 8.5f), row % 2 == 0 ? 2 : 1, 7, 8, 0, 1, 2);
            row++;
        }
    }

    // ==================================================================
    // Partes reutilizables (también las usa la vista cercana de Nivel1MapBuilder)
    // ==================================================================

    public static void AddBlob(CartoonKit.KitMesh b, System.Random rng, Vector3 pos, Vector3 radii, int sides)
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

    /// <summary>Árbol de caricatura: tronco torcido y follaje en capas. Submallas: tronco, follaje claro, follaje oscuro.</summary>
    public static void AddTree(CartoonKit.KitMesh b, System.Random rng, Vector3 basePos, float height, int blobs, int trunkSides, int blobSides,
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

    /// <summary>Poste con farolillo emisivo, sin luz real.</summary>
    public static void AddLampPost(CartoonKit.KitMesh b, Vector3 pos, int metalSub = 0, int glowSub = 1)
    {
        b.Submesh = metalSub;
        CartoonKit.AddLathe(b, pos, Quaternion.identity, new[] { -0.3f, 0.12f, 0.3f, 0.45f, 3.7f, 3.9f }, new[] { 0.2f, 0.18f, 0.11f, 0.08f, 0.06f, 0.09f }, 8, false, false);
        CartoonKit.AddLathe(b, pos + Vector3.up * 4.25f, Quaternion.identity, new[] { 0f, 0.2f }, new[] { 0.3f, 0f }, 8, false, false);
        b.Submesh = glowSub;
        CartoonKit.AddLathe(b, pos + Vector3.up * 3.9f, Quaternion.identity, new[] { 0f, 0.15f, 0.35f }, new[] { 0.09f, 0.2f, 0.26f }, 8, false, true);
    }
}
