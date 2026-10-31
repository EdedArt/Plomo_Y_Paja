using UnityEditor;
using UnityEngine;

/// <summary>
/// Crea la capa "Viewmodel" en ProjectSettings/TagManager (por SerializedObject, nunca a mano) si falta.
/// Usa el primer hueco libre desde la capa 8 (las capas 0-7 son de Unity). La llama "Crear Prefabs de Prueba"
/// antes de construir el Player, porque ViewmodelCameraRig necesita la capa para armar el stack de cámaras.
/// </summary>
public static class ViewmodelLayerSetup
{
    [MenuItem("Plomo y Paja/Crear Capa Viewmodel")]
    public static void EnsureLayerMenu()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        Debug.Log(EnsureLayer() >= 0
            ? "[Viewmodel] Capa '" + ViewmodelCameraRig.LayerName + "' lista (índice " + ViewmodelCameraRig.Layer + ")."
            : "[Viewmodel] No se pudo crear la capa '" + ViewmodelCameraRig.LayerName + "'.");
    }

    /// <summary>Devuelve el índice de la capa (la crea si falta) o -1 si no hay hueco.</summary>
    public static int EnsureLayer()
    {
        int existing = LayerMask.NameToLayer(ViewmodelCameraRig.LayerName);
        if (existing >= 0) return existing;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0)
        {
            Debug.LogError("[Viewmodel] No se pudo abrir ProjectSettings/TagManager.asset.");
            return -1;
        }

        var so = new SerializedObject(assets[0]);
        SerializedProperty layers = so.FindProperty("layers");
        if (layers == null || !layers.isArray)
        {
            Debug.LogError("[Viewmodel] TagManager sin lista de capas.");
            return -1;
        }

        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(slot.stringValue)) continue;
            slot.stringValue = ViewmodelCameraRig.LayerName;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("[Viewmodel] Capa '" + ViewmodelCameraRig.LayerName + "' creada en el índice " + i + " de TagManager.");
            return i;
        }
        Debug.LogError("[Viewmodel] No hay capas libres (8-31) para '" + ViewmodelCameraRig.LayerName + "'.");
        return -1;
    }
}
