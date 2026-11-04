using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Efectos de disparo compartidos y en pool (fase HUD.2, H11): nada de Instantiate por disparo.
/// Se crea solo la primera vez que alguien lo usa (uno por escena; se destruye con la escena).
///
/// POOLS (tamaños fijos, todo creado al empezar):
/// - Fogonazos 8 (SpriteRenderer billboard, 0.05 s) + luces 4 (punto cálido, 0.06 s).
/// - Estelas 8 (LineRenderer muzzle → impacto, 0.06 s).
/// - Polvo de impacto 16 (billboard, 0.35 s; 2 dibujos si existen fx_impacto_01/02).
/// - Agujeros de bala 32 (calcomanía pegada a la pared; se desvanece a los 10 s; el más viejo se reusa).
/// - Fuentes de sonido 3D 6 (disparos con pitch propio; las demás van por AudioLibrary).
/// DIBUJOS: Configure(fogonazo, polvo, agujero) los registra (lo hace EnemyGunfire con lo que le asignó el menú).
/// Sin dibujos: fogonazo y polvo = círculos por código, agujero = disco oscuro por código.
/// Tiempo: los efectos usan tiempo de juego (el hit-stop los congela un instante, como al resto del mundo).
/// </summary>
public class CombatFx : MonoBehaviour
{
    private const int FlashCount = 8, LightCount = 4, TracerCount = 8, PuffCount = 16, HoleCount = 32, AudioCount = 6;
    private const float FlashSeconds = 0.05f, LightSeconds = 0.06f, TracerSeconds = 0.06f, PuffSeconds = 0.35f;
    private const float HoleSeconds = 10f, HoleFade = 1.5f;

    private static CombatFx _instance;
    private static Sprite _flashSprite, _puffSprite, _puffSprite2, _holeSprite;
    private static Sprite _fallbackDot, _fallbackHole;

    private SpriteRenderer[] _flashes;
    private float[] _flashUntil;
    private Light[] _lights;
    private float[] _lightUntil;
    private LineRenderer[] _tracers;
    private float[] _tracerUntil;
    private SpriteRenderer[] _puffs;
    private float[] _puffStart;
    private SpriteRenderer[] _holes;
    private float[] _holeStart;
    private AudioSource[] _sources;
    private int _nextFlash, _nextLight, _nextTracer, _nextPuff, _nextHole, _nextSource;
    private Camera _camera;

    /// <summary>Registra los dibujos (null = se queda el que había o el de código).</summary>
    public static void Configure(Sprite flash, Sprite puff, Sprite puff2, Sprite hole)
    {
        if (flash != null) _flashSprite = flash;
        if (puff != null) _puffSprite = puff;
        if (puff2 != null) _puffSprite2 = puff2;
        if (hole != null) _holeSprite = hole;
    }

