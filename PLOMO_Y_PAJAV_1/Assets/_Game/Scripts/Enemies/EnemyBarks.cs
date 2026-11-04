using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Frases de los enemigos: globo de diálogo sobre la cabeza + voz (AudioLibrary.PlayVoice).
/// DÓNDE VA: raíz del enemigo, junto a EnemyHealth y EnemyAI (lo añade "Crear Prefabs de Prueba").
/// Sin audio grabado solo se ve el globo. Un solo globo por enemigo a la vez.
///
/// DISCIPLINA DE VOZ (3A7): todas las voces de enemigos pasan por AudioLibrary.TryPlayEnemyVoice (un solo canal,
/// prioridad muerte > rendición > daño > ver > patrulla, enfriamiento global). Probabilidades y tiempos en
/// AudioLibrary → Mix. Al morir el sonido NO verbal lo pone EnemyCombatFx siempre (caida_silbato + golpe_cartoon);
/// aquí solo se decide si además dice una frase. El globo sale solo si la frase se aceptó.
///
/// GLOBO (estilo cartoon): Canvas en el mundo a 1 unidad = 1 cm. Ancho máximo maxWidth (m), alto ajustado al texto.
/// Panel crema con esquinas redondeadas y borde negro + colita que apunta al guardia (texturas hechas por código, una
/// sola vez para todos). Texto en Luckiest Guy (font, lo asigna el menú; vacío = fuente por defecto de TMP) con contorno
/// negro grueso. Entra con rebote de escala y sale desvaneciéndose.
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
public class EnemyBarks : MonoBehaviour
{
    [SerializeField] private string voiceSet = BarkDatabase.DefaultSet;
    [Tooltip("Altura de la punta de la colita sobre los pies (m)")]
    [SerializeField] private float bubbleHeight = 2f;
    [SerializeField] private float bubbleSeconds = 2f;
    [SerializeField] private Vector2 patrolInterval = new Vector2(15f, 30f);
    [SerializeField] private float patrolRange = 15f;

    [Header("Globo (estilo cartoon)")]
    [Tooltip("Luckiest Guy SDF (Fonts/LuckiestGuy SDF). Vacío = fuente por defecto de TMP")]
    [SerializeField] private TMP_FontAsset font;
    [Tooltip("Ancho máximo del globo (m); el alto se ajusta al texto")]
    [SerializeField] private float maxWidth = 1.2f;
    [Tooltip("Tamaño de letra en cm")]
    [SerializeField] private float fontSize = 13f;
    [SerializeField] private Color textColor = Color.black;
    [Tooltip("Grosor del contorno negro del texto (unidades SDF de TMP, 0..0.5)")]
    [SerializeField, Range(0f, 0.5f)] private float textOutline = 0.28f;
    [Tooltip("Segundos del rebote de entrada")]
    [SerializeField] private float popSeconds = 0.25f;
    [Tooltip("Segundos del desvanecido de salida (dentro de bubbleSeconds)")]
    [SerializeField] private float fadeSeconds = 0.3f;

    private static readonly Color Cream = new Color32(0xF2, 0xE6, 0xC8, 0xFF);
    private const float UnitsPerMeter = 100f;  // escala del Canvas 0.01: 1 unidad = 1 cm
    private const float PadX = 7f, PadY = 4f;  // margen del texto dentro del panel (cm)
    private const float MinPanelWidth = 30f;
    private const float TailWidth = 14f, TailHeight = 11f;
    private const float PanelBorder = 5f / PanelSliceMultiplier; // grosor del borde del panel en cm (5 px del sprite)
    private const float PanelSliceMultiplier = 3f;               // 3 px del sprite = 1 cm

    private static Sprite _panelSprite;
    private static Sprite _tailSprite;
    private static Material _outlineMaterial;
    private static TMP_FontAsset _outlineMaterialFont;
    private static float _outlineMaterialWidth;

    private EnemyHealth _health;
    private EnemyAI _ai;
    private GameObject _bubble;
    private RectTransform _bubbleRect;
    private RectTransform _panelRect;
    private CanvasGroup _group;
    private TextMeshProUGUI _label;
    private float _bubbleShownAt;
    private float _bubbleUntil;
    private float _nextHurtAttempt;
    private static float _lastDeathPhrase = -999f;
    private float _nextPatrolBark;
    private bool _seenPlayer;
    private bool _dead;

