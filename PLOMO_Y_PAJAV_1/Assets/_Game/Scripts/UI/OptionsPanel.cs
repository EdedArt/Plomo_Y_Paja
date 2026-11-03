using UnityEngine;
using UnityEngine.UI;

// Panel de opciones compartido por el menu de inicio y el de pausa.
// Guarda volumen y pantalla completa en PlayerPrefs y los aplica al abrir
// cualquier escena, asi que no hace falta que el panel este presente.
public class OptionsPanel : MonoBehaviour
{
    private const string KeyVolumen = "opt_volumen";
    private const string KeyPantalla = "opt_pantalla_completa";

    [SerializeField] private Slider volumen;
    [SerializeField] private Toggle pantallaCompleta;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AplicarGuardado()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(KeyVolumen, 1f);
        if (PlayerPrefs.HasKey(KeyPantalla))
            Screen.fullScreen = PlayerPrefs.GetInt(KeyPantalla) == 1;
    }

    private void Awake()
    {
        if (volumen != null) volumen.onValueChanged.AddListener(CambiarVolumen);
        if (pantallaCompleta != null) pantallaCompleta.onValueChanged.AddListener(CambiarPantalla);
    }

    private void OnEnable()
    {
        if (volumen != null) volumen.SetValueWithoutNotify(PlayerPrefs.GetFloat(KeyVolumen, 1f));
        if (pantallaCompleta != null) pantallaCompleta.SetIsOnWithoutNotify(Screen.fullScreen);
    }

    private void CambiarVolumen(float valor)
    {
        AudioListener.volume = valor;
        PlayerPrefs.SetFloat(KeyVolumen, valor);
    }

    private void CambiarPantalla(bool activo)
    {
        Screen.fullScreen = activo;
        PlayerPrefs.SetInt(KeyPantalla, activo ? 1 : 0);
    }
}
