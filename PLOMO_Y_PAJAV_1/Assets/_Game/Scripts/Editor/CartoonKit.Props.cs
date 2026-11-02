using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static LevelBuilderUtils;

/// <summary>Kit: muebles, props, luces con haz, techo de paja, cerca, terreno, adoquines, caminos y fachadas.</summary>
public static partial class CartoonKit
{
    public const string ModelFolder = "Assets/_Game/Art/Environment/Models";
    public const string LightConeShaderName = "PlomoYPaja/LightCone";

    /// <summary>Nombres del kit que se pueden reemplazar con un modelo externo y su altura de referencia (m).</summary>
    public static readonly Dictionary<string, float> ModelHeights = new Dictionary<string, float>
    {
        { "barril", 1.0f }, { "caja", 0.8f }, { "mesa", 0.8f }, { "silla", 1.0f }, { "lampara_pared", 0.6f },
        { "lampara_techo", 0.5f }, { "maceta", 0.9f }, { "cerca", 1.4f }, { "farol", 4.3f },
    };

    // ==================================================================
    // Modelos externos
    // ==================================================================

    /// <summary>
    /// Si existe Models/&lt;kitName&gt;.fbx o .glb lo instancia, lo escala a la altura de referencia del kit y le pone
    /// materiales CartoonEnvironment con el color principal de cada submalla. Devuelve null si no hay modelo.
    /// </summary>
    public static GameObject TryModel(string kitName, string name, Vector3 position, Quaternion rotation, Transform parent)
    {
        GameObject asset = null;
        foreach (string ext in new[] { ".fbx", ".glb", ".gltf", ".obj" })
        {
            asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + "/" + kitName + ext);
            if (asset != null) break;
        }
        if (asset == null) return null;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, rotation);

        // Escala: que mida lo mismo que la pieza procedural
        Bounds bounds = RendererBounds(go);
        if (bounds.size.y > 0.001f && ModelHeights.TryGetValue(kitName, out float target))
            go.transform.localScale *= target / bounds.size.y;

        int index = 0;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = GetOrCreateMaterial($"Modelo_{kitName}_{index++}", CharacterLook.MainColor(mats[i]), 0f, true);
            r.sharedMaterials = mats;
        }

        bounds = RendererBounds(go);
        BoxCollider col = go.AddComponent<BoxCollider>();
        col.center = go.transform.InverseTransformPoint(bounds.center);
        Vector3 s = go.transform.lossyScale;
        col.size = new Vector3(bounds.size.x / Mathf.Max(0.0001f, s.x), bounds.size.y / Mathf.Max(0.0001f, s.y), bounds.size.z / Mathf.Max(0.0001f, s.z));
        return go;
    }

    private static Bounds RendererBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    /// <summary>Prop procedural con collider; si hay modelo externo con ese nombre del kit, usa el modelo.</summary>
    private static GameObject Prop(string kitName, string name, Vector3 position, float yaw, Transform parent, string key,
        Action<KitMesh> build, params string[] materials)
    {
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        if (kitName != null)
        {
            GameObject model = TryModel(kitName, name, position, rot, parent);
            if (model != null) return model;
        }
        Mesh mesh = GetMesh(key, build);
        var mats = new Material[materials.Length];
        for (int i = 0; i < materials.Length; i++) mats[i] = M(materials[i]);
        GameObject go = MeshObject(name, mesh, mats, parent, position, rot);
        FitCollider(go);
        return go;
    }

    // ==================================================================
    // Props
    // ==================================================================

    /// <summary>Barril abultado: duelas de madera, 3 aros de metal y tablas de la tapa desparejas.</summary>
    public static GameObject Barrel(string name, Vector3 position, float yaw, Transform parent, int variant = 0)
    {
        return Prop("barril", name, position, yaw, parent, "Barril_" + variant, b =>
        {
            System.Random rng = Rng("barril" + variant);
            const int rings = 9;
            var ys = new float[rings];
            var rs = new float[rings];
            for (int i = 0; i < rings; i++)
            {
                float t = i / (rings - 1f);
                ys[i] = t * 0.98f;
                rs[i] = 0.3f + 0.075f * Mathf.Sin(Mathf.PI * t);
            }
            b.Submesh = 0;
            b.Lathe(ys, rs, 14, true, false);
            b.Submesh = 1;
            foreach (float t in new[] { 0.12f, 0.5f, 0.88f })
            {
                float r = 0.3f + 0.075f * Mathf.Sin(Mathf.PI * t) + 0.012f;
                b.Lathe(new[] { t * 0.98f - 0.03f, t * 0.98f + 0.03f }, new[] { r, r }, 14, false, false);
            }
            b.Submesh = 2;
            for (int i = 0; i < 4; i++)
            {
                float x = -0.21f + i * 0.14f;
                float w = 0.13f, len = 2f * Mathf.Sqrt(Mathf.Max(0.01f, 0.3f * 0.3f - x * x));
                AddBox(b, new Vector3(x, 0.965f + Range(rng, -0.012f, 0.012f), 0f), new Vector3(w, 0.03f, len),
                    Quaternion.Euler(Range(rng, -2f, 2f), 0f, Range(rng, -2f, 2f)), 0.01f);
            }
        }, "Madera", "Metal", "MaderaClara");
    }

    /// <summary>Caja de madera redondeada con juntas de tablas y listones en las esquinas.</summary>
    public static GameObject Crate(string name, Vector3 position, float yaw, Transform parent, float size = 0.8f)
    {
        return Prop("caja", name, position, yaw, parent, "Caja_" + Cm(size), b =>
        {
            b.Submesh = 0;
            AddBox(b, new Vector3(0f, size * 0.5f, 0f), Vector3.one * size, Quaternion.identity, size * 0.08f, 2);
            b.Submesh = 1;
            float h = size * 0.5f;
            for (int face = 0; face < 4; face++)
            {
                Quaternion q = Quaternion.Euler(0f, face * 90f, 0f);
                for (int j = 1; j <= 2; j++)
                    AddBox(b, q * new Vector3(0f, 0f, h + 0.004f) + Vector3.up * (size * j / 3f), new Vector3(size * 0.9f, 0.025f, 0.012f), q, 0.005f);
                foreach (int s in new[] { -1, 1 })
                    AddBox(b, q * new Vector3(s * h * 0.86f, 0f, h + 0.012f) + Vector3.up * h, new Vector3(size * 0.12f, size * 0.98f, 0.03f), q, 0.012f);
            }
        }, "MaderaClara", "MaderaOscura");
    }

    /// <summary>Mesa redonda de pedestal. La superficie queda a 0.78 m.</summary>
    public static GameObject RoundTable(string name, Vector3 position, Transform parent, float radius = 0.6f)
    {
        return Prop("mesa", name, position, 0f, parent, "MesaRedonda_" + Cm(radius), b =>
        {
            b.Submesh = 0;
            b.Lathe(new[] { 0.72f, 0.74f, 0.78f }, new[] { radius * 0.96f, radius, radius * 0.97f }, 20, true, true);
            b.Submesh = 1;
            b.Lathe(new[] { 0f, 0.05f, 0.08f, 0.3f, 0.6f, 0.72f }, new[] { 0.32f, 0.3f, 0.09f, 0.06f, 0.07f, 0.14f }, 14, true, true);
        }, "MaderaClara", "MaderaOscura");
    }

    /// <summary>
    /// Silla con respaldo curvo. yaw = hacia dónde mira quien se sienta. wobbly = una pata más corta, asiento y
    /// respaldo chuecos (casa pobre).
    /// </summary>
    public static GameObject Chair(string name, Vector3 position, float yaw, Transform parent, bool wobbly = false)
    {
        return Prop("silla", name, position, yaw, parent, wobbly ? "SillaCoja" : "Silla", b =>
        {
            b.Submesh = 0;
            Quaternion seatTilt = wobbly ? Quaternion.Euler(2.5f, 0f, 3.5f) : Quaternion.identity;
            AddBox(b, new Vector3(0f, wobbly ? 0.44f : 0.46f, 0f), new Vector3(0.46f, 0.07f, 0.46f), seatTilt, 0.03f, 2);
            b.Submesh = 1;
            foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
            {
                bool shortLeg = wobbly && sx > 0 && sz < 0;
                float legH = shortLeg ? 0.34f : 0.44f;
                AddBox(b, new Vector3(sx * 0.18f, legH * 0.5f, sz * 0.18f), new Vector3(0.06f, legH, 0.06f), Quaternion.Euler(sz * 3f, 0f, -sx * 3f), 0.02f);
            }
            // Respaldo curvo: travesaño en arco + 3 barrotes (torcido si es coja)
            const int pieces = 7;
            float lean = wobbly ? 9f : 0f;
            for (int i = 0; i < pieces; i++)
            {
                float a = Mathf.Lerp(-55f, 55f, i / (pieces - 1f)) * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Sin(a) * 0.26f, 0.95f - (wobbly ? Mathf.Sin(a) * 0.05f : 0f), -0.08f - Mathf.Cos(a) * 0.15f);
                AddBox(b, p, new Vector3(0.1f, 0.09f, 0.05f), Quaternion.Euler(0f, a * Mathf.Rad2Deg * 0.8f, lean), 0.02f);
            }
            for (int i = -1; i <= 1; i++)
                AddBox(b, new Vector3(i * 0.15f, 0.71f, -0.21f - (1 - Mathf.Abs(i)) * 0.02f), new Vector3(0.05f, wobbly && i == 0 ? 0.3f : 0.44f, 0.04f),
                    Quaternion.Euler(-6f, 0f, lean * 0.5f), 0.015f);
        }, "MaderaClara", "Madera");
    }

    /// <summary>Sofá gordito: base, respaldo, brazos redondos y dos cojines. Mira hacia +Z local.</summary>
    public static GameObject Sofa(string name, Vector3 position, float yaw, Transform parent, float width = 2.1f)
    {
        return Prop(null, name, position, yaw, parent, "Sofa_" + Cm(width), b =>
        {
            b.Submesh = 0;
            AddBox(b, new Vector3(0f, 0.25f, 0f), new Vector3(width, 0.36f, 0.9f), Quaternion.identity, 0.12f, 2);
            AddBox(b, new Vector3(0f, 0.62f, -0.33f), new Vector3(width, 0.6f, 0.26f), Quaternion.Euler(-8f, 0f, 0f), 0.12f, 2);
            foreach (int s in new[] { -1, 1 })
                AddBox(b, new Vector3(s * (width * 0.5f - 0.08f), 0.5f, 0f), new Vector3(0.24f, 0.5f, 0.92f), Quaternion.identity, 0.11f, 2);
            b.Submesh = 1;
            foreach (int s in new[] { -1, 1 })
                AddBox(b, new Vector3(s * (width * 0.25f - 0.05f), 0.5f, 0.06f), new Vector3(width * 0.44f, 0.16f, 0.7f), Quaternion.identity, 0.07f, 2);
            b.Submesh = 2;
            foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
                AddLathe(b, new Vector3(sx * (width * 0.5f - 0.15f), 0f, sz * 0.32f), Quaternion.identity,
                    new[] { 0f, 0.07f }, new[] { 0.04f, 0.06f }, 8);
        }, "Sofa", "Cojin", "MaderaOscura");
    }

    /// <summary>
    /// Cómoda con 3 cajones y pomos de latón. La tapa queda exactamente a "height". drawersOut = saqueada: dos cajones
    /// sacados y uno a medias (no tapan la parte de arriba, donde queda la pista).
    /// </summary>
    public static GameObject Dresser(string name, Vector3 position, float yaw, Transform parent, Vector3 size, bool drawersOut = false)
    {
        return Prop(null, name, position, yaw, parent, $"Comoda_{Cm(size.x)}x{Cm(size.y)}x{Cm(size.z)}{(drawersOut ? "_Saqueada" : "")}", b =>
        {
            b.Submesh = 0;
            AddBox(b, new Vector3(0f, (size.y - 0.06f) * 0.5f, 0f), new Vector3(size.x, size.y - 0.06f, size.z), Quaternion.identity, 0.05f, 2);
            AddBox(b, new Vector3(0f, size.y - 0.03f, 0f), new Vector3(size.x + 0.06f, 0.06f, size.z + 0.06f), Quaternion.identity, 0.025f, 2);
            b.Submesh = 1;
            float[] pull = { 0.3f, 0.1f, 0.26f };
            for (int i = 0; i < 3; i++)
            {
                float y = 0.12f + (size.y - 0.2f) * (i + 0.5f) / 3f;
                float out_ = drawersOut ? pull[i] : 0f;
                float h = (size.y - 0.3f) / 3f;
                b.Submesh = 1;
                AddBox(b, new Vector3(0f, y, size.z * 0.5f + 0.01f + out_), new Vector3(size.x * 0.86f, h, 0.03f),
                    drawersOut ? Quaternion.Euler(0f, 0f, i == 1 ? 0f : (i - 1) * 2f) : Quaternion.identity, 0.015f);
                if (drawersOut)
                {
                    // Cajón sacado: fondo y paredes bajas
                    AddBox(b, new Vector3(0f, y - h * 0.4f, size.z * 0.5f + out_ * 0.5f), new Vector3(size.x * 0.84f, 0.02f, out_ + 0.04f), Quaternion.identity, 0.008f, 1);
                    foreach (int sx in new[] { -1, 1 })
                        AddBox(b, new Vector3(sx * size.x * 0.42f, y - h * 0.1f, size.z * 0.5f + out_ * 0.5f), new Vector3(0.02f, h * 0.7f, out_ + 0.04f), Quaternion.identity, 0.008f, 1);
                }
                b.Submesh = 2;
                AddLathe(b, new Vector3(0f, y, size.z * 0.5f + 0.025f + out_), Quaternion.Euler(90f, 0f, 0f), new[] { 0f, 0.02f, 0.045f }, new[] { 0.02f, 0.04f, 0f }, 8);
            }
        }, "Madera", "MaderaClara", "Laton");
    }

    /// <summary>
    /// Nevera de los años 30: cuerpo gordito y redondeado, manija cromada, un poco abollada, con la puerta
    /// entreabierta y una luz tenue adentro. +Z local = el lado de la puerta. lighting = grupo de luces (opcional).
    /// </summary>
    public static GameObject IceBox(string name, Vector3 position, float yaw, Transform parent, Transform lighting = null)
    {
        GameObject go = Prop(null, name, position, yaw, parent, "NeveraAbierta", b =>
        {
            const float w = 0.8f, h = 1.7f, d = 0.7f, wall = 0.07f;
            b.Submesh = 0;
            // Carcasa hueca por delante: laterales, fondo, tapa abultada y base
            foreach (int sx in new[] { -1, 1 })
                AddBox(b, new Vector3(sx * (w * 0.5f - wall * 0.5f), h * 0.5f + 0.06f, 0f), new Vector3(wall, h - 0.1f, d), Quaternion.identity, 0.035f, 2);
            AddBox(b, new Vector3(0f, h * 0.5f + 0.06f, -d * 0.5f + wall * 0.5f), new Vector3(w, h - 0.1f, wall), Quaternion.identity, 0.035f, 2);
            AddBox(b, new Vector3(0f, h - 0.04f, 0f), new Vector3(w + 0.04f, 0.2f, d + 0.04f), Quaternion.identity, 0.09f, 3);       // tapa abultada
            AddBox(b, new Vector3(0f, 0.1f, 0f), new Vector3(w, 0.2f, d), Quaternion.identity, 0.06f, 2);                             // base
            b.Submesh = 3;                                       // estantes
            for (int i = 0; i < 3; i++)
                AddBox(b, new Vector3(0f, 0.45f + i * 0.4f, 0.02f), new Vector3(w - 2 * wall, 0.02f, d - 0.12f), Quaternion.identity, 0.005f, 0);
            b.Submesh = 2;                                       // interior que brilla tenue
            AddBox(b, new Vector3(0f, h * 0.5f + 0.06f, -d * 0.5f + wall + 0.005f), new Vector3(w - 2 * wall, h - 0.3f, 0.01f), Quaternion.identity, 0f, 0);

            // Puerta entreabierta: bisagra en el lado derecho (x = +w/2), abre ~28° hacia afuera
            Quaternion open = Quaternion.Euler(0f, 28f, 0f);
            Vector3 hinge = new Vector3(w * 0.5f - 0.02f, h * 0.5f + 0.06f, d * 0.5f);
            Matrix4x4 saved = b.Matrix;
            b.Matrix = saved * Matrix4x4.TRS(hinge, open, Vector3.one);
            b.Submesh = 0;
            AddBox(b, new Vector3(-(w - 0.04f) * 0.5f, 0f, 0.03f), new Vector3(w - 0.04f, h - 0.12f, 0.07f), Quaternion.identity, 0.06f, 3);
            b.Submesh = 4;                                       // abolladura: placa hundida, algo torcida
            AddBox(b, new Vector3(-0.26f, -0.2f, 0.07f), new Vector3(0.28f, 0.22f, 0.012f), Quaternion.Euler(0f, 0f, 14f), 0.05f, 2, 0.01f, 4);
            b.Submesh = 1;                                       // manija cromada gorda
            AddBox(b, new Vector3(-(w - 0.04f) + 0.1f, 0.12f, 0.1f), new Vector3(0.05f, 0.38f, 0.05f), Quaternion.identity, 0.02f, 2);
            foreach (int sy in new[] { -1, 1 })
                AddBox(b, new Vector3(-(w - 0.04f) + 0.1f, 0.12f + sy * 0.15f, 0.07f), new Vector3(0.04f, 0.04f, 0.05f), Quaternion.identity, 0.015f, 1);
            b.Matrix = saved;
        }, "Nevera", "Cromo", "InteriorNevera", "Metal", "NeveraAbollada");

        if (lighting != null)
        {
            Vector3 inside = position + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 1.1f, 0.12f);
            CreatePointLight(name + "_LuzInterior", inside, new Color32(0xCF, 0xE8, 0xB8, 0xFF), 0.7f, 2.4f, lighting);
        }
        return go;
    }

    /// <summary>Alfombra: rectángulo redondeado con borde de otro color. Sin collider (se camina encima).</summary>
    public static GameObject Rug(string name, Vector3 center, Vector2 size, float yaw, Transform parent)
    {
        string key = $"Alfombra_{Cm(size.x)}x{Cm(size.y)}";
        Mesh mesh = GetMesh(key, b =>
        {
            b.Submesh = 0;
            AddBox(b, new Vector3(0f, 0.008f, 0f), new Vector3(size.x, 0.016f, size.y), Quaternion.identity, 0.008f, 1);
            b.Submesh = 1;
            AddBox(b, new Vector3(0f, 0.012f, 0f), new Vector3(size.x - 0.3f, 0.016f, size.y - 0.3f), Quaternion.identity, 0.008f, 1);
        });
        return MeshObject(name, mesh, new[] { M("AlfombraBorde"), M("Alfombra") }, parent, center, Quaternion.Euler(0f, yaw, 0f));
    }

    /// <summary>Planta en maceta redonda con hojas en tarjetas.</summary>
    public static GameObject PottedPlant(string name, Vector3 position, Transform parent, int variant = 0)
    {
        return Prop("maceta", name, position, 0f, parent, "Maceta_" + variant, b =>
        {
            System.Random rng = Rng("maceta" + variant);
            b.Submesh = 0;
            b.Lathe(new[] { 0f, 0.04f, 0.3f, 0.36f, 0.4f }, new[] { 0.15f, 0.17f, 0.22f, 0.25f, 0.24f }, 14, true, false);
            b.Submesh = 1;
            b.Lathe(new[] { 0.34f, 0.36f }, new[] { 0.22f, 0.21f }, 14, false, true);
            b.Submesh = 2;
            int leaves = 7;
            for (int i = 0; i < leaves; i++)
            {
                float yaw = i * 360f / leaves + Range(rng, -15f, 15f);
                float tilt = Range(rng, 25f, 55f);
                float len = Range(rng, 0.45f, 0.65f);
                Quaternion q = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(tilt, 0f, 0f);
                Vector3 basePos = new Vector3(0f, 0.36f, 0f);
                Vector3 up = q * Vector3.up, side = q * Vector3.right;
                b.Card(new[]
                {
                    basePos, basePos + up * len * 0.4f + side * 0.09f, basePos + up * len, basePos + up * len * 0.4f - side * 0.09f
                }, q * Vector3.forward);
            }
        }, "Maceta", "Tierra", "Hoja");
    }

    /// <summary>Fardo de paja suelto, atado con dos cuerdas (decoración del exterior de Porky).</summary>
    public static GameObject StrawBale(string name, Vector3 position, float yaw, Transform parent)
    {
        return Prop(null, name, position, yaw, parent, "FardoPaja", b =>
        {
            b.Submesh = SubStraw;
            StrawBundle(b, new Vector3(0f, 0.3f, 0f), 1.1f, 0.32f, true, Quaternion.identity);
            for (int s = 0; s <= SubRope; s++) b.Submesh = s;
        }, "Paja", "Paja", "Paja", "Paja", "PajaOscura", "Cuerda");
    }

    /// <summary>Pajitas sueltas y astillas en el piso (escombros de la casa). Una malla, sin collider.</summary>
    public static GameObject StrawDebris(string name, Vector3 center, float radius, int count, Transform parent)
    {
        string key = "PajaSuelta_" + name;
        Mesh mesh = GetMesh(key, b =>
        {
            System.Random rng = Rng(key);
            for (int i = 0; i < count; i++)
            {
                b.Submesh = i % 3 == 0 ? 1 : 0;
                float a = Range(rng, 0f, Mathf.PI * 2f), d = Mathf.Sqrt((float)rng.NextDouble()) * radius;
                Vector3 p = new Vector3(Mathf.Cos(a) * d, 0.015f + Range(rng, 0f, 0.03f), Mathf.Sin(a) * d);
                AddBox(b, p, new Vector3(0.025f, 0.02f, Range(rng, 0.25f, 0.6f)),
                    Quaternion.Euler(Range(rng, -8f, 8f), Range(rng, 0f, 360f), Range(rng, -20f, 20f)), 0f, 0);
            }
        });
        return MeshObject(name, mesh, new[] { M("Paja"), M("PajaOscura") }, parent, center, Quaternion.identity);
    }

    /// <summary>Pedazo de escombro irregular (caja redondeada deformada).</summary>
    public static GameObject Rubble(string name, Vector3 position, float size, float yaw, Material mat, Transform parent, int seed)
    {
        Vector3 dims = new Vector3(size * 1.1f, size * 0.75f, size * 0.9f);
        return Box(name, position + Vector3.up * dims.y * 0.45f, dims, mat, parent, new Vector3(0f, yaw, 0f), size * 0.25f, true,
            size * 0.08f, seed);
    }

    // ==================================================================
    // Luces con haz falso (volumétrico)
    // ==================================================================

    /// <summary>
    /// Cono de luz falso: malla abierta, aditiva, sin iluminación y muy tenue. No es estático ni entra al NavMesh.
    /// apex = donde está la bombilla; el cono baja "height" metros.
    /// </summary>
    public static GameObject LightCone(string name, Vector3 apex, float height, float bottomRadius, Color color, Transform parent,
        float intensity = 0.08f)
    {
        string key = $"ConoLuz_{Cm(height)}x{Cm(bottomRadius)}";
        Mesh mesh = GetMesh(key, b => b.Lathe(new[] { -height, -height * 0.5f, 0f },
            new[] { bottomRadius, bottomRadius * 0.55f + 0.06f, 0.08f }, 24, false, false));
        GameObject go = MeshObject(name, mesh, new[] { LightConeMaterial() }, parent, apex, Quaternion.identity);
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_Color", color);
        mpb.SetFloat("_Intensity", intensity);
        mr.SetPropertyBlock(mpb);
        MakeNonStatic(go);
        return go;
    }

    private static Material LightConeMaterial()
    {
        const string key = "__cone";
        if (SessionMaterials.TryGetValue(key, out Material cached) && cached != null) return cached;
        string path = MaterialFolder + "/Kit_ConoLuz.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find(LightConeShaderName);
            if (shader == null)
            {
                Debug.LogWarning("[CartoonKit] Falta el shader " + LightConeShaderName + "; los haces de luz no se verán.");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            EnsureFolder(MaterialFolder);
            mat = new Material(shader) { name = "Kit_ConoLuz" };
            AssetDatabase.CreateAsset(mat, path);
        }
        SessionMaterials[key] = mat;
        return mat;
    }

    /// <summary>Lámpara de pared con brazo curvo, campana y bombilla + luz puntual + haz. yaw = hacia dónde sale de la pared.</summary>
    public static Light WallLamp(string name, Vector3 wallPoint, float yaw, Transform parent, Transform lighting, Color color,
        float intensity, float range)
    {
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 bulbLocal = new Vector3(0f, -0.1f, 0.42f);
        GameObject model = TryModel("lampara_pared", name, wallPoint, rot, parent);
        if (model == null)
        {
            Mesh mesh = GetMesh("LamparaPared", b =>
            {
                b.Submesh = 0;
                AddBox(b, new Vector3(0f, 0f, 0.02f), new Vector3(0.16f, 0.24f, 0.04f), Quaternion.identity, 0.02f);
                // Brazo: cuarto de círculo que sale de la pared y baja a la campana
                const int pieces = 7;
                for (int i = 0; i < pieces; i++)
                {
                    float a = Mathf.PI * 0.5f * (i + 0.5f) / pieces;
                    Vector3 p = new Vector3(0f, Mathf.Sin(a) * 0.22f, 0.04f + (1f - Mathf.Cos(a)) * 0.38f);
                    float theta = -Mathf.Atan2(0.22f * Mathf.Cos(a), 0.38f * Mathf.Sin(a)) * Mathf.Rad2Deg;
                    AddBox(b, p, new Vector3(0.035f, 0.035f, 0.1f), Quaternion.Euler(theta, 0f, 0f), 0.012f);
                }
                b.Submesh = 1;
                AddLathe(b, new Vector3(0f, 0.2f, 0.42f), Quaternion.identity, new[] { -0.22f, -0.16f, -0.06f, 0f },
                    new[] { 0.16f, 0.13f, 0.06f, 0.02f }, 12, false, true);
                b.Submesh = 2;
                AddLathe(b, new Vector3(0f, -0.06f, 0.42f), Quaternion.identity, new[] { -0.06f, -0.03f, 0f, 0.03f },
                    new[] { 0f, 0.055f, 0.06f, 0.03f }, 10, false, false);
            });
            MeshObject(name, mesh, new[] { M("Metal"), M("Laton"), M("Bombilla") }, parent, wallPoint, rot);
        }

        Vector3 bulb = wallPoint + rot * bulbLocal;
        Light light = CreatePointLight(name + "_Luz", bulb, color, intensity, range, lighting);
        LightCone(name + "_Haz", bulb, bulb.y, Mathf.Min(1.4f, range * 0.18f), color, lighting, 0.06f);
        return light;
    }

    /// <summary>Lámpara colgante: cordón, campana tipo tazón y bombilla + luz + haz hasta el piso.</summary>
    public static Light HangingLamp(string name, Vector3 ceilingPoint, float drop, Transform parent, Transform lighting, Color color,
        float intensity, float range)
    {
        GameObject model = TryModel("lampara_techo", name, ceilingPoint - Vector3.up * (drop + 0.25f), Quaternion.identity, parent);
        if (model == null)
        {
            Mesh mesh = GetMesh("LamparaTecho_" + Cm(drop), b =>
            {
                b.Submesh = 0;
                AddBox(b, new Vector3(0f, -drop * 0.5f, 0f), new Vector3(0.025f, drop, 0.025f), Quaternion.identity, 0f, 0);
                AddLathe(b, Vector3.zero, Quaternion.identity, new[] { -0.03f, 0f }, new[] { 0.09f, 0.08f }, 10);
                b.Submesh = 1;
                AddLathe(b, new Vector3(0f, -drop, 0f), Quaternion.identity, new[] { -0.24f, -0.2f, -0.1f, 0f },
                    new[] { 0.32f, 0.3f, 0.18f, 0.05f }, 16, false, true);
                b.Submesh = 2;
                AddLathe(b, new Vector3(0f, -drop - 0.2f, 0f), Quaternion.identity, new[] { -0.08f, -0.04f, 0f },
                    new[] { 0f, 0.07f, 0.05f }, 10, false, false);
            });
            MeshObject(name, mesh, new[] { M("Metal"), M("Ventana"), M("Bombilla") }, parent, ceilingPoint, Quaternion.identity);
        }

        Vector3 bulb = ceilingPoint - Vector3.up * (drop + 0.26f);
        Light light = CreatePointLight(name + "_Luz", bulb, color, intensity, range, lighting);
        LightCone(name + "_Haz", bulb, bulb.y, 1.3f, color, lighting, 0.07f);
        return light;
    }

    /// <summary>Farol de calle: poste torneado, brazo curvo, farolillo de campana + luz + haz. armYaw = hacia dónde sale el brazo.</summary>
    public static Light StreetLamp(string name, Vector3 basePos, float armYaw, Transform parent, Transform lighting, Color color,
        float intensity, float range, float tilt = 0f, bool broken = false)
    {
        Quaternion rot = Quaternion.Euler(0f, armYaw, tilt);
        Vector3 lanternLocal = new Vector3(0f, 3.75f, 1.0f);
        GameObject model = TryModel("farol", name, basePos, rot, parent);
        if (model == null)
        {
            Mesh mesh = GetMesh(broken ? "FarolRoto" : "Farol", b =>
            {
                b.Submesh = 0;
                b.Lathe(new[] { 0f, 0.12f, 0.3f, 0.45f, 2.5f, 4.0f, 4.15f, 4.25f },
                    new[] { 0.22f, 0.2f, 0.12f, 0.09f, 0.07f, 0.06f, 0.08f, 0f }, 12, true, false);
                // Brazo: sale del poste, sube en curva y baja al farolillo
                const int pieces = 10;
                for (int i = 0; i < pieces; i++)
                {
                    float a = Mathf.PI * (i + 0.5f) / pieces; // 0..180°
                    Vector3 p = new Vector3(0f, 3.95f + Mathf.Sin(a) * 0.3f, 0.5f - Mathf.Cos(a) * 0.5f);
                    AddBox(b, p, new Vector3(0.06f, 0.06f, 0.18f), Quaternion.Euler(-(90f - a * Mathf.Rad2Deg), 0f, 0f), 0.02f);
                }
                // Voluta decorativa bajo el brazo
                AddLathe(b, new Vector3(0f, 3.7f, 0.18f), Quaternion.Euler(0f, 0f, 90f), new[] { -0.03f, 0.03f }, new[] { 0.12f, 0.12f }, 12, true, true);
                if (broken)
                {
                    // Cable pelado que cuelga del farolillo y un pedazo de vidrio roto
                    AddBox(b, new Vector3(0.05f, 3.2f, 1.0f), new Vector3(0.02f, 0.9f, 0.02f), Quaternion.Euler(0f, 0f, 12f), 0.005f, 0);
                    AddBox(b, new Vector3(0.18f, 2.8f, 1.02f), new Vector3(0.1f, 0.14f, 0.02f), Quaternion.Euler(0f, 0f, 28f), 0.005f, 0);
                }
                b.Submesh = 1;
                AddLathe(b, new Vector3(0f, 3.95f, 1.0f), Quaternion.identity, new[] { -0.32f, -0.26f, -0.1f, 0f, 0.05f },
                    new[] { 0.26f, 0.25f, 0.14f, 0.05f, 0.02f }, 14, false, true);
                b.Submesh = 2;
                AddLathe(b, new Vector3(0f, 3.75f, 1.0f), Quaternion.identity, new[] { -0.1f, -0.05f, 0f, 0.06f },
                    new[] { 0f, 0.09f, 0.1f, 0.05f }, 10, false, false);
            });
            MeshObject(name, mesh, new[] { M("Metal"), M("Metal"), M("Bombilla") }, parent, basePos, rot);
        }

        Vector3 lantern = basePos + rot * lanternLocal;
        Light light = CreatePointLight(name + "_Luz", lantern - Vector3.up * 0.05f, color, intensity, range, lighting);
        LightCone(name + "_Haz", lantern, lantern.y, 2.2f, color, lighting, 0.05f);
        return light;
    }

    // ==================================================================
    // Techo de paja
    // ==================================================================

    /// <summary>
    /// Techo de paja ralo: cuatro aguas abultadas hechas con una malla de facetas (cada celda ~0.6 m), con agujeros
    /// de gotera (holes: x,z de MUNDO y radio en y), celdas que faltan al azar, parches de paja oscura, la cara de abajo
    /// oscura y flecos dentados colgando del alero. center = centro del alero. Sin collider.
    /// </summary>
    public static GameObject ThatchedRoof(string name, Vector3 center, float sizeX, float sizeZ, float height, Transform parent,
        params Vector3[] holes)
    {
        string key = $"TechoPaja_{name}_{Cm(sizeX)}x{Cm(sizeZ)}x{Cm(height)}_h{holes.Length}";
        Mesh mesh = GetMesh(key, b =>
        {
            System.Random rng = Rng(key);
            int nx = Mathf.Max(8, Mathf.CeilToInt(sizeX / 0.6f)), nz = Mathf.Max(6, Mathf.CeilToInt(sizeZ / 0.6f));
            var grid = new Vector3[nx + 1, nz + 1];
            for (int j = 0; j <= nz; j++)
            for (int i = 0; i <= nx; i++)
            {
                float u = i / (float)nx * 2f - 1f, v = j / (float)nz * 2f - 1f;
                float r = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                float wx = center.x + u * sizeX * 0.5f, wz = center.z + v * sizeZ * 0.5f;
                float hgt = height * Mathf.Pow(1f - r, 0.85f);
                hgt += (Mathf.PerlinNoise(wx * 1.3f + 5f, wz * 1.3f + 9f) - 0.5f) * 0.2f * Mathf.Clamp01(1f - r);
                grid[i, j] = new Vector3(u * sizeX * 0.5f, Mathf.Max(hgt, r > 0.97f ? -0.06f : 0f), v * sizeZ * 0.5f);
            }

            for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                Vector3 pa = grid[i, j], pb = grid[i + 1, j], pc = grid[i + 1, j + 1], pd = grid[i, j + 1];
                float cx = center.x + (pa.x + pc.x) * 0.5f, cz = center.z + (pa.z + pc.z) * 0.5f;
                float ru = Mathf.Max(Mathf.Abs((pa.x + pc.x) / sizeX), Mathf.Abs((pa.z + pc.z) / sizeZ));

                bool hole = false;
                foreach (Vector3 hl in holes)
                {
                    float d = Vector2.Distance(new Vector2(cx, cz), new Vector2(hl.x, hl.z));
                    if (d < hl.y * (0.8f + 0.5f * Mathf.PerlinNoise(cx * 3f + 1f, cz * 3f + 2f))) hole = true;
                }
                if (hole || (ru < 0.85f && rng.NextDouble() < 0.05)) continue;                  // agujero o celda perdida

                b.Submesh = rng.NextDouble() < 0.3 ? 1 : 0;
                Tri3(b, pa, pd, pc);
                Tri3(b, pa, pc, pb);
                b.Submesh = 2;                                                                   // cara de abajo, oscura
                Tri3(b, pa, pc, pd, true);
                Tri3(b, pa, pb, pc, true);
            }

            // Flecos dentados en el alero, dos capas
            for (int layer = 0; layer < 2; layer++)
            {
                b.Submesh = layer == 0 ? 1 : 0;
                float grow = 1f + layer * 0.03f;
                float hx = sizeX * 0.5f * grow, hz = sizeZ * 0.5f * grow;
                Vector3[] corners = { new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz), new Vector3(hx, 0f, hz), new Vector3(-hx, 0f, hz) };
                for (int e = 0; e < 4; e++)
                {
                    Vector3 a = corners[e], c = corners[(e + 1) % 4];
                    Vector3 outward = Vector3.Cross(Vector3.up, c - a).normalized;
                    float len = Vector3.Distance(a, c);
                    int teeth = Mathf.Max(3, Mathf.RoundToInt(len / 0.3f));
                    var pts = new List<Vector3>();
                    for (int t = 0; t <= teeth; t++)
                    {
                        Vector3 p = Vector3.Lerp(a, c, t / (float)teeth) + outward * 0.06f;
                        pts.Add(p + Vector3.up * (0.02f - layer * 0.08f));
                        if (t < teeth)
                        {
                            Vector3 mid = Vector3.Lerp(a, c, (t + 0.5f) / teeth) + outward * 0.09f;
                            pts.Add(mid + Vector3.down * (Range(rng, 0.22f, 0.42f) + layer * 0.06f));
                        }
                    }
                    for (int t = 0; t + 2 < pts.Count; t += 2)
                        if (rng.NextDouble() > 0.12) b.Card(new[] { pts[t], pts[t + 2], pts[t + 1] }, outward); // fleco que falta
                }
            }
            b.Submesh = 2;
        });
        GameObject go = MeshObject(name, mesh, new[] { M("Paja"), M("PajaOscura"), M("PajaOscura") }, parent, center, Quaternion.identity);
        go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        return go;
    }

    /// <summary>Triángulo plano con su normal; flip = cara de abajo.</summary>
    private static void Tri3(KitMesh b, Vector3 p0, Vector3 p1, Vector3 p2, bool flip = false)
    {
        Vector3 n = Vector3.Cross(p1 - p0, p2 - p0).normalized;
        if (flip) n = -n;
        int a = b.Vertex(p0, n, Vector2.zero), c = b.Vertex(p1, n, Vector2.right), d = b.Vertex(p2, n, Vector2.up);
        b.Tri(a, c, d);
    }

    // ==================================================================
    // Cerca
    // ==================================================================

    /// <summary>
    /// Cerca de madera: postes, dos travesaños y tablas puntiagudas de alturas desparejas.
    /// Colisión: tramos de caja invisibles (mismos huecos que una pared). "cerca.fbx" se repite cada 2 m.
    /// </summary>
    public static Transform Fence(string name, Vector3 start, Vector3 end, float height, Transform parent, params WallOpening[] openings)
    {
        Transform fence = CreateWallWithOpenings(name, start, end, height, 0.2f, M("Madera"), parent, openings);
        foreach (MeshRenderer r in fence.GetComponentsInChildren<MeshRenderer>()) r.enabled = false;

        Vector3 delta = end - start;
        delta.y = 0f;
        float length = delta.magnitude;
        if (length < 0.01f) return fence;
        Vector3 dir = delta / length;
        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
        List<Vector4> segments = WallSegments(length, height, openings);

        bool hasModel = false;
        foreach (string ext in new[] { ".fbx", ".glb", ".gltf", ".obj" })
            hasModel |= AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + "/cerca" + ext) != null;
        if (hasModel)
        {
            int n = 0;
            foreach (Vector4 seg in segments)
                for (float z = seg.x + 1f; z < seg.y; z += 2f)
                {
                    GameObject m = TryModel("cerca", $"{name}_Modelo{n++}", start + dir * z, rot * Quaternion.Euler(0f, 90f, 0f), fence);
                    if (m != null) UnityEngine.Object.DestroyImmediate(m.GetComponent<BoxCollider>());
                }
            return fence;
        }

        string key = $"Cerca_{name}_{Cm(length)}_{Cm(height)}";
        Mesh mesh = GetMesh(key, b =>
        {
            System.Random rng = Rng(key);
            foreach (Vector4 seg in segments)
            {
                if (seg.z > 0.01f) continue;
                float len = seg.y - seg.x;
                b.Submesh = 0;
                int posts = Mathf.Max(1, Mathf.CeilToInt(len / 1.8f));
                for (int p = 0; p <= posts; p++)
                {
                    float z = seg.x + len * p / posts;
                    AddBox(b, new Vector3(0f, height * 0.52f, z), new Vector3(0.13f, height * 1.04f, 0.13f),
                        Quaternion.Euler(Range(rng, -2f, 2f), 0f, Range(rng, -2f, 2f)), 0.04f, 1);
                }
                foreach (float y in new[] { height * 0.28f, height * 0.75f })
                    AddBox(b, new Vector3(0.07f, y, (seg.x + seg.y) * 0.5f), new Vector3(0.05f, 0.09f, len),
                        Quaternion.Euler(0f, 0f, Range(rng, -1.5f, 1.5f)), 0.02f, 1);
                b.Submesh = 1;
                for (float z = seg.x + 0.1f; z < seg.y - 0.05f; z += 0.17f)
                {
                    double roll = rng.NextDouble();
                    if (roll < 0.14) continue;                                                    // tabla que falta
                    float h = height * Range(rng, 0.82f, 0.98f);
                    if (roll > 0.88) h = height * Range(rng, 0.3f, 0.6f);                         // tabla rota
                    float tilt = roll > 0.6 && roll < 0.78 ? Range(rng, -14f, 14f) : Range(rng, -3f, 3f);
                    var q = Quaternion.Euler(0f, 0f, tilt);
                    AddBox(b, new Vector3(0.11f, h * 0.5f, z), new Vector3(0.03f, h, 0.12f), q, 0.012f, 1);
                    // Punta
                    Vector3 tip = new Vector3(0.11f, h, z);
                    b.Card(new[] { tip + new Vector3(0.016f, -0.01f, -0.06f), tip + new Vector3(0.016f, 0.09f, 0f), tip + new Vector3(0.016f, -0.01f, 0.06f) }, Vector3.right);
                    b.Card(new[] { tip + new Vector3(-0.016f, -0.01f, -0.06f), tip + new Vector3(-0.016f, 0.09f, 0f), tip + new Vector3(-0.016f, -0.01f, 0.06f) }, Vector3.left);
                }
            }
        });
        MeshObject(name + "_Tablas", mesh, new[] { M("MaderaOscura"), M("Madera") }, fence, start, rot);
        return fence;
    }

    // ==================================================================
    // Terreno, adoquines y caminos
    // ==================================================================

    /// <summary>
    /// Suelo irregular en cuadrícula con MeshCollider. flatness(x,z) en mundo: 0 = plano (casa, caminos, bordes), 1 = libre.
    /// </summary>
    public static GameObject UnevenGround(string name, Vector3 center, Vector2 size, float cell, float amplitude,
        Func<float, float, float> flatness, Material mat, Transform parent)
    {
        string key = $"Terreno_{name}_{Cm(size.x)}x{Cm(size.y)}";
        int nx = Mathf.Max(2, Mathf.CeilToInt(size.x / cell)), nz = Mathf.Max(2, Mathf.CeilToInt(size.y / cell));
        Mesh mesh = GetMesh(key, b =>
        {
            var heights = new float[(nx + 1) * (nz + 1)];
            for (int j = 0; j <= nz; j++)
            for (int i = 0; i <= nx; i++)
            {
                float x = -size.x * 0.5f + size.x * i / nx, z = -size.y * 0.5f + size.y * j / nz;
                float wx = center.x + x, wz = center.z + z;
                float n = Mathf.PerlinNoise(wx * 0.18f + 11.3f, wz * 0.18f + 4.7f) - 0.5f;
                n += (Mathf.PerlinNoise(wx * 0.6f + 2.1f, wz * 0.6f + 9.4f) - 0.5f) * 0.35f;
                heights[j * (nx + 1) + i] = n * 2f * amplitude * Mathf.Clamp01(flatness(wx, wz));
            }
            for (int j = 0; j <= nz; j++)
            for (int i = 0; i <= nx; i++)
            {
                float x = -size.x * 0.5f + size.x * i / nx, z = -size.y * 0.5f + size.y * j / nz;
                float hl = heights[j * (nx + 1) + Mathf.Max(0, i - 1)], hr = heights[j * (nx + 1) + Mathf.Min(nx, i + 1)];
                float hd = heights[Mathf.Max(0, j - 1) * (nx + 1) + i], hu = heights[Mathf.Min(nz, j + 1) * (nx + 1) + i];
                Vector3 n = new Vector3(hl - hr, 2f * size.x / nx, hd - hu).normalized;
                b.Vertex(new Vector3(x, heights[j * (nx + 1) + i], z), n, new Vector2(x, z));
            }
            for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                int a = j * (nx + 1) + i, c = a + nx + 2;
                b.Tri(a, c, a + 1);
                b.Tri(a, a + nx + 1, c);
            }
        });
        GameObject go = MeshObject(name, mesh, new[] { mat }, parent, center, Quaternion.identity);
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        return go;
    }

    /// <summary>Calle de adoquines redondeados sobre una base oscura (collider plano en la base).</summary>
    public static GameObject Cobblestones(string name, Vector3 center, Vector2 size, Transform parent)
    {
        GameObject root = Box(name, center + Vector3.down * 0.1f, new Vector3(size.x, 0.2f, size.y), M("PiedraOscura"), parent, default, 0f);
        string key = $"Adoquines_{name}_{Cm(size.x)}x{Cm(size.y)}";
        Mesh mesh = GetMesh(key, b =>
        {
            System.Random rng = Rng(key);
            const float step = 0.55f;
            int nx = Mathf.FloorToInt(size.x / step), nz = Mathf.FloorToInt(size.y / step);
            for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                float x = -size.x * 0.5f + step * (i + 0.5f + (j % 2) * 0.5f);
                if (x > size.x * 0.5f - step * 0.4f) continue;
                float z = -size.y * 0.5f + step * (j + 0.5f);
                b.Submesh = rng.NextDouble() < 0.25 ? 1 : 0;
                AddBox(b, new Vector3(x + Range(rng, -0.03f, 0.03f), 0.01f + Range(rng, -0.01f, 0.01f), z + Range(rng, -0.03f, 0.03f)),
                    new Vector3(step * Range(rng, 0.78f, 0.9f), 0.09f, step * Range(rng, 0.75f, 0.88f)),
                    Quaternion.Euler(0f, Range(rng, -6f, 6f), 0f), 0.04f, 1);
            }
        });
        GameObject stones = MeshObject(name + "_Piedras", mesh, new[] { M("Piedra"), M("PiedraOscura") }, root.transform, center, Quaternion.identity);
        stones.transform.position = center;
        return root;
    }

    /// <summary>Camino de tierra: cinta con bordes irregulares por encima del suelo plano. Sin collider.</summary>
    public static GameObject DirtPath(string name, Vector3[] points, float width, Transform parent)
    {
        string key = "Camino_" + name;
        Mesh mesh = GetMesh(key, b =>
        {
            System.Random rng = Rng(key);
            int prevL = -1, prevR = -1;
            for (int s = 0; s < points.Length - 1; s++)
            {
                Vector3 a = points[s], c = points[s + 1];
                Vector3 dir = (c - a).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, c) / 0.4f));
                for (int k = (s == 0 ? 0 : 1); k <= steps; k++)
                {
                    Vector3 p = Vector3.Lerp(a, c, k / (float)steps);
                    float wl = width * 0.5f * Range(rng, 0.8f, 1.15f), wr = width * 0.5f * Range(rng, 0.8f, 1.15f);
                    int l = b.Vertex(p - side * wl + Vector3.up * 0.015f, Vector3.up, new Vector2(0f, k));
                    int r = b.Vertex(p + side * wr + Vector3.up * 0.015f, Vector3.up, new Vector2(1f, k));
                    if (prevL >= 0) { b.Tri(prevL, l, r); b.Tri(prevL, r, prevR); }
                    prevL = l;
                    prevR = r;
                }
            }
        });
        return MeshObject(name, mesh, new[] { M("Tierra") }, parent, Vector3.zero, Quaternion.identity);
    }

    // ==================================================================
    // Fachadas de la calle
    // ==================================================================

    /// <summary>
    /// Hilera de edificios de enfrente (x de xMin a xMax, fachada en zFront mirando a +Z): bloques de alturas y
    /// colores distintos, cornisas, ventanas (algunas encendidas) y puertas. Collider por bloque.
    /// </summary>
    public static Transform Facades(string name, float xMin, float xMax, float zFront, float depth, Transform parent)
    {
        Transform root = CreateGroup(name, parent);
        System.Random rng = Rng(name);
        string[] walls = { "Ladrillo", "Revoque", "RevoqueCalido" };
        float x = xMin;
        int n = 0;
        while (x < xMax - 0.5f)
        {
            float w = Mathf.Min(Range(rng, 6f, 9f), xMax - x);
            float h = Range(rng, 5.5f, 9f);
            float cx = x + w * 0.5f;
            string wallMat = walls[n % walls.Length];
            Box($"{name}_Bloque{n}", new Vector3(cx, h * 0.5f, zFront - depth * 0.5f), new Vector3(w - 0.1f, h, depth), M(wallMat), root, default, 0.12f);
            Box($"{name}_Cornisa{n}", new Vector3(cx, h + 0.1f, zFront - depth * 0.5f + 0.1f), new Vector3(w + 0.1f, 0.3f, depth + 0.3f),
                M("TechoEdificio"), root, default, 0.1f, false);
            Box($"{name}_Zocalo{n}", new Vector3(cx, 0.3f, zFront + 0.05f), new Vector3(w - 0.1f, 0.6f, 0.15f), M("PiedraOscura"), root, default, 0.06f, false);

            int cols = Mathf.Max(1, Mathf.FloorToInt(w / 2.2f));
            int floors = Mathf.Max(1, Mathf.FloorToInt((h - 1.5f) / 2.6f));
            for (int f = 0; f < floors; f++)
            for (int c = 0; c < cols; c++)
            {
                float wx = x + w * (c + 0.5f) / cols, wy = 2f + f * 2.6f;
                if (f == 0 && c == cols / 2)
                {
                    DoorLeaf($"{name}_Puerta{n}", new Vector3(wx, 1.1f, zFront + 0.04f), Quaternion.identity, 1.1f, 2.2f, M("Puerta"), root);
                    continue;
                }
                WindowFrame($"{name}_Ventana{n}_{f}_{c}", new Vector3(wx, wy, zFront + 0.07f), Quaternion.identity, 1f, 1.2f, root,
                    rng.NextDouble() < 0.55);
            }
            x += w;
            n++;
        }
        return root;
    }
}
