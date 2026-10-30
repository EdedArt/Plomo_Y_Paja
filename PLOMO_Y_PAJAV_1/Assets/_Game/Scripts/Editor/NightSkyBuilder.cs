using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Cielo nocturno pintado + anillo de fondo + niebla (fase 3A2). Lo usa cada LevelBuilder.
/// - Cúpula con degradado (#1B1F5C arriba, #6B4A7A horizonte) y estrellas; Backdrops/cielo_noche.png si existe.
/// - Luna crema grande con halo y 5 nubes "dibujadas" (manchas suaves con borde de aguada) que giran despacio.
/// - Anillo de fondo: colinas cercanas, colinas lejanas y la silueta de Porcópolis con ventanas encendidas.
/// - Suelo lejano hasta el horizonte (nunca se ve el "fin del mundo") y niebla lineal del color del horizonte.
/// Imágenes opcionales en Assets/_Game/Art/Backdrops/: cielo_noche, luna, nube_01..05, colinas_cerca,
/// colinas_lejos, ciudad (.png). Si faltan se generan proceduralmente en Generated/Textures/.
/// Nada de esto es estático: no entra al NavMesh.
/// </summary>
public static class NightSkyBuilder
{
    public const string BackdropFolder = "Assets/_Game/Art/Backdrops";
    private const string TextureFolder = CartoonKit.MeshFolder + "/Textures";

    public static readonly Color Top = new Color32(0x1B, 0x1F, 0x5C, 0xFF);
    public static readonly Color Horizon = new Color32(0x6B, 0x4A, 0x7A, 0xFF);

    private const float DomeRadius = 190f;

    public static Transform Build(Vector3 center, Vector3 moonDirection)
    {
        Transform sky = LevelBuilderUtils.CreateGroup("--- SKY ---");
        sky.position = center;

        BuildDome(sky, center);
        BuildMoon(sky, center, moonDirection);
        BuildClouds(sky, center);
        BuildRing(sky, center, "Colinas_Lejos", "colinas_lejos", 150f, 30f, -3f, 0.5f, 2450, () => HillsTexture(true));
        BuildRing(sky, center, "Porcopolis", "ciudad", 122f, 34f, -2f, 0.28f, 2451, SkylineTexture);
        BuildRing(sky, center, "Colinas_Cerca", "colinas_cerca", 92f, 18f, -2.5f, 0.12f, 2452, () => HillsTexture(false));
        BuildFarGround(sky, center);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Horizon;
        RenderSettings.fogStartDistance = 22f;
        RenderSettings.fogEndDistance = 170f;

        CartoonKit.MakeNonStatic(sky.gameObject);
        return sky;
    }

    // ==================================================================
    // Piezas
    // ==================================================================

    private static void BuildDome(Transform sky, Vector3 center)
    {
        Mesh mesh = CartoonKit.GetMesh("CieloCupula", b =>
        {
            const int rings = 18;
            var ys = new float[rings];
            var rs = new float[rings];
            for (int i = 0; i < rings; i++)
            {
                float a = Mathf.Lerp(-0.35f, Mathf.PI * 0.5f, i / (rings - 1f));
                ys[i] = Mathf.Sin(a) * DomeRadius;
                rs[i] = Mathf.Cos(a) * DomeRadius;
            }
            b.Lathe(ys, rs, 48, false, false, true);
        });

        Material mat = GetMaterial("Cielo_Cupula", "PlomoYPaja/SkyDome");
        if (mat == null) return;
        mat.SetColor("_TopColor", Top);
        mat.SetColor("_HorizonColor", Horizon);
        Texture2D image = LoadBackdrop("cielo_noche");
        mat.SetTexture("_MainTex", image);
        mat.SetFloat("_UseTexture", image != null ? 1f : 0f);
        GameObject go = CartoonKit.MeshObject("Cielo", mesh, new[] { mat }, sky, center, Quaternion.identity);
        NoShadows(go);
    }

    private static void BuildMoon(Transform sky, Vector3 center, Vector3 moonDirection)
    {
        // Dirección desde la que llega la luz de luna, un poco bajada para que se vea al caminar
        Vector3 dir = -moonDirection.normalized;
        float elevation = Mathf.Clamp(Mathf.Asin(dir.y) * Mathf.Rad2Deg, 22f, 34f);
        Vector3 flat = new Vector3(dir.x, 0f, dir.z).normalized;
        Vector3 d = Quaternion.AngleAxis(-elevation, Vector3.Cross(Vector3.up, flat)) * flat;
        Card(sky, "Luna", "luna", MoonTexture, center + d * 160f, center, 38f, 38f, 2900, 0f, true);
    }

