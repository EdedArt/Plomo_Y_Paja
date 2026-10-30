using System.Collections.Generic;
using UnityEngine;
using static LevelBuilderUtils;

/// <summary>Kit: paredes, marcos de puerta con arco, puertas, ventanas y pisos de tablas.</summary>
public static partial class CartoonKit
{
    /// <summary>Qué lleva cada cara de una pared.</summary>
    public enum WallFace
    {
        None,
        /// <summary>Zócalo de madera oscura (1 m) con paneles, moldura y rodapié — interiores.</summary>
        Wainscot,
        /// <summary>Fardos de paja apilados y atados — exterior de la casa de Porky.</summary>
        Straw,
        /// <summary>Solo rodapié y remate arriba — muros de la calle y el callejón.</summary>
        Plain
    }

    private const int SubCore = 0, SubDarkWood = 1, SubTrim = 2, SubStraw = 3, SubStrawDark = 4, SubRope = 5, SubPaper = 6;

    /// <summary>
    /// Pared del kit de start a end. faceRight/faceLeft = acabado de cada lado (derecha = a la derecha de start→end).
    /// Mismos huecos que CreateWallWithOpenings; las puertas (hueco más bajo que la pared y de hasta 2.2 m)
    /// reciben un marco con arco redondo. Colliders: una caja por tramo.
    /// </summary>
    public static Transform Wall(string name, Vector3 start, Vector3 end, float height, float thickness, Material core,
        WallFace faceRight, WallFace faceLeft, Transform parent, params WallOpening[] openings)
    {
        return Wall(name, start, end, height, thickness, core, faceRight, faceLeft, parent, null, openings);
    }

    /// <summary>
    /// Igual, con "holes": rectángulos (x = distancia desde start, y = altura) donde la paja no se dibuja, para
    /// que las ventanas decorativas queden hundidas entre los fardos.
    /// </summary>
    public static Transform Wall(string name, Vector3 start, Vector3 end, float height, float thickness, Material core,
        WallFace faceRight, WallFace faceLeft, Transform parent, Rect[] holes, params WallOpening[] openings)
    {
        Transform wall = CreateGroup(name, parent);
        Vector3 delta = end - start;
        delta.y = 0f;
        float length = delta.magnitude;
        if (length < 0.01f) return wall;

        Vector3 dir = delta / length;
        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
        wall.SetPositionAndRotation(start, rot);

        List<Vector4> segments = WallSegments(length, height, openings);
        int index = 0;
        foreach (Vector4 seg in segments)
        {
            float segLength = seg.y - seg.x;
            Vector3 center = start + dir * (seg.x + segLength * 0.5f) + Vector3.up * ((seg.z + seg.w) * 0.5f);
            Box($"{name}_{(seg.z > 0f ? "Dintel" : "Tramo")}{index++}", center, new Vector3(thickness, seg.w - seg.z, segLength),
                core, wall, rot.eulerAngles, 0.03f);
        }

        // Decoración: una sola malla por pared con submallas (núcleo, madera oscura, moldura, paja, paja oscura, cuerda, empapelado)
        string key = "Pared_" + name + "_" + Cm(length) + "_" + Cm(height) + "_" + (int)faceRight + (int)faceLeft +
                     (holes != null ? "_h" + holes.Length : "");
        Mesh decor = GetMesh(key, b =>
        {
            System.Random rng = Rng(key);
            foreach (Vector4 seg in segments)
            {
                BuildFace(b, seg, thickness, +1, faceRight, rng, holes);
                BuildFace(b, seg, thickness, -1, faceLeft, rng, holes);
            }
            if (openings != null)
                foreach (WallOpening o in openings)
                    if (o.height < height - 0.01f && o.width <= 2.2f)
                        BuildDoorFrame(b, o, thickness, faceRight == WallFace.Wainscot, faceLeft == WallFace.Wainscot);
            for (int s = 0; s <= SubPaper; s++) b.Submesh = s; // siempre 7 submallas
        });
        MeshObject(name + "_Acabado", decor,
            new[] { core, M("MaderaOscura"), M("MaderaClara"), M("Paja"), M("PajaOscura"), M("Cuerda"), M("Empapelado") },
            wall, start, rot);
        return wall;
    }

