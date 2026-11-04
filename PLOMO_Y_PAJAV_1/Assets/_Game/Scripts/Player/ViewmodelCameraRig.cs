using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Cámara aparte para las manos (URP camera stacking), para que nada del mundo se dibuje encima de ellas:
///
///   CameraHolder
///   ├── Main Camera          Base, culling mask SIN la capa "Viewmodel", zoom de apuntar (solo esta cámara)
///   └── ViewmodelCamera      Overlay, solo ve la capa "Viewmodel", FOV fijo 60, near 0.1 / far 10,
///                            Post Processing ON, agregada al stack de la Main Camera, borra la profundidad
///
/// Por qué así: un canvas en la cola transparente de la cámara del mundo compite con la lluvia, los conos de luz y
/// la sangre (ZTest no basta: los transparentes que se dibujan después tapan las manos). Con una cámara Overlay
/// las manos se dibujan DESPUÉS de todo el mundo, y como es la última cámara del stack, el post-procesado
/// (grano, viñeta, color, bloom) se aplica a mundo + manos juntos. El HUD sigue en Screen Space - Overlay, que URP dibuja
/// después del post-procesado, así que no lo recibe.
///
/// La ViewmodelCamera es HERMANA de la Main Camera (hija del mismo CameraHolder), no hija: ShotgunFeedback mueve y
/// gira la Main Camera durante el render al disparar; así las manos no heredan ese temblor. Como solo ve la capa
/// Viewmodel, su posición en el mundo no importa.
///
/// Lo llaman "Crear Prefabs de Prueba" (queda guardado en el prefab del Player) y FirstPersonViewmodel al arrancar
/// (por si el Player de una escena no se regeneró). Es idempotente.
/// </summary>
public static class ViewmodelCameraRig
{
    public const string LayerName = "Viewmodel";
    public const string CameraName = "ViewmodelCamera";
    public const float FixedFov = 60f;
    public const float NearClip = 0.1f;
    public const float FarClip = 10f;
    /// <summary>Distancia del canvas a la ViewmodelCamera (m).</summary>
    public const float PlaneDistance = 1f;

    /// <summary>Índice de la capa "Viewmodel", o -1 si no existe en TagManager (la crea "Crear Prefabs de Prueba").</summary>
    public static int Layer => LayerMask.NameToLayer(LayerName);

    /// <summary>
    /// Deja listo el stack de cámaras para el viewmodel y devuelve la ViewmodelCamera.
    /// null si falta la capa (el viewmodel se dibuja entonces en Overlay, sin post-procesado) o no hay cámara.
    /// </summary>
    public static Camera Ensure(Camera main)
    {
        int layer = Layer;
        if (main == null || layer < 0) return null;

        Transform parent = main.transform.parent != null ? main.transform.parent : main.transform;
        Transform existing = parent.Find(CameraName);
        Camera cam;
        if (existing != null && existing.TryGetComponent(out cam)) { }
        else
        {
            var go = new GameObject(CameraName);
            go.transform.SetParent(parent, false);
            cam = go.AddComponent<Camera>();
        }
        cam.gameObject.layer = layer;

        // Misma pose que la cámara principal (no importa para el render: solo ve su capa)
        cam.transform.localPosition = main.transform.localPosition;
        cam.transform.localRotation = main.transform.localRotation;
        cam.transform.localScale = Vector3.one;

        cam.clearFlags = CameraClearFlags.Depth;
        cam.cullingMask = 1 << layer;
        cam.orthographic = false;
        cam.fieldOfView = FixedFov;
        cam.nearClipPlane = NearClip;
        cam.farClipPlane = FarClip;
        cam.useOcclusionCulling = false;
        cam.allowMSAA = false;
        cam.allowDynamicResolution = false;
        cam.depth = main.depth + 1f;
        if (cam.CompareTag("MainCamera")) cam.tag = "Untagged"; // Camera.main debe seguir siendo la del mundo

        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        data.renderType = CameraRenderType.Overlay;
        data.renderPostProcessing = true;   // la última cámara del stack es la que aplica el post-procesado
        // clearDepth (solo lectura en URP 14) vale true por defecto en las cámaras Overlay: las manos van siempre encima del mundo
        data.renderShadows = false;

        main.cullingMask &= ~(1 << layer);  // la cámara del mundo no dibuja las manos
        UniversalAdditionalCameraData mainData = main.GetUniversalAdditionalCameraData();
        mainData.renderPostProcessing = true;
        if (!mainData.cameraStack.Contains(cam)) mainData.cameraStack.Add(cam);
        return cam;
    }

    public static void SetLayerRecursive(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform) SetLayerRecursive(child.gameObject, layer);
    }
}