    private static void BuildClouds(Transform sky, Vector3 center)
    {
        Transform clouds = LevelBuilderUtils.CreateGroup("Nubes", sky);
        clouds.position = center;
        clouds.gameObject.AddComponent<CloudDrift>();

        System.Random rng = CartoonKit.Rng("nubes");
        for (int i = 0; i < 5; i++)
        {
            float yaw = i * 72f + CartoonKit.Range(rng, -20f, 20f);
            float elevation = CartoonKit.Range(rng, 10f, 26f);
            float dist = CartoonKit.Range(rng, 135f, 150f);
            Vector3 d = Quaternion.Euler(-elevation, yaw, 0f) * Vector3.forward;
            float w = CartoonKit.Range(rng, 42f, 62f);
            int index = i;
            Card(clouds, "Nube_" + (i + 1), "nube_0" + (i + 1), () => CloudTexture(index), center + d * dist, center, w, w * 0.42f,
                2901 + i, 0.25f, true);
        }
    }

    private static void BuildRing(Transform sky, Vector3 center, string name, string imageName, float radius, float height,
        float baseY, float fogTint, int queue, System.Func<Texture2D> procedural)
    {
        Mesh mesh = CartoonKit.GetMesh($"Anillo_{CartoonKit.Cm(radius)}x{CartoonKit.Cm(height)}", b =>
            b.Lathe(new[] { 0f, height }, new[] { radius, radius }, 64, false, false, true));

        Material mat = GetMaterial("Fondo_" + name, "PlomoYPaja/BackdropCard");
        if (mat == null) return;
        Texture2D tex = LoadBackdrop(imageName);
        if (tex == null) tex = SaveTexture(name, procedural(), TextureWrapMode.Repeat);
        mat.SetTexture("_MainTex", tex);
        mat.SetTextureScale("_MainTex", new Vector2(imageName == "ciudad" ? 3f : 2f, 1f));
        mat.SetColor("_HorizonColor", Horizon);
        mat.SetFloat("_FogTint", fogTint);
        SetCutout(mat, queue);

        GameObject go = CartoonKit.MeshObject(name, mesh, new[] { mat }, sky, center + Vector3.up * baseY, Quaternion.Euler(0f, queue * 37f, 0f));
        NoShadows(go);
    }

    private static void BuildFarGround(Transform sky, Vector3 center)
    {
        Mesh mesh = CartoonKit.GetMesh("SueloLejano", b =>
            b.Lathe(new[] { 0f, 0f, 0f }, new[] { DomeRadius, 60f, 0f }, 48, false, false));
        // Torno plano con radios decrecientes = disco con la cara hacia arriba
        GameObject go = CartoonKit.MeshObject("Suelo_Lejano", mesh, new[] { CartoonKit.M("PastoLejano") }, sky,
            center + Vector3.down * 0.35f, Quaternion.identity);
        NoShadows(go);
    }

    /// <summary>Tarjeta (quad) que mira al centro del nivel.</summary>
    private static void Card(Transform parent, string name, string imageName, System.Func<Texture2D> procedural, Vector3 position,
        Vector3 lookAt, float width, float height, int queue, float fogTint, bool blended)
    {
        Material mat = GetMaterial("Fondo_" + name, "PlomoYPaja/BackdropCard");
        if (mat == null) return;
        Texture2D tex = LoadBackdrop(imageName);
        if (tex == null) tex = SaveTexture(name, procedural(), TextureWrapMode.Clamp);
        mat.SetTexture("_MainTex", tex);
        mat.SetColor("_HorizonColor", Horizon);
        mat.SetFloat("_FogTint", fogTint);
        if (blended) SetBlended(mat, queue);
        else SetCutout(mat, queue);

        GameObject go = CartoonKit.MeshObject(name, Resources.GetBuiltinResource<Mesh>("Quad.fbx"), new[] { mat }, parent, position,
            Quaternion.LookRotation(position - lookAt));
        go.transform.localScale = new Vector3(width, height, 1f);
        NoShadows(go);
    }

    // ==================================================================
    // Materiales y texturas
    // ==================================================================

