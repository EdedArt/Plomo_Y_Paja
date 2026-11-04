using System.Collections;
using UnityEngine;

/// <summary>
/// Salida del nivel (N1.4, N10): al final del sendero, fundido a negro de 1 s con el sonido y carga la escena siguiente por
/// GameManager.LoadLevelAsync. Es una carga normal de nivel: GameStateTracker saca la foto de entrada en sceneLoaded.
/// DÓNDE VA: "Salida_Bosque" de "--- HUELLAS ---", en Anchor_LevelExit (Plomo y Paja → Nivel 1 → Colocar huellas). Capa Ignore Raycast.
/// SI LA ESCENA NO ESTÁ EN BUILD SETTINGS: error en consola y aviso de prueba del guion; el jugador sigue ahí y puede volver a intentar.
/// SIN PRECARGA AL ABRIR EL PORTÓN: la carga empieza al entrar al trigger y la escena se activa al terminar el fundido. Una carga
/// a la espera de activarse detiene las demás, así que morir después de abrir el portón dejaría colgado el reinicio de LevelDeathRestart.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class LevelExit : MonoBehaviour
{
    [SerializeField] private string nextScene = "Campamento";
    [SerializeField] private float fadeSeconds = 1f;
    [Tooltip("Aviso de prueba del guion, solo si la escena no está en Build Settings")]
    [SerializeField] private string missingSceneToast = "Continuará en el campamento…";

    private bool _inside, _leaving, _reportedMissing;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) _inside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _inside = false;
        _reportedMissing = false; // al volver a entrar, avisa otra vez
    }

    private void Update()
    {
        if (!_inside || _leaving || _reportedMissing || PlayerAim.GameplayBlocked()) return;

        GameManager manager = GameManager.Instance;
        if (manager == null || !GameManager.CanLoadLevel(nextScene))
        {
            _reportedMissing = true;
            Debug.LogError($"[Salida] No se puede cargar \"{nextScene}\": " + (manager == null ? "no hay GameManager." : "no está en Build Settings (Plomo y Paja → Nivel 2 → Generar Campamento)."));
            HudWilbur.ShowToast(missingSceneToast);
            return;
        }
        StartCoroutine(Leave(manager));
    }

    private IEnumerator Leave(GameManager manager)
    {
        _leaving = true;
        // Sin guardias ni daño durante el fundido, igual que en una cinemática (I7)
        CinematicState.Set(this, true);
        UiHideReasons.Set(UiHideReasons.Reason.Cinematic, true);
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null && player.TryGetComponent(out PlayerController controller)) controller.enabled = false;

        AsyncOperation load = manager.LoadLevelAsync(nextScene, false);
        float volume = AudioListener.volume;
        if (load != null) load.completed += _ => AudioListener.volume = volume; // este objeto ya no existe cuando termina

        ScreenFade fade = FootprintsSceneDirector.FindSceneFade(gameObject);
        if (fade != null) StartCoroutine(fade.FadeTo(1f, fadeSeconds));
        for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
        {
            AudioListener.volume = Mathf.Lerp(volume, 0f, t / fadeSeconds);
            yield return null;
        }
        AudioListener.volume = 0f;

        if (load == null)
        {
            // CanLoadLevel dijo que sí y aun así falló: no dejar la pantalla negra ni al jugador atrapado
            Debug.LogError($"[Salida] Falló la carga de \"{nextScene}\".");
            AudioListener.volume = volume;
            if (fade != null) fade.Alpha = 0f;
            if (player != null && player.TryGetComponent(out PlayerController again)) again.enabled = true;
            UiHideReasons.Set(UiHideReasons.Reason.Cinematic, false);
            CinematicState.Set(this, false);
            _leaving = false;
            _reportedMissing = true;
            yield break;
        }

        while (load.progress < 0.9f) yield return null;
        load.allowSceneActivation = true;
    }

    private void OnDestroy()
    {
        // Estático: no debe quedar prendido en la escena siguiente
        if (_leaving) UiHideReasons.Set(UiHideReasons.Reason.Cinematic, false);
        CinematicState.Set(this, false);
    }
}