    /// <summary>Acabado de una cara de un tramo (coordenadas locales: z a lo largo de la pared, x hacia la cara).</summary>
    private static void BuildFace(KitMesh b, Vector4 seg, float thickness, int side, WallFace face, System.Random rng,
        Rect[] holes)
    {
        if (face == WallFace.None) return;
        float from = seg.x + 0.01f, to = seg.y - 0.01f, bottom = seg.z, top = seg.w;
        float len = to - from;
        if (len < 0.05f) return;
        float surface = side * thickness * 0.5f;
        float mid = (from + to) * 0.5f;

        switch (face)
        {
            case WallFace.Wainscot:
                // Empapelado verde grisáceo sobre el zócalo (y sobre los dinteles): el interior deja de ser todo naranja
                float paperBottom = bottom > 0.01f ? bottom : Mathf.Min(1f, top);
                if (top - paperBottom > 0.02f)
                {
                    b.Submesh = SubPaper;
                    AddBox(b, new Vector3(surface + side * 0.012f, (paperBottom + top) * 0.5f, mid),
                        new Vector3(0.024f, top - paperBottom, len), Quaternion.identity, 0.004f, 1);
                }
                if (bottom > 0.01f) return; // dinteles: sin zócalo
                float panelTop = Mathf.Min(1f, top);
                b.Submesh = SubDarkWood;
                AddBox(b, new Vector3(surface + side * 0.02f, panelTop * 0.5f, mid), new Vector3(0.04f, panelTop, len), Quaternion.identity, 0.012f);
                // Paneles en relieve
                int panels = Mathf.Max(1, Mathf.FloorToInt(len / 0.9f));
                float step = len / panels;
                for (int i = 0; i < panels; i++)
                {
                    float z = from + step * (i + 0.5f);
                    AddBox(b, new Vector3(surface + side * 0.05f, 0.55f, z), new Vector3(0.03f, 0.55f, step - 0.22f), Quaternion.identity, 0.012f);
                }
                b.Submesh = SubTrim;
                AddBox(b, new Vector3(surface + side * 0.035f, panelTop, mid), new Vector3(0.07f, 0.08f, len), Quaternion.identity, 0.025f);
                AddBox(b, new Vector3(surface + side * 0.03f, 0.075f, mid), new Vector3(0.06f, 0.15f, len), Quaternion.identity, 0.02f);
                break;

            case WallFace.Plain:
                b.Submesh = SubTrim;
                if (bottom < 0.01f)
                    AddBox(b, new Vector3(surface + side * 0.03f, 0.1f, mid), new Vector3(0.06f, 0.2f, len), Quaternion.identity, 0.02f);
                AddBox(b, new Vector3(surface + side * 0.04f, top - 0.06f, mid), new Vector3(0.08f, 0.12f, len), Quaternion.identity, 0.03f);
                break;

            case WallFace.Straw:
                const float rowHeight = 0.34f;
                int rows = Mathf.Max(1, Mathf.RoundToInt((top - bottom) / rowHeight));
                float rh = (top - bottom) / rows;
                for (int row = 0; row < rows; row++)
                {
                    float y = bottom + rh * (row + 0.5f);
                    float radius = rh * 0.58f;
                    float z = from - (row % 2) * 0.45f; // escalonado
                    int n = 0;
                    while (z < to)
                    {
                        float bundle = Range(rng, 0.85f, 1.25f);
                        float z0 = Mathf.Max(z, from), z1 = Mathf.Min(z + bundle, to);
                        if (z1 - z0 > 0.12f && !InHole(holes, z0, z1, y, radius))
                        {
                            b.Submesh = (n + row) % 3 == 0 ? SubStrawDark : SubStraw;
                            float r = radius * Range(rng, 0.9f, 1.08f);
                            StrawBundle(b, new Vector3(surface + side * r * 0.55f, y, (z0 + z1) * 0.5f), z1 - z0, r,
                                (n + row) % 3 == 1, Quaternion.identity);
                        }
                        z += bundle;
                        n++;
                    }
                }
                break;
        }
    }

    private static bool InHole(Rect[] holes, float z0, float z1, float y, float radius)
    {
        if (holes == null) return false;
        foreach (Rect h in holes)
            if (z1 > h.xMin && z0 < h.xMax && y + radius * 0.6f > h.yMin && y - radius * 0.6f < h.yMax) return true;
        return false;
    }