    private static CombatFx Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("CombatFx (pool)");
                _instance = go.AddComponent<CombatFx>();
                _instance.Build();
            }
            return _instance;
        }
    }

    // ============ API ============

    /// <summary>Fogonazo en la boca del cañón (dibujo + luz).</summary>
    public static void MuzzleFlash(Vector3 position, float size = 0.45f, Color tint = default)
    {
        CombatFx fx = Instance;
        SpriteRenderer f = fx._flashes[fx._nextFlash];
        fx._flashUntil[fx._nextFlash] = Time.time + FlashSeconds;
        fx._nextFlash = (fx._nextFlash + 1) % FlashCount;
        f.sprite = _flashSprite != null ? _flashSprite : Fallback();
        f.color = tint == default ? new Color(1f, 0.93f, 0.75f, 1f) : tint;
        f.transform.position = position;
        f.transform.localScale = Vector3.one * (size / Mathf.Max(0.01f, f.sprite.bounds.size.x)) * Random.Range(0.85f, 1.15f);
        fx.FaceCamera(f.transform, Random.Range(0f, 360f));
        f.enabled = true;

        Light l = fx._lights[fx._nextLight];
        fx._lightUntil[fx._nextLight] = Time.time + LightSeconds;
        fx._nextLight = (fx._nextLight + 1) % LightCount;
        l.transform.position = position;
        l.enabled = true;
    }

    /// <summary>Estela de la bala del cañón al punto final.</summary>
    public static void Tracer(Vector3 from, Vector3 to)
    {
        CombatFx fx = Instance;
        LineRenderer t = fx._tracers[fx._nextTracer];
        fx._tracerUntil[fx._nextTracer] = Time.time + TracerSeconds;
        fx._nextTracer = (fx._nextTracer + 1) % TracerCount;
        t.SetPosition(0, from);
        t.SetPosition(1, to);
        t.enabled = true;
    }

    /// <summary>Bala que pega en el mundo: polvo + agujero pegado a la superficie + sonido de impacto.</summary>
    public static void Impact(Vector3 point, Vector3 normal, bool hole = true)
    {
        CombatFx fx = Instance;
        SpriteRenderer p = fx._puffs[fx._nextPuff];
        fx._puffStart[fx._nextPuff] = Time.time;
        fx._nextPuff = (fx._nextPuff + 1) % PuffCount;
        Sprite puff = _puffSprite != null ? _puffSprite : Fallback();
        if (_puffSprite2 != null && Random.value < 0.5f) puff = _puffSprite2;
        p.sprite = puff;
        p.color = _puffSprite != null ? Color.white : new Color(0.62f, 0.55f, 0.48f, 0.9f);
        p.transform.position = point + normal * 0.05f;
        p.transform.localScale = Vector3.one * (0.35f / Mathf.Max(0.01f, puff.bounds.size.x));
        fx.FaceCamera(p.transform, Random.Range(0f, 360f));
        p.enabled = true;

        if (hole)
        {
            SpriteRenderer h = fx._holes[fx._nextHole];
            fx._holeStart[fx._nextHole] = Time.time;
            fx._nextHole = (fx._nextHole + 1) % HoleCount;
            Sprite s = _holeSprite != null ? _holeSprite : FallbackHole();
            h.sprite = s;
            h.color = Color.white;
            h.transform.SetPositionAndRotation(point + normal * 0.01f,
                Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            h.transform.localScale = Vector3.one * (0.09f / Mathf.Max(0.01f, s.bounds.size.x));
            h.enabled = true;
        }
        AudioLibrary.PlaySfx("sfx_impacto", point); // sin clip: no suena (lo avisa la librería una vez)
    }

    /// <summary>Sonido 3D de disparo con pitch y volumen propios (fuente del pool).</summary>
    public static void PlayAt(AudioClip clip, Vector3 position, float pitch, float volume)
    {
        if (clip == null) return;
        CombatFx fx = Instance;
        AudioSource s = fx._sources[fx._nextSource];
        fx._nextSource = (fx._nextSource + 1) % AudioCount;
        s.transform.position = position;
        s.pitch = pitch;
        s.PlayOneShot(clip, volume);
    }

    // ============ Armado ============

    private void Build()
    {
        _flashes = new SpriteRenderer[FlashCount];
        _flashUntil = new float[FlashCount];
        for (int i = 0; i < FlashCount; i++) _flashes[i] = MakeSprite("Fogonazo_" + i, 50);
        _lights = new Light[LightCount];
        _lightUntil = new float[LightCount];
        for (int i = 0; i < LightCount; i++)
        {
            var go = new GameObject("LuzDisparo_" + i);
            go.transform.SetParent(transform, false);
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.78f, 0.4f);
            l.range = 3.5f;
            l.intensity = 3.5f;
            l.shadows = LightShadows.None;
            l.enabled = false;
            _lights[i] = l;
        }
        _tracers = new LineRenderer[TracerCount];
        _tracerUntil = new float[TracerCount];
        Material lineMat = LineMaterial();
        for (int i = 0; i < TracerCount; i++)
        {
            var go = new GameObject("Estela_" + i);
            go.transform.SetParent(transform, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = 0.035f;
            lr.endWidth = 0.012f;
            lr.startColor = new Color(1f, 0.95f, 0.7f, 0.95f);
            lr.endColor = new Color(1f, 0.7f, 0.3f, 0.6f);
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            if (lineMat != null) lr.sharedMaterial = lineMat;
            lr.enabled = false;
            _tracers[i] = lr;
        }
        _puffs = new SpriteRenderer[PuffCount];
        _puffStart = new float[PuffCount];
        for (int i = 0; i < PuffCount; i++) _puffs[i] = MakeSprite("Polvo_" + i, 40);
        _holes = new SpriteRenderer[HoleCount];
        _holeStart = new float[HoleCount];
        for (int i = 0; i < HoleCount; i++)
        {
            _holes[i] = MakeSprite("Agujero_" + i, 0);
            _holeStart[i] = -999f;
        }
        _sources = new AudioSource[AudioCount];
        for (int i = 0; i < AudioCount; i++)
        {
            var go = new GameObject("SonidoDisparo_" + i);
            go.transform.SetParent(transform, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 2f;
            s.maxDistance = 35f;
            _sources[i] = s;
        }
    }

    private SpriteRenderer MakeSprite(string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = order;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sr.receiveShadows = false;
        CharacterLook.ApplySprite(sr); // plano, sin iluminación, como los personajes
        sr.enabled = false;
        return sr;
    }

    private static Material LineMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        return shader != null ? new Material(shader) { name = "EstelaBala" } : null;
    }

    private void FaceCamera(Transform t, float roll)
    {
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;
        t.rotation = Quaternion.LookRotation(_camera.transform.forward) * Quaternion.Euler(0f, 0f, roll);
    }

    private void Update()
    {
        float now = Time.time;
        for (int i = 0; i < FlashCount; i++)
            if (_flashes[i].enabled && now >= _flashUntil[i]) _flashes[i].enabled = false;
        for (int i = 0; i < LightCount; i++)
            if (_lights[i].enabled && now >= _lightUntil[i]) _lights[i].enabled = false;
        for (int i = 0; i < TracerCount; i++)
            if (_tracers[i].enabled && now >= _tracerUntil[i]) _tracers[i].enabled = false;
        for (int i = 0; i < PuffCount; i++)
        {
            if (!_puffs[i].enabled) continue;
            float t = (now - _puffStart[i]) / PuffSeconds;
            if (t >= 1f) { _puffs[i].enabled = false; continue; }
            Color c = _puffs[i].color;
            c.a = 1f - t * t;
            _puffs[i].color = c;
            _puffs[i].transform.position += Vector3.up * (Time.deltaTime * 0.3f);
        }
        for (int i = 0; i < HoleCount; i++)
        {
            if (!_holes[i].enabled) continue;
            float age = now - _holeStart[i];
            if (age >= HoleSeconds) { _holes[i].enabled = false; continue; }
            if (age > HoleSeconds - HoleFade)
            {
                Color c = _holes[i].color;
                c.a = (HoleSeconds - age) / HoleFade;
                _holes[i].color = c;
            }
        }
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) _instance = null; // el objeto viejo se fue con la escena
    }

    // ============ Dibujos de respaldo (por código, una sola vez) ============

    private static Sprite Fallback()
    {
        if (_fallbackDot == null) _fallbackDot = Disc("FxPuntoCodigo", 32, new Color32(255, 255, 255, 255), 0.5f);
        return _fallbackDot;
    }

    private static Sprite FallbackHole()
    {
        if (_fallbackHole == null) _fallbackHole = Disc("FxAgujeroCodigo", 32, new Color32(18, 14, 12, 255), 0.25f);
        return _fallbackHole;
    }

    private static Sprite Disc(string name, int size, Color32 color, float softness)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r; // 0 centro, 1 borde
                float a = Mathf.Clamp01((1f - d) / Mathf.Max(0.01f, softness));
                px[y * size + x] = new Color32(color.r, color.g, color.b, (byte)Mathf.RoundToInt(a * color.a));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
