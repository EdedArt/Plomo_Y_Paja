using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Puntos del cañón de los tiradores (fase HUD.2, H10).
/// - MENÚ Plomo y Paja → HUD → Crear puntos de cañón: crea (o deja) un MuzzlePointSet por tirador en Data/MuzzlePoints
///   con un punto de respaldo a la altura de la mano que dispara (medido a ojo en shoot_01) y lista los cuadros sin punto.
/// - MENÚ Plomo y Paja → HUD → Puntos de cañón: ventana para marcar el punto haciendo CLIC sobre el dibujo de cada cuadro
///   (cruz amarilla = punto propio, gris = respaldo), con Deshacer, "Copiar al siguiente cuadro", "Borrar punto" y
///   "Usar como respaldo".
/// - ConfigureGunfire: lo usan los menús de prefabs (guardias en Crear Prefabs de Prueba, Centavo en Colocar Centavo).
/// </summary>
public static class MuzzlePointsBuilder
{
    public const string Folder = "Assets/_Game/Data/MuzzlePoints";
    private const string SpriteSetFolder = "Assets/_Game/Art/SpriteSets";

    // Efectos (H11): el fogonazo del jugador; polvo y agujero del guion (Art/FX) o, mientras falten, el humo de Edgar / código
    private const string FlashPath = "Assets/_Game/Art/Viewmodel/FX/fx_fogonazo_01.png";
    private const string ImpactPath = "Assets/_Game/Art/FX/fx_impacto_01.png";
    private const string Impact2Path = "Assets/_Game/Art/FX/fx_impacto_02.png";
    private const string HolePath = "Assets/_Game/Art/FX/fx_agujero_bala.png";
    private const string SmokeFallbackPath = "Assets/_Game/Art/Characters/fx/humo_01.png";

    /// <summary>Tiradores conocidos y su punto de respaldo (normalizado, medido a ojo en shoot_01).</summary>
    public static readonly (string id, Vector2 fallback)[] Shooters =
    {
        ("guardia", new Vector2(0.46f, 0.49f)),     // 512: arma en (240, 255) desde arriba
        ("informante", new Vector2(0.46f, 0.50f)),  // mismo dibujo que el guardia
        ("contador", new Vector2(0.317f, 0.6f)),    // 1024: revólver en (325, 410) desde arriba
        ("donlobo", new Vector2(0.55f, 0.55f)),     // sin IA todavía (Fase 4): solo el asset
    };

