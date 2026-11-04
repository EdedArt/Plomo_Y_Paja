using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fase N1.4: huellas de lobo y de cerdo (N9), la escena del portón y la salida al Campamento (N10).
/// MENÚ: Plomo y Paja → Nivel 1 → Colocar huellas (también lo corren Generar Nivel Porky y Ampliar mapa). Idempotente: rehace "--- HUELLAS ---".
/// HUELLAS: quads sin collider, 2.5 cm sobre el piso (Camino_Bosque va a 1.5 cm y taparía los de 1 cm). El renderer no tiene
/// la función Decal de URP. Cada animal es una sola malla: 2 draw calls por rastro.
/// ARTE: Art/Props/huellas/huella_lobo_01.png y huella_cerdo_01.png; si faltan, se dibuja gen_&lt;id&gt;.png. Al llegar el arte, gana al volver a correr el menú.
/// PARA NIVEL 2: FootprintTrail.Build(nombre, puntos, grupo, reporte) dibuja el rastro sobre el piso de la escena abierta.
/// </summary>
public static class FootprintTrail
{
    public const string ArtFolder = "Assets/_Game/Art/Props/huellas";
    private const string RootName = "--- HUELLAS ---";
    private const string WolfArt = "huella_lobo_01", PigArt = "huella_cerdo_01";
    private const string GateName = "Gate_Bosque", ExitAnchorName = "Anchor_LevelExit";
    private const int IgnoreRaycastLayer = 2;
    private const float AboveGround = 0.025f;
    private const float WalkerSeparation = 0.34f;  // cada animal, a este lado del centro del rastro
    private const float FootSeparation = 0.09f;    // izquierda y derecha de cada animal
    private const float WolfSize = 0.32f, PigSize = 0.24f;
    private const float EyeLevel = 1.6f;

    [MenuItem("Plomo y Paja/Nivel 1/Colocar huellas")]
    public static void BuildMenu()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        if (!File.Exists(LevelBuilder_NivelPorky.ScenePath))
        {
            EditorUtility.DisplayDialog("Colocar huellas", $"No existe {LevelBuilder_NivelPorky.ScenePath}. Primero: Plomo y Paja → Generar Nivel Porky.", "Entendido");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != LevelBuilder_NivelPorky.ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(LevelBuilder_NivelPorky.ScenePath, OpenSceneMode.Single);
        }

