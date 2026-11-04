using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fase N1.1: siluetas con semilla para las tarjetas de fondo (Art/Environment/fondos/gen_&lt;id&gt;.png).
/// Si está el arte de Edgar (fondo_ciudad_01, fondo_colinas_01, fondo_bosque_01...: &lt;id&gt;.png en la misma carpeta) gana el arte;
/// basta con volver a correr el menú. Esta clase pone sus propios ajustes de importación (no hace falta tocar CharacterSpriteImporter).
/// </summary>
public static class BackdropTextureGenerator
{
    public const string Folder = "Assets/_Game/Art/Environment/fondos";
    public const string City = "fondo_ciudad_01";
    public const string Hills = "fondo_colinas_01";
    public const string Forest = "fondo_bosque_01";
    public const string Houses = "fondo_casas_01";
    public const string CampLight = "fondo_luz_campamento";

    private const int Width = 1024, Height = 512;

    private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
    private static readonly Color32 Ink = new Color32(0x1E, 0x18, 0x2E, 0xFF);
    private static readonly Color32 Lit = new Color32(0xFF, 0xD2, 0x6A, 0xFF);

    /// <summary>Textura para ese id: el arte de Edgar si existe; si no, la generada (se crea si falta).</summary>
    public static Texture2D Get(string id, StringBuilder report)
    {
        LevelBuilderUtils.EnsureFolder(Folder);
        string art = $"{Folder}/{id}.png";
        if (File.Exists(art))
        {
            ApplyImport(art);
            report.AppendLine($"✓ Fondo {id}: arte de Edgar");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(art);
        }

        string generated = $"{Folder}/gen_{id}.png";
        if (!File.Exists(generated))
        {
            Texture2D drawn = Draw(id);
            File.WriteAllBytes(generated, drawn.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(drawn);
            AssetDatabase.ImportAsset(generated, ImportAssetOptions.ForceUpdate);
        }
        ApplyImport(generated);
        report.AppendLine($"⚠ Fondo {id}: no está el arte, se usa la silueta generada");
        return AssetDatabase.LoadAssetAtPath<Texture2D>(generated);
    }

    private static void ApplyImport(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        if (importer.textureType == TextureImporterType.Default && importer.alphaIsTransparency &&
            importer.wrapMode == TextureWrapMode.Clamp && importer.mipmapEnabled) return;
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
    }

    private static Texture2D Draw(string id)
    {
        var px = new Color32[Width * Height];
        for (int i = 0; i < px.Length; i++) px[i] = Clear;
        switch (id)
        {
            case City: DrawCity(px); break;
            case Hills: DrawHills(px); break;
            case Forest: DrawForest(px); break;
            case Houses: DrawHouses(px); break;
            default: DrawGlow(px); break;
        }
        if (id != CampLight) Outline(px);
        var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // ==================================================================
    // Pintura
    // ==================================================================

    private static void Rect(Color32[] px, int x0, int y0, int x1, int y1, Color32 c, float lean = 0f)
    {
        for (int y = Mathf.Max(0, y0); y < Mathf.Min(Height, y1); y++)
        {
            int shift = Mathf.RoundToInt((y - y0) * lean);
            for (int x = Mathf.Max(0, x0 + shift); x < Mathf.Min(Width, x1 + shift); x++) px[y * Width + x] = c;
        }
    }

    private static void Disc(Color32[] px, float cx, float cy, float r, float squash, Color32 c)
    {
        int ry = Mathf.CeilToInt(r * squash);
        for (int y = Mathf.Max(0, (int)cy - ry); y < Mathf.Min(Height, (int)cy + ry + 1); y++)
        for (int x = Mathf.Max(0, (int)(cx - r)); x < Mathf.Min(Width, (int)(cx + r) + 1); x++)
        {
            float dx = (x - cx) / r, dy = (y - cy) / (r * squash);
            if (dx * dx + dy * dy <= 1f) px[y * Width + x] = c;
        }
    }

    private static void Triangle(Color32[] px, int x0, int x1, int y0, int apexX, int apexY, Color32 c)
    {
        for (int y = Mathf.Max(0, y0); y <= Mathf.Min(Height - 1, apexY); y++)
        {
            float t = (y - y0) / Mathf.Max(1f, apexY - y0);
            int a = Mathf.RoundToInt(Mathf.Lerp(x0, apexX, t)), b = Mathf.RoundToInt(Mathf.Lerp(x1, apexX, t));
            for (int x = Mathf.Max(0, a); x <= Mathf.Min(Width - 1, b); x++) px[y * Width + x] = c;
        }
    }

    private static void FillBelow(Color32[] px, Func<int, float> top, Color32 c)
    {
        for (int x = 0; x < Width; x++)
        {
            int h = Mathf.Min(Height, Mathf.RoundToInt(top(x)));
            for (int y = 0; y < h; y++) px[y * Width + x] = c;
        }
    }

    private static void Outline(Color32[] px)
    {
        var source = (Color32[])px.Clone();
        for (int y = 1; y < Height - 1; y++)
        for (int x = 1; x < Width - 1; x++)
        {
            int i = y * Width + x;
            if (source[i].a == 0) continue;
            if (source[i - 1].a == 0 || source[i + 1].a == 0 || source[i - Width].a == 0 || source[i + Width].a == 0) px[i] = Ink;
        }
    }

    // ==================================================================
    // Siluetas
    // ==================================================================

    private static void DrawCity(Color32[] px)
    {
        var rng = new System.Random(1931);
        Color32[] walls = { new Color32(0x2F, 0x2C, 0x55, 0xFF), new Color32(0x3A, 0x33, 0x60, 0xFF), new Color32(0x28, 0x28, 0x4C, 0xFF) };
        int x = 0, n = 0;
        while (x < Width)
        {
            int w = rng.Next(38, 96), h = rng.Next(110, 370);
            float lean = (float)(rng.NextDouble() * 0.08 - 0.04);
            Color32 wall = walls[n % walls.Length];
            Rect(px, x, 0, x + w, h, wall, lean);
            for (int wy = 24; wy < h - 16; wy += 26)
            for (int wx = x + 7; wx < x + w - 10; wx += 16)
                if (rng.NextDouble() < 0.32)
                {
                    int shift = Mathf.RoundToInt(wy * lean);
                    Rect(px, wx + shift, wy, wx + 8 + shift, wy + 12, Lit);
                }

            int roofShift = Mathf.RoundToInt(h * lean);
            if (n % 5 == 2) // tanque de agua
            {
                int cx = x + w / 2 + roofShift;
                Rect(px, cx - 15, h, cx - 11, h + 22, Ink);
                Rect(px, cx + 11, h, cx + 15, h + 22, Ink);
                Rect(px, cx - 20, h + 20, cx + 20, h + 54, wall);
                Triangle(px, cx - 23, cx + 23, h + 54, cx, h + 74, Ink);
            }
            else if (n % 3 == 0) // chimenea
            {
                Rect(px, x + w / 3 + roofShift, h, x + w / 3 + 12 + roofShift, h + rng.Next(22, 52), wall);
            }
            x += w - 2;
            n++;
        }
    }

    private static void DrawHills(Color32[] px)
    {
        FillBelow(px, x => 230f + 62f * Mathf.Sin(x * 0.0091f + 0.8f) + 26f * Mathf.Sin(x * 0.027f + 2.1f), new Color32(0x3F, 0x4E, 0x6A, 0xFF));
        FillBelow(px, x => 150f + 44f * Mathf.Sin(x * 0.0128f + 3.4f) + 18f * Mathf.Sin(x * 0.041f), new Color32(0x35, 0x52, 0x52, 0xFF));
    }

    private static void DrawForest(Color32[] px)
    {
        var rng = new System.Random(1933);
        Color32 back = new Color32(0x2A, 0x45, 0x48, 0xFF), front = new Color32(0x20, 0x3A, 0x34, 0xFF);
        FillBelow(px, x => 200f + 18f * Mathf.Sin(x * 0.02f), back);
        foreach (bool isFront in new[] { false, true })
        {
            Color32 layer = isFront ? front : back;
            float baseY = isFront ? 130f : 200f;
            for (int x = -20; x < Width + 20; x += rng.Next(22, 44))
            {
                float top = baseY + rng.Next(40, 120);
                Rect(px, x - 5, 0, x + 5, (int)top, layer);
                for (int k = 0; k < 4; k++)
                    Disc(px, x + rng.Next(-14, 15), top - k * 30f, 34f - k * 3f, 0.8f, layer);
            }
            if (isFront) FillBelow(px, x => 110f + 10f * Mathf.Sin(x * 0.05f), front);
        }
    }

    /// <summary>La colina del libro: la casa de ladrillo de Wilbur y la de madera de Max, con las ventanas encendidas.</summary>
    private static void DrawHouses(Color32[] px)
    {
        Color32 hill = new Color32(0x4A, 0x6B, 0x45, 0xFF);
        Func<int, float> top = x =>
        {
            float t = (x - Width * 0.5f) / (Width * 0.5f);
            return 96f + 222f * Mathf.Pow(Mathf.Max(0f, 1f - t * t), 1.6f) + 7f * Mathf.Sin(x * 0.03f);
        };
        FillBelow(px, top, hill);

        // Casa de Wilbur: ladrillo rojo, techo oscuro y chimenea
        int bx = 340, by = Mathf.RoundToInt(top(bx + 60)) - 6;
        Color32 brick = new Color32(0xA5, 0x45, 0x2F, 0xFF), mortar = new Color32(0x7C, 0x30, 0x24, 0xFF);
        Rect(px, bx, by, bx + 124, by + 84, brick);
        for (int y = by + 8; y < by + 84; y += 9) Rect(px, bx, y, bx + 124, y + 1, mortar);
        for (int y = by; y < by + 84; y += 18)
        for (int x = bx + 8; x < bx + 124; x += 18) Rect(px, x, y, x + 1, y + 9, mortar);
        Rect(px, bx + 92, by + 80, bx + 108, by + 136, mortar);
        Triangle(px, bx - 12, bx + 136, by + 84, bx + 62, by + 132, new Color32(0x3A, 0x2A, 0x38, 0xFF));
        Rect(px, bx + 52, by, bx + 74, by + 42, Ink);
        Rect(px, bx + 14, by + 36, bx + 40, by + 62, Lit);
        Rect(px, bx + 86, by + 36, bx + 112, by + 62, Lit);

        // Casa de Max: tablas de madera, adornos dorados
        int wx = 560, wy = Mathf.RoundToInt(top(wx + 52)) - 6;
        Color32 wood = new Color32(0x8A, 0x5A, 0x33, 0xFF), plank = new Color32(0x63, 0x3F, 0x24, 0xFF), gold = new Color32(0xE8, 0xB8, 0x3A, 0xFF);
        Rect(px, wx, wy, wx + 106, wy + 72, wood);
        for (int x = wx + 10; x < wx + 106; x += 12) Rect(px, x, wy, x + 2, wy + 72, plank);
        Triangle(px, wx - 12, wx + 118, wy + 72, wx + 53, wy + 116, plank);
        Rect(px, wx - 12, wy + 70, wx + 118, wy + 75, gold);
        Rect(px, wx + 44, wy, wx + 64, wy + 36, Ink);
        Rect(px, wx + 12, wy + 30, wx + 34, wy + 54, Lit);
        Rect(px, wx + 74, wy + 30, wx + 96, wy + 54, Lit);
        Disc(px, wx + 53, wy + 94, 7f, 1f, gold);
    }

    private static void DrawGlow(Color32[] px)
    {
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            float dx = (x - Width * 0.5f) / (Width * 0.5f), dy = (y - Height * 0.5f) / (Height * 0.5f);
            float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
            px[y * Width + x] = new Color32(0xFF, 0xB8, 0x60, (byte)(a * a * 255f));
        }
    }
}