    /// <summary>Fardo de paja acostado a lo largo de Z local (cilindro abultado), con dos cuerdas si tied.</summary>
    public static void StrawBundle(KitMesh b, Vector3 center, float length, float radius, bool tied, Quaternion rot)
    {
        int sub = b.Submesh;
        float half = length * 0.5f;
        Quaternion axis = rot * Quaternion.Euler(90f, 0f, 0f); // Y del torno -> Z
        float[] ys = { -half, -half * 0.92f, -half * 0.55f, 0f, half * 0.55f, half * 0.92f, half };
        float[] rs = { radius * 0.55f, radius * 0.88f, radius, radius * 0.96f, radius, radius * 0.88f, radius * 0.55f };
        AddLathe(b, center, axis, ys, rs, 9);
        if (!tied) return;
        b.Submesh = SubRope;
        for (int i = -1; i <= 1; i += 2)
        {
            float y = i * half * 0.55f;
            AddLathe(b, center, axis, new[] { y - 0.025f, y + 0.025f }, new[] { radius * 1.03f, radius * 1.03f }, 9, false, false);
        }
        b.Submesh = sub;
    }

    /// <summary>
    /// Marco de puerta gordito con arco de medio punto: jambas, dovelas del arco y los rellenos de las esquinas
    /// (del color de la pared) que convierten el hueco rectangular en un arco.
    /// </summary>
    private static void BuildDoorFrame(KitMesh b, WallOpening o, float thickness, bool paperRight, bool paperLeft)
    {
        float w = o.width, hgt = o.height;
        float radius = w * 0.5f;
        float spring = hgt - radius;     // altura donde empieza el arco
        float depth = thickness + 0.12f;
        const float trim = 0.14f;

        b.Submesh = SubTrim;
        // Jambas
        for (int s = -1; s <= 1; s += 2)
        {
            float z = o.center + s * (radius + trim * 0.35f);
            AddBox(b, new Vector3(0f, spring * 0.5f, z), new Vector3(depth, spring, trim), Quaternion.identity, 0.04f);
        }
        // Arco en 9 dovelas
        const int pieces = 9;
        for (int i = 0; i < pieces; i++)
        {
            float a = Mathf.PI * (i + 0.5f) / pieces;
            float r = radius + trim * 0.35f;
            Vector3 p = new Vector3(0f, spring + Mathf.Sin(a) * r, o.center - Mathf.Cos(a) * r);
            Quaternion q = Quaternion.Euler(-(90f - a * Mathf.Rad2Deg), 0f, 0f);
            AddBox(b, p, new Vector3(depth, trim, Mathf.PI * r / pieces + 0.05f), q, 0.04f);
        }

        // Rellenos de las esquinas superiores (entre el arco y el dintel), ambas caras
        const int steps = 8;
        for (int s = -1; s <= 1; s += 2)
        {
            // La cara con empapelado rellena las esquinas del arco con el mismo papel
            b.Submesh = (s > 0 ? paperRight : paperLeft) ? SubPaper : SubCore;
            float x = s * thickness * 0.5f;
            Vector3 normal = new Vector3(s, 0f, 0f);
            for (int corner = -1; corner <= 1; corner += 2)
            {
                var pts = new List<Vector3> { new Vector3(x, hgt, o.center + corner * radius) };
                for (int i = 0; i <= steps; i++)
                {
                    float a = Mathf.PI * 0.5f * i / steps; // de 0 (lado) a 90° (arriba)
                    pts.Add(new Vector3(x, spring + Mathf.Sin(a) * radius, o.center + corner * Mathf.Cos(a) * radius));
                }
                b.Card(pts.ToArray(), normal);
            }
        }
    }

