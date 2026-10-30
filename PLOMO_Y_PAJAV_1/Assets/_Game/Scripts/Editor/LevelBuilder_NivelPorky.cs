using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static LevelBuilderUtils;

/// <summary>
/// Genera desde cero el greybox de Nivel Porky (casa destruida del hermano menor).
/// MENÚ: Plomo y Paja → Generar Nivel Porky
///
/// PLANO (metros, X = a lo largo de la calle, Z = hacia el fondo):
///   Calle 40x10 (z -20..-10) → patio delantero (z -10..-6) → casa 16x12 (z -6..6)
///   → patio trasero 20x15 (z 6..21) → callejón (z 21..33, salida al fondo)
///
///   Casa:  Sala (x -8..2, z -6..2) | Dormitorio (x 2..8, z -6..2)
///          Cocina (x -8..8, z 2..6) a lo ancho del fondo
///   Puerta delantera en la sala, puerta trasera en la cocina,
///   boquete de 3 m en la fachada del dormitorio, techo parcial.
///
/// Camino libre de escombros: puerta delantera → sala → cocina → puerta trasera → callejón.
///
/// FASE 3A2: todo se construye con CartoonKit (paredes con zócalo y fardos de paja, piso de tablas, arcos,
/// muebles, techo de paja, cerca, faroles con haz) + cielo pintado (NightSkyBuilder). Las mallas del kit
/// quedan en Assets/_Game/Art/Environment/Generated/.
/// </summary>
public static class LevelBuilder_NivelPorky
{
    private const string ScenePath = "Assets/Scenes/NivelPorky.unity";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    private const float WallHeight = 3.5f;
    private const float WallThickness = 0.2f;
    private const float DoorWidth = 1.6f;
    private const float DoorHeight = 2.4f;

    // Materiales (se crean en Assets/Materials/Greybox si faltan; la paleta se reaplica al regenerar)
    // _straw = paredes y techo de la casa de Porky; _wall = muros ajenos (callejón, edificios)
    private static Material _floor, _wall, _straw, _wood, _debris, _ground, _street, _door, _window;

    private static readonly Color WarmLamp = new Color32(0xFF, 0xB0, 0x60, 0xFF);

    [MenuItem("Plomo y Paja/Generar Nivel Porky")]
    public static void Generate()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Generar Nivel Porky",
                $"{ScenePath} ya existe y se va a regenerar desde cero.\nSe perderán los cambios hechos a mano en esa escena.",
                "Sobrescribir", "Cancelar"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // Mallas del kit: se reconstruyen una vez por generación (fase 3A2)
        CartoonKit.BeginSession();

        // Player, IA y guardias salen de prefabs: crear los que falten antes de vaciar la escena
        TestPrefabGenerator.EnsurePrefabs(false);
        RenderingSetup.Configure();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        LoadMaterials();
        Transform environment = CreateGroup("--- ENVIRONMENT ---");
        Transform lighting = CreateGroup("--- LIGHTING ---");
        Transform markers = CreateGroup("--- MARKERS ---");

        BuildGround(environment);
        BuildHouse(environment);
        BuildInterior(environment, lighting);
        BuildBoundaries(environment);
        BuildDestruction(environment);
        BuildYardProps(environment);
        Transform story = BuildStory(environment, lighting);
        BuildStreetLamps(environment, lighting);
        Light moon = BuildLighting(lighting);
        BuildMarkers(markers);
        NoirVolumeBuilder.CreateTechnicolorVolume(lighting);
        NightSkyBuilder.Build(new Vector3(0f, 0f, 5f), moon.transform.forward);
        BuildActors();

        // Solo el entorno es estático; luces, haces, cielo y marcadores no
        MarkStaticRecursive(environment.gameObject);
        CartoonKit.ExcludeFromNavMesh(story); // cartas, manchas y huellas no cuentan para el NavMesh

        // Guardar antes de hornear: el NavMesh se guarda junto a la escena
        EditorSceneManager.SaveScene(scene, ScenePath);
        BakeNavMesh();
        EditorSceneManager.SaveScene(scene, ScenePath);

        AddToBuildSettings();
        AssetDatabase.SaveAssets();