    private static Material GetMaterial(string name, string shaderName)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogWarning("[NightSky] Falta el shader " + shaderName + ".");
            return null;
        }
        string path = LevelBuilderUtils.MaterialFolder + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
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

    private static void SetCutout(Material mat, int queue)
    {
        mat.SetFloat("_SrcBlend", (float)BlendMode.One);
        mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
        mat.SetFloat("_ZWrite", 1f);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.renderQueue = queue;
    }

    private static void SetBlended(Material mat, int queue)
    {
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cutoff", 0f);
        mat.renderQueue = queue;
    }

    private static void NoShadows(GameObject go)
    {
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    private static Texture2D LoadBackdrop(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>(BackdropFolder + "/" + name + ".png");
    }

    private static Texture2D SaveTexture(string name, Texture2D tex, TextureWrapMode wrap)
    {
        LevelBuilderUtils.EnsureFolder(TextureFolder);
        string path = TextureFolder + "/" + name + ".png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.wrapMode = wrap;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Color32[] Blank(int w, int h, Color32 c)
    {
        var px = new Color32[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = c;
        return px;
    }

    private static Texture2D Finish(int w, int h, Color32[] px)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    /// <summary>Luna crema con cráteres suaves, borde de tinta y halo.</summary>
    private static Texture2D MoonTexture()
    {
        const int size = 256;
        var px = Blank(size, size, new Color32(0, 0, 0, 0));
        Color cream = new Color32(0xF2, 0xE6, 0xC8, 0xFF);
        Color crater = new Color32(0xD8, 0xC6, 0xA0, 0xFF);
        Color ink = new Color32(0x4A, 0x3A, 0x5E, 0xFF);
        Vector2[] craters = { new Vector2(0.42f, 0.58f), new Vector2(0.6f, 0.42f), new Vector2(0.5f, 0.34f), new Vector2(0.36f, 0.4f) };
        float[] craterR = { 0.07f, 0.05f, 0.035f, 0.04f };
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
            float d = Vector2.Distance(p, new Vector2(0.5f, 0.5f));
            const float r = 0.3f;
            Color c;
            if (d < r)
            {
                c = cream;
                for (int i = 0; i < craters.Length; i++)
                    if (Vector2.Distance(p, craters[i]) < craterR[i]) c = Color.Lerp(c, crater, 0.8f);
                c = Color.Lerp(c, ink, Mathf.SmoothStep(0f, 1f, (d - (r - 0.02f)) / 0.02f) * 0.6f);
                c.a = 1f;
            }
            else
            {
                float halo = Mathf.Exp(-(d - r) * 14f) * 0.5f;
                c = new Color(cream.r, cream.g, cream.b, halo);
            }
            px[y * size + x] = c;
        }
        return Finish(size, size, px);
    }

    /// <summary>Nube: suma de círculos suaves (metabolas) con un borde más oscuro tipo aguada y luz de luna arriba.</summary>
    private static Texture2D CloudTexture(int seed)
    {
        const int w = 512, h = 220;
        var px = Blank(w, h, new Color32(0, 0, 0, 0));
        System.Random rng = new System.Random(1000 + seed);
        int blobs = 7 + seed % 3;
        var centers = new Vector2[blobs];
        var radii = new float[blobs];
        for (int i = 0; i < blobs; i++)
        {
            float t = (i + 0.5f) / blobs;
            centers[i] = new Vector2(Mathf.Lerp(0.15f, 0.85f, t), 0.42f + (float)rng.NextDouble() * 0.16f - (Mathf.Abs(t - 0.5f) * 0.1f));
            radii[i] = 0.09f + (float)rng.NextDouble() * 0.08f * (1f - Mathf.Abs(t - 0.5f));
        }
        Color body = new Color32(0x6E, 0x5E, 0x95, 0xFF), light = new Color32(0xB9, 0xA9, 0xD9, 0xFF), ink = new Color32(0x3B, 0x2F, 0x5E, 0xFF);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            Vector2 p = new Vector2((x + 0.5f) / w, (y + 0.5f) / h * (h / (float)w) * 2.3f);
            float f = 0f;
            for (int i = 0; i < blobs; i++)
            {
                Vector2 c = new Vector2(centers[i].x, centers[i].y * (h / (float)w) * 2.3f);
                float r = radii[i];
                f += r * r / Mathf.Max(0.0001f, (p - c).sqrMagnitude);
            }
            f *= 0.55f;
            float wobble = (Mathf.PerlinNoise(x * 0.03f + seed * 7f, y * 0.03f) - 0.5f) * 0.25f;
            f += wobble;
            if (f < 0.85f) continue;
            float alpha = Mathf.SmoothStep(0f, 1f, (f - 0.85f) / 0.25f);
            float edge = 1f - Mathf.SmoothStep(0f, 1f, (f - 0.95f) / 0.5f);
            float top = (float)y / h;
            Color col = Color.Lerp(body, light, Mathf.Clamp01(top * 1.3f - 0.2f) * 0.8f);
            col = Color.Lerp(col, ink, edge * 0.65f);
            col.a = alpha * 0.92f;
            px[y * w + x] = col;
        }
        return Finish(w, h, px);
    }

    /// <summary>Silueta de colinas que se repite sin costura (suma de senos de frecuencia entera).</summary>
    private static Texture2D HillsTexture(bool far)
    {
        const int w = 1024, h = 256;
        var px = Blank(w, h, new Color32(0, 0, 0, 0));
        Color baseCol = far ? (Color)new Color32(0x54, 0x46, 0x78, 0xFF) : new Color32(0x2E, 0x3A, 0x45, 0xFF);
        Color rim = far ? (Color)new Color32(0x7E, 0x6A, 0x9E, 0xFF) : new Color32(0x4A, 0x6A, 0x5A, 0xFF);
        int[] freqs = far ? new[] { 1, 2, 3, 5 } : new[] { 2, 3, 5, 8, 13 };
        float[] phase = { 0.3f, 1.7f, 2.9f, 4.1f, 5.3f };
        for (int x = 0; x < w; x++)
        {
            float u = x / (float)w;
            float n = 0f, norm = 0f;
            for (int i = 0; i < freqs.Length; i++)
            {
                float a = 1f / (i + 1);
                n += Mathf.Sin(u * Mathf.PI * 2f * freqs[i] + phase[i]) * a;
                norm += a;
            }
            float top = Mathf.Lerp(0.35f, 0.85f, n / norm * 0.5f + 0.5f) * h;
            for (int y = 0; y < (int)top; y++)
            {
                float t = y / top;
                Color c = Color.Lerp(baseCol * 0.7f, baseCol, t);
                if (top - y < 4f) c = rim; // filo iluminado por la luna
                c.a = 1f;
                px[y * w + x] = c;
            }
        }
        return Finish(w, h, px);
    }

    /// <summary>Silueta de Porcópolis: edificios, techos en punta, tanques de agua y ventanas encendidas.</summary>
    private static Texture2D SkylineTexture()
    {
        const int w = 2048, h = 256;
        var px = Blank(w, h, new Color32(0, 0, 0, 0));
        System.Random rng = new System.Random(4242);
        Color[] bodies = { new Color32(0x2B, 0x24, 0x40, 0xFF), new Color32(0x35, 0x2B, 0x4D, 0xFF), new Color32(0x24, 0x22, 0x38, 0xFF) };
        Color lit = new Color32(0xFF, 0xD2, 0x7A, 0xFF), dark = new Color32(0x1A, 0x16, 0x28, 0xFF);

        int x = 0;
        while (x < w)
        {
            int bw = 24 + rng.Next(48);
            int bh = 50 + rng.Next(140);
            Color body = bodies[rng.Next(bodies.Length)];
            int roof = rng.Next(4); // 0 plano, 1 punta, 2 tanque, 3 escalonado
            for (int i = 0; i < bw; i++)
            {
                int px0 = (x + i) % w; // envuelve: textura sin costura
                int top = bh;
                if (roof == 1) top = bh + (int)((bw * 0.5f - Mathf.Abs(i - bw * 0.5f)) * 0.8f);
                if (roof == 3 && (i < bw / 4 || i > bw * 3 / 4)) top = bh - 12;
                for (int y = 0; y < Mathf.Min(top, h); y++) px[y * w + px0] = body;
                if (roof == 2 && i > bw / 3 && i < bw * 2 / 3)
                    for (int y = bh; y < Mathf.Min(bh + 16, h); y++) px[y * w + px0] = body;
            }
            // Ventanas
            for (int wy = 10; wy < bh - 10; wy += 12)
            for (int wx = 4; wx < bw - 6; wx += 9)
            {
                Color c = rng.NextDouble() < 0.33 ? lit : dark;
                for (int yy = 0; yy < 6; yy++)
                for (int xx = 0; xx < 4; xx++)
                    px[(wy + yy) * w + (x + wx + xx) % w] = c;
            }
            x += bw + rng.Next(6);
        }
        return Finish(w, h, px);
    }
}
