using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PISTAS.1: lo que hace falta en cada escena sin tocar escenas ni builders.
/// 1. Pistas de los marcadores: cada LevelMarker de tipo Clue (los pone LevelBuilder_NivelPorky: Clue_nota_porky_01,
///    Clue_contrato_01) que no tenga ya un CluePickup con ese id en la escena recibe uno en runtime, apoyado en la
///    superficie que haya bajo el marcador. Las anclas de LevelBuilder_Campamento ya traen su Pista_Auto_*: no se duplican.
/// 2. Aviso "Pista: título" para las pistas que se ganan hablando por DialogueNPC (Colmillo, Tuerto): escucha su evento
///    estático OnInterrogationCompleted (no se toca DialogueNPC). Las de CluePickup.Award (Centavo) lo dan solas.
/// </summary>
public static class ClueRuntime
{
    private const string SpawnPrefix = "Pista_Runtime_";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        DialogueNPC.OnInterrogationCompleted -= HandleInterrogationCompleted;
        DialogueNPC.OnInterrogationCompleted += HandleInterrogationCompleted;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SpawnMissingClues(scene);
    }

    private static void HandleInterrogationCompleted(string npcId, InterrogationResult result, string clueId)
    {
        if (string.IsNullOrEmpty(clueId)) return;
        CluePickup.ShowClueToast(clueId, null);
    }

    /// <summary>CluePickup para cada marcador de pista que no tenga uno. Público para "Validar pistas".</summary>
    public static int SpawnMissingClues(Scene scene)
    {
        LevelMarker[] markers = Object.FindObjectsOfType<LevelMarker>();
        if (markers.Length == 0) return 0;
        CluePickup[] existing = Object.FindObjectsOfType<CluePickup>(true);
        int spawned = 0;
        foreach (LevelMarker marker in markers)
        {
            if (marker.Type != MarkerType.Clue || string.IsNullOrEmpty(marker.Id)) continue;
            if (marker.gameObject.scene != scene) continue;
            if (HasClue(existing, marker.Id)) continue;

            var go = new GameObject(SpawnPrefix + marker.Id);
            go.transform.SetParent(marker.transform, false);
            go.transform.SetPositionAndRotation(marker.transform.position, marker.transform.rotation);
            go.AddComponent<CluePickup>().Configure(marker.Id, true);
            spawned++;
        }
        return spawned;
    }

    private static bool HasClue(CluePickup[] pickups, string clueId)
    {
        foreach (CluePickup p in pickups)
            if (p != null && p.ClueId == clueId) return true;
        return false;
    }
}
