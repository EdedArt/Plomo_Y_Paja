using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Comprueba que el proyecto use URP y arregla lo que falte con un asset YA existente.
/// MENÚ: Plomo y Paja → Verificar URP
/// No crea ningún pipeline asset nuevo.
/// </summary>
public static class URPVerifier
{
    private const string QualitySettingsPath = "ProjectSettings/QualitySettings.asset";

    [MenuItem("Plomo y Paja/Verificar URP")]
    public static void Verify()
    {
        UniversalRenderPipelineAsset fallback = FindFirstUrpAsset();

        // ----- Graphics Settings (pipeline por defecto) -----
        RenderPipelineAsset graphics = GraphicsSettings.defaultRenderPipeline;
        Debug.Log($"[URP] GraphicsSettings.defaultRenderPipeline = {Describe(graphics)}");

        if (graphics == null)
        {
            if (fallback != null)
            {
                GraphicsSettings.defaultRenderPipeline = fallback;
                Debug.Log($"[URP] Default asignado a {AssetDatabase.GetAssetPath(fallback)}");
            }
            else
            {
                Debug.LogError("[URP] No hay ningún UniversalRenderPipelineAsset en el proyecto. " +
                               "Créalo a mano: Assets → Create → Rendering → URP Asset (with Universal Renderer).");
            }
        }

        // ----- Quality Settings (un pipeline por nivel; null = usa el de Graphics) -----
        string[] names = QualitySettings.names;
        var qualityObject = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(QualitySettingsPath)[0]);
        SerializedProperty levels = qualityObject.FindProperty("m_QualitySettings");
        bool changed = false;

        for (int i = 0; i < names.Length; i++)
        {
            RenderPipelineAsset asset = QualitySettings.GetRenderPipelineAssetAt(i);
            Debug.Log($"[URP] Calidad {i} '{names[i]}': renderPipeline = {Describe(asset)}");

            if (asset != null || fallback == null) continue;

            SerializedProperty pipeline = levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline");
            if (pipeline == null) continue;
            pipeline.objectReferenceValue = fallback;
            changed = true;
            Debug.Log($"[URP] Calidad {i} '{names[i]}' asignada a {AssetDatabase.GetAssetPath(fallback)}");
        }

        if (changed)
        {
            qualityObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        // ----- Resultado -----
        bool graphicsOk = GraphicsSettings.defaultRenderPipeline != null;
        int missing = 0;
        for (int i = 0; i < names.Length; i++)
            if (QualitySettings.GetRenderPipelineAssetAt(i) == null) missing++;

        if (graphicsOk && missing == 0)
            Debug.Log("[URP] OK: Graphics y todos los niveles de calidad usan un pipeline.");
        else
            Debug.LogWarning($"[URP] Pendiente: Graphics {(graphicsOk ? "ok" : "SIN pipeline")}, " +
                             $"{missing} nivel(es) de calidad sin pipeline propio.");
    }

    private static UniversalRenderPipelineAsset FindFirstUrpAsset()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset != null) return asset;
        }
        return null;
    }

    private static string Describe(RenderPipelineAsset asset)
    {
        return asset == null ? "NULL" : $"{asset.name} ({AssetDatabase.GetAssetPath(asset)})";
    }
}
