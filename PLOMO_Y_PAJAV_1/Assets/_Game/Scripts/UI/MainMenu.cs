using UnityEngine;
using UnityEngine.SceneManagement;

// Menu de inicio. Los metodos publicos se conectan a los botones (OnClick).
public class MainMenu : MonoBehaviour
{
    [SerializeField] private string escenaJuego = "NivelPorky";
    [SerializeField] private GameObject panelPrincipal;
    [SerializeField] private GameObject panelOpciones;
    [SerializeField] private GameObject panelCreditos;

    private void Start()
    {
        // Por si se llega desde una partida pausada.
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        MostrarPrincipal();
    }

    public void Jugar()
    {
        SceneManager.LoadScene(escenaJuego);
    }

    public void AbrirOpciones()
    {
        Mostrar(panelOpciones);
    }

    public void AbrirCreditos()
    {
        Mostrar(panelCreditos);
    }

    public void MostrarPrincipal()
    {
        Mostrar(panelPrincipal);
    }

    public void Salir()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Mostrar(GameObject panel)
    {
        if (panelPrincipal != null) panelPrincipal.SetActive(panel == panelPrincipal);
        if (panelOpciones != null) panelOpciones.SetActive(panel == panelOpciones);
        if (panelCreditos != null) panelCreditos.SetActive(panel == panelCreditos);
    }
}
