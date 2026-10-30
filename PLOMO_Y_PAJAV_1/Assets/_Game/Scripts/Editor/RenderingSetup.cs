using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Ajustes del renderer URP para el look pintado (fase 3A2).
/// - Añade la feature "Screen Space Ambient Occlusion" (si no está) con:
///   Intensity 1, Radius 0.3, Direct Lighting Strength 0.25, Source Depth Normals,
///   Samples Medium, Blur High, Downsample off, After Opaque off, Falloff 100 m.
/// - Activa sombras suaves en el asset del pipeline (la luna las usa).
/// La clase de URP es internal en URP 14: se crea por reflexión y se configura con SerializedObject.
/// </summary>
public static class RenderingSetup
{
    private const string SsaoTypeName = "UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion";

    [MenuItem("Plomo y Paja/Configurar Renderer (SSAO)")]
    public static void Configure()
    {
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null)
        {
            Debug.LogError("[RenderingSetup] No hay un UniversalRenderPipelineAsset activo en Graphics Settings.");
            return;
        }

        var pipelineSo = new SerializedObject(pipeline);
        SerializedProperty soft = pipelineSo.FindProperty("m_SoftShadowsSupported");
        if (soft != null) soft.boolValue = true;
        pipelineSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedProperty list = pipelineSo.FindProperty("m_RendererDataList");
        var renderer = list != null && list.arraySize > 0
            ? list.GetArrayElementAtIndex(0).objectReferenceValue as ScriptableRendererData
            : null;
        if (renderer == null)
        {
            Debug.LogError("[RenderingSetup] El pipeline no tiene renderer data en el índice 0.");
            return;
        }

        ScriptableRendererFeature ssao = FindOrAddSsao(renderer);
        if (ssao == null) return;

        var so = new SerializedObject(ssao);
        SetFloat(so, "m_Settings.Intensity", 1f);
        SetFloat(so, "m_Settings.Radius", 0.3f);
        SetFloat(so, "m_Settings.DirectLightingStrength", 0.25f);
        SetFloat(so, "m_Settings.Falloff", 100f);
        SetEnum(so, "m_Settings.Source", 1);      // DepthNormals
        SetEnum(so, "m_Settings.Samples", 1);     // Medium
        SetEnum(so, "m_Settings.BlurQuality", 0); // High
        SetBool(so, "m_Settings.Downsample", false);
        SetBool(so, "m_Settings.AfterOpaque", false);
        so.ApplyModifiedPropertiesWithoutUndo();
        ssao.SetActive(true);

        EditorUtility.SetDirty(ssao);
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(pipeline);
        AssetDatabase.SaveAssets();
        Debug.Log("[RenderingSetup] SSAO listo en '" + renderer.name + "' (Intensity 1, Radius 0.3) y sombras suaves activas.");
    }

    private static ScriptableRendererFeature FindOrAddSsao(ScriptableRendererData renderer)
    {
        foreach (ScriptableRendererFeature f in renderer.rendererFeatures)
            if (f != null && f.GetType().FullName == SsaoTypeName) return f;

        Type type = typeof(UniversalRendererData).Assembly.GetType(SsaoTypeName);
        if (type == null)
        {
            Debug.LogError("[RenderingSetup] No se encontró la clase de SSAO de URP. Añádela a mano: Renderer > Add Renderer Feature > Screen Space Ambient Occlusion.");
            return null;
        }

        var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
        feature.name = "ScreenSpaceAmbientOcclusion";
        AssetDatabase.AddObjectToAsset(feature, renderer);
        AssetDatabase.SaveAssets();
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

        // Igual que el inspector de URP: lista de features + mapa de IDs locales
        var rendererSo = new SerializedObject(renderer);
        SerializedProperty features = rendererSo.FindProperty("m_RendererFeatures");
        SerializedProperty map = rendererSo.FindProperty("m_RendererFeatureMap");
        features.arraySize++;
        features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
        if (map != null)
        {
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
        }
        rendererSo.ApplyModifiedPropertiesWithoutUndo();
        renderer.SetDirty();
        return feature;
    }

    private static void SetFloat(SerializedObject so, string path, float value)
    {
        SerializedProperty p = so.FindProperty(path);
        if (p != null) p.floatValue = value;
        else Debug.LogWarning("[RenderingSetup] Propiedad no encontrada: " + path);
    }

    private static void SetEnum(SerializedObject so, string path, int value)
    {
        SerializedProperty p = so.FindProperty(path);
        if (p != null) p.intValue = value;
        else Debug.LogWarning("[RenderingSetup] Propiedad no encontrada: " + path);
    }

    private static void SetBool(SerializedObject so, string path, bool value)
    {
        SerializedProperty p = so.FindProperty(path);
        if (p != null) p.boolValue = value;
        else Debug.LogWarning("[RenderingSetup] Propiedad no encontrada: " + path);
    }
}
