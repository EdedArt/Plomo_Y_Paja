using System.Collections;
using UnityEngine;

/// <summary>
/// Efectos de violencia caricaturesca hechos por código (sin prefabs de partículas):
/// gotas de sangre con gravedad, manchas en el piso, humo, estrellitas, dientes y hit-stop.
/// DÓNDE VA: raíz del Player (lo añade "Crear Prefabs de Prueba" con sus materiales).
///
/// Todo está en pools de tamaño fijo: disparar mucho nunca crea objetos sin límite.
/// - Partículas: anillo de MaxParticles, se dibujan con Graphics.DrawMesh (sin GameObjects)
/// - Manchas: MaxDecals quads reutilizados; se desvanecen a los 30 s
/// Si falta un material, ese efecto simplemente no se dibuja.
/// </summary>
public class CartoonFx : MonoBehaviour
{
    public static CartoonFx Instance { get; private set; }

    public static readonly Color BloodColor = new Color32(0xB3, 0x00, 0x1B, 0xFF);

    [Header("Materiales (los crea el generador de prefabs)")]
    [SerializeField] private Material bloodMaterial;
    [SerializeField] private Material decalMaterial;   // transparente, para poder desvanecer
    [SerializeField] private Material smokeMaterial;
    [SerializeField] private Material starMaterial;
    [SerializeField] private Material toothMaterial;
    [SerializeField] private Material flashMeshMaterial;
    [SerializeField] private Material flashSpriteMaterial;

    [Header("Manchas")]
    [SerializeField] private float decalLifetime = 30f;
    [SerializeField] private float decalFadeTime = 2f;

    private const int MaxParticles = 384;
    private const int MaxDecals = 40;
    private const float Gravity = -14f;

    private enum Kind : byte { None, Blood, Tooth, Smoke, Star }

    private struct Particle
    {
        public Kind kind;
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 center;      // estrellas: centro de la órbita
        public Quaternion rotation;
        public Vector3 spinAxis;
        public float spinSpeed;
        public float size;
        public float age;
        public float life;
        public float groundY;
        public bool decalOnLand;
        public bool resting;
    }

    private readonly Particle[] _particles = new Particle[MaxParticles];
    private int _nextParticle;

    private class Decal
    {
        public Transform transform;
        public MeshRenderer renderer;
        public float spawnTime;
        public float baseAlpha;
    }

    private readonly Decal[] _decals = new Decal[MaxDecals];
    private int _nextDecal;
    private Transform _decalRoot;
    private MaterialPropertyBlock _mpb;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Mesh _sphere, _cube, _quad;

    private Coroutine _hitStop;
    private float _hitStopUntil;

