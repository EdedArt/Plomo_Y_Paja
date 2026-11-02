using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static LevelBuilderUtils;

/// <summary>
/// Kit: pobreza, vicio y escena del crimen (fase 3A4). Todo es decorativo y SIN collider (cartas, botellas,
/// manchas, huellas...), así que no cambia el NavMesh. Mallas con semilla fija, siempre cartoon: nada de gore realista.
/// Coordenadas locales de las piezas de pared: x = a lo largo de la pared, y = arriba, z = hacia fuera de la pared.
/// </summary>
public static partial class CartoonKit
{
    /// <summary>Quita el flag "Navigation Static" a un grupo (cartas, manchas...: no deben influir en el NavMesh).</summary>
    public static void ExcludeFromNavMesh(Transform root)
    {
#pragma warning disable 618
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags & ~StaticEditorFlags.NavigationStatic);
        }
#pragma warning restore 618
    }

    /// <summary>Objeto decorativo de una malla (sin collider).</summary>
    private static GameObject Deco(string name, string key, Action<KitMesh> build, Transform parent, Vector3 position,
        float yaw, params string[] materials)
    {
        Mesh mesh = GetMesh(key, build);
        var mats = new Material[materials.Length];
        for (int i = 0; i < materials.Length; i++) mats[i] = M(materials[i]);
        return MeshObject(name, mesh, mats, parent, position, Quaternion.Euler(0f, yaw, 0f));
    }

    private static void Lie(KitMesh b, Vector3 position, float yaw, float[] ys, float[] rs, int sides, bool capBottom = true, bool capTop = false)
    {
        // Torno acostado: el eje Y del perfil pasa a ser el largo, con rumbo "yaw"
        AddLathe(b, position, Quaternion.Euler(0f, yaw, 90f), ys, rs, sides, capBottom, capTop);
    }

    // ==================================================================
    // Vicio: cartas, dados, boletos de carreras, botellas, facturas
    // ==================================================================

    /// <summary>
    /// Montoncito de cosas del jugador: cartas, dados, boletos de apuestas y botellas vacías (algunas caídas).
    /// center.y = altura del piso. Todo mide menos de 30 cm.
    /// </summary>
    public static GameObject GamblerClutter(string name, Vector3 center, float radius, int seed, Transform parent,
        int cards = 7, int dice = 2, int tickets = 4, int bottles = 2)
    {
        string key = $"Vicio_{name}";
        return Deco(name, key, b =>
        {
            System.Random rng = Rng(key + seed);
            Vector3 P(float spread = 1f)
            {
                float a = Range(rng, 0f, Mathf.PI * 2f), d = Mathf.Sqrt((float)rng.NextDouble()) * radius * spread;
                return new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
            }

            for (int i = 0; i < cards; i++)
            {
                b.Submesh = rng.NextDouble() < 0.45 ? 1 : 0;
                AddBox(b, P() + Vector3.up * (0.002f + i * 0.0006f), new Vector3(0.065f, 0.004f, 0.095f),
                    Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), 0.0015f, 0);
            }
            b.Submesh = 2;
            for (int i = 0; i < tickets; i++)
                AddBox(b, P() + Vector3.up * 0.002f, new Vector3(0.05f, 0.003f, 0.13f),
                    Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), 0.001f, 0);
            b.Submesh = 5;
            for (int i = 0; i < dice; i++)
                AddBox(b, P(0.7f) + Vector3.up * 0.0175f, Vector3.one * 0.035f,
                    Quaternion.Euler(Range(rng, 0f, 20f), Range(rng, 0f, 360f), Range(rng, 0f, 20f)), 0.006f, 1);
            for (int i = 0; i < bottles; i++)
            {
                b.Submesh = i % 2 == 0 ? 3 : 4;
                Vector3 p = P(0.9f);
                if (rng.NextDouble() < 0.5)
                    AddLathe(b, p, Quaternion.identity, BottleY, BottleR, 8, true, true);                     // de pie
                else
                    Lie(b, p + Vector3.up * 0.04f, Range(rng, 0f, 360f), BottleY, BottleR, 8);               // caída
            }
        }, parent, center, 0f, "Carta", "CartaRoja", "Boleto", "Botella", "BotellaAmbar", "Yeso");
    }

    private static readonly float[] BottleY = { 0f, 0.015f, 0.15f, 0.19f, 0.26f };
    private static readonly float[] BottleR = { 0.032f, 0.04f, 0.04f, 0.015f, 0.016f };

    /// <summary>Pila de facturas vencidas (con sello rojo) y un boleto de empeño naranja al lado.</summary>
    public static GameObject OverdueBills(string name, Vector3 position, float yaw, Transform parent)
    {
        return Deco(name, "FacturasVencidas", b =>
        {
            System.Random rng = Rng("facturas");
            b.Submesh = 0;
            for (int i = 0; i < 9; i++)
                AddBox(b, new Vector3(Range(rng, -0.012f, 0.012f), 0.005f + i * 0.0045f, Range(rng, -0.012f, 0.012f)),
                    new Vector3(0.2f, 0.004f, 0.28f), Quaternion.Euler(0f, Range(rng, -14f, 14f), 0f), 0.001f, 0);
            b.Submesh = 1;
            AddBox(b, new Vector3(0.03f, 0.05f, -0.04f), new Vector3(0.1f, 0.003f, 0.045f), Quaternion.Euler(0f, 18f, 0f), 0.001f, 0);
            b.Submesh = 2;                                                       // boleto de empeño
            AddBox(b, new Vector3(0.28f, 0.003f, 0.05f), new Vector3(0.09f, 0.004f, 0.055f), Quaternion.Euler(0f, 24f, 0f), 0.001f, 0);
            AddBox(b, new Vector3(0.33f, 0.003f, 0.0f), new Vector3(0.09f, 0.004f, 0.055f), Quaternion.Euler(0f, -12f, 0f), 0.001f, 0);
        }, parent, position, yaw, "Factura", "Sello", "Boleto");
    }

    /// <summary>Platos rotos: cascos blancos y de borde azul tirados por el piso.</summary>
    public static GameObject BrokenPlates(string name, Vector3 center, float radius, int seed, Transform parent)
    {
        string key = "Platos_" + name;
        return Deco(name, key, b =>
        {
            System.Random rng = Rng(key + seed);
            for (int i = 0; i < 13; i++)
            {
                b.Submesh = i % 3 == 0 ? 1 : 0;
                float a = Range(rng, 0f, Mathf.PI * 2f), d = Mathf.Sqrt((float)rng.NextDouble()) * radius;
                float w = Range(rng, 0.04f, 0.11f), l = Range(rng, 0.04f, 0.1f);
                // Casco: una cuña delgada (caja chata y torcida)
                AddBox(b, new Vector3(Mathf.Cos(a) * d, 0.006f + (float)rng.NextDouble() * 0.006f, Mathf.Sin(a) * d),
                    new Vector3(w, 0.012f, l), Quaternion.Euler(Range(rng, -10f, 10f), Range(rng, 0f, 360f), Range(rng, -12f, 12f)), 0.003f, 1);
            }
        }, parent, center, 0f, "Plato", "PlatoAzul");
    }

    // ==================================================================
    // Manchas, huellas de lobo y rasguños
    // ==================================================================

    /// <summary>Mancha plana e irregular (café, humedad, charco). Sin collider.</summary>
    public static GameObject Stain(string name, Vector3 center, float radius, int seed, Transform parent, string material = "Mancha")
    {
        string key = $"Mancha_{name}_{Cm(radius)}_{seed}";
        return Deco(name, key, b =>
        {
            System.Random rng = Rng(key);
            const int n = 14;
            var pts = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                float r = radius * Range(rng, 0.65f, 1.15f);
                pts[i] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r * 0.85f);
            }
            b.Card(pts, Vector3.up);
        }, parent, center, Range(Rng(key), 0f, 360f), material);
    }

    /// <summary>
    /// Huellas embarradas de lobo siguiendo una ruta (cada ~0.9 m, izquierda y derecha alternadas).
    /// Cada punto de "path" lleva su altura (y = piso). Se dibujan 3 cm sobre el piso.
    /// </summary>
    public static GameObject WolfPawPrints(string name, Vector3[] path, Transform parent, float spacing = 0.9f)
    {
        string key = "Huellas_" + name;
        return Deco(name, key, b =>
        {
            float walked = 0f, nextAt = 0f;
            int index = 0;
            for (int s = 0; s < path.Length - 1; s++)
            {
                Vector3 a = path[s], c = path[s + 1];
                float len = Vector3.Distance(a, c);
                Vector3 dir = (c - a); dir.y = 0f; dir.Normalize();
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                while (nextAt <= walked + len)
                {
                    float t = (nextAt - walked) / Mathf.Max(0.001f, len);
                    Vector3 p = Vector3.Lerp(a, c, t) + side * (index % 2 == 0 ? -0.17f : 0.17f) + Vector3.up * 0.03f;
                    AddPaw(b, p, Quaternion.LookRotation(dir) * Quaternion.Euler(0f, index % 2 == 0 ? 6f : -6f, 0f));
                    nextAt += spacing;
                    index++;
                }
                walked += len;
            }
        }, parent, Vector3.zero, 0f, "Barro");
    }

    private static void AddPaw(KitMesh b, Vector3 position, Quaternion rot)
    {
        // Almohadilla grande (elipse) + 4 deditos redondos adelante
        Matrix4x4 saved = b.Matrix;
        b.Matrix = saved * Matrix4x4.TRS(position, rot, new Vector3(1f, 1f, 1.15f));
        b.Lathe(new[] { 0f, 0.006f }, new[] { 0.085f, 0.075f }, 9, false, true);
        b.Matrix = saved;
        for (int i = 0; i < 4; i++)
        {
            float x = (i - 1.5f) * 0.062f, z = 0.135f + (i == 1 || i == 2 ? 0.03f : 0f);
            Vector3 toe = position + rot * new Vector3(x, 0f, z);
            AddLathe(b, toe, rot, new[] { 0f, 0.006f }, new[] { 0.027f, 0.023f }, 6, false, true);
        }
    }

    /// <summary>
    /// Rasguños de lobo (4 surcos paralelos, ligeramente curvos) sobre una pared. center = punto en la pared;
    /// yaw = hacia dónde mira la cara de la pared (z local fuera de la pared).
    /// </summary>
    public static GameObject ClawMarks(string name, Vector3 center, float yaw, Transform parent, float scale = 1f)
    {
        return Deco(name, "Rasgunos_" + Cm(scale), b =>
        {
            System.Random rng = Rng("rasgunos");
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * 0.1f * scale;
                float len = Range(rng, 0.7f, 0.95f) * scale;
                Quaternion q = Quaternion.Euler(0f, 0f, -16f + Range(rng, -3f, 3f));
                b.Submesh = 1;                                              // borde claro: papel desgarrado
                AddBox(b, new Vector3(x, 0f, 0.002f), new Vector3(0.05f * scale, len * 1.02f, 0.008f), q, 0.002f, 0);
                b.Submesh = 0;                                              // surco oscuro
                AddBox(b, new Vector3(x, 0f, 0.008f), new Vector3(0.026f * scale, len, 0.01f), q, 0.003f, 0);
            }
        }, parent, center, yaw, "Rasguno", "Paja");
    }

    // ==================================================================
    // Paredes maltratadas: papel roto, huecos tapados con tablas
    // ==================================================================

    /// <summary>
    /// Pedazo de pared dañada pegado al empapelado: parche de paja donde se rompió el papel, tiras de papel
    /// colgando y, si boarded, un hueco oscuro tapado con tablas clavadas en cruz. yaw = hacia dónde mira la pared.
    /// </summary>
    public static GameObject WallDamage(string name, Vector3 center, float yaw, Transform parent, int seed, bool boarded)
    {
        string key = $"DanoPared_{name}_{seed}_{(boarded ? 1 : 0)}";
        return Deco(name, key, b =>
        {
            System.Random rng = Rng(key);
            const float zBase = 0.004f;
            if (!boarded)
            {
                // Parche de paja de varios trozos solapados + tiras de papel que cuelgan
                b.Submesh = 0;
                for (int i = 0; i < 4; i++)
                    AddBox(b, new Vector3(Range(rng, -0.3f, 0.3f), Range(rng, -0.4f, 0.3f), zBase + i * 0.001f),
                        new Vector3(Range(rng, 0.35f, 0.7f), Range(rng, 0.3f, 0.6f), 0.006f),
                        Quaternion.Euler(0f, 0f, Range(rng, -25f, 25f)), 0.003f, 0);
                b.Submesh = 1;
                for (int i = 0; i < 3; i++)
                    AddBox(b, new Vector3(-0.35f + i * 0.33f + Range(rng, -0.05f, 0.05f), 0.38f - i * 0.04f, zBase + 0.012f),
                        new Vector3(0.14f, Range(rng, 0.35f, 0.65f), 0.008f), Quaternion.Euler(Range(rng, -6f, 6f), 0f, Range(rng, -14f, 14f)), 0.003f, 0);
            }
            else
            {
                b.Submesh = 3;                                                     // hueco oscuro
                AddBox(b, new Vector3(0f, 0f, zBase), new Vector3(0.75f, 0.62f, 0.008f), Quaternion.Euler(0f, 0f, 4f), 0.004f, 0);
                b.Submesh = 0;                                                     // paja que asoma
                AddBox(b, new Vector3(-0.1f, 0.34f, zBase + 0.004f), new Vector3(0.5f, 0.14f, 0.008f), Quaternion.Euler(0f, 0f, 12f), 0.003f, 0);
                b.Submesh = 2;                                                     // tablas clavadas en cruz
                float[] angles = { -24f, 17f, 70f };
                for (int i = 0; i < 3; i++)
                    AddBox(b, new Vector3(Range(rng, -0.05f, 0.05f), Range(rng, -0.05f, 0.05f), zBase + 0.012f + i * 0.007f),
                        new Vector3(1.05f, 0.13f, 0.025f), Quaternion.Euler(0f, 0f, angles[i]), 0.008f, 1);
                b.Submesh = 4;                                                     // clavos
                for (int i = 0; i < 6; i++)
                    AddBox(b, new Vector3(Range(rng, -0.4f, 0.4f), Range(rng, -0.3f, 0.3f), zBase + 0.04f), Vector3.one * 0.025f, Quaternion.identity, 0.008f, 1);
            }
        }, parent, center, yaw, "Paja", "Empapelado", "MaderaClara", "Mancha", "Metal");
    }

    // ==================================================================
    // Techo con goteras: cielorraso con agujeros y techo de paja ralo
    // ==================================================================

    /// <summary>Cielorraso de losa plana con agujeros rectangulares (en x,z de mundo). Reparte cajas alrededor.</summary>
    public static Transform CeilingWithHoles(string name, Vector3 center, Vector2 size, float thickness, Material mat,
        Transform parent, params Rect[] holes)
    {
        Transform root = CreateGroup(name, parent);
        float x0 = center.x - size.x * 0.5f, x1 = center.x + size.x * 0.5f;
        float z0 = center.z - size.y * 0.5f, z1 = center.z + size.y * 0.5f;

        var xs = new SortedSet<float> { x0, x1 };
        foreach (Rect h in holes)
        {
            if (h.xMax > x0 && h.xMin < x1) { xs.Add(Mathf.Clamp(h.xMin, x0, x1)); xs.Add(Mathf.Clamp(h.xMax, x0, x1)); }
        }
        var xl = new List<float>(xs);
        int n = 0;
        for (int i = 0; i < xl.Count - 1; i++)
        {
            float a = xl[i], c = xl[i + 1];
            if (c - a < 0.02f) continue;
            float mid = (a + c) * 0.5f;
            // Intervalos en z libres de agujeros en esta franja
            var blocked = new List<Vector2>();
            foreach (Rect h in holes) if (mid > h.xMin && mid < h.xMax) blocked.Add(new Vector2(Mathf.Max(h.yMin, z0), Mathf.Min(h.yMax, z1)));
            blocked.Sort((p, q) => p.x.CompareTo(q.x));
            float cursor = z0;
            foreach (Vector2 bz in blocked)
            {
                if (bz.x - cursor > 0.02f) CeilingPiece(root, name, ref n, a, c, cursor, bz.x, center.y, thickness, mat);
                cursor = Mathf.Max(cursor, bz.y);
            }
            if (z1 - cursor > 0.02f) CeilingPiece(root, name, ref n, a, c, cursor, z1, center.y, thickness, mat);
        }
        return root;
    }

    private static void CeilingPiece(Transform root, string name, ref int n, float x0, float x1, float z0, float z1, float y,
        float thickness, Material mat)
    {
        Box($"{name}_{n++}", new Vector3((x0 + x1) * 0.5f, y, (z0 + z1) * 0.5f), new Vector3(x1 - x0, thickness, z1 - z0), mat, root, default, 0.02f);
    }

    /// <summary>
    /// Bombilla desnuda colgando de un cable (y no una lámpara con pantalla): cable, casquillo y foco + luz + haz.
    /// ceilingPoint = de dónde cuelga el cable.
    /// </summary>
    public static Light BareBulb(string name, Vector3 ceilingPoint, float drop, Transform parent, Transform lighting, Color color,
        float intensity, float range)
    {
        Mesh mesh = GetMesh("BombillaDesnuda_" + Cm(drop), b =>
        {
            b.Submesh = 0;
            AddBox(b, new Vector3(0f, -drop * 0.5f, 0f), new Vector3(0.012f, drop, 0.012f), Quaternion.identity, 0f, 0);
            AddLathe(b, new Vector3(0f, -drop, 0f), Quaternion.identity, new[] { -0.07f, -0.04f, 0f }, new[] { 0.022f, 0.03f, 0.022f }, 8);
            b.Submesh = 1;
            AddLathe(b, new Vector3(0f, -drop - 0.07f, 0f), Quaternion.identity, new[] { -0.09f, -0.06f, 0f, 0.05f },
                new[] { 0.015f, 0.052f, 0.058f, 0.022f }, 10, false, false);
        });
        MeshObject(name, mesh, new[] { M("Metal"), M("Bombilla") }, parent, ceilingPoint, Quaternion.identity);
        Vector3 bulb = ceilingPoint - Vector3.up * (drop + 0.13f);
        Light light = CreatePointLight(name + "_Luz", bulb, color, intensity, range, lighting);
        LightCone(name + "_Haz", bulb, bulb.y, 1.2f, color, lighting, 0.05f);
        return light;
    }

    // ==================================================================
    // Cama de pobre
    // ==================================================================

    /// <summary>Colchón flaco directo en el piso, con manta arrugada y una almohada aplastada. Sin collider.</summary>
    public static GameObject FloorMattress(string name, Vector3 position, float yaw, Transform parent)
    {
        return Deco(name, "ColchonSuelo", b =>
        {
            b.Submesh = 0;
            AddBox(b, new Vector3(0f, 0.045f, 0f), new Vector3(1.85f, 0.09f, 0.95f), Quaternion.identity, 0.04f, 2, 0.012f, 3);
            b.Submesh = 1;                                                       // manta hecha un nudo
            AddBox(b, new Vector3(0.25f, 0.11f, 0.05f), new Vector3(1.0f, 0.07f, 0.7f), Quaternion.Euler(0f, -14f, 4f), 0.03f, 2, 0.015f, 5);
            AddBox(b, new Vector3(0.05f, 0.14f, -0.2f), new Vector3(0.5f, 0.07f, 0.3f), Quaternion.Euler(10f, 20f, -6f), 0.03f, 2, 0.012f, 7);
            b.Submesh = 2;                                                       // almohada plana
            AddBox(b, new Vector3(-0.72f, 0.115f, 0f), new Vector3(0.38f, 0.07f, 0.55f), Quaternion.Euler(0f, 6f, 0f), 0.03f, 2, 0.01f, 9);
        }, parent, position, yaw, "Colchon", "Manta", "Yeso");
    }
}
