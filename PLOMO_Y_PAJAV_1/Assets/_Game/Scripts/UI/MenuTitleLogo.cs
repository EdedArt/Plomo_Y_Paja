using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Título del menú (M11): "PLOMO y PAJA" editando la malla TMP letra por letra, sin basura por cuadro.
/// DÓNDE VA: objeto "Logo" de PanelPrincipal (lo arma Plomo y Paja → Crear menu de inicio). Si existe menu_logo.png, usa
/// esa imagen ("logoImage") con el bamboleo, el salto del rayo y el tinte, sin efectos por letra.
///
/// - Cada palabra son hasta 3 TMP con el mismo texto: sombra dura detrás, letra con degradado, brillo crema encima.
/// - Pose fija con semilla: arco general y, por letra, rotación, escala y altura al azar.
/// - En reposo: ola desfasada por letra y bamboleo general. El bamboleo va en la malla, no en la escala del RectTransform,
///   para que TMP no recalcule su escala SDF cada cuadro.
/// - Eventos de LightningFlash: el rayo da destello blanco de un cuadro y salto escalonado; cada relámpago lejano, tinte frío.
/// - MainMenu mueve este RectTransform de la posición de espera a la del menú.
/// </summary>
public class MenuTitleLogo : MonoBehaviour
{
    [Serializable]
    public class Word
    {
        public TMP_Text main;
        [Tooltip("TMP detrás, con el mismo texto (opcional)")]
        public TMP_Text shadow;
        [Tooltip("TMP encima, con el mismo texto (opcional)")]
        public TMP_Text shine;
        public Color top = Color.white;
        public Color bottom = Color.white;
        [Tooltip("Desplazamiento vertical respecto a la línea base común (px)")]
        public float verticalOffset;
    }

    [Header("Piezas (las pone el builder)")]
    [SerializeField] private Word[] words;
    [SerializeField] private Image logoImage;
    [SerializeField] private LightningFlash lightning;

    [Header("Composición")]
    [SerializeField] private float wordGap = 14f;
    [Tooltip("Línea base de las palabras respecto al centro del logo (px)")]
    [SerializeField] private float baselineY = -46f;
    [SerializeField] private Vector2 shadowOffset = new Vector2(8f, -8f);
    [SerializeField] private Color shadowColor = new Color32(0x2A, 0x1A, 0x10, 0xFF);
    [SerializeField] private Color shineColor = new Color32(0xFF, 0xF4, 0xD6, 0xFF);
    [SerializeField, Range(0f, 1f)] private float shineAlpha = 0.55f;

    [Header("Pose fija")]
    [SerializeField] private int seed = 1930;
    [Tooltip("Ángulo total del arco de punta a punta")]
    [SerializeField] private float arcDegrees = 6f;
    [SerializeField] private float letterRotation = 4f;
    [SerializeField] private float letterScale = 0.05f;
    [SerializeField] private float letterHeight = 6f;

    [Header("Reposo: ola por letra")]
    [SerializeField] private float wavePixels = 3f;
    [SerializeField] private float wavePeriod = 2.2f;
    [Tooltip("Desfase entre letras seguidas (radianes)")]
    [SerializeField] private float wavePhasePerLetter = 0.55f;

    [Header("Reposo: bamboleo general")]
    [SerializeField] private float wobbleDegrees = 1f;
    [SerializeField] private float wobbleScale = 0.01f;
    [SerializeField] private float wobblePeriod = 3f;

    [Header("Rayo: salto escalonado")]
    [SerializeField] private float jumpStagger = 0.03f;
    [SerializeField] private float jumpSeconds = 0.35f;
    [SerializeField] private float jumpHeight = 28f;
    [SerializeField] private Vector2 squash = new Vector2(1.15f, 0.85f);
    [SerializeField] private Vector2 stretch = new Vector2(0.9f, 1.15f);
    [Tooltip("Fracción del salto en que termina de aplastarse")]
    [SerializeField, Range(0.05f, 0.5f)] private float squashEnd = 0.2f;
    [Tooltip("Fracción del salto en que termina de estirarse; lo demás es el rebote")]
    [SerializeField, Range(0.1f, 0.9f)] private float stretchEnd = 0.5f;
    [SerializeField] private float bounceDamping = 4f;
    [SerializeField] private float bounceCycles = 1.5f;
    [Tooltip("Cuadros en blanco al caer el rayo")]
    [SerializeField] private int whiteFrames = 1;