    private void Awake()
    {
        _health = GetComponent<EnemyHealth>();
        TryGetComponent(out _ai);
        _nextPatrolBark = Time.time + Random.Range(patrolInterval.x, patrolInterval.y);
    }

    private void OnEnable()
    {
        if (_health.onDamageTaken != null) _health.onDamageTaken.AddListener(OnDamage);
        _health.OnSurrender += OnSurrender;
        _health.OnDeath += OnDeath;
    }

    private void OnDisable()
    {
        if (_health.onDamageTaken != null) _health.onDamageTaken.RemoveListener(OnDamage);
        _health.OnSurrender -= OnSurrender;
        _health.OnDeath -= OnDeath;
    }

    private void Update()
    {
        if (_bubble != null && _bubble.activeSelf && Time.time >= _bubbleUntil) _bubble.SetActive(false);
        if (_dead || _ai == null) return;

        EnemyAI.EnemyState state = _ai.CurrentState;
        if (!_seenPlayer && (state == EnemyAI.EnemyState.Alert || state == EnemyAI.EnemyState.Chase
                             || state == EnemyAI.EnemyState.Attack))
        {
            _seenPlayer = true;                                  // una sola vez por episodio de alerta (con o sin suerte)
            if (Roll(AudioLibrary.Mix.spottedPhraseChance)) Say("ver", AudioLibrary.PrioritySpotted);
        }
        else if (_seenPlayer && (state == EnemyAI.EnemyState.Patrol || state == EnemyAI.EnemyState.Idle))
        {
            _seenPlayer = false;                                 // volvió a la calma: puede avisar en la próxima alerta
        }

        if (state == EnemyAI.EnemyState.Patrol && Time.time >= _nextPatrolBark)
        {
            _nextPatrolBark = Time.time + Random.Range(patrolInterval.x, patrolInterval.y);
            Camera cam = Camera.main;
            if (cam != null && (cam.transform.position - transform.position).sqrMagnitude <= patrolRange * patrolRange
                && AudioLibrary.SecondsSinceEnemyPhrase >= AudioLibrary.Mix.patrolQuietSeconds
                && Roll(AudioLibrary.Mix.patrolPhraseChance))
                Say("patrulla", AudioLibrary.PriorityPatrol);
        }
    }

    private void LateUpdate()
    {
        if (_bubble == null || !_bubble.activeSelf) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        _bubbleRect.position = transform.position + Vector3.up * bubbleHeight;
        Vector3 away = _bubbleRect.position - cam.transform.position;
        if (away.sqrMagnitude > 0.0001f) _bubbleRect.rotation = Quaternion.LookRotation(away);

        // Entrada: rebote de escala (0 → ~1.17 → 1). Salida: desvanecido en los últimos fadeSeconds
        float pop = popSeconds > 0f ? BackOut((Time.time - _bubbleShownAt) / popSeconds) : 1f;
        _bubbleRect.localScale = Vector3.one * (pop / UnitsPerMeter);
        _group.alpha = fadeSeconds > 0f ? Mathf.Clamp01((_bubbleUntil - Time.time) / fadeSeconds) : 1f;
    }

    /// <summary>Curva con rebote: 0 en t=0, se pasa un poco de 1 y vuelve a 1 en t=1.</summary>
    private static float BackOut(float t)
    {
        t = Mathf.Clamp01(t) - 1f;
        const float c1 = 2.2f;
        const float c3 = c1 + 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }

    private void OnDamage()
    {
        // Los 8 perdigones llaman a la vez: el enfriamiento se gasta en el intento, haya suerte o no
        if (_dead || Time.time < _nextHurtAttempt) return;
        if (_health.HealthPercentage <= 0f) return; // el golpe mortal no hace quejido: habla (o no) la muerte
        _nextHurtAttempt = Time.time + AudioLibrary.Mix.hurtPhraseCooldown;
        if (Roll(AudioLibrary.Mix.hurtPhraseChance)) Say("dano", AudioLibrary.PriorityHurt);
    }

    private void OnSurrender()
    {
        if (!_dead && Roll(AudioLibrary.Mix.surrenderPhraseChance)) Say("rendir", AudioLibrary.PrioritySurrender);
    }

