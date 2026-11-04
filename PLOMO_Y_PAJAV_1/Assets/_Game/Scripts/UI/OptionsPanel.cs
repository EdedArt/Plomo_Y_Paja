using UnityEngine;
using UnityEngine.UI;

// Panel de opciones compartido por el menu de inicio y el de pausa.
// Guarda volumen, musica, sensibilidad y pantalla completa en PlayerPrefs y los
// aplica al abrir cualquier escena, asi que no hace falta que el panel este presente.
public class OptionsPanel : MonoBehaviour
{
    private const string KeyVolumen = "opt_volumen";
    private const string KeyPantalla = "opt_pantalla_completa";
    private const string KeySensibilidad = "opt_sensibilidad";

    private static float sensibilidad = -1f;

    [SerializeField] private Slider volumen;
    [SerializeField] private Toggle pantallaCompleta;
    [SerializeField] private Slider musica;
    [SerializeField] private Slider sensibilidadRaton;

    /// <summary>Multiplicador de la sensibilidad del raton (1 = la del juego). Sin PlayerPrefs por cuadro.</summary>
    public static float Sensibilidad
    {
        get
        {
            if (sensibilidad < 0f) sensibilidad = PlayerPrefs.GetFloat(KeySensibilidad, 1f);
            return sensibilidad;
        }
    }

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
        if (musica != null) musica.onValueChanged.AddListener(CambiarMusica);
        if (sensibilidadRaton != null) sensibilidadRaton.onValueChanged.AddListener(CambiarSensibilidad);
    }

    private void OnEnable()
    {
        if (volumen != null) volumen.SetValueWithoutNotify(PlayerPrefs.GetFloat(KeyVolumen, 1f));
        if (pantallaCompleta != null) pantallaCompleta.SetIsOnWithoutNotify(Screen.fullScreen);
        if (musica != null) musica.SetValueWithoutNotify(MusicPlayer.UserVolume);
        if (sensibilidadRaton != null) sensibilidadRaton.SetValueWithoutNotify(Sensibilidad);
    }

    private void OnDisable()
    {
        // Al cerrar el panel, para que sobreviva aunque el juego se cierre mal.
        PlayerPrefs.Save();
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

    private void CambiarMusica(float valor)
    {
        MusicPlayer.UserVolume = valor;
    }

    private void CambiarSensibilidad(float valor)
    {
        sensibilidad = Mathf.Max(0.01f, valor);
        PlayerPrefs.SetFloat(KeySensibilidad, sensibilidad);
    }
}
