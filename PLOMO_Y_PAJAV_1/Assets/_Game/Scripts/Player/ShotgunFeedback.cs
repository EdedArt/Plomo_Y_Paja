using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Sensación del disparo: culatazo de cámara, temblor de pantalla, destello de luz en el cañón
/// y onomatopeya de cómic ("¡BANG!") cerca de la boca de la escopeta.
/// DÓNDE VA: GameObject "Shotgun" junto a ShotgunController (lo añade "Crear Prefabs de Prueba").
///
/// El retroceso y el temblor se aplican solo mientras la cámara renderiza y se deshacen al terminar,
/// así no se acumulan ni pelean con el mouse look de PlayerController (ni desvían los perdigones).
/// </summary>
[RequireComponent(typeof(ShotgunController))]
public class ShotgunFeedback : MonoBehaviour
{
    [Header("Culatazo de cámara")]
    [SerializeField] private float recoilDegrees = 4f;
    [SerializeField] private float recoilRecovery = 0.15f;

    [Header("Temblor")]
    [SerializeField] private float shakeDuration = 0.08f;
    [SerializeField] private float shakeAmount = 0.05f;

    [Header("Destello")]
    [SerializeField] private float muzzleLightDuration = 0.05f;
    [SerializeField] private Vector3 muzzleLocalPosition = new Vector3(0.3f, -0.2f, 1f);

    [Header("Onomatopeya")]
    [SerializeField] private Color popupColor = new Color32(0xFF, 0xD2, 0x3F, 0xFF);
    [Tooltip("Posición en pantalla (0..1) cerca del cañón")]
    [SerializeField] private Vector2 popupViewport = new Vector2(0.7f, 0.3f);
    [SerializeField] private float popupDuration = 0.4f;

    private static readonly string[] Words = { "¡BANG!", "¡BLAM!", "¡POW!" };
    private const int PopupPool = 4;

    private ShotgunController _shotgun;
    private Camera _camera;
    private Light _muzzleLight;
    private float _muzzleOffAt;

    private float _recoil;          // 1 → 0
    private float _shakeUntil;
    private bool _offsetApplied;
    private Quaternion _savedRotation;
    private Vector3 _savedPosition;

    private RectTransform _canvasRect;
    private TextMeshProUGUI[] _popups;
    private float[] _popupStart;
    private int _nextPopup;
    private int _lastWord = -1;

    private void Awake()
    {
        _shotgun = GetComponent<ShotgunController>();
        _camera = GetComponentInParent<Camera>();
        if (_camera == null) _camera = Camera.main;

        // Luz del fogonazo (apagada salvo 0.05 s por disparo)
        var lightGo = new GameObject("MuzzleLight");
        Transform parent = _camera != null ? _camera.transform : transform;
        lightGo.transform.SetParent(parent, false);
        lightGo.transform.localPosition = muzzleLocalPosition;
        _muzzleLight = lightGo.AddComponent<Light>();
        _muzzleLight.type = LightType.Point;
        _muzzleLight.color = new Color32(0xFF, 0xC8, 0x6B, 0xFF);
        _muzzleLight.range = 8f;
        _muzzleLight.intensity = 4f;
        _muzzleLight.shadows = LightShadows.None;
        _muzzleLight.enabled = false;

        BuildPopups();
    }

    private void OnEnable()
    {
        _shotgun.OnFired += HandleFired;
        RenderPipelineManager.beginCameraRendering += ApplyOffset;
        RenderPipelineManager.endCameraRendering += RestoreOffset;
    }

    private void OnDisable()
    {
        _shotgun.OnFired -= HandleFired;
        RenderPipelineManager.beginCameraRendering -= ApplyOffset;
        RenderPipelineManager.endCameraRendering -= RestoreOffset;
        if (_muzzleLight != null) _muzzleLight.enabled = false;
    }

    private void HandleFired()
    {
        _recoil = 1f;
        _shakeUntil = Time.unscaledTime + shakeDuration;
        _muzzleLight.enabled = true;
        _muzzleOffAt = Time.unscaledTime + muzzleLightDuration;
        ShowPopup();
    }

