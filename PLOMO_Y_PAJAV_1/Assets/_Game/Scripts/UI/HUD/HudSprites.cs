using UnityEngine;

/// <summary>
/// Dibujos del HUD hechos por código (fase HUD.1): sirven de base (panel redondeado, borde de pantalla, cuña de dirección)
/// y de placeholder cuando falta el arte. Se crean UNA vez y quedan en caché (sin basura después).
/// </summary>
public static class HudSprites
{
    private static Sprite _panel, _circle, _wedge, _edge;

    /// <summary>Rectángulo redondeado blanco, 9-slice (borde 10 px): barra, marco, papel del aviso, placeholders.</summary>
    public static Sprite Panel
    {
        get
        {
            if (_panel == null)
                _panel = Build("HudPanel", 32, 32, (x, y) => RoundedRect(x, y, 32, 32, 9f), new Vector4(10, 10, 10, 10));
            return _panel;
        }
    }

    /// <summary>Círculo blanco con borde suave (placeholder de la cara).</summary>
    public static Sprite Circle
    {
        get
        {
            if (_circle == null)
                _circle = Build("HudCircle", 64, 64, (x, y) => Mathf.Clamp01(31.5f - Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f))), Vector4.zero);
            return _circle;
        }
    }

    /// <summary>Cuña (punta hacia el centro de la pantalla = abajo del dibujo) con contorno oscuro: indicador de dirección.</summary>
    public static Sprite Wedge
    {
        get
        {
            if (_wedge == null) _wedge = BuildWedge();
            return _wedge;
        }
    }

    /// <summary>Borde de pantalla: transparente en el centro, opaco en los bordes (destello rojo y latido de poca vida).</summary>
    public static Sprite Edge
    {
        get
        {
            if (_edge == null)
                _edge = Build("HudEdge", 128, 128, (x, y) =>
                {
                    float u = (x + 0.5f) / 128f, v = (y + 0.5f) / 128f;
                    float d = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)); // 0 en el borde, 0.5 en el centro
                    return 1f - Mathf.SmoothStep(0f, 1f, d / 0.22f);
                }, Vector4.zero);
            return _edge;
        }
    }

    private static float RoundedRect(int x, int y, int w, int h, float r)
    {
        float px = x + 0.5f, py = y + 0.5f;
        float dx = Mathf.Max(r - px, px - (w - r), 0f);
        float dy = Mathf.Max(r - py, py - (h - r), 0f);
        return Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
    }

    private static Sprite BuildWedge()
    {
        const int w = 64, h = 48;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "HudWedge", wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // Triángulo redondeado: ancho arriba (afuera), punta abajo (hacia el centro)
                float t = (y + 0.5f) / h;                    // 0 abajo (punta) .. 1 arriba
                float half = Mathf.Lerp(3f, w * 0.5f - 2f, t); // medio ancho a esta altura
                float dx = Mathf.Abs(x + 0.5f - w * 0.5f);
                float inside = Mathf.Clamp01(half - dx + 0.5f) * Mathf.Clamp01((h - 2f) - y + 0.5f) * Mathf.Clamp01(y - 1f + 0.5f);
                float core = Mathf.Clamp01(half - 4f - dx + 0.5f) * Mathf.Clamp01((h - 6f) - y + 0.5f) * Mathf.Clamp01(y - 5f + 0.5f);
                // contorno casi negro, relleno blanco (se tiñe de rojo con Image.color)
                byte c = (byte)Mathf.RoundToInt(Mathf.Lerp(30f, 255f, core));
                px[y * w + x] = new Color32(c, c, c, (byte)Mathf.RoundToInt(inside * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
    }

    private static Sprite Build(string name, int w, int h, System.Func<int, int, float> alpha, Vector4 border)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(x, y)) * 255f));
        tex.SetPixels32(px);
        tex.Apply(false, true);
        Sprite s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        s.name = name;
        return s;
    }
}
