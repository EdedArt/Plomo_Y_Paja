using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Importación de Assets/_Game/Art/Viewmodel/ (manos en primera persona y efectos):
/// Sprite (2D and UI), Single, sin compresión, mipmaps (Kaiser) con filtro Trilinear, máximo 2048 y Alpha Is Transparency.
/// Sin compresión ni bordes duros: la calidad del contorno es lo que más se nota en estas imágenes.
/// Contrato del arte: Assets/_Game/Art/Viewmodel/README_ART.md
///
/// Se aplica al importar y además se COMPRUEBA al abrir el Editor y cada vez que llegan PNG nuevos: si Unity ya
/// estaba abierto y los importó antes de que existiera esta clase (o con otros ajustes), se reimportan solos.
/// </summary>
public class ViewmodelArtImporter : AssetPostprocessor
{
    public const string ViewmodelFolder = "Assets/_Game/Art/Viewmodel";

    // Subir la versión reimporta todo el arte con los ajustes nuevos
    // 3: también fuerza textureShape = 2D (los PNG del revólver y sus FX venían con Cube: no generaban Sprite, fase 3C.5.1)
    public override uint GetVersion() => 3;

    private static readonly HashSet<string> _triedFix = new HashSet<string>();

    private void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (!IsViewmodelTexture(path)) return;
        Apply((TextureImporter)assetImporter);
    }

    private static bool IsViewmodelTexture(string path)
    {
        return path.StartsWith(ViewmodelFolder + "/") && path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase);
    }

    private static void Apply(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.textureShape = TextureImporterShape.Texture2D; // con Cube Unity no crea el Sprite
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = true;
        importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
        importer.mipMapsPreserveCoverage = false;
        importer.streamingMipmaps = false;
        importer.filterMode = FilterMode.Trilinear;
        importer.maxTextureSize = 2048;
        importer.alphaIsTransparency = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.isReadable = false;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMode = (int)SpriteImportMode.Single;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePixelsPerUnit = 100f;
        importer.SetTextureSettings(settings);
    }

    private static bool NeedsFix(TextureImporter importer)
    {
        return importer.textureType != TextureImporterType.Sprite
               || importer.textureShape != TextureImporterShape.Texture2D
               || importer.spriteImportMode != SpriteImportMode.Single
               || importer.textureCompression != TextureImporterCompression.Uncompressed
               || !importer.mipmapEnabled
               || importer.filterMode != FilterMode.Trilinear
               || importer.maxTextureSize != 2048
               || !importer.alphaIsTransparency;
    }

    /// <summary>Reimporta (una sola vez por sesión) los PNG del viewmodel que no tengan los ajustes correctos.</summary>
    private static void FixImporters(IEnumerable<string> paths)
    {
        int fixedCount = 0;
        foreach (string raw in paths)
        {
            string path = raw.Replace('\\', '/');
            if (!IsViewmodelTexture(path) || _triedFix.Contains(path)) continue;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || !NeedsFix(importer)) continue;
            _triedFix.Add(path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); // OnPreprocessTexture aplica los ajustes
            fixedCount++;
        }
        if (fixedCount > 0) Debug.Log("[Viewmodel] " + fixedCount + " PNG reimportados con los ajustes de calidad del viewmodel.");
    }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        FixImporters(imported);
    }

    /// <summary>
    /// Arregla YA (sin el límite de una vez por sesión) los PNG de una carpeta del viewmodel con ajustes incorrectos.
    /// La llama ViewmodelProfileBuilder antes de buscar los Sprites. Devuelve cuántos reimportó.
    /// </summary>
    public static int FixFolderNow(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return 0;
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!IsViewmodelTexture(path)) continue;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || !NeedsFix(importer)) continue;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            count++;
        }
        return count;
    }

    [InitializeOnLoadMethod]
    private static void CheckOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (!AssetDatabase.IsValidFolder(ViewmodelFolder)) return;
            var paths = new List<string>();
            // "t:Texture" (no "t:Texture2D"): también encuentra los PNG importados como Cube, que son los que hay que arreglar
            foreach (string guid in AssetDatabase.FindAssets("t:Texture", new[] { ViewmodelFolder }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            FixImporters(paths);
        };
    }
}