    /// <summary>Hoja de puerta con 4 paneles en relieve y un pomo grande y redondo. position = centro de la hoja.</summary>
    public static GameObject DoorLeaf(string name, Vector3 position, Quaternion rotation, float width, float height,
        Material leafMat, Transform parent)
    {
        string key = $"Puerta_{Cm(width)}x{Cm(height)}";
        Mesh mesh = GetMesh(key, b =>
        {
            const float t = 0.07f;
            b.Submesh = 0;
            AddBox(b, Vector3.zero, new Vector3(width, height, t), Quaternion.identity, 0.03f, 2);
            for (int side = -1; side <= 1; side += 2)
            for (int row = 0; row < 2; row++)
            for (int col = -1; col <= 1; col += 2)
            {
                float py = -height * 0.5f + height * (row == 0 ? 0.27f : 0.7f);
                float ph = height * (row == 0 ? 0.36f : 0.4f);
                AddBox(b, new Vector3(col * width * 0.22f, py, side * (t * 0.5f + 0.012f)),
                    new Vector3(width * 0.34f, ph, 0.025f), Quaternion.identity, 0.01f);
            }
            b.Submesh = 1;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 knob = new Vector3(width * 0.36f, -height * 0.04f, side * (t * 0.5f + 0.05f));
                AddLathe(b, knob, Quaternion.Euler(side * 90f, 0f, 0f), new[] { -0.05f, -0.03f, 0f, 0.03f, 0.05f },
                    new[] { 0.025f, 0.06f, 0.075f, 0.06f, 0.0f }, 10);
            }
        });
        GameObject go = MeshObject(name, mesh, new[] { leafMat, M("Laton") }, parent, position, rotation);
        go.AddComponent<BoxCollider>().size = new Vector3(width, height, 0.1f);
        return go;
    }

    /// <summary>Ventana: marco grueso redondeado con cruz y un vidrio cálido emisivo detrás. +Z local = hacia fuera.</summary>
    public static GameObject WindowFrame(string name, Vector3 center, Quaternion rotation, float width, float height,
        Transform parent, bool lit = true)
    {
        string key = $"Ventana_{Cm(width)}x{Cm(height)}";
        Mesh mesh = GetMesh(key, b =>
        {
            const float bar = 0.13f, depth = 0.14f;
            b.Submesh = 0;
            AddBox(b, new Vector3(0f, height * 0.5f, 0f), new Vector3(width + bar, bar, depth), Quaternion.identity, 0.05f);
            AddBox(b, new Vector3(0f, -height * 0.5f, 0.02f), new Vector3(width + bar * 1.6f, bar * 1.1f, depth + 0.06f), Quaternion.identity, 0.05f);
            AddBox(b, new Vector3(-width * 0.5f, 0f, 0f), new Vector3(bar, height, depth), Quaternion.identity, 0.05f);
            AddBox(b, new Vector3(width * 0.5f, 0f, 0f), new Vector3(bar, height, depth), Quaternion.identity, 0.05f);
            AddBox(b, Vector3.zero, new Vector3(width, bar * 0.6f, depth * 0.7f), Quaternion.identity, 0.03f);
            AddBox(b, Vector3.zero, new Vector3(bar * 0.6f, height, depth * 0.7f), Quaternion.identity, 0.03f);
            b.Submesh = 1;
            AddBox(b, new Vector3(0f, 0f, -depth * 0.3f), new Vector3(width, height, 0.02f), Quaternion.identity, 0f, 0);
        });
        return MeshObject(name, mesh, new[] { M("Ventana"), M(lit ? "VidrioCalido" : "VentanaApagada") }, parent, center, rotation);
    }

    /// <summary>
    /// Piso de tablas: tablas sueltas con pequeños desniveles, giros y rendijas, dos tonos de madera.
    /// Collider plano (una caja) para caminar sin tropezar. center.y = altura de la superficie.
    /// </summary>
    public static GameObject PlankFloor(string name, Vector3 center, Vector2 size, Transform parent)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = center;
        BoxCollider col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, -0.1f, 0f);
        col.size = new Vector3(size.x, 0.2f, size.y);

        string key = $"Tablas_{name}_{Cm(size.x)}x{Cm(size.y)}";
        Mesh mesh = GetMesh(key, b =>
        {
            System.Random rng = Rng(key);
            const float plankWidth = 0.24f, gap = 0.018f, thick = 0.05f;
            // Base oscura para que las rendijas se vean
            b.Submesh = 2;
            AddBox(b, new Vector3(0f, -thick * 0.6f, 0f), new Vector3(size.x, thick * 0.3f, size.y), Quaternion.identity, 0f, 0);

            int rows = Mathf.FloorToInt(size.y / plankWidth);
            float z0 = -size.y * 0.5f + plankWidth * 0.5f;
            for (int r = 0; r < rows; r++)
            {
                float x = -size.x * 0.5f - Range(rng, 0f, 1.2f);
                while (x < size.x * 0.5f)
                {
                    float plank = Range(rng, 1.4f, 2.8f);
                    float a = Mathf.Max(x, -size.x * 0.5f), c = Mathf.Min(x + plank, size.x * 0.5f);
                    if (c - a > 0.15f)
                    {
                        b.Submesh = rng.NextDouble() < 0.5 ? 0 : 1;
                        AddBox(b, new Vector3((a + c) * 0.5f, -thick * 0.5f + Range(rng, -0.006f, 0.006f), z0 + r * plankWidth),
                            new Vector3(c - a - gap, thick, plankWidth - gap), Quaternion.Euler(0f, Range(rng, -0.6f, 0.6f), 0f), 0.012f, 1);
                    }
                    x += plank;
                }
            }
        });
        var visual = MeshObject(name + "_Tablas", mesh, new[] { M("PisoTabla"), M("PisoTablaClara"), M("MaderaOscura") },
            root.transform, center, Quaternion.identity);
        visual.transform.localPosition = Vector3.zero;
        return root;
    }
}