    private void OnDeath()
    {
        if (_dead) return;
        _dead = true;
        AudioLibrary.CutEnemyVoiceBelow(AudioLibrary.PriorityDeath);   // ningún quejido pendiente suena encima
        if (_bubble != null) _bubble.SetActive(false);                 // y su globo de daño desaparece

        AudioLibrary.MixSettings mix = AudioLibrary.Mix;
        if (Time.time - _lastDeathPhrase >= mix.deathPhraseCooldown && Roll(mix.deathPhraseChance))
        {
            _lastDeathPhrase = Time.time;
            Say("morir", AudioLibrary.PriorityDeath);
        }
    }

    private static bool Roll(float chance)
    {
        return chance >= 1f || Random.value < chance;
    }

    private void Say(string evt, int priority)
    {
        if (!BarkDatabase.GetRandom(voiceSet, evt, out string lineId, out string text)) return;
        if (!AudioLibrary.TryPlayEnemyVoice(lineId, transform.position + Vector3.up * 1.6f, priority)) return;
        ShowBubble(text);
    }

    private void ShowBubble(string text)
    {
        if (_bubble == null) BuildBubble();
        _label.text = text;

        // Ancho = el del texto (tope maxWidth), alto = el que pide el texto con ese ancho
        float maxText = Mathf.Max(10f, maxWidth * UnitsPerMeter - PadX * 2f);
        Vector2 preferred = _label.GetPreferredValues(text, maxText, 0f);
        float width = Mathf.Max(MinPanelWidth, Mathf.Min(preferred.x, maxText) + PadX * 2f);
        float height = preferred.y + PadY * 2f;
        _panelRect.sizeDelta = new Vector2(width, height);
        _bubbleRect.sizeDelta = new Vector2(width, height + TailHeight - PanelBorder);

        _bubbleShownAt = Time.time;
        _bubbleUntil = Time.time + bubbleSeconds;
        _bubbleRect.localScale = Vector3.zero;                         // el rebote arranca desde 0 en LateUpdate
        _group.alpha = 1f;
        _bubble.SetActive(true);
    }

    private void BuildBubble()
    {
        // Cuelga de la raíz (no del Visual) para no girar ni encogerse con la muerte.
        // Pivote abajo al centro = punta de la colita: queda justo en bubbleHeight.
        _bubble = new GameObject("Bark_Bubble", typeof(RectTransform));
        _bubble.transform.SetParent(transform, false);
        _bubbleRect = (RectTransform)_bubble.transform;
        _bubbleRect.pivot = new Vector2(0.5f, 0f);
        _bubbleRect.localScale = Vector3.one / UnitsPerMeter;
        Canvas canvas = _bubble.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        _group = _bubble.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable = false;

        // Panel: esquinas redondeadas con borde negro (9-slice), apoyado sobre la colita
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(_bubble.transform, false);
        _panelRect = (RectTransform)panel.transform;
        _panelRect.anchorMin = _panelRect.anchorMax = new Vector2(0.5f, 0f);
        _panelRect.pivot = new Vector2(0.5f, 0f);
        _panelRect.anchoredPosition = new Vector2(0f, TailHeight - PanelBorder);
        Image img = panel.AddComponent<Image>();
        img.sprite = PanelSprite();
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = PanelSliceMultiplier;
        img.color = Color.white;
        img.raycastTarget = false;

        // Colita: después del panel para que su crema tape el borde de abajo del panel en ese tramo
        var tail = new GameObject("Tail", typeof(RectTransform));
        tail.transform.SetParent(_bubble.transform, false);
        RectTransform tailRect = (RectTransform)tail.transform;
        tailRect.anchorMin = tailRect.anchorMax = new Vector2(0.5f, 0f);
        tailRect.pivot = new Vector2(0.5f, 0f);
        tailRect.anchoredPosition = Vector2.zero;
        tailRect.sizeDelta = new Vector2(TailWidth, TailHeight);
        Image tailImg = tail.AddComponent<Image>();
        tailImg.sprite = TailSprite();
        tailImg.color = Color.white;
        tailImg.raycastTarget = false;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(panel.transform, false);
        RectTransform tr = (RectTransform)textGo.transform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(PadX, PadY); tr.offsetMax = new Vector2(-PadX, -PadY);
        _label = textGo.AddComponent<TextMeshProUGUI>();
        if (font != null) _label.font = font;
        Material outlined = OutlineMaterial(_label.font, textOutline);
        if (outlined != null) _label.fontSharedMaterial = outlined;
        _label.fontSize = fontSize;
        _label.enableAutoSizing = false;
        _label.color = textColor;
        _label.alignment = TextAlignmentOptions.Center;
        _label.enableWordWrapping = true;
        _label.raycastTarget = false;

        _bubble.SetActive(false);
    }

