using UnityEngine;

/// <summary>
/// ScriptableObject para guardar la API Key de Anthropic.
///
/// CÓMO CREAR ESTE ARCHIVO:
/// En Unity: clic derecho en Assets/_Game/Settings → Create → Game → API Settings
///
/// IMPORTANTE: Este archivo .asset está en .gitignore y NO SE SUBE AL REPO.
/// Cada desarrollador crea el suyo localmente con su propia key.
/// Obtener key gratis en: https://console.anthropic.com
///
/// ¿POR QUÉ UN SCRIPTABLEOBJECT?
/// Porque permite editar la key desde el Inspector de Unity sin tocar el código.
/// Es la forma más segura en Unity para manejar configuraciones sensibles.
/// </summary>
[CreateAssetMenu(fileName = "APISettings", menuName = "Game/API Settings", order = 0)]
public class APISettings : ScriptableObject
{
    [Header("Anthropic API")]
    [Tooltip("Tu API Key de Anthropic. Obtener en console.anthropic.com")]
    [SerializeField] private string apiKey = "";

    // Propiedad pública de solo lectura
    public string ApiKey => apiKey;

    /// <summary>
    /// Verificar si la key está configurada (útil para mostrar warnings en el Inspector).
    /// </summary>
    public bool IsConfigured => !string.IsNullOrEmpty(apiKey) && apiKey.StartsWith("sk-ant-");
}
