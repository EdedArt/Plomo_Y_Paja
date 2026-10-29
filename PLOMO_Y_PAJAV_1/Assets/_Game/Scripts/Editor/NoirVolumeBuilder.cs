using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Perfil de post-procesado noir del GDD (guardado como asset) y Volume global para la escena.
/// Lo usan los LevelBuilder; el perfil solo se crea si falta.
/// </summary>
public static class NoirVolumeBuilder
{
    public const string ProfilePath = "Assets/_Game/Settings/NoirVolumeProfile.asset";

    /// <summary>Carga NoirVolumeProfile.asset o lo crea con los valores del GDD.</summary>
    public static VolumeProfile GetOrCreateProfile()
    {
        var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (existing != null) return existing;

        LevelBuilderUtils.EnsureFolder("Assets/_Game/Settings");

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);

        // Los componentes se guardan como sub-assets del perfil
        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.saturation.Override(-70f);
        color.contrast.Override(25f);

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.35f);
        vignette.color.Override(Color.black);

        FilmGrain grain = profile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0.35f);

        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1.2f);
        bloom.intensity.Override(0.1f);

        AssetDatabase.AddObjectToAsset(color, profile);
        AssetDatabase.AddObjectToAsset(vignette, profile);
        AssetDatabase.AddObjectToAsset(grain, profile);
        AssetDatabase.AddObjectToAsset(bloom, profile);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    /// <summary>Crea el GameObject "Volume_Noir" (global) con el perfil noir.</summary>
    public static Volume CreateGlobalVolume(Transform parent)
    {
        var go = new GameObject("Volume_Noir");
        if (parent != null) go.transform.SetParent(parent, false);

        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.sharedProfile = GetOrCreateProfile();
        return volume;
    }

    // ==================================================================
    // Look Technicolor (fase 3A): a todo color, cálido y legible de noche
    // ==================================================================

    public const string TechnicolorProfilePath = "Assets/_Game/Settings/TechnicolorVolumeProfile.asset";

    /// <summary>Carga TechnicolorVolumeProfile.asset o lo crea con los valores de la fase 3A.</summary>
    public static VolumeProfile GetOrCreateTechnicolorProfile()
    {
        var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(TechnicolorProfilePath);
        if (existing != null) return existing;

        LevelBuilderUtils.EnsureFolder("Assets/_Game/Settings");

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, TechnicolorProfilePath);

        Tonemapping tonemapping = profile.Add<Tonemapping>();
        tonemapping.mode.Override(TonemappingMode.Neutral);

        ColorAdjustments color = profile.Add<ColorAdjustments>();
        color.postExposure.Override(0.4f);
        color.contrast.Override(10f);
        color.saturation.Override(20f);
        color.colorFilter.Override(new Color32(0xFF, 0xF4, 0xE6, 0xFF));

        WhiteBalance whiteBalance = profile.Add<WhiteBalance>();
        whiteBalance.temperature.Override(8f);

        Bloom bloom = profile.Add<Bloom>();
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(0.6f);

        Vignette vignette = profile.Add<Vignette>();
        vignette.intensity.Override(0.2f);
        vignette.color.Override(Color.black);

        FilmGrain grain = profile.Add<FilmGrain>();
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0.12f);

        foreach (VolumeComponent component in profile.components)
            AssetDatabase.AddObjectToAsset(component, profile);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    /// <summary>
    /// Crea "Volume_Technicolor" (global) con el perfil Technicolor y NoirPostProcess,
    /// que en Play lo sustituye por una copia con los mismos valores y hace el pulso de daño.
    /// </summary>
    public static Volume CreateTechnicolorVolume(Transform parent)
    {
        var go = new GameObject("Volume_Technicolor");
        if (parent != null) go.transform.SetParent(parent, false);

        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.sharedProfile = GetOrCreateTechnicolorProfile();
        go.AddComponent<NoirPostProcess>();
        return volume;
    }
}