    // ===================== Texturas y material compartidos (una vez para todos los guardias) =====================

    /// <summary>Material del texto con contorno negro grueso (compartido por fuente; sin instancias por guardia).</summary>
    private static Material OutlineMaterial(TMP_FontAsset fontAsset, float width)
    {
        if (fontAsset == null || fontAsset.material == null) return null;
        if (_outlineMaterial != null && _outlineMaterialFont == fontAsset && Mathf.Approximately(_outlineMaterialWidth, width))
            return _outlineMaterial;
        var mat = new Material(fontAsset.material) { name = fontAsset.name + " Contorno (globo)" };
        mat.hideFlags = HideFlags.DontSave | HideFlags.DontUnloadUnusedAsset;
        mat.EnableKeyword("OUTLINE_ON");
        if (mat.HasProperty("_OutlineColor")) mat.SetColor("_OutlineColor", Color.black);
        if (mat.HasProperty("_OutlineWidth")) mat.SetFloat("_OutlineWidth", width);
        if (mat.HasProperty("_FaceDilate")) mat.SetFloat("_FaceDilate", width * 0.5f); // el contorno no se come la letra
        _outlineMaterial = mat;
        _outlineMaterialFont = fontAsset;
        _outlineMaterialWidth = width;
        return mat;
    }

    /// <summary>Rectángulo redondeado 64×64: crema con borde negro de 5 px, radio 20 px, 9-slice de 24 px.</summary>
    private static Sprite PanelSprite()
    {
        if (_panelSprite != null) return _panelSprite;
        const int size = 64;
        const float radius = 20f, border = 5f;
        Texture2D tex = NewTexture(size, size, "Bark_Panel");
        var pixels = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // Distancia con signo al rectángulo redondeado (negativa dentro)
            float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
            float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
            pixels[y * size + x] = Shade(d, border);
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        _panelSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(24f, 24f, 24f, 24f));
        _panelSprite.name = "Bark_Panel";
        _panelSprite.hideFlags = HideFlags.DontSave | HideFlags.DontUnloadUnusedAsset;
        return _panelSprite;
    }

    /// <summary>Colita 28×22 apuntando abajo: crema con borde negro en los lados (arriba abierta: se une al panel).</summary>
    private static Sprite TailSprite()
    {
        if (_tailSprite != null) return _tailSprite;
        const int w = 28, h = 22;
        // Mismo grosor visual que el borde del panel: el sprite se dibuja a 2 px por cm
        float border = PanelBorder * (w / TailWidth);
        Texture2D tex = NewTexture(w, h, "Bark_Tail");
        var pixels = new Color32[w * h];
        float halfW = w * 0.5f;
        float slope = halfW / h;                                    // medio ancho por fila
        float norm = Mathf.Sqrt(1f + slope * slope);                // pasa la distancia horizontal a perpendicular
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float py = y + 0.5f;
            float dx = Mathf.Abs(x + 0.5f - halfW);
            float d = (dx - py * slope) / norm;                     // < 0 dentro del triángulo (punta abajo)
            pixels[y * w + x] = Shade(d, border);
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        _tailSprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0f), 100f, 0, SpriteMeshType.FullRect);
        _tailSprite.name = "Bark_Tail";
        _tailSprite.hideFlags = HideFlags.DontSave | HideFlags.DontUnloadUnusedAsset;
        return _tailSprite;
    }

    private static Texture2D NewTexture(int w, int h, string name)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave | HideFlags.DontUnloadUnusedAsset,
        };
        return tex;
    }

    /// <summary>Color de un píxel según su distancia con signo d al borde: negro en la franja del borde, crema dentro,
    /// transparente fuera; medio píxel de suavizado en cada orilla.</summary>
    private static Color32 Shade(float d, float border)
    {
        float alpha = Mathf.Clamp01(0.5f - d);
        float inner = Mathf.Clamp01(0.5f - (d + border));
        Color c = Color.Lerp(Color.black, Cream, inner);
        c.a = alpha;
        return c;
    }
}
