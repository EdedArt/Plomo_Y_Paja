using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Kit modular caricaturesco (fase 3A2): todo gordito, redondeado y un poco irregular, con semilla fija.
/// Las mallas se guardan como assets en Assets/_Game/Art/Environment/Generated/ (las escenas no cargan
/// mallas enormes en línea). Archivos:
///   CartoonKit.cs           — constructor de mallas, caché, paleta, cajas redondeadas
///   CartoonKit.Structure.cs — paredes con zócalo/paja, marcos de puerta con arco, puertas, ventanas, pisos
///   CartoonKit.Props.cs     — barriles, cajas, mesas, sillas, lámparas, alfombras, plantas, cerca, faroles,
///                             techo de paja, terreno, adoquines, caminos, fachadas
/// Modelos externos: si existe Assets/_Game/Art/Environment/Models/&lt;nombre&gt;.fbx|.glb con un nombre del kit,
/// se usa en lugar del procedural (ver TryModel).
/// </summary>
public static partial class CartoonKit
{
    public const string MeshFolder = "Assets/_Game/Art/Environment/Generated";

    // ==================================================================
    // Constructor de mallas
    // ==================================================================

    /// <summary>Acumula vértices y triángulos (con submallas) bajo una matriz de transformación actual.</summary>
    public class KitMesh
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector3> N = new List<Vector3>();
        public readonly List<Vector2> UV = new List<Vector2>();
        public readonly List<List<int>> Sub = new List<List<int>> { new List<int>() };

        private int _sub;
        private Matrix4x4 _m = Matrix4x4.identity;
        private Matrix4x4 _nm = Matrix4x4.identity;

        public int Submesh
        {
            get => _sub;
            set
            {
                _sub = value;
                while (Sub.Count <= value) Sub.Add(new List<int>());
            }
        }

        public Matrix4x4 Matrix
        {
            get => _m;
            set
            {
                _m = value;
                _nm = value.inverse.transpose;
            }
        }

        public void SetTRS(Vector3 pos, Quaternion rot, Vector3 scale) => Matrix = Matrix4x4.TRS(pos, rot, scale);
        public void SetTRS(Vector3 pos, Quaternion rot) => Matrix = Matrix4x4.TRS(pos, rot, Vector3.one);
        public void ResetMatrix() => Matrix = Matrix4x4.identity;

        public int Vertex(Vector3 p, Vector3 n, Vector2 uv)
        {
            V.Add(_m.MultiplyPoint3x4(p));
            N.Add(_nm.MultiplyVector(n).normalized);
            UV.Add(uv);
            return V.Count - 1;
        }

        public void Tri(int a, int b, int c)
        {
            List<int> t = Sub[_sub];
            t.Add(a); t.Add(b); t.Add(c);
        }