        Debug.Log($"[LevelBuilder] Nivel Porky generado en {ScenePath}");
    }

    private static void LoadMaterials()
    {
        // Paleta Technicolor (fase 3A); shader pintado CartoonEnvironment (fase 3A2)
        _floor = GetOrCreateMaterial("Greybox_Suelo", new Color32(0xA6, 0x8B, 0x6B, 0xFF), 0.1f, true);
        _wall = GetOrCreateMaterial("Greybox_Pared", new Color32(0x7A, 0x74, 0x86, 0xFF), 0.1f, true);
        _straw = GetOrCreateMaterial("Greybox_Paja", new Color32(0xE8, 0xC3, 0x5A, 0xFF), 0.1f, true);
        _wood = GetOrCreateMaterial("Greybox_Madera", new Color32(0x8A, 0x5A, 0x33, 0xFF), 0.2f, true);
        _debris = GetOrCreateMaterial("Greybox_Escombros", new Color32(0x7A, 0x6A, 0x55, 0xFF), 0.1f, true);
        _ground = GetOrCreateMaterial("Greybox_Exterior", new Color32(0x5E, 0x8C, 0x3A, 0xFF), 0.1f, true);
        _street = GetOrCreateMaterial("Greybox_Adoquin", new Color32(0x6C, 0x7A, 0x96, 0xFF), 0.25f, true);
        _door = GetOrCreateMaterial("Greybox_Puerta", new Color32(0xC0, 0x39, 0x2B, 0xFF), 0.3f, true);
        _window = GetOrCreateMaterial("Greybox_Ventana", new Color32(0x2A, 0x9D, 0x8F, 0xFF), 0.3f, true);
        if (_ground.HasProperty("_PaintNoise")) _ground.SetFloat("_PaintNoise", 0.6f);
    }

    // ============ Suelos ============

    // Caminos que quedan planos en el terreno irregular (y donde se dibuja la tierra)
    private static readonly Vector3[] FrontPath = { new Vector3(-5f, 0f, -6.3f), new Vector3(-5f, 0f, -8.2f), new Vector3(-4.4f, 0f, -10f) };
    private static readonly Vector3[] BackPath = { new Vector3(-4f, 0f, 6.3f), new Vector3(-3.4f, 0f, 10f), new Vector3(-1.4f, 0f, 15.5f), new Vector3(0f, 0f, 21f) };

    private const float FloorTop = 0.06f;

    // Goteras: agujeros del cielorraso (rect x,z) y del techo de paja (x, z, radio). Fuera de pistas y puertas.
    private static readonly Rect LeakKitchenA = new Rect(-1.5f, 3.9f, 1.4f, 1.4f);      // centro (-0.8, 4.6)
    private static readonly Rect LeakKitchenB = new Rect(4.6f, 3.0f, 1f, 1f);           // centro (5.1, 3.5)
    private static readonly Rect LeakSala = new Rect(-5.35f, -2.75f, 1.5f, 1.5f);       // centro (-4.6, -2.0)

    private static void BuildGround(Transform env)
    {
        Transform ground = CreateGroup("Suelos", env);
        CartoonKit.Cobblestones("Calle", new Vector3(0f, 0f, -15f), new Vector2(40f, 10f), ground);
        CartoonKit.Cobblestones("Callejon_Suelo", new Vector3(0f, 0f, 27f), new Vector2(4f, 12f), ground);

        // Patio delantero + pasillos laterales + patio trasero (z -10..21): suave, irregular, plano en caminos y bordes
        CartoonKit.UnevenGround("Patios", new Vector3(0f, 0f, 5.5f), new Vector2(20f, 31f), 0.5f, 0.12f, GroundFreedom, _ground, ground);
        CartoonKit.DirtPath("Camino_Delantero", FrontPath, 1.6f, ground);
        CartoonKit.DirtPath("Camino_Trasero", BackPath, 1.7f, ground);

        // Piso de tablas de la casa, 6 cm sobre el patio
        CartoonKit.PlankFloor("Casa_Piso", new Vector3(0f, FloorTop, 0f), new Vector2(16.2f, 12.2f), ground, 1f);
    }

    /// <summary>0 = plano (casa, caminos, bordes de la losa), 1 = ondulado.</summary>
    private static float GroundFreedom(float x, float z)
    {
        if (Mathf.Abs(x) < 8.9f && Mathf.Abs(z) < 6.9f) return 0f;
        float d = Mathf.Min(DistanceToPath(FrontPath, x, z), DistanceToPath(BackPath, x, z));
        float path = Mathf.InverseLerp(1.2f, 2.6f, d);
        float edge = Mathf.InverseLerp(0.3f, 1.6f, Mathf.Min(10f - Mathf.Abs(x), Mathf.Min(z + 10f, 21f - z)));
        float house = Mathf.InverseLerp(0f, 1.5f, Mathf.Max(Mathf.Abs(x) - 8.9f, Mathf.Abs(z) - 6.9f));
        return path * edge * house;
    }

    private static float DistanceToPath(Vector3[] path, float x, float z)
    {
        var p = new Vector2(x, z);
        float best = float.MaxValue;
        for (int i = 0; i < path.Length - 1; i++)
        {
            Vector2 a = new Vector2(path[i].x, path[i].z), b = new Vector2(path[i + 1].x, path[i + 1].z);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
        }
        return best;
    }

    // ============ Casa ============

    /// <summary>Hueco en los fardos para una ventana decorativa (medido desde el inicio de la pared).</summary>
    private static Rect WindowHole(float along) => new Rect(along - 0.75f, 1.05f, 1.5f, 1.3f);

    private static void BuildHouse(Transform env)
    {
        Transform house = CreateGroup("Casa", env);
        float t = WallThickness;
        const CartoonKit.WallFace Straw = CartoonKit.WallFace.Straw, Wood = CartoonKit.WallFace.Wainscot;

        // Fachada (z = -6), de -x a +x: derecha = afuera. Puerta delantera en x = -5, boquete de 3 m en x = 4.5
        CartoonKit.Wall("Pared_Fachada", new Vector3(-8f - t / 2f, 0f, -6f), new Vector3(8f + t / 2f, 0f, -6f), WallHeight, t, _straw,
            Straw, Wood, house, new[] { WindowHole(7.1f) },
            new WallOpening(3.1f, DoorWidth, DoorHeight),     // x = -5
            new WallOpening(12.6f, 3f, WallHeight));          // x = 4.5 (boquete)

        // Fondo (z = 6): derecha = adentro. Puerta trasera en x = -4
        CartoonKit.Wall("Pared_Fondo", new Vector3(-8f - t / 2f, 0f, 6f), new Vector3(8f + t / 2f, 0f, 6f), WallHeight, t, _straw,
            Wood, Straw, house, new[] { WindowHole(11.1f) }, new WallOpening(4.1f, DoorWidth, DoorHeight));

        // Laterales sólidos (de -z a +z: derecha = +x)
        CartoonKit.Wall("Pared_Izquierda", new Vector3(-8f, 0f, -6f), new Vector3(-8f, 0f, 6f), WallHeight, t, _straw,
            Wood, Straw, house, new[] { WindowHole(4f) });
        CartoonKit.Wall("Pared_Derecha", new Vector3(8f, 0f, -6f), new Vector3(8f, 0f, 6f), WallHeight, t, _straw,
            Straw, Wood, house);

        // Interiores: zócalo de madera en las dos caras, marcos con arco en cada puerta
        CartoonKit.Wall("Pared_Sala_Dormitorio", new Vector3(2f, 0f, -6f), new Vector3(2f, 0f, 2f), WallHeight, t, _straw,
            Wood, Wood, house, new WallOpening(4f, DoorWidth, DoorHeight));          // puerta en z = -2
        CartoonKit.Wall("Pared_Sala_Cocina", new Vector3(-8f, 0f, 2f), new Vector3(2f, 0f, 2f), WallHeight, t, _straw,
            Wood, Wood, house, new WallOpening(5f, DoorWidth, DoorHeight));          // puerta en x = -3
        CartoonKit.Wall("Pared_Dormitorio_Cocina", new Vector3(2f, 0f, 2f), new Vector3(8f, 0f, 2f), WallHeight, t, _straw,
            Wood, Wood, house, new WallOpening(3f, DoorWidth, DoorHeight));          // puerta en x = 5

        // Cielorraso de madera con vigas (cocina completa y fondo de la sala) + techo de paja encima
        Transform roof = CreateGroup("Techo", house);
        CartoonKit.CeilingWithHoles("Techo_Cocina", new Vector3(0f, WallHeight + 0.1f, 4.05f), new Vector2(16.2f, 4.1f), 0.2f,
            CartoonKit.M("Cielorraso"), roof, LeakKitchenA, LeakKitchenB);
        CartoonKit.CeilingWithHoles("Techo_Sala", new Vector3(-3f, WallHeight + 0.1f, -0.5f), new Vector2(10.2f, 5f), 0.2f,
            CartoonKit.M("Cielorraso"), roof, LeakSala);
        for (int i = 0; i < 4; i++)
        {
            // Una viga de la cocina cedió: más baja e inclinada
            bool sagging = i == 2;
            CreateBox($"Viga_Cocina_{i}", new Vector3(-6f + i * 4f, WallHeight - (sagging ? 0.2f : 0.08f), 4.05f), new Vector3(0.22f, 0.18f, 4f), _wood, roof,
                sagging ? new Vector3(3.5f, 0f, 0f) : Vector3.zero);
        }
        for (int i = 0; i < 3; i++)
            CreateBox($"Viga_Sala_{i}", new Vector3(-6.5f + i * 3.5f, WallHeight - 0.08f, -0.5f), new Vector3(0.22f, 0.18f, 4.9f), _wood, roof);
        CartoonKit.ThatchedRoof("TechoPaja_Cocina", new Vector3(0f, WallHeight + 0.2f, 4.1f), 17.2f, 5.2f, 1.6f, roof,
            new Vector3(-0.8f, 4.6f, 0.85f), new Vector3(5.1f, 3.5f, 0.65f));
        CartoonKit.ThatchedRoof("TechoPaja_Sala", new Vector3(-3f, WallHeight + 0.22f, -0.6f), 10.9f, 5.8f, 1.9f, roof,
            new Vector3(-4.6f, -2.0f, 0.9f));

        // Puerta delantera pateada: cuelga de una sola bisagra (arriba, pegada a la pared) y su lado libre cae
        const float doorH = DoorHeight - 0.08f;
        var hinge = new GameObject("Puerta_Delantera_Bisagra").transform;
        hinge.SetParent(house, false);
        hinge.position = new Vector3(-5.72f, DoorHeight + 0.05f, -5.85f);
        GameObject leaf = CartoonKit.DoorLeaf("Puerta_Delantera", hinge.position + new Vector3(0f, -doorH * 0.5f, 0.75f),
            Quaternion.Euler(0f, 90f, 0f), 1.5f, doorH, _door, hinge);
        hinge.rotation = Quaternion.Euler(10f, 0f, 0f);
        leaf.GetComponent<BoxCollider>().enabled = false;          // contra la jamba, nunca estorba el paso
        // Astillas del marco en el piso
        for (int k = 0; k < 4; k++)
            CreateBox($"Astilla_Puerta_{k}", new Vector3(-5.55f + k * 0.17f, FloorTop + 0.02f, -5.55f + (k % 2) * 0.25f), new Vector3(0.05f, 0.03f, 0.4f),
                CartoonKit.M("MaderaClara"), house, new Vector3(0f, 25f + k * 40f, 0f)).GetComponent<BoxCollider>().enabled = false;

        // Ventanas (decorativas, hundidas entre los fardos de la cara exterior). +Z local = hacia afuera
        Transform windows = CreateGroup("Ventanas", house);
        CartoonKit.WindowFrame("Ventana_Fachada", new Vector3(-1f, 1.7f, -6f - t / 2f - 0.05f), Quaternion.LookRotation(Vector3.back), 1.2f, 1f, windows, false, CartoonKit.WindowDamage.Boarded);
        CartoonKit.WindowFrame("Ventana_Fondo", new Vector3(3f, 1.7f, 6f + t / 2f + 0.05f), Quaternion.LookRotation(Vector3.forward), 1.2f, 1f, windows, true, CartoonKit.WindowDamage.Cracked);
        CartoonKit.WindowFrame("Ventana_Izquierda", new Vector3(-8f - t / 2f - 0.05f, 1.7f, -2f), Quaternion.LookRotation(Vector3.left), 1.2f, 1f, windows, false, CartoonKit.WindowDamage.Cracked);
    }

    // ============ Interior: muebles y lámparas ============
    // Libre: x -6..-2 en sala/cocina, delante de cada puerta y el paso cocina → puerta trasera.

    private static readonly Color InteriorLamp = new Color32(0xFF, 0xB8, 0x70, 0xFF);

    private static void BuildInterior(Transform env, Transform lighting)
    {
        Transform furniture = CreateGroup("Muebles", env);

        // ---- Sala: alfombra, sofá, cajita del contrato, caja de mesita con las facturas
        CartoonKit.Rug("Alfombra_Sala", new Vector3(-4.2f, FloorTop, -2.4f), new Vector2(3.4f, 2.4f), 0f, furniture);
        CartoonKit.Sofa("Sofa", new Vector3(-7.35f, FloorTop, -1f), 90f, furniture, 2.2f);
        CartoonKit.Crate("Caja_Contrato", new Vector3(-6.5f, FloorTop, -2.5f), 15f, furniture, 0.24f); // la pista queda encima (y 0.3)
        CartoonKit.Crate("Caja_Mesita", new Vector3(-7.4f, FloorTop, -3.9f), -8f, furniture, 0.5f);      // la caja hace de mesita
        // Maceta tumbada con la tierra regada
        GameObject plant = CartoonKit.PottedPlant("Planta_Tumbada", new Vector3(1.2f, FloorTop + 0.2f, -5.3f), furniture, 0);
        plant.transform.rotation = Quaternion.Euler(0f, 40f, 82f);
        // Silla patas arriba (volcada de lado)
        GameObject tipped = CartoonKit.Chair("Silla_Volcada_Sala", new Vector3(0.5f, FloorTop + 0.24f, -3.8f), 0f, furniture, true);
        tipped.transform.rotation = Quaternion.Euler(0f, 25f, 90f);
        CartoonKit.BareBulb("Bombilla_Sala", new Vector3(-3.5f, WallHeight, -1.2f), 0.75f, furniture, lighting, InteriorLamp, 1.7f, 5.5f);

        // ---- Cocina: mesa volcada, silla coja, silla volcada, nevera de los años 30, barril y caja en el rincón
        GameObject table = CartoonKit.RoundTable("Mesa_Volcada", new Vector3(2f, FloorTop + 0.35f, 4f), furniture, 0.55f);
        table.transform.rotation = Quaternion.Euler(0f, 20f, 80f);
        CartoonKit.Chair("Silla_Coja", new Vector3(0.9f, FloorTop, 4.1f), 90f, furniture, true);
        GameObject tipped2 = CartoonKit.Chair("Silla_Volcada_Cocina", new Vector3(3.3f, FloorTop + 0.24f, 3.3f), 0f, furniture, true);
        tipped2.transform.rotation = Quaternion.Euler(0f, -60f, 90f);
        CartoonKit.IceBox("Nevera", new Vector3(7.4f, FloorTop, 5.25f), 180f, furniture, lighting);
        CartoonKit.Barrel("Barril_Cocina", new Vector3(-7.3f, FloorTop, 5.3f), 0f, furniture, 0);
        CartoonKit.Crate("Caja_Cocina", new Vector3(-6.35f, FloorTop, 5.35f), 8f, furniture, 0.7f);
        Light kitchenBulb = CartoonKit.BareBulb("Bombilla_Cocina", new Vector3(0f, WallHeight, 3.3f), 0.6f, furniture, lighting, InteriorLamp, 2f, 6.5f);
        MakeSubtleFlicker(kitchenBulb);                                  // la única que parpadea, apenas

        // ---- Dormitorio: cómoda saqueada (la nota queda encima, y 1.1), colchón en el piso, caja de mesita de noche
        CartoonKit.Dresser("Cajonera", new Vector3(2.45f, FloorTop, 0.8f), 90f, furniture, new Vector3(1.2f, 1f - FloorTop, 0.55f), true);
        CartoonKit.FloorMattress("Colchon", new Vector3(6.7f, FloorTop, 0.9f), 12f, furniture);
        CartoonKit.Crate("Caja_Mesita_Noche", new Vector3(7.55f, FloorTop, 0.05f), 30f, furniture, 0.45f);
        CreateBox("Tablon_Bombilla", new Vector3(7.25f, WallHeight - 0.15f, -0.6f), new Vector3(1.5f, 0.1f, 0.1f), _wood, furniture);
        CartoonKit.BareBulb("Bombilla_Dormitorio", new Vector3(6.6f, WallHeight - 0.2f, -0.6f), 0.7f, furniture, lighting, InteriorLamp, 1.1f, 4.5f);
    }

    /// <summary>Parpadeo suave y raro: un bajón de vez en cuando, casi nunca un apagón.</summary>
    private static void MakeSubtleFlicker(Light light)
    {
        LightFlicker flicker = light.gameObject.AddComponent<LightFlicker>();
        var so = new SerializedObject(flicker);
        so.FindProperty("noiseAmount").floatValue = 0.08f;
        so.FindProperty("dipChancePerSecond").floatValue = 0.35f;
        so.FindProperty("dipMinMultiplier").floatValue = 0.6f;
        so.FindProperty("offChancePerSecond").floatValue = 0.02f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ============ Historia: casa de un jugador endeudado, saqueada ============
    // Todo es decorativo (sin colliders) y sale del NavMesh. Nada tapa las dos pistas ni las puertas.

    private static Transform BuildStory(Transform env, Transform lighting)
    {
        Transform story = CreateGroup("Narrativa", env);
        float y = FloorTop + 0.012f;

        // Vicio: cartas, dados, boletos y botellas que llevan la vista hacia las pistas
        CartoonKit.GamblerClutter("Vicio_Puerta", new Vector3(-5.2f, y, -4.8f), 0.45f, 1, story, 8, 2, 5, 1);
        CartoonKit.GamblerClutter("Vicio_Sofa", new Vector3(-5.9f, y, -3.5f), 0.5f, 2, story, 7, 2, 4, 2);
        CartoonKit.GamblerClutter("Vicio_Alfombra", new Vector3(-5.4f, y + 0.012f, -2.0f), 0.5f, 3, story, 6, 1, 3, 2);
        CartoonKit.GamblerClutter("Vicio_Dormitorio_A", new Vector3(3.2f, y, -1.6f), 0.5f, 4, story, 8, 2, 4, 1);
        CartoonKit.GamblerClutter("Vicio_Dormitorio_B", new Vector3(3.6f, y, -0.2f), 0.45f, 5, story, 6, 1, 4, 1);
        CartoonKit.GamblerClutter("Vicio_Cocina_A", new Vector3(-6.4f, y, 3.0f), 0.5f, 6, story, 3, 0, 2, 3);
        CartoonKit.GamblerClutter("Vicio_Cocina_B", new Vector3(4.3f, y, 4.7f), 0.45f, 7, story, 6, 2, 3, 1);
        CartoonKit.GamblerClutter("Vicio_Mesita_Noche", new Vector3(7.55f, FloorTop + 0.45f, 0.05f), 0.1f, 8, story, 2, 1, 1, 1);
        CartoonKit.OverdueBills("Facturas_Mesita", new Vector3(-7.4f, FloorTop + 0.5f, -3.9f), 20f, story);
        CartoonKit.OverdueBills("Facturas_Cocina", new Vector3(3.6f, y, 5.3f), -30f, story);

        // Escena del crimen: platos rotos, rasguños de lobo, huellas embarradas de la puerta a la salida
        CartoonKit.BrokenPlates("Platos_Cocina", new Vector3(2.9f, y, 4.4f), 0.7f, 1, story);
        CartoonKit.BrokenPlates("Platos_Cocina_2", new Vector3(0.9f, y, 2.9f), 0.5f, 2, story);
        CartoonKit.BrokenPlates("Platos_Sala", new Vector3(0.7f, y, -3.0f), 0.45f, 3, story);
        CartoonKit.ClawMarks("Rasgunos_Sala", new Vector3(-7.866f, 1.7f, -4.7f), 90f, story);
        CartoonKit.ClawMarks("Rasgunos_Cocina", new Vector3(-1.9f, 1.6f, 5.866f), 180f, story, 0.9f);

        // Paredes pobres: papel roto con la paja a la vista y huecos tapados con tablas
        CartoonKit.WallDamage("Dano_Sala_Oeste_A", new Vector3(-7.866f, 1.9f, 0.9f), 90f, story, 1, true);
        CartoonKit.WallDamage("Dano_Sala_Oeste_B", new Vector3(-7.866f, 2.3f, -1.0f), 90f, story, 2, false);
        CartoonKit.WallDamage("Dano_Sala_Fachada_A", new Vector3(-1.5f, 2.0f, -5.866f), 0f, story, 3, false);
        CartoonKit.WallDamage("Dano_Sala_Fachada_B", new Vector3(0.2f, 1.9f, -5.866f), 0f, story, 4, true);
        CartoonKit.WallDamage("Dano_Sala_Cocina", new Vector3(-6.0f, 2.3f, 1.866f), 180f, story, 5, false);
        CartoonKit.WallDamage("Dano_Cocina_Sala", new Vector3(1.0f, 1.9f, 2.134f), 0f, story, 6, true);
        CartoonKit.WallDamage("Dano_Cocina_Fondo_A", new Vector3(0.9f, 1.9f, 5.866f), 180f, story, 7, true);
        CartoonKit.WallDamage("Dano_Cocina_Fondo_B", new Vector3(3.8f, 2.4f, 5.866f), 180f, story, 8, false);
        CartoonKit.WallDamage("Dano_Cocina_Este", new Vector3(7.866f, 2.3f, 3.3f), -90f, story, 9, false);
        CartoonKit.WallDamage("Dano_Dormitorio_Oeste", new Vector3(2.134f, 2.2f, -4.6f), 90f, story, 10, false);
        CartoonKit.WallDamage("Dano_Dormitorio_Este", new Vector3(7.866f, 1.9f, -3.2f), -90f, story, 11, true);

        // Huellas de lobo: del umbral de la puerta delantera, por la sala y la cocina, hasta la puerta trasera y la salida
        var paws = new[]
        {
            new Vector3(-5f, 0f, -7.4f), new Vector3(-5f, 0f, -6.2f), new Vector3(-4.9f, FloorTop, -5.2f), new Vector3(-4.3f, FloorTop, -3.0f),
            new Vector3(-3.4f, FloorTop, -0.5f), new Vector3(-3.05f, FloorTop, 2.0f), new Vector3(-3.1f, FloorTop, 3.5f),
            new Vector3(-4.0f, FloorTop, 5.2f), new Vector3(-4.0f, FloorTop, 6.2f), new Vector3(-3.4f, 0f, 10f), new Vector3(-1.4f, 0f, 15.5f),
            new Vector3(0f, 0f, 21f), new Vector3(0f, 0f, 27f), new Vector3(0f, 0f, 31f),
        };
        CartoonKit.WolfPawPrints("Huellas_Lobo", paws, story);

        // Manchas de la casa y charcos de las goteras
        CartoonKit.Stain("Mancha_Sala", new Vector3(-3.2f, y, -4.0f), 0.55f, 1, story);
        CartoonKit.Stain("Mancha_Cocina", new Vector3(-2.4f, y, 3.2f), 0.6f, 2, story);
        CartoonKit.Stain("Mancha_Dormitorio", new Vector3(4.6f, y, -4.2f), 0.7f, 3, story);
        CartoonKit.Stain("Tierra_Maceta", new Vector3(1.55f, y, -5.0f), 0.3f, 4, story, "Tierra");
        return story;
    }

    // ============ Límites: cerca, callejón, edificios de enfrente ============

    private static void BuildBoundaries(Transform env)
    {
        Transform bounds = CreateGroup("Limites", env);
        const float fence = 1.6f;
        const CartoonKit.WallFace Plain = CartoonKit.WallFace.Plain;
        Material brick = CartoonKit.M("Ladrillo"), plaster = CartoonKit.M("Revoque");

        CartoonKit.Fence("Valla_Izquierda", new Vector3(-10f, 0f, -10f), new Vector3(-10f, 0f, 21f), fence, bounds);
        CartoonKit.Fence("Valla_Derecha", new Vector3(10f, 0f, -10f), new Vector3(10f, 0f, 21f), fence, bounds);
        // Cerca del fondo con salida de 4 m al callejón (x -2..2)
        CartoonKit.Fence("Valla_Fondo", new Vector3(-10.1f, 0f, 21f), new Vector3(10.1f, 0f, 21f), fence, bounds,
            new WallOpening(10.1f, 4f, fence));

        // Callejón
        CartoonKit.Wall("Callejon_Muro_Izq", new Vector3(-2.1f, 0f, 21f), new Vector3(-2.1f, 0f, 33f), 4f, 0.2f, brick, Plain, Plain, bounds);
        CartoonKit.Wall("Callejon_Muro_Der", new Vector3(2.1f, 0f, 21f), new Vector3(2.1f, 0f, 33f), 4f, 0.2f, brick, Plain, Plain, bounds);
        CartoonKit.Wall("Callejon_Muro_Fondo", new Vector3(-2.2f, 0f, 33.1f), new Vector3(2.2f, 0f, 33.1f), 4f, 0.2f, plaster, Plain, Plain, bounds);

        // Calle: fachadas de enfrente, muros en los extremos y cerca junto al patio
        CartoonKit.Facades("Edificios_Enfrente", -20.2f, 20.2f, -20f, 2f, bounds);
        CartoonKit.Wall("Calle_Muro_Izq", new Vector3(-20.1f, 0f, -20f), new Vector3(-20.1f, 0f, -10f), 3f, 0.2f, brick, Plain, Plain, bounds);
        CartoonKit.Wall("Calle_Muro_Der", new Vector3(20.1f, 0f, -20f), new Vector3(20.1f, 0f, -10f), 3f, 0.2f, brick, Plain, Plain, bounds);
        CartoonKit.Fence("Calle_Borde_Izq", new Vector3(-20.2f, 0f, -10f), new Vector3(-10f, 0f, -10f), fence, bounds);
        CartoonKit.Fence("Calle_Borde_Der", new Vector3(10f, 0f, -10f), new Vector3(20.2f, 0f, -10f), fence, bounds);
    }

    // ============ Destrucción: losas caídas, escombros y paja suelta ============
    // Nada en x -6..-2 de sala/cocina ni frente a ninguna puerta.

    private static void BuildDestruction(Transform env)
    {
        Transform ruins = CreateGroup("Escombros", env);

        // Pedazo de techo colgando dentro del dormitorio (apoyado en la pared derecha)
        CreateBox("Losa_Techo_Colgante", new Vector3(6.6f, 1.5f, -0.5f), new Vector3(3.6f, 0.2f, 3f), _straw, ruins,
            new Vector3(0f, 0f, 50f));
        // Pedazo de fachada caído en el patio delantero, al lado del boquete (deja libre x 3..5 y z < -8.3)
        CreateBox("Losa_Fachada_Caida", new Vector3(6.3f, 0.3f, -7.2f), new Vector3(2.6f, 0.2f, 1.8f), _straw, ruins,
            new Vector3(0f, 15f, 12f));
        // Viga caída en el patio trasero, lejos del camino a la valla
        CreateBox("Viga_Caida", new Vector3(7f, 0.15f, 16f), new Vector3(0.3f, 0.3f, 3.5f), _wood, ruins,
            new Vector3(0f, 25f, 0f));

        var chunks = new (Vector3 pos, float size, float yaw)[]
        {
            (new Vector3(7.2f, FloorTop, -4.6f), 0.5f, 20f),    // dormitorio
            (new Vector3(6.2f, FloorTop, -5.3f), 0.3f, 40f),
            (new Vector3(7.3f, FloorTop, -3.4f), 0.4f, 10f),
            (new Vector3(3.2f, FloorTop, -4.8f), 0.3f, 65f),
            (new Vector3(-8.5f, 0f, -9.2f), 0.4f, 30f),         // patio delantero
            (new Vector3(2.4f, 0f, -7.3f), 0.3f, 55f),
            (new Vector3(-7.3f, FloorTop, -5.3f), 0.4f, 15f),   // esquina de la sala
            (new Vector3(6f, 0f, 12f), 0.5f, 35f),              // patio trasero
            (new Vector3(7f, 0f, 13f), 0.3f, 70f),
            (new Vector3(-8f, 0f, 18f), 0.4f, 5f),
        };
        for (int i = 0; i < chunks.Length; i++)
        {
            var c = chunks[i];
            CartoonKit.Rubble($"Escombro_{i:00}", c.pos, c.size, c.yaw, _debris, ruins, i);
        }

        // Paja suelta alrededor del boquete, dentro del dormitorio y en el patio trasero
        CartoonKit.StrawDebris("Paja_Boquete", new Vector3(4.6f, 0.02f, -7.6f), 1.8f, 70, ruins);
        CartoonKit.StrawDebris("Paja_Dormitorio", new Vector3(5.8f, FloorTop, -3.6f), 1.6f, 50, ruins);
        CartoonKit.StrawDebris("Paja_PatioTrasero", new Vector3(6.6f, 0.02f, 14.5f), 1.5f, 35, ruins);
    }

    // ============ Exterior: barriles, fardos, macetas ============
    // Fuera de los pasillos laterales (x ±8..±10) y de la línea de patrulla (z -8 y z 9).

    private static void BuildYardProps(Transform env)
    {
        Transform props = CreateGroup("Patio_Props", env);
        CartoonKit.PottedPlant("Maceta_Puerta_Izq", new Vector3(-6.45f, 0f, -6.75f), props, 1);
        CartoonKit.PottedPlant("Maceta_Puerta_Der", new Vector3(-3.55f, 0f, -6.75f), props, 2);
        CartoonKit.StrawBale("Fardo_Delantero_A", new Vector3(6.9f, 0f, -9.35f), 8f, props);
        CartoonKit.StrawBale("Fardo_Delantero_B", new Vector3(-2.2f, 0f, -9.4f), -12f, props);

        // Fardos caídos del muro de la fachada
        GameObject fallen;
        fallen = CartoonKit.StrawBale("Fardo_Caido_A", new Vector3(-7.4f, 0.2f, -6.85f), 20f, props);
        fallen.transform.rotation = Quaternion.Euler(8f, 20f, 14f);
        fallen = CartoonKit.StrawBale("Fardo_Caido_B", new Vector3(-0.6f, 0.22f, -6.8f), -30f, props);
        fallen.transform.rotation = Quaternion.Euler(-6f, -30f, 20f);
        fallen = CartoonKit.StrawBale("Fardo_Caido_C", new Vector3(1.6f, 0.15f, -7.1f), 70f, props);
        fallen.transform.rotation = Quaternion.Euler(0f, 70f, 4f);

        CartoonKit.Barrel("Barril_Patio_A", new Vector3(7.4f, 0f, 18.2f), 0f, props, 1);
        CartoonKit.Barrel("Barril_Patio_B", new Vector3(6.6f, 0f, 18.9f), 40f, props, 2);
        CartoonKit.Crate("Caja_Patio", new Vector3(7.5f, 0f, 19.6f), 20f, props, 0.8f);
        CartoonKit.StrawBale("Fardo_Patio", new Vector3(-6.5f, 0f, 19.6f), 85f, props);

        CartoonKit.Barrel("Barril_Callejon_A", new Vector3(1.45f, 0f, 25f), 0f, props, 3);
        CartoonKit.Barrel("Barril_Callejon_B", new Vector3(1.5f, 0f, 29.6f), 25f, props, 0);
        CartoonKit.Crate("Caja_Callejon", new Vector3(-1.5f, 0f, 27.2f), -10f, props, 0.6f);
    }

    // ============ Faroles y luces de exterior ============
    // En el camino del jugador siempre hay una pileta de luz como mucho cada ~8 m.

    private static void BuildStreetLamps(Transform env, Transform lighting)
    {
        Transform lamps = CreateGroup("Faroles", env);

        // El farol "roto": torcido y es el único que parpadea
        Light broken = CartoonKit.StreetLamp("Farol_Roto", new Vector3(-7f, 0f, -10.6f), 90f, lamps, lighting, WarmLamp, 2.5f, 12f, 4f);
        broken.gameObject.AddComponent<LightFlicker>();

        CartoonKit.StreetLamp("Farol_Calle", new Vector3(7f, 0f, -10.6f), -90f, lamps, lighting, WarmLamp, 2.5f, 12f);
        CartoonKit.StreetLamp("Farol_Enfrente", new Vector3(2f, 0f, -19.3f), 0f, lamps, lighting, WarmLamp, 2.3f, 11f);
        CartoonKit.StreetLamp("Farol_Patio", new Vector3(-8.8f, 0f, 15f), 90f, lamps, lighting, WarmLamp, 2.2f, 11f);
        CartoonKit.StreetLamp("Farol_Porton", new Vector3(3.1f, 0f, 20.3f), -90f, lamps, lighting, WarmLamp, 2f, 10f);

        // Aplique sobre la puerta trasera (por fuera) y dos en el callejón
        CartoonKit.WallLamp("Aplique_PuertaTrasera", new Vector3(-2.85f, 2.55f, 6.35f), 0f, lamps, lighting, WarmLamp, 1.8f, 8f);
        CartoonKit.WallLamp("Aplique_Callejon_A", new Vector3(1.98f, 2.7f, 24f), -90f, lamps, lighting, WarmLamp, 1.6f, 8f);
        CartoonKit.WallLamp("Aplique_Callejon_B", new Vector3(-1.98f, 2.7f, 30f), 90f, lamps, lighting, WarmLamp, 1.6f, 8f);
    }

    private static Light BuildLighting(Transform lighting)
    {
        // Luna: direccional fría con sombras suaves
        var moonGo = new GameObject("Luz_Luna");
        moonGo.transform.SetParent(lighting, false);
        moonGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        Light moon = moonGo.AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color32(0xBF, 0xD4, 0xFF, 0xFF);
        moon.intensity = 0.5f;
        moon.shadows = LightShadows.Soft;

        // Luz de luna que entra por el boquete del dormitorio, con su haz
        Color moonColor = new Color32(0xBF, 0xD4, 0xFF, 0xFF);
        Vector3 from = new Vector3(4.3f, 4.6f, -9.2f), to = new Vector3(5.4f, FloorTop, -3f);
        var spotGo = new GameObject("Luz_Luna_Boquete");
        spotGo.transform.SetParent(lighting, false);
        spotGo.transform.SetPositionAndRotation(from, Quaternion.LookRotation(to - from));
        Light spot = spotGo.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = moonColor;
        spot.intensity = 3f;
        spot.range = 12f;
        spot.spotAngle = 40f;
        spot.shadows = LightShadows.Soft;
        GameObject beam = CartoonKit.LightCone("Luz_Luna_Boquete_Haz", from, Vector3.Distance(from, to), 1.9f, moonColor, lighting, 0.07f);
        beam.transform.rotation = Quaternion.FromToRotation(Vector3.down, (to - from).normalized);

        // Ambiente nocturno en degradado (cielo índigo, horizonte violeta, suelo terroso)
        RenderSettings.sun = moon;
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color32(0x3A, 0x3F, 0x8F, 0xFF);
        RenderSettings.ambientEquatorColor = new Color32(0x6B, 0x4A, 0x7A, 0xFF);
        RenderSettings.ambientGroundColor = new Color32(0x4A, 0x33, 0x26, 0xFF);
        // La niebla la configura NightSkyBuilder (lineal, color del horizonte)
        return moon;
    }

    // ============ Marcadores ============

    private static void BuildMarkers(Transform markers)
    {
        CreateMarker("PlayerSpawn", MarkerType.PlayerSpawn, "", new Vector3(0f, 0f, -15f), 0f, markers);

        CreateMarker("Clue_nota_porky_01", MarkerType.Clue, "nota_porky_01", new Vector3(2.5f, 1.1f, 0.8f), 90f, markers);
        CreateMarker("Clue_contrato_01", MarkerType.Clue, "contrato_01", new Vector3(-6.5f, 0.3f, -2.5f), 0f, markers);

        // Patrulla alrededor del exterior de la casa (pasillos laterales de 1.8 m)
        Vector3[] patrol =
        {
            new Vector3(-9f, 0f, -8f), new Vector3(9f, 0f, -8f), new Vector3(9f, 0f, 9f), new Vector3(-9f, 0f, 9f)
        };
        for (int i = 0; i < patrol.Length; i++)
            CreateMarker($"Patrol_A_{i}", MarkerType.PatrolWaypoint, "A", patrol[i], 0f, markers);

        // Centinela en el patio trasero mirando a la puerta trasera (x -4, z 6)
        CreateMarker("Sentinel_PatioTrasero", MarkerType.SentinelPoint, "", new Vector3(-4f, 0f, 13f), 180f, markers);

        CreateMarker("Guard_guardia_porky", MarkerType.InterrogableGuard, "guardia_porky", new Vector3(5.5f, 0f, 4.3f), -90f, markers);

        LevelMarker exit = CreateMarker("LevelExit", MarkerType.LevelExit, "nivel_porky_salida", new Vector3(0f, 0f, 32f), 0f, markers);
        BoxCollider trigger = exit.gameObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 1.5f, 0f);
        trigger.size = new Vector3(3f, 3f, 1f);
    }

    // ============ Actores: jugador, IA y guardias (se colocan sobre los marcadores) ============

    private static void BuildActors()
    {
        var markers = new List<LevelMarker>(Object.FindObjectsOfType<LevelMarker>());
        markers.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        // El Player trae su propia cámara y AudioListener
        LevelMarker spawn = markers.Find(m => m.Type == MarkerType.PlayerSpawn);
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabGenerator.PlayerPath);
        if (playerPrefab != null && spawn != null)
        {
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
        }
        else
        {
            Debug.LogWarning("[LevelBuilder] Sin Player.prefab o PlayerSpawn: el nivel queda sin jugador.");
        }

        // Debe ser raíz de la escena para que DontDestroyOnLoad funcione
        PlaceRoot(TestPrefabGenerator.AICompanionPath);

        Transform actors = CreateGroup("--- ACTORS ---");
        GameObject guardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabGenerator.GuardPath);
        GameObject interrogablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabGenerator.InterrogablePath);

        // Patrullero: ruta Patrol_A_0..3
        var waypoints = markers.FindAll(m => m.Type == MarkerType.PatrolWaypoint && m.Id == "A");
        if (guardPrefab != null && waypoints.Count > 0)
        {
            GameObject patrol = SpawnAt(guardPrefab, "Guard_Patrulla_A", waypoints[0].transform, actors);
            var so = new SerializedObject(patrol.GetComponent<EnemyAI>());
            so.FindProperty("enemyType").enumValueIndex = (int)EnemyAI.EnemyType.Patrullero;
            SerializedProperty points = so.FindProperty("patrolPoints");
            points.arraySize = waypoints.Count;
            for (int i = 0; i < waypoints.Count; i++)
                points.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i].transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Centinela: quieto mirando hacia donde apunta el marcador
        LevelMarker sentinel = markers.Find(m => m.Type == MarkerType.SentinelPoint);
        if (guardPrefab != null && sentinel != null)
        {
            GameObject guard = SpawnAt(guardPrefab, "Guard_Centinela", sentinel.transform, actors);
            var so = new SerializedObject(guard.GetComponent<EnemyAI>());
            so.FindProperty("enemyType").enumValueIndex = (int)EnemyAI.EnemyType.Centinela;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Guardias interrogables: npcId = id del marcador
        foreach (LevelMarker marker in markers.FindAll(m => m.Type == MarkerType.InterrogableGuard))
        {
            if (interrogablePrefab == null) break;
            GameObject guard = SpawnAt(interrogablePrefab, $"Guard_Interrogable_{marker.Id}", marker.transform, actors);
            var so = new SerializedObject(guard.GetComponent<EnemyHealth>());
            so.FindProperty("npcId").stringValue = marker.Id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        RemoveStrayCameras();
    }

    private static GameObject SpawnAt(GameObject prefab, string name, Transform marker, Transform parent)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = name;
        instance.transform.SetParent(parent, false);
        instance.transform.SetPositionAndRotation(marker.position, marker.rotation);
        return instance;
    }

    private static void PlaceRoot(string prefabPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[LevelBuilder] Falta {prefabPath}.");
            return;
        }
        PrefabUtility.InstantiatePrefab(prefab);
    }

    /// <summary>Solo debe haber la cámara y el AudioListener del Player.</summary>
    private static void RemoveStrayCameras()
    {
        foreach (Camera cam in Object.FindObjectsOfType<Camera>(true))
            if (cam.GetComponentInParent<PlayerController>() == null)
                Object.DestroyImmediate(cam.gameObject);

        foreach (AudioListener listener in Object.FindObjectsOfType<AudioListener>(true))
            if (listener.GetComponentInParent<PlayerController>() == null)
                Object.DestroyImmediate(listener);
    }

    // ============ NavMesh y Build Settings ============

    private static void BakeNavMesh()
    {
        // com.unity.ai.navigation no está instalado: se usa el baker integrado
#pragma warning disable 618
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
#pragma warning restore 618

        float radius = UnityEngine.AI.NavMesh.GetSettingsByIndex(0).agentRadius;
        if (radius > 0.5f)
            Debug.LogWarning($"[LevelBuilder] Radio del agente = {radius}. Con puertas de {DoorWidth} m conviene 0.5 o menos.");
    }

    private static void AddToBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == ScenePath)) return;

        int sampleIndex = scenes.FindIndex(s => s.path == SampleScenePath);
        if (sampleIndex < 0 && File.Exists(SampleScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(SampleScenePath, true));
            sampleIndex = scenes.Count - 1;
        }

        scenes.Insert(sampleIndex + 1, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
