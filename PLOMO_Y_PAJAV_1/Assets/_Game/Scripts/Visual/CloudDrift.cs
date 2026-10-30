using UnityEngine;

/// <summary>
/// Hace girar muy despacio el grupo de nubes del cielo alrededor del eje Y (el cielo "respira").
/// DÓNDE VA: grupo "Nubes" del cielo (lo crea el generador del nivel).
/// </summary>
public class CloudDrift : MonoBehaviour
{
    [Tooltip("Grados por segundo")]
    [SerializeField] private float degreesPerSecond = 0.35f;

    public float DegreesPerSecond
    {
        get => degreesPerSecond;
        set => degreesPerSecond = value;
    }

    private void Update()
    {
        transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.World);
    }
}
