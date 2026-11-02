using UnityEngine;

/// <summary>
/// Pone en un SpriteRenderer el cuadro de boca cerrada (_01) o abierta (_02) que dice un LipSyncDriver.
/// DÓNDE VA: junto al SpriteRenderer. No usar en un objeto con BillboardSprite que reproduce un set (se pisarían);
/// para eso, leer LipSyncDriver.IsOpen y pedirle el cuadro al BillboardSprite (como hace CentavoController).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class LipSyncSpriteAdapter : MonoBehaviour
{
    [SerializeField] private LipSyncDriver driver;
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite openSprite;

    private SpriteRenderer _renderer;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
    }

    public void SetDriver(LipSyncDriver lipSync)
    {
        driver = lipSync;
    }

    /// <summary>Cambia el par de la emoción actual (ej: herido_01 / herido_02).</summary>
    public void SetPair(Sprite closed, Sprite open)
    {
        closedSprite = closed;
        openSprite = open;
        Apply();
    }

    private void LateUpdate()
    {
        Apply();
    }

    private void Apply()
    {
        if (_renderer == null) return;
        bool open = driver != null && driver.IsOpen;
        Sprite target = open && openSprite != null ? openSprite : closedSprite;
        if (target != null && _renderer.sprite != target) _renderer.sprite = target;
    }
}
