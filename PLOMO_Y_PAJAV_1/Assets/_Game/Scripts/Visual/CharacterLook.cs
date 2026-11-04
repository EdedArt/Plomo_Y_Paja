using UnityEngine;

/// <summary>
/// Regla de legibilidad (fase 3A2): personajes, pistas y objetos = sin iluminación, planos, con contorno.
/// El escenario = pintado, suave, sin contorno. Este helper reparte los materiales de personaje.
///
/// Los materiales viven en Resources/CharacterLook/ (los crea "Plomo y Paja/Crear Materiales de Personajes",
/// que también corre "Crear Prefabs de Prueba"). Si faltan, se intenta crearlos desde el shader;
/// si tampoco hay shader, todo se queda con su material de antes (nada se rompe).
/// </summary>
public static class CharacterLook
{
    public const string ResourceFolder = "CharacterLook";
    public const string SpriteMaterialName = "PersonajeSprite";
    public const string PlaceholderMaterialName = "PersonajePlaceholder";
    public const string BlobMaterialName = "SombraBlob";

    public const string SpriteShaderName = "PlomoYPaja/CharacterSpriteUnlit";
    public const string PlaceholderShaderName = "PlomoYPaja/CharacterPlaceholderUnlit";
    public const string BlobShaderName = "PlomoYPaja/BlobShadow";

    public static readonly int FlashId = Shader.PropertyToID("_FlashAmount");
    public static readonly int TintId = Shader.PropertyToID("_Tint");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private static Material _sprite, _placeholder, _blob;
    private static bool _loaded;
    private static MaterialPropertyBlock _mpb;

    public static Material SpriteMaterial { get { Load(); return _sprite; } }
    public static Material PlaceholderMaterial { get { Load(); return _placeholder; } }
    public static Material BlobMaterial { get { Load(); return _blob; } }

    private static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        _sprite = LoadOrCreate(SpriteMaterialName, SpriteShaderName);
        _placeholder = LoadOrCreate(PlaceholderMaterialName, PlaceholderShaderName);
        _blob = LoadOrCreate(BlobMaterialName, BlobShaderName);
    }

    private static Material LoadOrCreate(string name, string shaderName)
    {
        Material mat = Resources.Load<Material>(ResourceFolder + "/" + name);
        if (mat != null) return mat;
        Shader shader = Shader.Find(shaderName);
        return shader != null ? new Material(shader) { name = name + " (runtime)" } : null;
    }

    /// <summary>Color principal de un material cualquiera (URP _BaseColor o _Color).</summary>
    public static Color MainColor(Material mat)
    {
        if (mat == null) return Color.white;
        if (mat.HasProperty(BaseColorId)) return mat.GetColor(BaseColorId);
        if (mat.HasProperty(ColorId)) return mat.GetColor(ColorId);
        return Color.white;
    }

    /// <summary>Pasa un renderer de malla al look plano con contorno, conservando su color.</summary>
    public static void ApplyPlaceholder(Renderer r)
    {
        if (r == null || r is SpriteRenderer || PlaceholderMaterial == null) return;
        if (r.sharedMaterial == PlaceholderMaterial) return;

        Color color = MainColor(r.sharedMaterial);
        r.sharedMaterial = PlaceholderMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        SetColor(r, TintId, color);
    }

    /// <summary>Pasa un SpriteRenderer al shader sin iluminación.</summary>
    public static void ApplySprite(SpriteRenderer r)
    {
        if (r == null || SpriteMaterial == null) return;
        r.sharedMaterial = SpriteMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    /// <summary>¿El material actual entiende _FlashAmount?</summary>
    public static bool SupportsFlash(Renderer r)
    {
        return r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty(FlashId);
    }

    /// <summary>Destello blanco por renderer (no toca el material compartido).</summary>
    public static void SetFlash(Renderer r, float amount)
    {
        if (!SupportsFlash(r)) return;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(_mpb);
        _mpb.SetFloat(FlashId, amount);
        r.SetPropertyBlock(_mpb);
    }

    private static void SetColor(Renderer r, int id, Color c)
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(_mpb);
        _mpb.SetColor(id, c);
        r.SetPropertyBlock(_mpb);
    }
}