        /// <summary>
        /// Caja redondeada centrada en el origen. bevel = radio de las aristas, seg = subdivisiones del bisel,
        /// jitter = irregularidad (m) con semilla. Los vértices compartidos entre caras se mueven igual.
        /// </summary>
        public void RoundedBox(Vector3 size, float bevel, int seg = 2, float jitter = 0f, int seed = 0)
        {
            Vector3 h = size * 0.5f;
            float minHalf = Mathf.Min(h.x, Mathf.Min(h.y, h.z));
            float r = Mathf.Clamp(bevel, 0f, minHalf * 0.95f);
            if (r < 0.002f) { r = 0f; seg = 0; }
            seg = Mathf.Max(seg, r > 0f ? 1 : 0);

            float[][] coords = { Coords(h.x, r, seg), Coords(h.y, r, seg), Coords(h.z, r, seg) };
            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3, w = (axis + 2) % 3;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    float[] cu = coords[u], cw = coords[w];
                    int baseIndex = V.Count;
                    for (int j = 0; j < cw.Length; j++)
                    for (int i = 0; i < cu.Length; i++)
                    {
                        Vector3 p = Vector3.zero;
                        p[axis] = sign * h[axis];
                        p[u] = cu[i];
                        p[w] = cw[j];

                        Vector3 inner = new Vector3(
                            Mathf.Clamp(p.x, -h.x + r, h.x - r),
                            Mathf.Clamp(p.y, -h.y + r, h.y - r),
                            Mathf.Clamp(p.z, -h.z + r, h.z - r));
                        Vector3 d = p - inner;
                        Vector3 faceN = Vector3.zero;
                        faceN[axis] = sign;
                        Vector3 n = d.sqrMagnitude > 1e-10f ? d.normalized : faceN;
                        Vector3 pos = r > 0f ? inner + n * r : p;
                        if (jitter > 0f) pos += Jitter(p, seed) * jitter;
                        Vertex(pos, n, new Vector2(p[u], p[w]));
                    }

                    int cols = cu.Length;
                    for (int j = 0; j < cw.Length - 1; j++)
                    for (int i = 0; i < cols - 1; i++)
                    {
                        int a = baseIndex + j * cols + i;
                        int b = a + 1, c = a + cols + 1, dd = a + cols;
                        if (sign > 0) { Tri(a, b, c); Tri(a, c, dd); }
                        else { Tri(a, c, b); Tri(a, dd, c); }
                    }
                }
            }
        }

        private static float[] Coords(float half, float r, int seg)
        {
            if (r <= 0f) return new[] { -half, half };
            var list = new List<float>();
            for (int i = 0; i <= seg; i++) list.Add(-half + r * i / seg);
            for (int i = 0; i <= seg; i++) list.Add(half - r + r * i / seg);
            return list.ToArray();
        }

        /// <summary>
        /// Sólido de revolución alrededor de Y. ys/rs = perfil (de abajo a arriba). inward = normales hacia dentro
        /// (cúpula del cielo, anillo de fondo).
        /// </summary>
        public void Lathe(float[] ys, float[] rs, int sides, bool capBottom, bool capTop, bool inward = false,
            float startAngle = 0f)
        {
            int rings = ys.Length;
            int baseIndex = V.Count;
            for (int i = 0; i < rings; i++)
            {
                int i0 = Mathf.Max(0, i - 1), i1 = Mathf.Min(rings - 1, i + 1);
                float dy = ys[i1] - ys[i0], dr = rs[i1] - rs[i0];
                for (int k = 0; k <= sides; k++)
                {
                    float ang = startAngle * Mathf.Deg2Rad + k / (float)sides * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    Vector3 n = (dir * dy - Vector3.up * dr).normalized;
                    if (n.sqrMagnitude < 1e-6f) n = dir;
                    if (inward) n = -n;
                    Vertex(dir * rs[i] + Vector3.up * ys[i], n, new Vector2(k / (float)sides, rings > 1 ? i / (float)(rings - 1) : 0f));
                }
            }

            int stride = sides + 1;
            for (int i = 0; i < rings - 1; i++)
            for (int k = 0; k < sides; k++)
            {
                int a = baseIndex + i * stride + k, b = a + 1, c = a + stride, d = c + 1;
                if (!inward) { Tri(a, c, b); Tri(b, c, d); }
                else { Tri(a, b, c); Tri(b, d, c); }
            }

            if (capBottom) Cap(ys[0], rs[0], sides, false, startAngle);
            if (capTop) Cap(ys[rings - 1], rs[rings - 1], sides, true, startAngle);
        }

        private void Cap(float y, float radius, int sides, bool top, float startAngle)
        {
            if (radius < 0.0005f) return;
            Vector3 n = top ? Vector3.up : Vector3.down;
            int center = Vertex(Vector3.up * y, n, new Vector2(0.5f, 0.5f));
            int first = V.Count;
            for (int k = 0; k <= sides; k++)
            {
                float ang = startAngle * Mathf.Deg2Rad + k / (float)sides * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                Vertex(dir * radius + Vector3.up * y, n, new Vector2(0.5f + dir.x * 0.5f, 0.5f + dir.z * 0.5f));
            }
            for (int k = 0; k < sides; k++)
            {
                if (top) Tri(center, first + k + 1, first + k);
                else Tri(center, first + k, first + k + 1);
            }
        }

        /// <summary>Polígono plano de doble cara (hojas, flecos de paja). Puntos en orden, se triangula en abanico.</summary>
        public void Card(Vector3[] points, Vector3 normal)
        {
            int count = points.Length;
            // Que la cara "de frente" tenga la normal pedida, sea cual sea el orden de los puntos
            if (count >= 3 && Vector3.Dot(Vector3.Cross(points[1] - points[0], points[2] - points[0]), normal) < 0f)
                normal = -normal;
            int front = V.Count;
            for (int i = 0; i < count; i++) Vertex(points[i], normal, new Vector2(i / (float)count, 0f));
            int back = V.Count;
            for (int i = 0; i < count; i++) Vertex(points[i], -normal, new Vector2(i / (float)count, 0f));
            for (int i = 1; i < count - 1; i++)
            {
                Tri(front, front + i, front + i + 1);
                Tri(back, back + i + 1, back + i);
            }
        }

        public void ApplyTo(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(V);
            mesh.SetNormals(N);
            mesh.SetUVs(0, UV);
            mesh.subMeshCount = Sub.Count;
            for (int i = 0; i < Sub.Count; i++) mesh.SetTriangles(Sub[i], i);
            mesh.RecalculateBounds();
        }
    }

    // ==================================================================
    // Azar determinista
    // ==================================================================

    public static float Hash(float x, float y, float z, int seed)
    {
        float h = Mathf.Sin(x * 127.1f + y * 311.7f + z * 74.7f + seed * 13.37f) * 43758.5453f;
        return h - Mathf.Floor(h);
    }

    private static Vector3 Jitter(Vector3 p, int seed)
    {
        float x = Mathf.Round(p.x * 1000f) / 1000f, y = Mathf.Round(p.y * 1000f) / 1000f, z = Mathf.Round(p.z * 1000f) / 1000f;
        return new Vector3(Hash(x, y, z, seed) - 0.5f, Hash(y, z, x, seed + 7) - 0.5f, Hash(z, x, y, seed + 13) - 0.5f) * 2f;
    }

    /// <summary>System.Random con semilla fija por nombre (mismo resultado en cada generación).</summary>
    public static System.Random Rng(string key)
    {
        int seed = 17;
        foreach (char c in key) seed = seed * 31 + c;
        return new System.Random(seed);
    }

    public static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

    // ==================================================================
    // Caché de mallas (assets)
    // ==================================================================

    private static readonly Dictionary<string, Mesh> SessionMeshes = new Dictionary<string, Mesh>();
    private static readonly Dictionary<string, Material> SessionMaterials = new Dictionary<string, Material>();

    /// <summary>Al empezar a generar un nivel: las mallas se reconstruyen una vez por generación.</summary>
    public static void BeginSession()
    {
        SessionMeshes.Clear();
        SessionMaterials.Clear();
    }

    public static Mesh GetMesh(string key, Action<KitMesh> build)
    {
        if (SessionMeshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;

        LevelBuilderUtils.EnsureFolder(MeshFolder);
        string path = MeshFolder + "/" + key + ".asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh { name = key };

        var builder = new KitMesh();
        build(builder);
        builder.ApplyTo(mesh);

        if (isNew) AssetDatabase.CreateAsset(mesh, path);
        else EditorUtility.SetDirty(mesh);
        SessionMeshes[key] = mesh;
        return mesh;
    }

    /// <summary>Número para nombres de archivo (centímetros enteros).</summary>
    public static string Cm(float v) => Mathf.RoundToInt(v * 100f).ToString();

    // ==================================================================
    // Paleta del kit (materiales CartoonEnvironment en Assets/Materials/Greybox/Kit_*.mat)
    // ==================================================================

    private static readonly Dictionary<string, Color> Palette = new Dictionary<string, Color>
    {
        { "MaderaOscura", new Color32(0x5A, 0x3A, 0x22, 0xFF) },
        { "Madera", new Color32(0x8A, 0x5A, 0x33, 0xFF) },
        { "MaderaClara", new Color32(0xB0, 0x7A, 0x45, 0xFF) },
        { "Yeso", new Color32(0xE9, 0xD9, 0xB0, 0xFF) },
        { "Paja", new Color32(0xE8, 0xC3, 0x5A, 0xFF) },
        { "PajaOscura", new Color32(0xB8, 0x91, 0x3A, 0xFF) },
        { "Cuerda", new Color32(0x9C, 0x7B, 0x4E, 0xFF) },
        { "Metal", new Color32(0x4A, 0x4F, 0x5C, 0xFF) },
        { "Laton", new Color32(0xD4, 0xA2, 0x3A, 0xFF) },
        { "Alfombra", new Color32(0xB0, 0x3A, 0x2E, 0xFF) },
        { "AlfombraBorde", new Color32(0x2A, 0x9D, 0x8F, 0xFF) },
        { "Maceta", new Color32(0xC0, 0x62, 0x3A, 0xFF) },
        { "Hoja", new Color32(0x4F, 0x8A, 0x3A, 0xFF) },
        { "Tierra", new Color32(0x8A, 0x6A, 0x45, 0xFF) },
        { "Piedra", new Color32(0x6C, 0x7A, 0x96, 0xFF) },
        { "PiedraOscura", new Color32(0x4E, 0x58, 0x72, 0xFF) },
        { "Sofa", new Color32(0x7A, 0x3B, 0x5C, 0xFF) },
        { "Cojin", new Color32(0xD9, 0x8C, 0x3F, 0xFF) },
        { "Puerta", new Color32(0xC0, 0x39, 0x2B, 0xFF) },
        { "Ventana", new Color32(0x2A, 0x9D, 0x8F, 0xFF) },
        { "Nevera", new Color32(0xE6, 0xE0, 0xC8, 0xFF) },
        { "Ladrillo", new Color32(0x8C, 0x4A, 0x3A, 0xFF) },
        { "Revoque", new Color32(0x9A, 0x8A, 0xA8, 0xFF) },
        { "RevoqueCalido", new Color32(0xC9, 0x8E, 0x5A, 0xFF) },
        { "TechoEdificio", new Color32(0x3A, 0x3F, 0x5A, 0xFF) },
        { "Pasto", new Color32(0x5E, 0x8C, 0x3A, 0xFF) },
        { "PastoLejano", new Color32(0x3E, 0x5A, 0x3A, 0xFF) },
    };

    private static readonly Dictionary<string, Color> Emissive = new Dictionary<string, Color>
    {
        { "VidrioCalido", new Color(1.6f, 0.95f, 0.45f) },
        { "Bombilla", new Color(3.2f, 2.1f, 1.1f) },
        { "VentanaApagada", new Color(0.05f, 0.04f, 0.08f) },
    };

    /// <summary>Material del kit por nombre (ver Palette/Emissive). Desconocido = gris.</summary>
    public static Material M(string name)
    {
        if (SessionMaterials.TryGetValue(name, out Material cached) && cached != null) return cached;

        Material mat;
        if (Emissive.TryGetValue(name, out Color emission))
            mat = LevelBuilderUtils.GetOrCreateEmissiveMaterial("Kit_" + name, new Color(0.2f, 0.15f, 0.1f), emission);
        else
            mat = LevelBuilderUtils.GetOrCreateMaterial("Kit_" + name, Palette.TryGetValue(name, out Color c) ? c : Color.gray, 0f, true);

        if (mat.HasProperty("_PaintNoise")) mat.SetFloat("_PaintNoise", name.StartsWith("Pasto") || name == "Tierra" ? 0.6f : 0.35f);
        SessionMaterials[name] = mat;
        return mat;
    }

    // ==================================================================
    // Objetos
    // ==================================================================

    public static GameObject MeshObject(string name, Mesh mesh, Material[] mats, Transform parent, Vector3 position,
        Quaternion rotation)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, rotation);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = mats;
        return go;
    }

    /// <summary>Malla de caja redondeada del tamaño exacto (sin escalar el transform, así el bisel no se deforma).</summary>
    public static Mesh RoundedBoxMesh(Vector3 size, float bevel = -1f, int seg = 2, float jitter = 0f, int seed = 0)
    {
        if (bevel < 0f) bevel = DefaultBevel(size);
        string key = $"RBox_{Cm(size.x)}x{Cm(size.y)}x{Cm(size.z)}_b{Mathf.RoundToInt(bevel * 1000f)}_s{seg}" +
                     (jitter > 0f ? $"_j{Mathf.RoundToInt(jitter * 1000f)}_{seed}" : "");
        return GetMesh(key, b => b.RoundedBox(size, bevel, seg, jitter, seed));
    }

    public static float DefaultBevel(Vector3 size)
    {
        float min = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
        return Mathf.Min(0.06f, min * 0.3f);
    }

    /// <summary>Caja redondeada con BoxCollider (reemplaza a los cubos del greybox).</summary>
    public static GameObject Box(string name, Vector3 center, Vector3 size, Material mat, Transform parent,
        Vector3 euler = default, float bevel = -1f, bool collider = true, float jitter = 0f, int seed = 0)
    {
        Mesh mesh = RoundedBoxMesh(size, bevel, 2, jitter, seed);
        GameObject go = MeshObject(name, mesh, new[] { mat }, parent, center, Quaternion.Euler(euler));
        if (collider) go.AddComponent<BoxCollider>().size = size;
        return go;
    }

    /// <summary>Añade una caja redondeada al builder en la posición/rotación local dada.</summary>
    public static void AddBox(KitMesh b, Vector3 center, Vector3 size, Quaternion rot, float bevel = -1f, int seg = 1,
        float jitter = 0f, int seed = 0)
    {
        Matrix4x4 saved = b.Matrix;
        b.Matrix = saved * Matrix4x4.TRS(center, rot, Vector3.one);
        b.RoundedBox(size, bevel < 0f ? DefaultBevel(size) : bevel, seg, jitter, seed);
        b.Matrix = saved;
    }

    public static void AddBox(KitMesh b, Vector3 center, Vector3 size) => AddBox(b, center, size, Quaternion.identity);

    /// <summary>Añade un sólido de revolución en la posición local dada (eje Y del objeto).</summary>
    public static void AddLathe(KitMesh b, Vector3 position, Quaternion rot, float[] ys, float[] rs, int sides,
        bool capBottom = true, bool capTop = true)
    {
        Matrix4x4 saved = b.Matrix;
        b.Matrix = saved * Matrix4x4.TRS(position, rot, Vector3.one);
        b.Lathe(ys, rs, sides, capBottom, capTop);
        b.Matrix = saved;
    }

    /// <summary>Collider de caja ajustado a los bounds de la malla (props).</summary>
    public static BoxCollider FitCollider(GameObject go)
    {
        MeshFilter mf = go.GetComponent<MeshFilter>();
        BoxCollider box = go.AddComponent<BoxCollider>();
        if (mf != null && mf.sharedMesh != null)
        {
            box.center = mf.sharedMesh.bounds.center;
            box.size = mf.sharedMesh.bounds.size;
        }
        return box;
    }

    /// <summary>Saca un objeto (y sus hijos) del horneado del NavMesh y de "static" (luces falsas, cielo).</summary>
    public static void MakeNonStatic(GameObject go)
    {
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
    }
}
