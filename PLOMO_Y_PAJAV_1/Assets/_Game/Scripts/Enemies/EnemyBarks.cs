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
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
public class EnemyBarks : MonoBehaviour
{
    [SerializeField] private string voiceSet = BarkDatabase.DefaultSet;
    [SerializeField] private float bubbleHeight = 2.3f;
    [SerializeField] private float bubbleSeconds = 2f;
    [SerializeField] private Vector2 patrolInterval = new Vector2(15f, 30f);
    [SerializeField] private float patrolRange = 15f;

    private static readonly Color Cream = new Color32(0xF2, 0xE6, 0xC8, 0xFF);

    private EnemyHealth _health;
    private EnemyAI _ai;
    private GameObject _bubble;
    private TextMeshProUGUI _label;
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
        _bubble.transform.position = transform.position + Vector3.up * bubbleHeight;
        Vector3 away = _bubble.transform.position - cam.transform.position;
        if (away.sqrMagnitude > 0.0001f) _bubble.transform.rotation = Quaternion.LookRotation(away);
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
        _bubbleUntil = Time.time + bubbleSeconds;
        _bubble.SetActive(true);
    }

    private void BuildBubble()
    {
        // Cuelga de la raíz (no del Visual) para no girar ni encogerse con la muerte
        _bubble = new GameObject("Bark_Bubble", typeof(RectTransform));
        _bubble.transform.SetParent(transform, false);
        _bubble.transform.localScale = Vector3.one * 0.01f;
        Canvas canvas = _bubble.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform rect = (RectTransform)_bubble.transform;
        rect.sizeDelta = new Vector2(360f, 110f);

        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(_bubble.transform, false);
        RectTransform pr = (RectTransform)panel.transform;
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = pr.offsetMax = Vector2.zero;
        Image img = panel.AddComponent<Image>();
        img.color = Cream;
        img.raycastTarget = false;
        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(4f, -4f);

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(panel.transform, false);
        RectTransform tr = (RectTransform)textGo.transform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(14f, 8f); tr.offsetMax = new Vector2(-14f, -8f);
        _label = textGo.AddComponent<TextMeshProUGUI>();
        _label.color = Color.black;
        _label.fontStyle = FontStyles.Bold;
        _label.alignment = TextAlignmentOptions.Center;
        _label.enableWordWrapping = true;
        _label.enableAutoSizing = true;
        _label.fontSizeMin = 20f;
        _label.fontSizeMax = 34f;
        _label.raycastTarget = false;

        _bubble.SetActive(false);
    }
}