    [Header("Relámpago lejano: tinte frío")]
    [SerializeField] private Color coldTint = new Color32(0xD6, 0xE6, 0xFF, 0xFF);
    [SerializeField] private float coldTintSeconds = 0.15f;

    private enum LayerKind { Main, Shadow, Shine }

    private class Layer
    {
        public TMP_Text text;
        public LayerKind kind;
        public int word;
        public int first;
        public int count;
        public bool valid;
        public Vector3[] baseVerts;
        public int[] meshIndex;
        public int[] vertexIndex;
        public Color32 top;
        public Color32 bottom;
    }

    private Layer[] _layers = new Layer[0];
    private Vector2[] _wordPositions;
    private Vector2[] _pivots;
    private int _letterCount;
    private float[] _poseRotation, _poseScale, _poseY;
    private float[] _scaleX, _scaleY, _cos, _sin, _offsetY;
    private bool _ready;
    private RectTransform _rect;
    private float _globalCos = 1f, _globalSin, _globalScale = 1f;
    private float _jumpStart = -1f;
    private int _whiteLeft;
    private bool _white;
    private float _tintStart = -1f;
    private float _tint;
    private bool _colorsDirty = true;
    private Action<UnityEngine.Object> _onTextChanged;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        if (logoImage != null || words == null || words.Length == 0) return;

        int layers = 0;
        foreach (Word w in words)
        {
            if (w.main == null) continue;
            layers += 1 + (w.shadow != null ? 1 : 0) + (w.shine != null ? 1 : 0);
        }
        _layers = new Layer[layers];
        _wordPositions = new Vector2[words.Length];

        int index = 0;
        for (int i = 0; i < words.Length; i++)
        {
            Word w = words[i];
            if (w.main == null) continue;
            int count = VisibleLength(w.main.text);
            _layers[index++] = NewLayer(w.main, LayerKind.Main, i, _letterCount, count, w.top, w.bottom);
            if (w.shadow != null)
                _layers[index++] = NewLayer(w.shadow, LayerKind.Shadow, i, _letterCount, count, shadowColor, shadowColor);
            if (w.shine != null)
            {
                Color fade = shineColor;
                fade.a = 0f;
                Color lit = shineColor;
                lit.a = shineAlpha;
                _layers[index++] = NewLayer(w.shine, LayerKind.Shine, i, _letterCount, count, lit, fade);
            }
            _letterCount += count;
        }

