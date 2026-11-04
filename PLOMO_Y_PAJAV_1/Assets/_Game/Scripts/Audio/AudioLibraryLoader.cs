using UnityEngine;

/// <summary>
/// Mantiene cargada la AudioLibrary y la registra como activa.
/// DÓNDE VA: raíz del Player (lo añade "Crear Prefabs de Prueba").
/// Sin esta referencia el asset no entraría en el build.
/// </summary>
public class AudioLibraryLoader : MonoBehaviour
{
    [SerializeField] private AudioLibrary library;

    private void Awake()
    {
        if (library != null) AudioLibrary.SetActive(library);
    }
}