    public Material FlashMeshMaterial => flashMeshMaterial;
    public Material FlashSpriteMaterial => flashSpriteMaterial;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        _sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        _cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        _quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        _mpb = new MaterialPropertyBlock();
    }

    private void OnDisable()
    {
        // El hit-stop nunca puede quedarse congelado
        if (_hitStop != null)
        {
            StopCoroutine(_hitStop);
            _hitStop = null;
            RestoreTimeScale();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ==================================================================
    // API estática (no hace nada si no hay CartoonFx en la escena)
    // ==================================================================

    /// <summary>Chorro de 20–30 gotas gordas hacia "direction" que manchan el piso al caer.</summary>
    public static void BloodBurst(Vector3 position, Vector3 direction, int count = -1)
    {
        if (Instance == null) return;
        Instance.SpawnBlood(position, direction, count < 0 ? Random.Range(20, 31) : count);
    }

    /// <summary>Humo, 3 estrellitas girando sobre la cabeza, 2–4 dientes y un charco.</summary>
    public static void DeathBurst(Vector3 headPosition, Vector3 groundPosition)
    {
        if (Instance == null) return;
        Instance.SpawnDeath(headPosition, groundPosition);
    }

    /// <summary>Congela el juego unos milisegundos (tiempo real). Siempre restaura la escala de tiempo.</summary>
    public static void HitStop(float seconds)
    {
        if (Instance == null || !Instance.isActiveAndEnabled) return;
        Instance.StartHitStop(seconds);
    }

    // ==================================================================
    // Partículas
    // ==================================================================

    private ref Particle NextParticle()
    {
        ref Particle p = ref _particles[_nextParticle];
        _nextParticle = (_nextParticle + 1) % MaxParticles; // si se llena, se pisa la más vieja
        p = default;
        return ref p;
    }

    private static float FindGround(Vector3 from, float fallback)
    {
        return Physics.Raycast(from + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 5f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            ? hit.point.y
            : fallback;
    }

    private void SpawnBlood(Vector3 position, Vector3 direction, int count)
    {
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        float ground = FindGround(position, position.y - 1.2f);

        for (int i = 0; i < count; i++)
        {
            ref Particle p = ref NextParticle();
            p.kind = Kind.Blood;
            p.position = position + Random.insideUnitSphere * 0.15f;
            p.velocity = direction * Random.Range(1.5f, 3.5f) + Vector3.up * Random.Range(1f, 3.2f)
                         + Random.insideUnitSphere * 1.2f;
            p.size = Random.Range(0.07f, 0.13f);
            p.life = 3f;
            p.groundY = ground;
            p.decalOnLand = i % 4 == 0; // una de cada cuatro deja mancha
            p.rotation = Quaternion.identity;
        }
    }

    private void SpawnDeath(Vector3 head, Vector3 groundPos)
    {
        float ground = FindGround(groundPos + Vector3.up, groundPos.y);

        // Humo: bolas que crecen y suben
        for (int i = 0; i < 7; i++)
        {
            ref Particle p = ref NextParticle();
            p.kind = Kind.Smoke;
            p.position = groundPos + Vector3.up * 0.4f + Random.insideUnitSphere * 0.5f;
            p.velocity = Vector3.up * Random.Range(0.4f, 0.9f) + Random.insideUnitSphere * 0.4f;
            p.size = Random.Range(0.35f, 0.55f);
            p.life = Random.Range(0.55f, 0.8f);
            p.rotation = Quaternion.identity;
        }

        // Tres estrellitas en órbita sobre la cabeza
        for (int i = 0; i < 3; i++)
        {
            ref Particle p = ref NextParticle();
            p.kind = Kind.Star;
            p.center = head + Vector3.up * 0.35f;
            p.position = p.center;
            p.spinSpeed = i * (Mathf.PI * 2f / 3f); // desfase inicial
            p.size = 0.13f;
            p.life = 1.8f;
            p.rotation = Quaternion.identity;
        }

        // Dientes volando
        int teeth = Random.Range(2, 5);
        for (int i = 0; i < teeth; i++)
        {
            ref Particle p = ref NextParticle();
            p.kind = Kind.Tooth;
            p.position = head;
            p.velocity = Random.insideUnitSphere * 2.5f + Vector3.up * Random.Range(2.5f, 4f);
            p.size = 0.07f;
            p.life = 2f;
            p.groundY = ground;
            p.rotation = Random.rotation;
            p.spinAxis = Random.onUnitSphere;
            p.spinSpeed = Random.Range(400f, 900f);
        }

        SpawnDecal(new Vector3(groundPos.x, ground, groundPos.z), Random.Range(1.1f, 1.5f), 0.95f);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return; // pausa / hit-stop: todo queda quieto

        for (int i = 0; i < MaxParticles; i++)
        {
            ref Particle p = ref _particles[i];
            if (p.kind == Kind.None) continue;

            p.age += dt;
            if (p.age >= p.life)
            {
                p.kind = Kind.None;
                continue;
            }

            switch (p.kind)
            {
                case Kind.Blood:
                    p.velocity.y += Gravity * dt;
                    p.position += p.velocity * dt;
                    if (p.position.y <= p.groundY)
                    {
                        if (p.decalOnLand)
                            SpawnDecal(new Vector3(p.position.x, p.groundY, p.position.z), Random.Range(0.25f, 0.45f), 0.9f);
                        p.kind = Kind.None;
                    }
                    break;

                case Kind.Tooth:
                    if (!p.resting)
                    {
                        p.velocity.y += Gravity * dt;
                        p.position += p.velocity * dt;
                        p.rotation = Quaternion.AngleAxis(p.spinSpeed * dt, p.spinAxis) * p.rotation;
                        if (p.position.y <= p.groundY + p.size * 0.5f)
                        {
                            p.position.y = p.groundY + p.size * 0.5f;
                            // Un rebote y luego se queda en el piso
                            if (p.velocity.y < -2f) p.velocity = new Vector3(p.velocity.x * 0.4f, -p.velocity.y * 0.35f, p.velocity.z * 0.4f);
                            else p.resting = true;
                        }
                    }
                    break;

                case Kind.Smoke:
                    p.position += p.velocity * dt;
                    p.velocity *= 1f - 2f * dt;
                    break;

                case Kind.Star:
                    float angle = p.spinSpeed + p.age * 5f;
                    p.position = p.center + new Vector3(Mathf.Cos(angle), Mathf.Sin(p.age * 8f) * 0.05f, Mathf.Sin(angle)) * 0.4f;
                    p.rotation = Quaternion.Euler(0f, p.age * 360f, 45f);
                    break;
            }
        }

        UpdateDecals();
    }

    private void LateUpdate()
    {
        // Se dibuja también en pausa para que nada desaparezca de golpe
        for (int i = 0; i < MaxParticles; i++)
        {
            ref Particle p = ref _particles[i];
            if (p.kind == Kind.None) continue;

            Mesh mesh;
            Material mat;
            Vector3 scale;
            float t = p.age / p.life;
            switch (p.kind)
            {
                case Kind.Blood:
                    mesh = _sphere; mat = bloodMaterial;
                    scale = new Vector3(p.size, p.size * 1.25f, p.size);
                    break;
                case Kind.Tooth:
                    mesh = _cube; mat = toothMaterial;
                    float shrink = t > 0.8f ? 1f - (t - 0.8f) / 0.2f : 1f;
                    scale = new Vector3(p.size * 0.8f, p.size, p.size * 0.7f) * shrink;
                    break;
                case Kind.Smoke:
                    mesh = _sphere; mat = smokeMaterial;
                    float grow = Mathf.Lerp(1f, 2.4f, Mathf.Sqrt(t)) * (t > 0.75f ? 1f - (t - 0.75f) / 0.25f : 1f);
                    scale = Vector3.one * p.size * grow;
                    break;
                case Kind.Star:
                    mesh = _cube; mat = starMaterial;
                    float pop = t < 0.1f ? t / 0.1f : (t > 0.85f ? 1f - (t - 0.85f) / 0.15f : 1f);
                    scale = new Vector3(p.size, p.size, p.size * 0.3f) * pop;
                    break;
                default:
                    continue;
            }
            if (mat == null || mesh == null) continue;
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(p.position, p.rotation, scale), mat, 0);
        }
    }

    // ==================================================================
    // Manchas en el piso (pool fijo)
    // ==================================================================

    private void SpawnDecal(Vector3 groundPoint, float size, float alpha)
    {
        if (decalMaterial == null || _quad == null) return;

        Decal decal = _decals[_nextDecal];
        if (decal == null)
        {
            if (_decalRoot == null) _decalRoot = new GameObject("CartoonFx_Manchas").transform;
            var go = new GameObject("Mancha_" + _nextDecal);
            go.transform.SetParent(_decalRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = decalMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            decal = new Decal { transform = go.transform, renderer = mr };
            _decals[_nextDecal] = decal;
        }
        _nextDecal = (_nextDecal + 1) % MaxDecals;

        // Manchas un poco escalonadas en altura para que no parpadeen entre ellas
        decal.transform.position = groundPoint + Vector3.up * (0.015f + Random.value * 0.01f);
        decal.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
        decal.transform.localScale = new Vector3(size, size * Random.Range(0.7f, 1f), 1f);
        decal.spawnTime = Time.time;
        decal.baseAlpha = alpha;
        decal.renderer.gameObject.SetActive(true);
        SetDecalAlpha(decal, alpha);
    }

    private void UpdateDecals()
    {
        float now = Time.time;
        for (int i = 0; i < MaxDecals; i++)
        {
            Decal d = _decals[i];
            if (d == null || !d.renderer.gameObject.activeSelf) continue;

            float age = now - d.spawnTime;
            if (age < decalLifetime) continue;

            float fade = 1f - (age - decalLifetime) / decalFadeTime;
            if (fade <= 0f) d.renderer.gameObject.SetActive(false);
            else SetDecalAlpha(d, d.baseAlpha * fade);
        }
    }

    private void SetDecalAlpha(Decal decal, float alpha)
    {
        Color c = BloodColor;
        c.a = alpha;
        _mpb.SetColor(BaseColorId, c);
        _mpb.SetColor(ColorId, c);
        decal.renderer.SetPropertyBlock(_mpb);
    }

    // ==================================================================
    // Hit-stop
    // ==================================================================

    private void StartHitStop(float seconds)
    {
        _hitStopUntil = Mathf.Max(_hitStopUntil, Time.unscaledTime + seconds);
        if (_hitStop != null) return;

        // No congelar si el juego ya está en pausa (o algo más controla el tiempo)
        if (Time.timeScale < 1f) return;
        _hitStop = StartCoroutine(HitStopRoutine());
    }

    private IEnumerator HitStopRoutine()
    {
        Time.timeScale = 0f;
        while (Time.unscaledTime < _hitStopUntil)
            yield return null;
        _hitStop = null;
        RestoreTimeScale();
    }

    private static void RestoreTimeScale()
    {
        // Si durante el hit-stop se pausó el juego, la pausa manda
        bool paused = GameManager.Instance != null && GameManager.Instance.IsGamePaused;
        if (!paused && Time.timeScale == 0f) Time.timeScale = 1f;
    }
}
