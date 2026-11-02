using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Ajustes gráficos en runtime. Hoy: el SSAO (oclusión ambiental) del renderer de URP.
///
/// AL ARRANCAR (solo, sin escena ni prefab): si la GPU no puede con el SSAO, lo apaga y avisa una vez
/// "[Graficos] SSAO desactivado: GPU sin soporte". Motivo: en GPUs de nivel DX10 URP no puede crear la copia
/// de profundidad (R32 SFloat) que el SSAO necesita y el nivel se ve casi negro.
/// Condiciones para apagarlo: shader model &lt; 4.5, sin RFloat / R32_SFloat como render target, sin R8,
/// sin texturas de profundidad, o modo de prueba "sin SSAO" (ver ForceNoSsaoKey / argumento -sinssao).
///
/// MENÚ DE OPCIONES (futuro): GraphicsSettingsService.SetAmbientOcclusion(bool).
///
/// No guarda nada en el asset del renderer: SetActive solo cambia la copia en memoria, y al salir
/// (Play → Stop en el Editor) se devuelve el estado original para no ensuciar el asset.
/// </summary>
public static class GraphicsSettingsService
{
    /// <summary>PlayerPrefs (1 = forzar el modo sin SSAO, para probar en una PC buena). Lo cambia el menú del Editor.</summary>
    public const string ForceNoSsaoKey = "Graficos.ForzarSinSSAO";

    /// <summary>Argumento de línea de comandos para forzar el modo sin SSAO en una build.</summary>
    public const string ForceNoSsaoArg = "-sinssao";

    private const string SsaoTypeName = "ScreenSpaceAmbientOcclusion"; // Clase interna de URP 14

    private static readonly List<ScriptableRendererFeature> _ssaoFeatures = new List<ScriptableRendererFeature>();
    private static readonly Dictionary<ScriptableRendererFeature, bool> _originalState = new Dictionary<ScriptableRendererFeature, bool>();
    private static bool _initialized;
    private static bool _gpuSupportsSsao = true;
    private static string _unsupportedReason = "";

    /// <summary>¿La GPU puede con el SSAO? (si no, SetAmbientOcclusion(true) no lo enciende)</summary>
    public static bool SupportsAmbientOcclusion
    {
        get { EnsureInitialized(); return _gpuSupportsSsao; }
    }

    /// <summary>¿El SSAO está encendido ahora mismo?</summary>
    public static bool AmbientOcclusionEnabled
    {
        get
        {
            EnsureInitialized();
            foreach (ScriptableRendererFeature f in _ssaoFeatures)
                if (f != null && f.isActive) return true;
            return false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        // Reiniciar el estado estático (por si el Editor no recarga el dominio al entrar en Play)
        _initialized = false;
        _ssaoFeatures.Clear();
        _originalState.Clear();
        EnsureInitialized();

        if (!_gpuSupportsSsao)
        {
            SetFeaturesActive(false);
            Debug.Log("[Graficos] SSAO desactivado: GPU sin soporte (" + _unsupportedReason + ")");
        }
        else if (IsForcedOff())
        {
            SetFeaturesActive(false);
            Debug.Log("[Graficos] SSAO desactivado: modo de prueba \"sin SSAO\" forzado");
        }
    }

    /// <summary>Enciende o apaga el SSAO (menú de opciones). Devuelve el estado final:
    /// pedir true en una GPU sin soporte lo deja apagado.</summary>
    public static bool SetAmbientOcclusion(bool enabled)
    {
        EnsureInitialized();
        if (enabled && !_gpuSupportsSsao) enabled = false;
        SetFeaturesActive(enabled);
        return AmbientOcclusionEnabled;
    }

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        _gpuSupportsSsao = CheckGpuSupport(out _unsupportedReason);
        FindSsaoFeatures();
        Application.quitting -= RestoreOriginalState;
        Application.quitting += RestoreOriginalState;
    }

    /// <summary>Lo que necesitan el SSAO de URP y la copia de profundidad que él mismo pide.</summary>
    private static bool CheckGpuSupport(out string reason)
    {
        if (SystemInfo.graphicsShaderLevel < 45)
        {
            reason = "shader model " + SystemInfo.graphicsShaderLevel / 10f + " < 4.5";
            return false;
        }
        if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat)
            || !SystemInfo.IsFormatSupported(GraphicsFormat.R32_SFloat, FormatUsage.Render))
        {
            reason = "sin R32 SFloat";
            return false;
        }
        if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8))
        {
            reason = "sin R8";
            return false;
        }
        if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Depth))
        {
            reason = "sin texturas de profundidad";
            return false;
        }
        reason = "";
        return true;
    }

    private static bool IsForcedOff()
    {
        if (PlayerPrefs.GetInt(ForceNoSsaoKey, 0) == 1) return true;
        foreach (string arg in Environment.GetCommandLineArgs())
            if (string.Equals(arg, ForceNoSsaoArg, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>Busca el feature de SSAO en todos los renderers del pipeline activo (URP 14 no expone la lista).</summary>
    private static void FindSsaoFeatures()
    {
        var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (asset == null) return;

        FieldInfo field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        var list = field != null ? field.GetValue(asset) as ScriptableRendererData[] : null;
        if (list == null)
        {
            Debug.LogWarning("[Graficos] No se encontró la lista de renderers de URP: el SSAO no se puede apagar en runtime.");
            return;
        }

        foreach (ScriptableRendererData data in list)
        {
            if (data == null || data.rendererFeatures == null) continue;
            foreach (ScriptableRendererFeature feature in data.rendererFeatures)
                if (feature != null && feature.GetType().Name == SsaoTypeName && !_ssaoFeatures.Contains(feature))
                    _ssaoFeatures.Add(feature);
        }
    }

    private static void SetFeaturesActive(bool active)
    {
        foreach (ScriptableRendererFeature f in _ssaoFeatures)
        {
            if (f == null) continue;
            if (!_originalState.ContainsKey(f)) _originalState[f] = f.isActive; // Recordar cómo estaba en el asset
            f.SetActive(active);
        }
    }

    /// <summary>Al salir (o al parar Play en el Editor) el asset vuelve a como estaba.</summary>
    private static void RestoreOriginalState()
    {
        foreach (KeyValuePair<ScriptableRendererFeature, bool> pair in _originalState)
            if (pair.Key != null) pair.Key.SetActive(pair.Value);
        _originalState.Clear();
    }
}