        _pivots = new Vector2[_letterCount];
        _poseRotation = new float[_letterCount];
        _poseScale = new float[_letterCount];
        _poseY = new float[_letterCount];
        _scaleX = new float[_letterCount];
        _scaleY = new float[_letterCount];
        _cos = new float[_letterCount];
        _sin = new float[_letterCount];
        _offsetY = new float[_letterCount];
        _onTextChanged = HandleTextChanged;
    }

    private static int VisibleLength(string text)
    {
        int n = 0;
        foreach (char c in text) if (!char.IsWhiteSpace(c)) n++;
        return n;
    }

    private static Layer NewLayer(TMP_Text text, LayerKind kind, int word, int first, int count, Color top, Color bottom)
    {
        return new Layer
        {
            text = text,
            kind = kind,
            word = word,
            first = first,
            count = count,
            baseVerts = new Vector3[count * 4],
            meshIndex = new int[count],
            vertexIndex = new int[count],
            top = top,
            bottom = bottom,
        };
    }

    private void OnEnable()
    {
        if (lightning != null)
        {
            lightning.StrikeStarted += HandleStrike;
            lightning.AmbientFlashStarted += HandleAmbientFlash;
        }
        if (_onTextChanged != null) TMPro_EventManager.TEXT_CHANGED_EVENT.Add(_onTextChanged);
    }

    private void OnDisable()
    {
        if (lightning != null)
        {
            lightning.StrikeStarted -= HandleStrike;
            lightning.AmbientFlashStarted -= HandleAmbientFlash;
        }
        if (_onTextChanged != null) TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(_onTextChanged);
    }

    private void Start()
    {
        if (logoImage != null || _layers.Length == 0) return;

        LayOutWords();
        // Principales primero: de ellas salen los pivotes de cada letra.
        foreach (Layer layer in _layers)
        {
            if (layer.kind != LayerKind.Main) continue;
            layer.text.ForceMeshUpdate();
            CacheLayer(layer);
        }
        foreach (Layer layer in _layers)
        {
            if (layer.kind == LayerKind.Main) continue;
            layer.text.ForceMeshUpdate();
            CacheLayer(layer);
        }
        BuildPose();
        _ready = true;
    }

    // Palabras en fila, centradas en el logo y con la línea base común.
    private void LayOutWords()
    {
        float total = 0f;
        int placed = 0;
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].main == null) continue;
            total += words[i].main.GetPreferredValues().x;
            placed++;
        }
        total += wordGap * Mathf.Max(0, placed - 1);

        float x = -total * 0.5f;
        for (int i = 0; i < words.Length; i++)
        {
            Word w = words[i];
            if (w.main == null) continue;
            Vector2 size = w.main.GetPreferredValues();
            var position = new Vector2(x + size.x * 0.5f, baselineY + w.verticalOffset);
            _wordPositions[i] = position;
            Place(w.main, position, size);
            if (w.shine != null) Place(w.shine, position, size);
            if (w.shadow != null) Place(w.shadow, position + shadowOffset, size);
            x += size.x + wordGap;
        }
    }

    private static void Place(TMP_Text text, Vector2 position, Vector2 size)
    {
        RectTransform r = text.rectTransform;
        var center = new Vector2(0.5f, 0.5f);
        r.anchorMin = center;
        r.anchorMax = center;
        r.pivot = center;
        r.anchoredPosition = position;
        r.sizeDelta = new Vector2(size.x + 8f, size.y);
    }

    private void CacheLayer(Layer layer)
    {
        TMP_TextInfo info = layer.text.textInfo;
        layer.valid = false;
        if (info == null) return;

        int n = 0;
        for (int c = 0; c < info.characterCount && n < layer.count; c++)
        {
            TMP_CharacterInfo ch = info.characterInfo[c];
            if (!ch.isVisible) continue;

            Vector3[] vertices = info.meshInfo[ch.materialReferenceIndex].vertices;
            int v = ch.vertexIndex;
            for (int k = 0; k < 4; k++) layer.baseVerts[n * 4 + k] = vertices[v + k];
            layer.meshIndex[n] = ch.materialReferenceIndex;
            layer.vertexIndex[n] = v;
            if (layer.kind == LayerKind.Main)
                _pivots[layer.first + n] = new Vector2((vertices[v].x + vertices[v + 2].x) * 0.5f, ch.baseLine);
            n++;
        }
        layer.valid = n == layer.count;
    }

    private void BuildPose()
    {
        float min = float.MaxValue, max = float.MinValue;
        foreach (Layer layer in _layers)
        {
            if (layer.kind != LayerKind.Main) continue;
            float wordX = _wordPositions[layer.word].x;
            for (int n = 0; n < layer.count; n++)
            {
                float x = _pivots[layer.first + n].x + wordX;
                min = Mathf.Min(min, x);
                max = Mathf.Max(max, x);
            }
        }
        float radians = arcDegrees * Mathf.Deg2Rad;
        float radius = radians > 0.0001f ? Mathf.Max(1f, max - min) / radians : 0f;

        var random = new System.Random(seed);
        foreach (Layer layer in _layers)
        {
            if (layer.kind != LayerKind.Main) continue;
            float wordX = _wordPositions[layer.word].x;
            for (int n = 0; n < layer.count; n++)
            {
                int i = layer.first + n;
                float arcY = 0f, arcRotation = 0f;
                if (radius > 0f)
                {
                    float angle = (_pivots[i].x + wordX) / radius;
                    arcY = radius * (Mathf.Cos(angle) - 1f);
                    arcRotation = -angle * Mathf.Rad2Deg;
                }
                _poseRotation[i] = arcRotation + Signed(random) * letterRotation;
                _poseScale[i] = 1f + Signed(random) * letterScale;
                _poseY[i] = arcY + Signed(random) * letterHeight;
            }
        }
    }

    private static float Signed(System.Random random)
    {
        return (float)(random.NextDouble() * 2.0 - 1.0);
    }

    // ---------- eventos ----------

    private void HandleStrike()
    {
        _jumpStart = Time.unscaledTime;
        _whiteLeft = whiteFrames;
    }

    private void HandleAmbientFlash()
    {
        _tintStart = Time.unscaledTime;
    }

    // TMP regeneró una malla (al activar el panel o cambiar la resolución): se vuelve a tomar la base.
    private void HandleTextChanged(UnityEngine.Object changed)
    {
        if (!_ready) return;
        for (int i = 0; i < _layers.Length; i++)
        {
            Layer layer = _layers[i];
            if (layer.text != changed) continue;
            CacheLayer(layer);
            if (!layer.valid) return;
            WriteVertices(layer);
            WriteColors(layer);
            layer.text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
            return;
        }
    }

    // ---------- cada cuadro ----------

    private void Update()
    {
        float t = Time.unscaledTime;

        float wobble = t * Mathf.PI * 2f / Mathf.Max(0.1f, wobblePeriod);
        float rotation = Mathf.Sin(wobble) * wobbleDegrees * Mathf.Deg2Rad;
        _globalCos = Mathf.Cos(rotation);
        _globalSin = Mathf.Sin(rotation);
        _globalScale = 1f + Mathf.Sin(wobble * 0.73f + 1f) * wobbleScale;

        UpdateColorState(t);

        if (logoImage != null)
        {
            UpdateImage(t, rotation);
            return;
        }
        if (!_ready) return;

        ComputeLetters(t);

        TMP_VertexDataUpdateFlags flags = TMP_VertexDataUpdateFlags.Vertices;
        if (_colorsDirty) flags |= TMP_VertexDataUpdateFlags.Colors32;
        for (int i = 0; i < _layers.Length; i++)
        {
            Layer layer = _layers[i];
            if (!layer.valid) continue;
            WriteVertices(layer);
            if (_colorsDirty) WriteColors(layer);
            layer.text.UpdateVertexData(flags);
        }
        _colorsDirty = false;
    }

    private void UpdateColorState(float t)
    {
        if (_whiteLeft > 0)
        {
            _whiteLeft--;
            _white = true;
            _colorsDirty = true;
        }
        else if (_white)
        {
            _white = false;
            _colorsDirty = true;
        }

        if (_tintStart >= 0f)
        {
            float u = (t - _tintStart) / Mathf.Max(0.01f, coldTintSeconds);
            if (u >= 1f)
            {
                _tintStart = -1f;
                _tint = 0f;
            }
            else
            {
                _tint = Mathf.Sin(u * Mathf.PI);
            }
            _colorsDirty = true;
        }
    }

    private void UpdateImage(float t, float rotation)
    {
        Jump(t, 0, out float sx, out float sy, out _);
        _rect.localRotation = Quaternion.Euler(0f, 0f, rotation * Mathf.Rad2Deg);
        _rect.localScale = new Vector3(_globalScale * sx, _globalScale * sy, 1f);
        if (_colorsDirty)
        {
            logoImage.color = Color.Lerp(Color.white, coldTint, _tint);
            _colorsDirty = false;
        }
    }

    private void ComputeLetters(float t)
    {
        float wave = t * Mathf.PI * 2f / Mathf.Max(0.1f, wavePeriod);
        for (int i = 0; i < _letterCount; i++)
        {
            Jump(t, i, out float jumpX, out float jumpY, out float jumpLift);
            _scaleX[i] = _poseScale[i] * jumpX;
            _scaleY[i] = _poseScale[i] * jumpY;

            float radians = _poseRotation[i] * Mathf.Deg2Rad;
            _cos[i] = Mathf.Cos(radians);
            _sin[i] = Mathf.Sin(radians);
            _offsetY[i] = _poseY[i] + jumpLift + Mathf.Sin(wave + i * wavePhasePerLetter) * wavePixels;
        }
    }

    // Aplastar → estirar subiendo → asentarse con rebote. Fuera del salto: (1, 1, 0).
    private void Jump(float t, int letter, out float scaleX, out float scaleY, out float lift)
    {
        scaleX = 1f;
        scaleY = 1f;
        lift = 0f;
        if (_jumpStart < 0f) return;

        float u = (t - _jumpStart - letter * jumpStagger) / Mathf.Max(0.01f, jumpSeconds);
        if (u <= 0f || u >= 1f) return;

        if (u < squashEnd)
        {
            float k = u / squashEnd;
            scaleX = Mathf.Lerp(1f, squash.x, k);
            scaleY = Mathf.Lerp(1f, squash.y, k);
        }
        else if (u < stretchEnd)
        {
            float k = (u - squashEnd) / Mathf.Max(0.001f, stretchEnd - squashEnd);
            scaleX = Mathf.Lerp(squash.x, stretch.x, k);
            scaleY = Mathf.Lerp(squash.y, stretch.y, k);
            lift = jumpHeight * Mathf.Sin(k * Mathf.PI * 0.5f);
        }
        else
        {
            float k = (u - stretchEnd) / Mathf.Max(0.001f, 1f - stretchEnd);
            float settle = Mathf.Exp(-bounceDamping * k) * Mathf.Cos(k * Mathf.PI * 2f * bounceCycles);
            scaleX = 1f + (stretch.x - 1f) * settle;
            scaleY = 1f + (stretch.y - 1f) * settle;
            lift = jumpHeight * Mathf.Abs(settle);
        }
    }

    private void WriteVertices(Layer layer)
    {
        TMP_TextInfo info = layer.text.textInfo;
        Vector2 word = _wordPositions[layer.word];

        for (int n = 0; n < layer.count; n++)
        {
            int i = layer.first + n;
            Vector2 pivot = _pivots[i];
            float pivotX = pivot.x + word.x;
            float pivotY = pivot.y + word.y + _offsetY[i];
            Vector3[] vertices = info.meshInfo[layer.meshIndex[n]].vertices;
            int v = layer.vertexIndex[n];

            for (int k = 0; k < 4; k++)
            {
                Vector3 source = layer.baseVerts[n * 4 + k];
                float dx = (source.x - pivot.x) * _scaleX[i];
                float dy = (source.y - pivot.y) * _scaleY[i];
                float x = pivotX + dx * _cos[i] - dy * _sin[i];
                float y = pivotY + dx * _sin[i] + dy * _cos[i];

                source.x = (x * _globalCos - y * _globalSin) * _globalScale - word.x;
                source.y = (x * _globalSin + y * _globalCos) * _globalScale - word.y;
                vertices[v + k] = source;
            }
        }
    }

    // Vértices TMP: 0 abajo-izq, 1 arriba-izq, 2 arriba-der, 3 abajo-der.
    private void WriteColors(Layer layer)
    {
        Color32 top = layer.top;
        Color32 bottom = layer.bottom;
        if (layer.kind == LayerKind.Main)
        {
            if (_white)
            {
                top = Color.white;
                bottom = Color.white;
            }
            else if (_tint > 0f)
            {
                Color32 cold = coldTint;
                top = Color32.Lerp(top, cold, _tint);
                bottom = Color32.Lerp(bottom, cold, _tint);
            }
        }

        TMP_TextInfo info = layer.text.textInfo;
        for (int n = 0; n < layer.count; n++)
        {
            Color32[] colors = info.meshInfo[layer.meshIndex[n]].colors32;
            int v = layer.vertexIndex[n];
            colors[v] = bottom;
            colors[v + 1] = top;
            colors[v + 2] = top;
            colors[v + 3] = bottom;
        }
    }
}
