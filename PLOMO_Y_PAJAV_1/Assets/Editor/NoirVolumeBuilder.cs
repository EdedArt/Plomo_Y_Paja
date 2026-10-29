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
}
