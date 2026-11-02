using System;
using UnityEngine;

/// <summary>
/// Boca abierta / cerrada según el volumen de una voz (fase 3C, decisión D8). Reutilizable: Centavo en el mundo,
/// su retrato en el interrogatorio y, más adelante, los guardias.
/// DÓNDE VA: cualquier GameObject; voiceSource = la AudioSource que reproduce la voz.
///
/// CÓMO DECIDE:
/// - Cada frame lee 256 muestras de la fuente (GetOutputData, búfer reservado una vez: sin basura) y saca el RMS.
/// - Umbral = threshold × el pico de RMS del clip en curso (mínimo minThreshold). El pico se reinicia al cambiar de clip.
/// - Abre si RMS &gt; umbral; cierra si RMS &lt; umbral × hysteresis. Cada cambio dura al menos minHold segundos.
/// - Sin voz sonando = cerrada (_01).
/// Todo en tiempo real (Time.unscaledDeltaTime): funciona con Time.timeScale = 0 (D5).
///
/// QUIÉN LO LEE: IsOpen / OnMouthChanged. Adaptadores: LipSyncSpriteAdapter (SpriteRenderer) y
/// LipSyncImageAdapter (UI Image). CentavoController lo lee directo para pedirle el cuadro a su BillboardSprite.
/// </summary>
public class LipSyncDriver : MonoBehaviour
{
    private const int SampleCount = 256;

    [Tooltip("Fuente de la voz que se escucha")]
    [SerializeField] private AudioSource voiceSource;

    [Tooltip("Fracción del pico del clip a partir de la cual la boca se abre")]
    [SerializeField, Range(0.05f, 0.9f)] private float threshold = 0.35f;

    [Tooltip("Umbral mínimo de RMS (para clips muy bajos o el silencio inicial)")]
    [SerializeField, Min(0.0001f)] private float minThreshold = 0.01f;

    [Tooltip("Para cerrar, el RMS tiene que bajar de umbral × este valor (evita el aleteo)")]
    [SerializeField, Range(0.3f, 1f)] private float hysteresis = 0.7f;

    [Tooltip("Segundos mínimos entre dos cambios de boca")]
    [SerializeField, Min(0f)] private float minHold = 0.06f;

    private readonly float[] _samples = new float[SampleCount];
    private AudioClip _peakClip;
    private float _peak;
    private float _rms;
    private bool _open;
    private float _sinceChange = 999f;

    /// <summary>true = boca abierta (_02).</summary>
    public bool IsOpen => _open;

    /// <summary>Último RMS medido (para depurar el umbral).</summary>
    public float Rms => _rms;

    /// <summary>Umbral de apertura actual.</summary>
    public float CurrentThreshold => Mathf.Max(minThreshold, threshold * _peak);

    /// <summary>Cambió la boca (true = abierta).</summary>
    public event Action<bool> OnMouthChanged;

    public AudioSource VoiceSource => voiceSource;

    /// <summary>Cambia la fuente que se escucha (ej: la voz del interrogatorio).</summary>
    public void SetSource(AudioSource source)
    {
        voiceSource = source;
        _peakClip = null;
        _peak = 0f;
        SetOpen(false, true);
    }

    private void OnDisable()
    {
        SetOpen(false, true);
    }

    private void Update()
    {
        _sinceChange += Time.unscaledDeltaTime;

        if (voiceSource == null || !voiceSource.isPlaying || voiceSource.clip == null)
        {
            _rms = 0f;
            SetOpen(false, false);
            return;
        }

        if (voiceSource.clip != _peakClip)
        {
            _peakClip = voiceSource.clip;
            _peak = 0f;
        }

        voiceSource.GetOutputData(_samples, 0);
        float sum = 0f;
        for (int i = 0; i < SampleCount; i++) sum += _samples[i] * _samples[i];
        _rms = Mathf.Sqrt(sum / SampleCount);
        if (_rms > _peak) _peak = _rms;

        float open = CurrentThreshold;
        if (!_open && _rms > open) SetOpen(true, false);
        else if (_open && _rms < open * hysteresis) SetOpen(false, false);
    }

    private void SetOpen(bool open, bool force)
    {
        if (open == _open) return;
        if (!force && _sinceChange < minHold) return;
        _open = open;
        _sinceChange = 0f;
        OnMouthChanged?.Invoke(open);
    }
}