    private void Update()
    {
        // Tiempo real: el hit-stop no deja la luz encendida ni la cámara torcida
        float dt = Time.unscaledDeltaTime;
        _recoil = Mathf.MoveTowards(_recoil, 0f, dt / recoilRecovery);
        if (_muzzleLight.enabled && Time.unscaledTime >= _muzzleOffAt) _muzzleLight.enabled = false;
        UpdatePopups();
    }

    // ============ Cámara: desplazamiento solo durante el render ============

    private void ApplyOffset(ScriptableRenderContext context, Camera cam)
    {
        if (cam != _camera || _offsetApplied) return;

        Transform t = _camera.transform;
        _savedRotation = t.localRotation;
        _savedPosition = t.localPosition;
        _offsetApplied = true;

        float kick = _recoil * _recoil * recoilDegrees; // sube rápido, baja suave
        t.localRotation = _savedRotation * Quaternion.Euler(-kick, 0f, 0f);

        if (Time.unscaledTime < _shakeUntil)
            t.localPosition = _savedPosition + (Vector3)(Random.insideUnitCircle * shakeAmount);
    }

    private void RestoreOffset(ScriptableRenderContext context, Camera cam)
    {
        if (cam != _camera || !_offsetApplied) return;
        _camera.transform.localRotation = _savedRotation;
        _camera.transform.localPosition = _savedPosition;
        _offsetApplied = false;
    }

    // ============ Onomatopeyas (pool fijo) ============

    private void BuildPopups()
    {
        var canvasGo = new GameObject("ShotgunPopups_Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -5; // encima de las manos, debajo del HUD
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        _canvasRect = (RectTransform)canvasGo.transform;

        _popups = new TextMeshProUGUI[PopupPool];
        _popupStart = new float[PopupPool];
        for (int i = 0; i < PopupPool; i++)
        {
            var go = new GameObject("Popup_" + i, typeof(RectTransform));
            go.transform.SetParent(canvasGo.transform, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.sizeDelta = new Vector2(420f, 140f);

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = 84f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            text.color = popupColor;
            text.outlineColor = new Color32(0, 0, 0, 255);
            text.outlineWidth = 0.3f;
            text.text = "";
            go.SetActive(false);

            _popups[i] = text;
            _popupStart[i] = -1f;
        }
    }

    private void ShowPopup()
    {
        int word = Random.Range(0, Words.Length);
        if (word == _lastWord) word = (word + 1) % Words.Length;
        _lastWord = word;

        TextMeshProUGUI text = _popups[_nextPopup];
        _popupStart[_nextPopup] = Time.unscaledTime;
        _nextPopup = (_nextPopup + 1) % PopupPool;

        Vector2 size = _canvasRect.rect.size;
        RectTransform rect = text.rectTransform;
        rect.anchoredPosition = new Vector2(
            (popupViewport.x + Random.Range(-0.04f, 0.04f)) * size.x,
            (popupViewport.y + Random.Range(-0.04f, 0.04f)) * size.y);
        rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-14f, 14f));
        text.text = Words[word];
        text.alpha = 1f;
        text.gameObject.SetActive(true);
    }

    private void UpdatePopups()
    {
        for (int i = 0; i < PopupPool; i++)
        {
            if (_popupStart[i] < 0f) continue;
            float t = (Time.unscaledTime - _popupStart[i]) / popupDuration;
            TextMeshProUGUI text = _popups[i];
            if (t >= 1f)
            {
                _popupStart[i] = -1f;
                text.gameObject.SetActive(false);
                continue;
            }
            // Pop: 0 → 1.3 → 1 y se desvanece al final
            float scale = t < 0.25f ? Mathf.Lerp(0.2f, 1.3f, t / 0.25f) : Mathf.Lerp(1.3f, 1f, (t - 0.25f) / 0.75f);
            text.rectTransform.localScale = Vector3.one * scale;
            text.alpha = t > 0.7f ? 1f - (t - 0.7f) / 0.3f : 1f;
        }
    }
}
