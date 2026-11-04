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

    /// <summary>
    /// Carga TechnicolorVolumeProfile.asset (o lo crea) y le aplica siempre los valores actuales:
    /// fase 3A2 = un poco más oscuro y con más carácter (viñeta 0.3, grano Medium 0.18, contraste 15).
    /// Los mismos números están en NoirPostProcess (que copia el perfil en Play).
    /// </summary>
    public static VolumeProfile GetOrCreateTechnicolorProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(TechnicolorProfilePath);
        if (profile == null)
        {
            LevelBuilderUtils.EnsureFolder("Assets/_Game/Settings");
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, TechnicolorProfilePath);
        }

        Get<Tonemapping>(profile).mode.Override(TonemappingMode.Neutral);

        ColorAdjustments color = Get<ColorAdjustments>(profile);
        color.postExposure.Override(0.2f);
        color.contrast.Override(15f);
        color.saturation.Override(20f);
        color.colorFilter.Override(new Color32(0xFF, 0xF4, 0xE6, 0xFF));

        Get<WhiteBalance>(profile).temperature.Override(8f);

        Bloom bloom = Get<Bloom>(profile); // se mantiene para los faroles
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(0.6f);

        Vignette vignette = Get<Vignette>(profile);
        vignette.intensity.Override(0.3f);
        vignette.color.Override(Color.black);

        FilmGrain grain = Get<FilmGrain>(profile);
        grain.type.Override(FilmGrainLookup.Medium1);
        grain.intensity.Override(0.18f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    /// <summary>Componente del perfil; si falta se añade como sub-asset.</summary>
    private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T component)) return component;
        component = profile.Add<T>();
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
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