        CartoonKit.BeginSession();
        var report = new StringBuilder();
        BuildInto(report);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Nivel 1] Huellas:\n" + report);
        EditorUtility.DisplayDialog("Colocar huellas", report + "\nEscena guardada.", "Entendido");
    }

    /// <summary>Arma todo en la escena abierta. Va después del mapa (portón, sendero, anclas) y del taxi (franjas, subtítulos). No guarda.</summary>
    public static void BuildInto(StringBuilder report)
    {
        GameObject old = GameObject.Find("/" + RootName);
        if (old != null) Object.DestroyImmediate(old);
        Transform root = LevelBuilderUtils.CreateGroup(RootName);

        Vector3[] trail = BuildTrailPoints();
        Build("Huellas_Bosque", trail, root, report);

        ForestGate gate = SetupGate(root, trail, report);
        BuildDirector(root, gate, trail, report);
        BuildExit(root, report);
    }

    /// <summary>Detrás del cuarto → portón → ~25 m por el sendero, sobre el mismo camino que dibuja Nivel1MapBuilder.</summary>
    private static Vector3[] BuildTrailPoints()
    {
        Vector3 shed = ContadorYardBuilder.ShedPosition;
        Vector3[] forest = Nivel1MapBuilder.ForestTrailPoints;
        return new[]
        {
            new Vector3(shed.x + 1.6f, 0f, shed.z + 2.2f),
            new Vector3(shed.x + 1f, 0f, shed.z + 4.4f),
            forest[0], forest[1], forest[2], forest[3],
        };
    }

    // ==================================================================
    // Rastro (reutilizable)
    // ==================================================================

    /// <summary>
    /// Rastro de lobo y cerdo lado a lado, al mismo paso, alternando izquierda y derecha, con un poco de giro al azar. Los puntos se
    /// leen en x, z; la altura sale del piso (rayo por vértice). Necesita colliders en el piso de la escena abierta.
    /// </summary>
    public static GameObject Build(string name, Vector3[] path, Transform parent, StringBuilder report, float stride = 0.7f)
    {
        Physics.SyncTransforms();
        Material wolfMaterial = PrintMaterial(WolfArt, DrawWolf, report);
        Material pigMaterial = PrintMaterial(PigArt, DrawPig, report);

        var group = new GameObject(name);
        group.transform.SetParent(parent, false);
        group.transform.position = Vector3.zero;

        int steps = 0;
        // Cada animal va con su propia semilla, pero el paso es el mismo: quedan uno al lado del otro
        Mesh wolf = CartoonKit.GetMesh($"{name}_Lobo", b => steps = AddPrints(b, $"{name}_Lobo", path, stride, -WalkerSeparation, WolfSize));
        Mesh pig = CartoonKit.GetMesh($"{name}_Cerdo", b => AddPrints(b, $"{name}_Cerdo", path, stride, WalkerSeparation, PigSize));
        foreach ((string part, Mesh mesh, Material material) in new[] { ("Lobo", wolf, wolfMaterial), ("Cerdo", pig, pigMaterial) })
        {
            GameObject go = CartoonKit.MeshObject(part, mesh, new[] { material }, group.transform, Vector3.zero, Quaternion.identity);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        report.AppendLine($"✓ {name}: {steps} pasos de cada animal cada {stride} m, en quads");
        return group;
    }

    private static int AddPrints(CartoonKit.KitMesh b, string key, Vector3[] path, float stride, float lateral, float size)
    {
        System.Random rng = CartoonKit.Rng(key);
        int index = 0;
        float walked = 0f, nextAt = 0f;
        for (int s = 0; s < path.Length - 1; s++)
        {
            Vector3 a = path[s], c = path[s + 1];
            a.y = 0f;
            c.y = 0f;
            float length = Vector3.Distance(a, c);
            Vector3 dir = (c - a).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            while (nextAt <= walked + length)
            {
                Vector3 center = Vector3.Lerp(a, c, (nextAt - walked) / Mathf.Max(0.001f, length));
                float foot = index % 2 == 0 ? -FootSeparation : FootSeparation;
                center += side * (lateral + foot);
                float turn = (index % 2 == 0 ? -4f : 4f) + CartoonKit.Range(rng, -7f, 7f);
                AddQuad(b, center, Quaternion.LookRotation(dir) * Quaternion.Euler(0f, turn, 0f), size);
                nextAt += stride;
                index++;
            }
            walked += length;
        }
        return index;
    }

    private static void AddQuad(CartoonKit.KitMesh b, Vector3 center, Quaternion rotation, float size)
    {
        float half = size * 0.5f;
        Vector3 right = rotation * Vector3.right * half, forward = rotation * Vector3.forward * half;
        int v0 = Corner(b, center - right - forward, new Vector2(0f, 0f));
        int v1 = Corner(b, center - right + forward, new Vector2(0f, 1f));
        int v2 = Corner(b, center + right + forward, new Vector2(1f, 1f));
        int v3 = Corner(b, center + right - forward, new Vector2(1f, 0f));
        b.Tri(v0, v1, v2);
        b.Tri(v0, v2, v3);
    }

    private static int Corner(CartoonKit.KitMesh b, Vector3 p, Vector2 uv)
    {
        p.y = GroundHeight(p) + AboveGround;
        return b.Vertex(p, Vector3.up, uv);
    }

    /// <summary>
    /// El rayo nace a 0.6 m: arriba del terreno (ondula ±0.16 m) y dentro de la caja del portón, que un rayo que empieza adentro no ve.
    /// </summary>
    private static float GroundHeight(Vector3 p)
    {
        var origin = new Vector3(p.x, 0.6f, p.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return 0f;
    }

    private static Material PrintMaterial(string id, System.Func<Texture2D> draw, StringBuilder report)
    {
        Texture2D texture = PrintTexture(id, draw, report, out bool isArt);
        Shader shader = Shader.Find("PlomoYPaja/BackdropCard"); // sin luz, alpha suave, sin escribir profundidad
        if (shader == null)
        {
            report.AppendLine("✗ Falta el shader PlomoYPaja/BackdropCard: las huellas salen con el material de barro.");
            return CartoonKit.M("Barro");
        }

        string path = $"{LevelBuilderUtils.MaterialFolder}/Huella_{id}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            LevelBuilderUtils.EnsureFolder(LevelBuilderUtils.MaterialFolder);
            material = new Material(shader) { name = "Huella_" + id };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetTexture("_MainTex", texture);
        // Tinte húmedo y oscuro para el arte; el dibujo generado ya es oscuro. Se ve de noche sin luces reales
        material.SetColor("_Color", isArt ? new Color(0.62f, 0.56f, 0.52f, 1f) : Color.white);
        material.SetFloat("_FogTint", 0f);
        material.SetFloat("_Cutoff", 0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D PrintTexture(string id, System.Func<Texture2D> draw, StringBuilder report, out bool isArt)
    {
        LevelBuilderUtils.EnsureFolder(ArtFolder);
        string art = $"{ArtFolder}/{id}.png";
        isArt = File.Exists(art);
        string path = art;
        if (isArt) report.AppendLine($"✓ Huella {id}: arte de Edgar");
        else
        {
            path = $"{ArtFolder}/gen_{id}.png";
            if (!File.Exists(path))
            {
                Texture2D drawn = draw();
                File.WriteAllBytes(path, drawn.EncodeToPNG());
                Object.DestroyImmediate(drawn);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            report.AppendLine($"⚠ Huella {id}: no está el arte, se usa la generada");
        }

        if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
            !(importer.textureType == TextureImporterType.Default && importer.alphaIsTransparency &&
              importer.wrapMode == TextureWrapMode.Clamp && importer.mipmapEnabled))
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ------------------------------ Dibujos de reemplazo (elipses: x, y, radio x, radio y, giro) ------------------------------

    private static Texture2D DrawWolf() => DrawEllipses(new[]
    {
        new Vector4(0.50f, 0.34f, 0.21f, 0.18f),
        new Vector4(0.25f, 0.56f, 0.075f, 0.11f), new Vector4(0.41f, 0.70f, 0.075f, 0.12f),
        new Vector4(0.59f, 0.70f, 0.075f, 0.12f), new Vector4(0.75f, 0.56f, 0.075f, 0.11f),
        // Garras
        new Vector4(0.22f, 0.71f, 0.022f, 0.045f), new Vector4(0.40f, 0.87f, 0.022f, 0.045f),
        new Vector4(0.60f, 0.87f, 0.022f, 0.045f), new Vector4(0.78f, 0.71f, 0.022f, 0.045f),
    });

    private static Texture2D DrawPig() => DrawEllipses(new[]
    {
        // Pezuña partida en dos
        new Vector4(0.36f, 0.50f, 0.115f, 0.28f), new Vector4(0.64f, 0.50f, 0.115f, 0.28f),
    });

    private static Texture2D DrawEllipses(Vector4[] ellipses)
    {
        const int size = 256;
        const float ink = 1.16f;
        var mud = new Color32(0x3A, 0x26, 0x18, 0xE8);
        var outline = new Color32(0x14, 0x0C, 0x08, 0xF5);
        var clear = new Color32(0x14, 0x0C, 0x08, 0x00); // mismo color de la tinta: el borde no sale claro con los mipmaps
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float nearest = float.MaxValue;
                foreach (Vector4 e in ellipses)
                {
                    float dx = (u - e.x) / e.z, dy = (v - e.y) / e.w;
                    nearest = Mathf.Min(nearest, Mathf.Sqrt(dx * dx + dy * dy));
                }
                pixels[y * size + x] = nearest <= 1f ? mud : nearest <= ink ? outline : clear;
            }
        }
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    // ==================================================================
    // Portón, escena y salida
    // ==================================================================

    private static ForestGate SetupGate(Transform root, Vector3[] trail, StringBuilder report)
    {
        GameObject go = GameObject.Find(GateName);
        if (go == null)
        {
            report.AppendLine("✗ No está Gate_Bosque en la escena (Crear kit de bordes y Ampliar mapa): la escena corre sin portón.");
            return null;
        }
        if (!go.TryGetComponent(out ForestGate gate)) gate = go.AddComponent<ForestGate>();

        var missing = new StringBuilder();
        SetChild(gate, "leafLeft", go.transform, "Hoja_Izq", missing);
        SetChild(gate, "leafRight", go.transform, "Hoja_Der", missing);
        SetChild(gate, "chain", go.transform, "Cadena", missing);
        SetChild(gate, "padlock", go.transform, "Candado", missing);
        if (missing.Length > 0) report.AppendLine("✗ Faltan piezas en Gate_Bosque:" + missing);

        // La caja que cierra el paso es la de la raíz; los postes tienen la suya en los hijos
        if (go.TryGetComponent(out BoxCollider blocker)) IntroBookBuilder.SetRef(gate, "blocker", blocker);
        else report.AppendLine("✗ Gate_Bosque no tiene caja en la raíz: no se podría cerrar el paso.");

        var ambience = new GameObject("Ambiente_Bosque");
        ambience.transform.SetParent(root, false);
        ambience.transform.position = trail[trail.Length - 2] + Vector3.up * 1.5f;
        AudioSource source = ambience.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 8f;
        source.maxDistance = 26f;
        IntroBookBuilder.SetRef(gate, "forestAmbience", source);

        report.AppendLine("✓ Gate_Bosque: cerrado con cadena hasta la escena");
        return gate;
    }

    private static void SetChild(ForestGate gate, string property, Transform parent, string childName, StringBuilder missing)
    {
        Transform child = parent.Find(childName);
        if (child == null) missing.Append(' ').Append(childName);
        IntroBookBuilder.SetRef(gate, property, child);
    }

    private static void BuildDirector(Transform root, ForestGate gate, Vector3[] trail, StringBuilder report)
    {
        var go = new GameObject("Huellas_Escena") { layer = IgnoreRaycastLayer };
        go.transform.SetParent(root, false);

        // Todo el rincón detrás del cuarto, de la valla oeste a la cerca lateral y hasta el portón: no se puede rodear
        const float westFence = -10f;
        float eastFence = ContadorYardBuilder.YardFenceX;
        float front = ContadorYardBuilder.ShedPosition.z + 2f;
        float back = ContadorYardBuilder.RinconBackZ - 0.1f;
        go.transform.position = new Vector3((westFence + eastFence) * 0.5f, 1.5f, (front + back) * 0.5f);
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(eastFence - westFence, 3f, back - front);

        var voiceGo = new GameObject("Voz_Wilbur");
        voiceGo.transform.SetParent(go.transform, false);
        AudioSource voice = voiceGo.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;
        voice.dopplerLevel = 0f;

        var look = new GameObject("Mirada_Bosque");
        look.transform.SetParent(go.transform, false);
        look.transform.position = ExitPosition() + Vector3.up * EyeLevel;

        FootprintsSceneDirector director = go.AddComponent<FootprintsSceneDirector>();
        IntroBookBuilder.SetRef(director, "gate", gate);
        IntroBookBuilder.SetRef(director, "forestLook", look.transform);
        IntroBookBuilder.SetRef(director, "voice", voice);
        var so = new SerializedObject(director);
        SerializedProperty points = so.FindProperty("trail");
        points.arraySize = trail.Length;
        for (int i = 0; i < trail.Length; i++) points.GetArrayElementAtIndex(i).vector3Value = trail[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        foreach (System.Type needed in new[] { typeof(Letterbox), typeof(CinematicSubtitles), typeof(HoldToSkip), typeof(ScreenFade) })
            if (Object.FindObjectOfType(needed, true) == null)
                report.AppendLine($"⚠ No hay {needed.Name} en la escena (Intro → Construir taxi y llegada): la escena corre sin eso.");

        report.AppendLine("✓ Escena de las huellas: trigger en el rincón detrás del cuarto");
    }

    private static void BuildExit(Transform root, StringBuilder report)
    {
        var go = new GameObject("Salida_Bosque") { layer = IgnoreRaycastLayer };
        go.transform.SetParent(root, false);
        go.transform.position = ExitPosition() + Vector3.up * 1.5f;
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(7f, 3f, 2f); // de árbol a árbol del sendero
        go.AddComponent<LevelExit>();
        report.AppendLine($"✓ Salida a Campamento en {ExitPosition()}");
    }

    private static Vector3 ExitPosition()
    {
        GameObject anchor = GameObject.Find(ExitAnchorName);
        return anchor != null ? anchor.transform.position : Nivel1MapBuilder.LevelExitPosition;
    }
}
