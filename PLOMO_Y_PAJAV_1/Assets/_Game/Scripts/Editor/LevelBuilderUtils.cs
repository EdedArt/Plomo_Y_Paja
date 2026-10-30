using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Utilidades compartidas por los LevelBuilder (Nivel Porky, Nivel 2...).
/// Solo editor: crea primitivas, paredes con huecos, materiales greybox y marcadores.
/// </summary>
public static class LevelBuilderUtils
{
    public const string MaterialFolder = "Assets/Materials/Greybox";

    /// <summary>Hueco en una pared: centro medido desde el inicio de la pared (m).</summary>
    public struct WallOpening
    {
        public float center;
        public float width;
        public float height;

        public WallOpening(float center, float width, float height)
        {
            this.center = center;
            this.width = width;
            this.height = height;
        }
    }

    // ============ Jerarquía ============

    public static Transform CreateGroup(string name, Transform parent = null)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        return go.transform;
    }

    // ============ Geometría ============

    /// <summary>
    /// Cubo con collider. center/size en metros, rotación en grados (euler).
    /// </summary>
    public static GameObject CreateBox(string name, Vector3 center, Vector3 size, Material mat,
        Transform parent, Vector3 euler = default)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        if (parent != null) box.transform.SetParent(parent, false);
        box.transform.position = center;
        box.transform.rotation = Quaternion.Euler(euler);
        box.transform.localScale = size;
        if (mat != null) box.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return box;
    }

    /// <summary>
    /// Pared recta de start a end (a ras de suelo, y = start.y) con una puerta.
    /// doorCenter: distancia desde start al centro de la puerta.
    /// </summary>
    public static Transform CreateWallWithDoorway(string name, Vector3 start, Vector3 end, float height,
        float thickness, Material mat, Transform parent, float doorCenter, float doorWidth = 1.6f, float doorHeight = 2.4f)
    {
        return CreateWallWithOpenings(name, start, end, height, thickness, mat, parent,
            new WallOpening(doorCenter, doorWidth, doorHeight));
    }

    /// <summary>
    /// Pared recta con cualquier número de huecos (puertas, ventanas a ras de suelo, boquetes).
    /// Un hueco con height >= altura de la pared queda abierto hasta arriba (sin dintel).
    /// Sin huecos = pared sólida.
    /// </summary>
    public static Transform CreateWallWithOpenings(string name, Vector3 start, Vector3 end, float height,
        float thickness, Material mat, Transform parent, params WallOpening[] openings)
    {
        Transform wall = CreateGroup(name, parent);

        Vector3 delta = end - start;
        delta.y = 0f;
        float length = delta.magnitude;
        if (length < 0.01f) return wall;

        Vector3 dir = delta / length;
        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
        Vector3 euler = rot.eulerAngles;

        var sorted = new List<WallOpening>(openings ?? new WallOpening[0]);
        sorted.Sort((a, b) => a.center.CompareTo(b.center));

        float cursor = 0f;
        int index = 0;
        foreach (var opening in sorted)
        {
            float from = Mathf.Clamp(opening.center - opening.width * 0.5f, 0f, length);
            float to = Mathf.Clamp(opening.center + opening.width * 0.5f, 0f, length);

            // Tramo sólido antes del hueco
            if (from - cursor > 0.01f)
                AddSegment(wall, $"{name}_Tramo{index++}", start, dir, cursor, from, 0f, height, thickness, mat, euler);

            // Dintel sobre el hueco
            if (opening.height < height - 0.01f && to - from > 0.01f)
                AddSegment(wall, $"{name}_Dintel{index++}", start, dir, from, to, opening.height, height, thickness, mat, euler);

            cursor = Mathf.Max(cursor, to);
        }

        if (length - cursor > 0.01f)
            AddSegment(wall, $"{name}_Tramo{index}", start, dir, cursor, length, 0f, height, thickness, mat, euler);

        return wall;
    }

    private static void AddSegment(Transform parent, string name, Vector3 start, Vector3 dir, float from, float to,
        float bottom, float top, float thickness, Material mat, Vector3 euler)
    {
        float segLength = to - from;
        Vector3 center = start + dir * (from + segLength * 0.5f) + Vector3.up * ((bottom + top) * 0.5f);
        // El cubo rotado tiene su eje Z a lo largo de la pared
        CreateBox(name, center, new Vector3(thickness, top - bottom, segLength), mat, parent, euler);
    }

    // ============ Materiales ============

    public const string EnvironmentShaderName = "PlomoYPaja/CartoonEnvironment";
    private const string LitShaderName = "Universal Render Pipeline/Lit";

    /// <summary>
    /// Carga Assets/Materials/Greybox/{name}.mat o lo crea con el shader pintado CartoonEnvironment
    /// (fase 3A2). Los materiales viejos en URP Lit se convierten conservando color y emisión.
    /// Con updateColor = true también repinta un material existente.
    /// </summary>
    public static Material GetOrCreateMaterial(string name, Color color, float smoothness = 0.1f, bool updateColor = false)
    {
        EnsureFolder(MaterialFolder);
        string path = $"{MaterialFolder}/{name}.mat";
        Shader env = Shader.Find(EnvironmentShaderName);

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null)
        {
            if (env != null && mat.shader != null && mat.shader.name == LitShaderName)
            {
                ConvertToEnvironment(mat, env);
                EditorUtility.SetDirty(mat);
            }
            if (updateColor)
            {
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        Shader shader = env != null ? env : Shader.Find(LitShaderName);
        if (shader == null)
        {
            Debug.LogWarning("[LevelBuilder] No se encontró ni CartoonEnvironment ni URP Lit; se usa Standard.");
            shader = Shader.Find("Standard");
        }
        else if (env == null)
        {
            Debug.LogWarning("[LevelBuilder] No se encontró '" + EnvironmentShaderName + "'; se usa URP Lit.");
        }

        mat = new Material(shader) { name = name };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    /// <summary>Material pintado con emisión (ventanas, faroles). Crea o actualiza.</summary>
    public static Material GetOrCreateEmissiveMaterial(string name, Color color, Color emission)
    {
        Material mat = GetOrCreateMaterial(name, color, 0f, true);
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission);
        mat.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void ConvertToEnvironment(Material mat, Shader env)
    {
        Color color = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
        bool emissive = mat.IsKeywordEnabled("_EMISSION");
        Color emission = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
        mat.shader = env;
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_EmissionColor", emissive ? emission : Color.black);
    }

    /// <summary>Crea carpetas anidadas ("Assets/A/B") si faltan.</summary>
    public static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        int slash = folder.LastIndexOf('/');
        string parent = folder.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
    }

    // ============ Marcadores ============

    public static LevelMarker CreateMarker(string name, MarkerType type, string id, Vector3 position,
        float yaw, Transform parent)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        LevelMarker marker = go.AddComponent<LevelMarker>();
        marker.Setup(type, id);
        return marker;
    }

    // ============ Luces ============

    public static Light CreatePointLight(string name, Vector3 position, Color color, float intensity, float range,
        Transform parent)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = position;

        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.Soft;
        return light;
    }

    // ============ Static flags ============

    /// <summary>
    /// Marca el objeto y sus hijos como estáticos para navegación e iluminación.
    /// </summary>
    public static void MarkStaticRecursive(GameObject root)
    {
#pragma warning disable 618 // NavigationStatic queda obsoleto cuando se usa el paquete AI Navigation
        StaticEditorFlags flags = StaticEditorFlags.ContributeGI
                                | StaticEditorFlags.NavigationStatic
                                | StaticEditorFlags.BatchingStatic
                                | StaticEditorFlags.OccluderStatic
                                | StaticEditorFlags.OccludeeStatic
                                | StaticEditorFlags.ReflectionProbeStatic;
#pragma warning restore 618

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
    }
}