/// <summary>
/// MENÚ: Plomo y Paja → Crear Perfil Viewmodel Escopeta
/// Crea o actualiza Assets/_Game/Art/Viewmodel/VM_Shotgun.asset con los PNG de Viewmodel/Shotgun
/// (wilbur_vm_shotgun_&lt;clip&gt;_&lt;nn&gt;.png) y el fogonazo de Viewmodel/FX (fx_fogonazo_&lt;nn&gt;.png).
/// Al actualizar SOLO cambia los dibujos: anclas, tamaños y movimiento ajustados a mano se conservan.
/// El revólver usa la misma función (GetOrCreateRevolverProfile / menú "Crear Perfil Viewmodel Revólver").
/// </summary>
public static class ViewmodelProfileBuilder
{
    public const string ShotgunProfilePath = ViewmodelArtImporter.ViewmodelFolder + "/VM_Shotgun.asset";

    [MenuItem("Plomo y Paja/Crear Perfil Viewmodel Escopeta")]
    public static void BuildShotgunMenu()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        ViewmodelProfile profile = GetOrCreateShotgunProfile();
        Selection.activeObject = profile;
    }

    public static ViewmodelProfile GetOrCreateShotgunProfile()
    {
        return BuildProfile("shotgun", "Shotgun", "VM_Shotgun", "fx_fogonazo");
    }

    /// <summary>Versión actual de los tiempos "snappy" (fase 1.3). Un perfil con timingVersion menor se actualiza solo.</summary>
    public const int TimingVersion = 2;

    /// <summary>
    /// Fase 1.3: disparo 0.08 s, bombeo 0.35 s (también apuntando) y culatazo que vuelve en ~0.15 s (32 rad/s, amortiguación 0.7).
    /// Solo toca esos campos; el resto de lo ajustado a mano se conserva.
    /// </summary>
    public static bool ApplyTiming(ViewmodelProfile p)
    {
        if (p == null || p.timingVersion >= TimingVersion) return false;
        p.fire.minDuration = 0.08f;
        p.aimFire.minDuration = 0.08f;
        p.cycle.minDuration = 0.35f;
        p.aimCycle.minDuration = 0.35f;
        p.kickSpringFrequency = 32f;
        p.kickSpringDamping = 0.7f;
        p.timingVersion = TimingVersion;
        EditorUtility.SetDirty(p);
        return true;
    }

    /// <summary>Versión de la medición del punto de mira (fase 1.4). 2 (3C.5.2): VM_Revolver se marcó como medido cuando
    /// todavía no tenía cuadros y se quedó con el valor por defecto (960.5, 575); ahora se vuelve a medir.</summary>
    public const int SightVersion = 2;

    /// <summary>Mide aimSightPoint del PNG aimIdle del perfil (si aún no está medido). Mismo algoritmo que Tools/viewmodel_sight.py.</summary>
    public static bool ApplySight(ViewmodelProfile p)
    {
        if (p == null || p.sightVersion >= SightVersion) return false;
        // Sin dibujo de apuntar no se marca como medido: se mide cuando llegue el PNG (antes quedaba el valor por defecto)
        if (p.aimIdle == null || !p.aimIdle.HasFrames || !MeasureSightPoint(p.aimIdle.frames[0], out Vector2 point)) return false;
        p.aimSightPoint = point;
        p.sightVersion = SightVersion;
        EditorUtility.SetDirty(p);
        return true;
    }

    /// <summary>
    /// Punta de la mira delantera del dibujo de apuntar: el cúmulo opaco (alpha ≥ 128) más alto en la franja central (30–70 % del
    /// ancho), en px del arte con el origen abajo-izquierda. Lee el PNG del disco (no depende de cómo esté importado).
    /// </summary>
    public static bool MeasureSightPoint(Sprite sprite, out Vector2 point)
    {
        point = Vector2.zero;
        if (sprite == null) return false;
        string path = AssetDatabase.GetAssetPath(sprite);
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(path), false)) return false;
            int w = tex.width, h = tex.height;
            Color32[] px = tex.GetPixels32();          // fila 0 = abajo
            int x0 = (int)(w * 0.30f), x1 = (int)(w * 0.70f);
            int topRow = -1;                            // fila más alta con algún píxel opaco en la franja
            for (int y = h - 1; y >= 0 && topRow < 0; y--)
                for (int x = x0; x < x1; x++)
                    if (px[y * w + x].a >= 128) { topRow = y; break; }
            if (topRow < 0) return false;

            double sumX = 0;
            int count = 0;
            for (int y = topRow; y > topRow - 3 && y >= 0; y--)   // las 3 filas superiores del cúmulo
                for (int x = x0; x < x1; x++)
                    if (px[y * w + x].a >= 128) { sumX += x + 0.5; count++; }
            if (count == 0) return false;
            point = new Vector2((float)(sumX / count), topRow + 1); // borde superior de la fila más alta, origen abajo
            return true;
        }
        finally { Object.DestroyImmediate(tex); }
    }

    /// <summary>Al abrir el Editor, los VM_*.asset que ya existen reciben los tiempos y el punto de mira nuevos (no se editan a mano).</summary>
    [InitializeOnLoadMethod]
    private static void MigrateOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ViewmodelProfile", new[] { ViewmodelArtImporter.ViewmodelFolder }))
            {
                var profile = AssetDatabase.LoadAssetAtPath<ViewmodelProfile>(AssetDatabase.GUIDToAssetPath(guid));
                bool timing = ApplyTiming(profile);
                bool sight = ApplySight(profile);
                if (timing || sight)
                {
                    changed++;
                    Debug.Log("[Viewmodel] " + profile.name + " actualizado" + (timing ? " · tiempos de la fase 1.3" : "") +
                              (sight ? " · aimSightPoint = " + profile.aimSightPoint : ""));
                }
            }
            if (changed > 0) AssetDatabase.SaveAssets();
        };
    }

    public const string RevolverProfilePath = ViewmodelArtImporter.ViewmodelFolder + "/VM_Revolver.asset";

    [MenuItem("Plomo y Paja/Crear Perfil Viewmodel Revólver")]
    public static void BuildRevolverMenu()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        Selection.activeObject = GetOrCreateRevolverProfile();
    }

    /// <summary>
    /// VM_Revolver: idle, fire, aimidle y reload (abrir, meter, cerrar = 3 cuadros). Sin cycle: el revólver no bombea.
    /// Los valores propios del revólver (anclas, fogonazo más chico, culatazo más suave) solo se ponen al CREAR el asset.
    /// </summary>
    public static ViewmodelProfile GetOrCreateRevolverProfile()
    {
        ViewmodelProfile profile = BuildProfile("revolver", "Revolver", "VM_Revolver", "fx_fogonazo", p =>
        {
            p.hipMuzzle = new Vector2(1185f, 690f);   // boca del cañón en el cuadro "fire" (estimada)
            p.aimMuzzle = new Vector2(960f, 545f);    // boca del cañón apuntando (estimada)
            p.flashSize = 280f;
            p.kickOffset = new Vector2(30f, -45f);
            p.kickRotation = 6f;
        });
        ApplyRevolverMeasurements(profile);
        return profile;
    }

    /// <summary>Versión de la medición de handsScale (3C.5.2). Solo se mide una vez: después manda el ajuste a mano.</summary>
    public const int HandsScaleVersion = 1;
    private const string RimSpritePath = ViewmodelArtImporter.ViewmodelFolder + "/FX/fx_round_rim_01.png";

    /// <summary>
    /// Fase 3C.5.2, todo medido de los PNG (nada a mano):
    /// - recarga por etapas: reload = los 3 cuadros tal cual (abrir, meter, cerrar); los cambia el arma con sus eventos;
    /// - hipClearLeftArtX: borde izquierdo del arma en idle/fire (la mira no queda tapada);
    /// - huecos del tambor en reload_01 / reload_02 (balas visibles) y el dibujo de la bala (fx_round_rim_01);
    /// - handsScale (solo la primera vez): alto del guante de la escopeta / alto del guante del revólver, en idle.
    /// </summary>
    private static void ApplyRevolverMeasurements(ViewmodelProfile profile)
    {
        if (profile == null) return;
        var log = new System.Text.StringBuilder("[Viewmodel] VM_Revolver medido:");
        profile.reload.fps = 0f; // 3C.5 usaba una secuencia de 7 cuadros a 3.18 cps; ahora los cuadros los cambia el arma

        float left = float.MaxValue;
        foreach (ViewmodelProfile.Clip clip in new[] { profile.idle, profile.fire })
            if (clip != null && clip.HasFrames && MeasureOpaqueLeft(clip.frames[0], out float x)) left = Mathf.Min(left, x);
        profile.hipClearLeftArtX = left < float.MaxValue ? left : 0f;
        log.Append(" borde izq. del arma en la cadera = ").Append(profile.hipClearLeftArtX.ToString("0")).Append(" px;");

        Sprite[] reload = profile.reload.frames;
        float diameter = 0f;
        profile.chambersOpen = reload != null && reload.Length > 0 ? MeasureChambers(reload[0], out diameter) : new Vector2[0];
        profile.chambersInsert = reload != null && reload.Length > 1 ? MeasureChambers(reload[1], out _) : new Vector2[0];
        profile.chamberDiameter = profile.chambersOpen.Length > 0 ? diameter : 0f;
        profile.chamberRim = AssetDatabase.LoadAssetAtPath<Sprite>(RimSpritePath);
        log.Append(" huecos abrir ").Append(profile.chambersOpen.Length).Append(" / meter ").Append(profile.chambersInsert.Length)
           .Append(", diámetro ").Append(profile.chamberDiameter.ToString("0")).Append(" px;");
        if (profile.chambersOpen.Length != 5)
            Debug.LogWarning("[Viewmodel] ✗ VM_Revolver: se esperaban 5 huecos en reload_01 y se midieron " + profile.chambersOpen.Length + ".");
        if (profile.chamberRim == null) Debug.LogWarning("[Viewmodel] ✗ Falta " + RimSpritePath + ": no se ven las balas en el tambor.");

        if (profile.handsScaleVersion < HandsScaleVersion)
        {
            var shotgun = AssetDatabase.LoadAssetAtPath<ViewmodelProfile>(ShotgunProfilePath);
            if (shotgun != null && shotgun.idle.HasFrames && profile.idle.HasFrames
                && MeasureGloveHeight(shotgun.idle.frames[0], out float shotgunGlove)
                && MeasureGloveHeight(profile.idle.frames[0], out float revolverGlove) && revolverGlove > 0f)
            {
                profile.handsScale = Mathf.Clamp(Mathf.Round(shotgunGlove / revolverGlove * 100f) / 100f, 0.7f, 1.2f);
                profile.handsScaleVersion = HandsScaleVersion;
                log.Append(" guante escopeta ").Append(shotgunGlove.ToString("0")).Append(" px / revólver ")
                   .Append(revolverGlove.ToString("0")).Append(" px → handsScale ").Append(profile.handsScale.ToString("0.00")).Append(';');
            }
        }
        else log.Append(" handsScale ").Append(profile.handsScale.ToString("0.00")).Append(" (ajustado, no se vuelve a medir);");

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
    }

    // ---------- Lectura de PNG (del disco, no depende de cómo esté importado) ----------

    private static bool ReadPixels(Sprite sprite, out Color32[] px, out int w, out int h)
    {
        px = null;
        w = h = 0;
        if (sprite == null) return false;
        string path = AssetDatabase.GetAssetPath(sprite);
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(path), false)) return false;
            w = tex.width;
            h = tex.height;
            px = tex.GetPixels32(); // fila 0 = abajo
            return true;
        }
        finally { Object.DestroyImmediate(tex); }
    }

    /// <summary>Columna opaca (alpha ≥ 128) más a la izquierda, en px del arte 1920.</summary>
    public static bool MeasureOpaqueLeft(Sprite sprite, out float x)
    {
        x = 0f;
        if (!ReadPixels(sprite, out Color32[] px, out int w, out int h)) return false;
        for (int c = 0; c < w; c++)
            for (int r = 0; r < h; r++)
                if (px[r * w + c].a >= 128) { x = c * 1920f / w; return true; }
        return false;
    }

    /// <summary>
    /// Huecos del tambor abierto: píxeles opacos muy oscuros (máx(R,G,B) &lt; 45) erosionados con un cuadrado de 15 px (borra los
    /// contornos finos), componentes redondas (lados en proporción 0.7–1.43, área ≥ 400) de tamaño parecido (0.55–1.6 × la
    /// mediana de las 5 más grandes). Hasta 5, en el sentido de las agujas del reloj empezando por el que sigue al de arriba
    /// (el de arriba, donde el dibujo "meter" pone la bala, se llena el último). Px del arte, origen abajo-izquierda.
    /// Mismo algoritmo que el script de medición de la auditoría 3C.5.2.
    /// </summary>
    public static Vector2[] MeasureChambers(Sprite sprite, out float diameter)
    {
        diameter = 0f;
        if (!ReadPixels(sprite, out Color32[] px, out int w, out int h)) return new Vector2[0];
        const int k = 7;
        var dark = new bool[w * h];
        for (int i = 0; i < px.Length; i++)
        {
            Color32 c = px[i];
            dark[i] = c.a >= 200 && Mathf.Max(c.r, Mathf.Max(c.g, c.b)) < 45;
        }
        var horizontal = new bool[w * h];
        for (int y = 0; y < h; y++)
        {
            int run = 0;
            for (int x = 0; x < w; x++)
            {
                run = dark[y * w + x] ? run + 1 : 0;
                if (run >= 2 * k + 1) horizontal[y * w + x - k] = true;
            }
        }
        var eroded = new bool[w * h];
        for (int x = 0; x < w; x++)
        {
            int run = 0;
            for (int y = 0; y < h; y++)
            {
                run = horizontal[y * w + x] ? run + 1 : 0;
                if (run >= 2 * k + 1) eroded[(y - k) * w + x] = true;
            }
        }

        var candidates = new List<Chamber>();
        foreach (Blob b in Blobs(eroded, w, h))
        {
            float bw = b.MaxX - b.MinX + 1 + 2 * k, bh = b.MaxY - b.MinY + 1 + 2 * k;
            float aspect = bw / bh;
            if (b.Area < 400 || aspect < 0.7f || aspect > 1.43f) continue;
            candidates.Add(new Chamber
            {
                Center = new Vector2(b.SumX / b.Area + 0.5f, b.SumY / b.Area + 0.5f),
                Area = b.Area,
                Size = (bw + bh) * 0.5f,
            });
        }
        if (candidates.Count < 3) return new Vector2[0];
        candidates.Sort((a, b) => b.Area.CompareTo(a.Area));
        var areas = new List<int>();
        for (int i = 0; i < Mathf.Min(5, candidates.Count); i++) areas.Add(candidates[i].Area);
        areas.Sort();
        float median = areas[areas.Count / 2];
        var chosen = new List<Chamber>();
        foreach (Chamber c in candidates)
            if (c.Area >= 0.55f * median && c.Area <= 1.6f * median && chosen.Count < 5) chosen.Add(c);
        if (chosen.Count < 3) return new Vector2[0];

        Vector2 mid = Vector2.zero;
        float sizeSum = 0f;
        foreach (Chamber c in chosen) { mid += c.Center; sizeSum += c.Size; }
        mid /= chosen.Count;
        diameter = sizeSum / chosen.Count * 1920f / w;

        // Ángulo en el sentido de las agujas del reloj desde arriba (y hacia arriba): atan2(dx, dy)
        foreach (Chamber c in chosen)
            c.Angle = Mathf.Repeat(Mathf.Atan2(c.Center.x - mid.x, c.Center.y - mid.y) * Mathf.Rad2Deg, 360f);
        chosen.Sort((a, b) => a.Angle.CompareTo(b.Angle));
        int topIndex = 0;
        for (int i = 1; i < chosen.Count; i++)
            if (chosen[i].Center.y > chosen[topIndex].Center.y) topIndex = i;
        var result = new Vector2[chosen.Count];
        for (int i = 0; i < chosen.Count; i++)
        {
            Vector2 c = chosen[(topIndex + 1 + i) % chosen.Count].Center;
            result[i] = new Vector2(c.x * 1920f / w, c.y * 1080f / h);
        }
        return result;
    }

    private class Chamber
    {
        public Vector2 Center;
        public int Area;
        public float Size;
        public float Angle;
    }

    /// <summary>
    /// Alto (px del arte 1080) del guante más grande: píxeles opacos casi blancos (mín(R,G,B) &gt; 195, poco color), el
    /// componente más grande medido a la mitad de resolución. En idle es la mano que sostiene el arma.
    /// </summary>
    public static bool MeasureGloveHeight(Sprite sprite, out float height)
    {
        height = 0f;
        if (!ReadPixels(sprite, out Color32[] px, out int w, out int h)) return false;
        int sw = w / 2, sh = h / 2;
        var white = new bool[sw * sh];
        for (int y = 0; y < sh; y++)
            for (int x = 0; x < sw; x++)
            {
                Color32 c = px[(y * 2) * w + x * 2];
                int mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b)), mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                white[y * sw + x] = c.a > 200 && mn > 195 && mx - mn < 45;
            }
        Blob best = null;
        foreach (Blob b in Blobs(white, sw, sh))
            if (best == null || b.Area > best.Area) best = b;
        if (best == null) return false;
        height = (best.MaxY - best.MinY + 1) * 2f * 1080f / h;
        return true;
    }

    private class Blob
    {
        public int Area, MinX = int.MaxValue, MaxX = -1, MinY = int.MaxValue, MaxY = -1;
        public float SumX, SumY;
    }

    /// <summary>Componentes conexas (4 vecinos) de una máscara.</summary>
    private static List<Blob> Blobs(bool[] mask, int w, int h)
    {
        var blobs = new List<Blob>();
        var seen = new bool[w * h];
        var queue = new Queue<int>();
        for (int start = 0; start < mask.Length; start++)
        {
            if (!mask[start] || seen[start]) continue;
            var b = new Blob();
            seen[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % w, y = i / w;
                b.Area++;
                b.SumX += x;
                b.SumY += y;
                if (x < b.MinX) b.MinX = x;
                if (x > b.MaxX) b.MaxX = x;
                if (y < b.MinY) b.MinY = y;
                if (y > b.MaxY) b.MaxY = y;
                if (x > 0 && mask[i - 1] && !seen[i - 1]) { seen[i - 1] = true; queue.Enqueue(i - 1); }
                if (x < w - 1 && mask[i + 1] && !seen[i + 1]) { seen[i + 1] = true; queue.Enqueue(i + 1); }
                if (y > 0 && mask[i - w] && !seen[i - w]) { seen[i - w] = true; queue.Enqueue(i - w); }
                if (y < h - 1 && mask[i + w] && !seen[i + w]) { seen[i + w] = true; queue.Enqueue(i + w); }
            }
            blobs.Add(b);
        }
        return blobs;
    }

    public static ViewmodelProfile BuildProfile(string weaponId, string weaponFolder, string assetName, string flashPrefix,
                                                System.Action<ViewmodelProfile> onCreate = null)
    {
        string folder = ViewmodelArtImporter.ViewmodelFolder;
        LevelBuilderUtils.EnsureFolder(folder);
        string assetPath = $"{folder}/{assetName}.asset";

        var profile = AssetDatabase.LoadAssetAtPath<ViewmodelProfile>(assetPath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<ViewmodelProfile>();
            profile.weaponId = weaponId;
            profile.timingVersion = TimingVersion; // los valores por defecto de la clase ya son los nuevos
            if (onCreate != null) onCreate(profile);
            AssetDatabase.CreateAsset(profile, assetPath);
        }

        // 3C.5.1: si algún PNG quedó con ajustes malos (p. ej. textureShape Cube = sin Sprite), se reimporta ya
        string weaponPath = $"{folder}/{weaponFolder}";
        int reimported = ViewmodelArtImporter.FixFolderNow(weaponPath) + ViewmodelArtImporter.FixFolderNow($"{folder}/FX");
        if (reimported > 0) Debug.Log($"[Viewmodel] {reimported} PNG reimportados como Sprite 2D antes de armar {assetName}.");

        // clip → (número → sprite)
        var pattern = new Regex($@"^wilbur_vm_{Regex.Escape(weaponId)}_([a-z]+)_(\d+)$");
        Dictionary<string, SortedDictionary<int, Sprite>> clips = LoadNumbered(weaponPath, pattern);
        ReportMissing(assetName, weaponPath, pattern, clips);

        profile.idle.frames = Frames(clips, "idle");
        profile.fire.frames = Frames(clips, "fire");
        profile.cycle.frames = Frames(clips, "cycle");
        profile.reload.frames = Frames(clips, "reload");
        profile.aimIdle.frames = Frames(clips, "aimidle");
        profile.aimFire.frames = Frames(clips, "aimfire");
        profile.aimCycle.frames = Frames(clips, "aimcycle");

        Dictionary<string, SortedDictionary<int, Sprite>> fx = LoadNumbered(
            $"{folder}/FX", new Regex(@"^(fx_[a-z]+)_(\d+)$"));
        profile.flashSprites = Frames(fx, flashPrefix);

        foreach (string clip in clips.Keys)
            if (System.Array.IndexOf(KnownClips, clip) < 0)
                Debug.LogWarning($"[Viewmodel] Clip desconocido '{clip}' en {weaponFolder} (válidos: {string.Join(", ", KnownClips)})");

        ApplyTiming(profile);
        ApplySight(profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Viewmodel] {assetName}: idle {profile.idle.frames.Length}, fire {profile.fire.frames.Length}, " +
                  $"cycle {profile.cycle.frames.Length}, reload {profile.reload.frames.Length}, aimIdle {profile.aimIdle.frames.Length}, " +
                  $"aimFire {profile.aimFire.frames.Length}, aimCycle {profile.aimCycle.frames.Length}, fogonazos {profile.flashSprites.Length}");
        return profile;
    }

    private static readonly string[] KnownClips = { "idle", "fire", "cycle", "reload", "aimidle", "aimfire", "aimcycle" };

    /// <summary>
    /// ✗ claro en la consola: PNG que están en la carpeta con el nombre correcto pero no dieron Sprite (importación mala), o
    /// carpeta sin ningún PNG válido / sin idle (las manos no se verían).
    /// </summary>
    private static void ReportMissing(string assetName, string weaponPath, Regex pattern,
                                      Dictionary<string, SortedDictionary<int, Sprite>> clips)
    {
        var problems = new List<string>();
        string diskFolder = Path.Combine(Path.GetDirectoryName(Application.dataPath), weaponPath);
        if (!Directory.Exists(diskFolder))
        {
            Debug.LogError($"[Viewmodel] ✗ {assetName}: no existe la carpeta {weaponPath}.");
            return;
        }
        int matching = 0;
        foreach (string file in Directory.GetFiles(diskFolder, "*.png"))
        {
            string name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
            Match m = pattern.Match(name);
            if (!m.Success)
            {
                problems.Add($"{Path.GetFileName(file)} (nombre fuera del patrón wilbur_vm_<arma>_<clip>_<nn>.png)");
                continue;
            }
            matching++;
            bool loaded = clips.TryGetValue(m.Groups[1].Value, out var frames) && frames.ContainsKey(int.Parse(m.Groups[2].Value));
            if (!loaded)
            {
                var importer = AssetImporter.GetAtPath($"{weaponPath}/{Path.GetFileName(file)}") as TextureImporter;
                string why = importer == null ? "Unity no lo importó"
                    : $"tipo {importer.textureType}, forma {importer.textureShape}, modo {importer.spriteImportMode}";
                problems.Add($"{Path.GetFileName(file)} sin Sprite ({why})");
            }
        }
        if (matching == 0) problems.Add($"ningún PNG con el patrón en {weaponPath}");
        if (!clips.ContainsKey("idle")) problems.Add("falta el clip idle (sin él no se ven las manos)");

        if (problems.Count > 0)
            Debug.LogError($"[Viewmodel] ✗ {assetName}: faltan cuadros:\n  - " + string.Join("\n  - ", problems));
    }

    private static Dictionary<string, SortedDictionary<int, Sprite>> LoadNumbered(string folder, Regex pattern)
    {
        var result = new Dictionary<string, SortedDictionary<int, Sprite>>();
        if (!AssetDatabase.IsValidFolder(folder)) return result;

        foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetDirectoryName(path).Replace('\\', '/') != folder) continue; // sin subcarpetas
            Match m = pattern.Match(Path.GetFileNameWithoutExtension(path).ToLowerInvariant());
            if (!m.Success) continue;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) continue;

            string key = m.Groups[1].Value;
            if (!result.TryGetValue(key, out var frames)) result[key] = frames = new SortedDictionary<int, Sprite>();
            frames[int.Parse(m.Groups[2].Value)] = sprite;
        }
        return result;
    }

    private static Sprite[] Frames(Dictionary<string, SortedDictionary<int, Sprite>> clips, string key)
    {
        return clips.TryGetValue(key, out var frames) ? new List<Sprite>(frames.Values).ToArray() : new Sprite[0];
    }
}
