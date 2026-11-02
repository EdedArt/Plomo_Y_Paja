using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pone en una Image de UI el cuadro de boca cerrada (_01) o abierta (_02) que dice un LipSyncDriver
/// (retrato del interrogatorio, fase 3C.3).
/// DÓNDE VA: junto a la Image.
/// </summary>
[RequireComponent(typeof(Image))]
public class LipSyncImageAdapter : MonoBehaviour
{
    [SerializeField] private LipSyncDriver driver;
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite openSprite;

    private Image _image;
    private Sprite _override;

    private void Awake()
    {
        _image = GetComponent<Image>();
    }

    public void SetDriver(LipSyncDriver lipSync)
    {
        driver = lipSync;
    }

    /// <summary>Cambia el par de la emoción actual (ej: talk_01 / talk_02).</summary>
    public void SetPair(Sprite closed, Sprite open)
    {
        closedSprite = closed;
        openSprite = open;
        Apply();
    }

    /// <summary>Muestra este cuadro en lugar del par mientras no sea null (ej: blink_01 en silencio).</summary>
    public void SetOverride(Sprite sprite)
    {
        _override = sprite;
        Apply();
    }

    public bool IsOpen => driver != null && driver.IsOpen;

    private void LateUpdate()
    {
        Apply();
    }

    private void Apply()
    {
        if (_image == null) return;
        if (_override != null)
        {
            if (_image.sprite != _override) _image.sprite = _override;
            return;
        }
        bool open = driver != null && driver.IsOpen;
        Sprite target = open && openSprite != null ? openSprite : closedSprite;
        if (target != null && _image.sprite != target) _image.sprite = target;
    }
}