    [MenuItem("Plomo y Paja/HUD/Crear puntos de cañón")]
    public static void CreateMenu()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        var report = new StringBuilder();
        EnsureAll(report);
        Debug.Log("[HUD] Puntos de cañón:\n" + report);
        EditorUtility.DisplayDialog("Puntos de cañón", report.ToString(), "Entendido");
    }

    public static void EnsureAll(StringBuilder report)
    {
        foreach (var shooter in Shooters)
        {
            MuzzlePointSet set = GetOrCreate(shooter.id);
            report.AppendLine("✓ " + AssetDatabase.GetAssetPath(set) + " · respaldo " + set.fallback);
            List<string> missing = FramesWithoutPoint(set);
            if (missing.Count > 0) report.AppendLine("   sin punto propio (usan el respaldo): " + string.Join(", ", missing));
        }
        AssetDatabase.SaveAssets();
    }

    public static MuzzlePointSet GetOrCreate(string id)
    {
        LevelBuilderUtils.EnsureFolder(Folder);
        string path = Folder + "/Muzzle_" + id + ".asset";
        var set = AssetDatabase.LoadAssetAtPath<MuzzlePointSet>(path);
        if (set != null) return set;
        set = ScriptableObject.CreateInstance<MuzzlePointSet>();
        set.characterId = id;
        foreach (var shooter in Shooters)
            if (shooter.id == id) set.fallback = shooter.fallback;
        AssetDatabase.CreateAsset(set, path);
        return set;
    }

    public static CharacterSpriteSet SpriteSetFor(string id)
    {
        return AssetDatabase.LoadAssetAtPath<CharacterSpriteSet>(SpriteSetFolder + "/" + id + ".asset");
    }

    /// <summary>Cuadros de disparo ("shoot") del personaje que no tienen punto propio.</summary>
    public static List<string> FramesWithoutPoint(MuzzlePointSet set)
    {
        var missing = new List<string>();
        CharacterSpriteSet sprites = SpriteSetFor(set.characterId);
        if (sprites == null || !sprites.TryGet("shoot", out CharacterSpriteSet.AnimClip clip)) return missing;
        foreach (Sprite s in clip.frames)
            if (s != null && !set.TryGet(s, out _)) missing.Add(set.characterId + "/" + s.name);
        return missing;
    }

    /// <summary>Le pone a un EnemyGunfire el set de su personaje y los dibujos de efectos que haya.</summary>
    public static void ConfigureGunfire(EnemyGunfire gunfire, string id)
    {
        MuzzlePointSet set = GetOrCreate(id);
        var so = new SerializedObject(gunfire);
        so.FindProperty("muzzlePoints").objectReferenceValue = set;
        so.FindProperty("fallbackPoint").vector2Value = set.fallback;
        so.FindProperty("flashSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(FlashPath);
        Sprite impact = AssetDatabase.LoadAssetAtPath<Sprite>(ImpactPath);
        if (impact == null) impact = AssetDatabase.LoadAssetAtPath<Sprite>(SmokeFallbackPath);
        so.FindProperty("impactSprite").objectReferenceValue = impact;
        so.FindProperty("impactSprite2").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(Impact2Path);
        so.FindProperty("bulletHoleSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(HolePath);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Ids de arte de efectos que todavía faltan (para los reportes).</summary>
    public static List<string> MissingFxIds()
    {
        var missing = new List<string>();
        if (AssetDatabase.LoadAssetAtPath<Sprite>(ImpactPath) == null) missing.Add("fx_impacto_01");
        if (AssetDatabase.LoadAssetAtPath<Sprite>(Impact2Path) == null) missing.Add("fx_impacto_02");
        if (AssetDatabase.LoadAssetAtPath<Sprite>(HolePath) == null) missing.Add("fx_agujero_bala");
        return missing;
    }
}

/// <summary>Ventana para marcar el punto del cañón con un clic sobre cada cuadro (H10).</summary>
public class MuzzlePointWindow : EditorWindow
{
    private MuzzlePointSet _set;
    private readonly List<Sprite> _frames = new List<Sprite>();
    private readonly List<string> _frameNames = new List<string>();
    private int _index;
    private Vector2 _scroll;

    [MenuItem("Plomo y Paja/HUD/Puntos de cañón")]
    public static void Open()
    {
        var window = GetWindow<MuzzlePointWindow>("Puntos de cañón");
        window.minSize = new Vector2(420f, 560f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Personaje", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        foreach (var shooter in MuzzlePointsBuilder.Shooters)
            if (GUILayout.Button(shooter.id)) Select(MuzzlePointsBuilder.GetOrCreate(shooter.id));
        EditorGUILayout.EndHorizontal();

        var picked = (MuzzlePointSet)EditorGUILayout.ObjectField("Set", _set, typeof(MuzzlePointSet), false);
        if (picked != _set) Select(picked);
        if (_set == null)
        {
            EditorGUILayout.HelpBox("Elegí un personaje. Clic sobre el dibujo = punto del cañón de ese cuadro.", MessageType.Info);
            return;
        }
        if (_frames.Count == 0)
        {
            EditorGUILayout.HelpBox("No hay set de sprites para '" + _set.characterId + "' (Plomo y Paja → Reconstruir Sets de Sprites).",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("◀", GUILayout.Width(30))) _index = (_index + _frames.Count - 1) % _frames.Count;
        _index = EditorGUILayout.Popup(_index, _frameNames.ToArray());
        if (GUILayout.Button("▶", GUILayout.Width(30))) _index = (_index + 1) % _frames.Count;
        EditorGUILayout.EndHorizontal();

        Sprite sprite = _frames[_index];
        bool own = _set.TryGet(sprite, out Vector2 point);
        EditorGUILayout.LabelField(own ? "Punto propio: " + point.ToString("F3") : "Sin punto propio: usa el respaldo " + _set.fallback.ToString("F3"));

        Rect area = GUILayoutUtility.GetRect(position.width - 20f, position.width - 20f, GUILayout.ExpandWidth(true));
        Rect draw = FitRect(area, sprite.rect.width / sprite.rect.height);
        EditorGUI.DrawRect(draw, new Color(0.2f, 0.2f, 0.22f));
        Texture2D tex = sprite.texture;
        Rect tr = sprite.textureRect;
        GUI.DrawTextureWithTexCoords(draw, tex, new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height));
        DrawCross(draw, own ? point : _set.fallback, own ? Color.yellow : Color.gray);

        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && draw.Contains(e.mousePosition))
        {
            var p = new Vector2((e.mousePosition.x - draw.x) / draw.width, 1f - (e.mousePosition.y - draw.y) / draw.height);
            Undo.RecordObject(_set, "Punto del cañón");
            _set.Set(sprite, p);
            EditorUtility.SetDirty(_set);
            e.Use();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Copiar al siguiente cuadro"))
        {
            int next = (_index + 1) % _frames.Count;
            Undo.RecordObject(_set, "Copiar punto del cañón");
            _set.Set(_frames[next], own ? point : _set.fallback);
            EditorUtility.SetDirty(_set);
            _index = next;
        }
        if (GUILayout.Button("Borrar punto") && own)
        {
            Undo.RecordObject(_set, "Borrar punto del cañón");
            _set.Remove(sprite);
            EditorUtility.SetDirty(_set);
        }
        if (GUILayout.Button("Usar como respaldo"))
        {
            Undo.RecordObject(_set, "Respaldo del cañón");
            _set.fallback = own ? point : _set.fallback;
            EditorUtility.SetDirty(_set);
        }
        EditorGUILayout.EndHorizontal();

        List<string> missing = MuzzlePointsBuilder.FramesWithoutPoint(_set);
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(60f));
        EditorGUILayout.LabelField(missing.Count == 0 ? "✓ Todos los cuadros de disparo tienen punto."
            : "Cuadros de disparo sin punto: " + string.Join(", ", missing), EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
        if (GUILayout.Button("Guardar")) AssetDatabase.SaveAssets();
    }

    private void Select(MuzzlePointSet set)
    {
        _set = set;
        _frames.Clear();
        _frameNames.Clear();
        _index = 0;
        if (set == null) return;
        CharacterSpriteSet sprites = MuzzlePointsBuilder.SpriteSetFor(set.characterId);
        if (sprites == null) return;
        // Primero los cuadros de disparo, después el resto
        if (sprites.TryGet("shoot", out CharacterSpriteSet.AnimClip shoot)) AddClip(shoot);
        foreach (CharacterSpriteSet.AnimClip clip in sprites.clips)
            if (clip != null && clip.name != "shoot") AddClip(clip);
    }

    private void AddClip(CharacterSpriteSet.AnimClip clip)
    {
        for (int i = 0; i < clip.frames.Length; i++)
        {
            if (clip.frames[i] == null) continue;
            _frames.Add(clip.frames[i]);
            _frameNames.Add(clip.name + " " + (i + 1));
        }
    }

    private static Rect FitRect(Rect area, float aspect)
    {
        float w = area.width, h = area.width / aspect;
        if (h > area.height) { h = area.height; w = h * aspect; }
        return new Rect(area.x + (area.width - w) * 0.5f, area.y, w, h);
    }

    private static void DrawCross(Rect draw, Vector2 p, Color color)
    {
        float x = draw.x + p.x * draw.width, y = draw.y + (1f - p.y) * draw.height;
        EditorGUI.DrawRect(new Rect(x - 10f, y - 1f, 20f, 2f), color);
        EditorGUI.DrawRect(new Rect(x - 1f, y - 10f, 2f, 20f), color);
    }
}
