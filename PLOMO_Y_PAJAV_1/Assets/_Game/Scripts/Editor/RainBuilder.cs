using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Material y goteras de la lluvia (fase 3A4). La lluvia de afuera es el componente RainEmitter del Player;
/// aquí se crean el material compartido y las goteras de adentro (una gota cada ~0.2 s bajo cada agujero del techo).
/// </summary>
public static class RainBuilder
{
    private const string MaterialPath = LevelBuilderUtils.MaterialFolder + "/Fx_Lluvia.mat";

    /// <summary>Material translúcido azul pálido de las gotas (URP Particles/Unlit). Se crea una vez.</summary>
    public static Material GetRainMaterial()
    {
        LevelBuilderUtils.EnsureFolder(LevelBuilderUtils.MaterialFolder);
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            Debug.LogWarning("[Lluvia] No se encontró 'Universal Render Pipeline/Particles/Unlit'; la lluvia no se verá.");
            return mat;
        }
        if (mat == null)
        {
            mat = new Material(shader) { name = "Fx_Lluvia" };
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        else mat.shader = shader;

        Color color = new Color(0.78f, 0.86f, 1f, 0.55f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>
    /// Gotera: chorrito de gotas que cae desde el agujero del techo (ceiling) hasta el piso, y un charco debajo.
    /// El tiempo de vida se calcula para que las gotas desaparezcan justo al llegar al piso.
    /// </summary>
    public static void Leak(string name, Vector3 ceilingPoint, float floorY, Transform parent, int seed)
    {
        float drop = Mathf.Max(0.5f, ceilingPoint.y - floorY);

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = ceilingPoint;
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);       // la forma emite hacia su +Z: hacia abajo

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.gravityModifier = 1f;
        main.startLifetime = Mathf.Sqrt(2f * drop / 9.81f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.05f);
        main.startColor = new Color(0.7f, 0.82f, 1f, 0.85f);
        main.maxParticles = 40;

        var emission = ps.emission;
        emission.rateOverTime = 4f + (seed % 3);

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.12f;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = GetRainMaterial();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2.5f;
        renderer.velocityScale = 0.08f;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        CartoonKit.MakeNonStatic(go);
        CartoonKit.Stain(name + "_Charco", new Vector3(ceilingPoint.x, floorY + 0.012f, ceilingPoint.z), 0.5f, seed, parent, "Charco");
    }
}
