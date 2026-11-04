using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MENÚ: Plomo y Paja → Intro → Construir taxi y llegada (fase INTRO.2). También lo llama Generar Nivel Porky.
/// Idempotente: el grupo "--- INTRO ---" de NivelPorky se borra y se vuelve a armar; el prefab se reescribe.
///
/// Crea / actualiza:
///  - Prefabs/Level1/Taxi_Intro.prefab: taxi de los años 30 con primitivas del kit (capó largo, guardabarros redondos,
///    4 ruedas, estribos, letrero TAXI, franja a cuadros, amarillo y negro; un poco torcido, semilla fija), interior
///    oscuro (asientos, tablero con brillo cálido, volante, taxista en billboard, ventanas con lluvia), barrido de faroles,
///    techo con collider (RainEmitter lo toma como techo: lluvia ahogada y sin gotas adentro) y Anchor_TaxiSeat.
///  - En la escena: Taxi_Path, Taxi_ExitPath, Anchor_TaxiStop, Anchor_PlayerCurb, Anchor_HouseLook, un camión
///    estacionado (tapa la vista al guardia de la calle), la UI de la cinemática y el director.
///  - INTRO.3: WeaponDraw_Puerta (trigger de respaldo para sacar la escopeta, frente a la puerta de Porky) y
///    PlayerWeaponHolster en Player.prefab.
///  - Mueve el PlayerSpawn y el Player a la acera; el patrullero del patio arranca detrás de la casa (punto 2).
///  - Build Settings: Intro, NivelPorky (SampleScene fuera de la lista; la escena no se borra).
///
/// UBICACIÓN (ver la auditoría INTRO.2): la parada está en la punta oeste de la calle, la ronda del guardia de la calle
/// (Patrulla_B, LevelBuilder_NivelPorky.ExtraGuards) empieza detrás del camión en la punta este.
/// </summary>
public static class IntroTaxiBuilder
{
    public const string PrefabFolder = "Assets/_Game/Prefabs/Level1";
    public const string PrefabPath = PrefabFolder + "/Taxi_Intro.prefab";
    private const string MaterialFolder = "Assets/_Game/Materials/Intro";
    private const string WindowMaterialPath = MaterialFolder + "/IntroTaxiVentana.mat";
    private const string WindowShader = "PlomoYPaja/Intro/RainWindow";
    private const string TaxistaFolder = "Assets/_Game/Art/Characters/taxista";
    private const string IntroScenePath = "Assets/Scenes/Intro.unity";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string RootName = "--- INTRO ---";
    private const int RoofLayer = 1; // TransparentFX: RainEmitter lo ve como techo; la vista de los guardias (Default) no

    // ---- Ubicación (mundo). Calle: x -20..20, z -20..-10; la casa mira a la calle (fachada en z = -6) ----
    /// <summary>Acera de la punta oeste, junto a la cerca, mirando a lo largo de la calle (+x).</summary>
    public static readonly Vector3 CurbPosition = new Vector3(-14.0f, 0f, -10.9f);
    public const float CurbYaw = 90f;
    /// <summary>Parada: el taxi mira al oeste (-x), así la puerta trasera derecha queda del lado de la acera.</summary>
    public static readonly Vector3 TaxiStopPosition = new Vector3(-14.6f, 0f, -12.45f);
    public const float TaxiStopYaw = -90f;
    /// <summary>Puerta delantera de la casa de Porky (x -5, fachada z -6), a la altura de los ojos.</summary>
    public static readonly Vector3 HouseLookPosition = new Vector3(-5f, 1.6f, -6.2f);

    private static readonly Vector3[] ArrivePoints =
    {
        new Vector3(17.6f, 0f, -15.6f), new Vector3(8f, 0f, -15.6f), new Vector3(-4f, 0f, -15.0f),
        new Vector3(-10.5f, 0f, -12.9f), TaxiStopPosition,
    };
    /// <summary>
    /// INTRO.2.1: salida recta al oeste (hacia donde ya mira el taxi), a espaldas de la cámara de la llegada: el giro hacia la
    /// casa va de yaw 20° a 62° y el taxi queda siempre ≥ 31° fuera del borde del cuadro (16:9 y 21:9, FOV vertical 60°).
    /// Pasa el muro del extremo oeste (x -20.1) fuera de cuadro y sigue hasta 46 m: el motor (3D, maxDistance 45) ya no se oye
    /// cuando el director lo apaga. Antes: vuelta en U y salida al este por el carril sur, cruzando el cuadro.
    /// </summary>
    private static readonly Vector3[] ExitPoints =
    {
        TaxiStopPosition, new Vector3(-20f, 0f, -12.45f), new Vector3(-30f, 0f, -12.45f), new Vector3(-45f, 0f, -12.45f),
        new Vector3(-60f, 0f, -12.45f),
    };

    /// <summary>Hacia dónde mira Wilbur al bajarse del taxi (yaw del mundo): la cerca de la acera, a la izquierda de la casa.</summary>
    public const float CurbStandYaw = 20f;

    // INTRO.2.1, vista desde el asiento: ojos a 1.42 m (antes 1.20, por DEBAJO del alféizar de 1.23). El alféizar bajó a 1.15
    // (puertas más bajas) y la ventana lateral creció hacia abajo: vidrio visible 1.15..1.695 (techo), centro 1.42 = los ojos,
    // alféizar 0.27 m por debajo de los ojos. El techo no se movió.
    private const float SeatEyeHeight = 1.42f;
    private const float WindowSill = 1.15f;
    private const float WindowTop = 1.73f;

    /// <summary>Camión estacionado en el carril de la casa, punta este: tapa la vista desde la acera y el patio delantero.</summary>
    public static readonly Vector3 TruckCenter = new Vector3(10.8f, 0f, -11.75f);
    public static readonly Vector3 TruckSize = new Vector3(5.6f, 2.7f, 2.4f);

    /// <summary>
    /// INTRO.3: trigger de respaldo para sacar la escopeta, en el patio delantero frente a la puerta de Porky (x -5, fachada
    /// z -6): 2.4 × 2 × 2 m, de z -8.2 a -6.2. Quien va a la puerta desde la acera lo cruza sí o sí.
    /// </summary>
    public static readonly Vector3 DrawTriggerCenter = new Vector3(-5f, 1f, -7.2f);
    public static readonly Vector3 DrawTriggerSize = new Vector3(2.4f, 2f, 2f);

    /// <summary>Punto de la ruta A donde arranca el patrullero del patio (Patrol_A_2 = esquina de atrás, detrás de la casa).</summary>
    public const int YardPatrolFirstPoint = 2;

    // Guion §3 y §4 (v1.1). Copiados EXACTOS.
    private static readonly (string speaker, string id, string text, bool taxista, string sfx)[] TaxiLines =
    {
        ("Taxista", "taxista_01", "Hasta aquí llego, señor. De esta esquina para allá es territorio de Don Lobo. Tenga cuidado.", true, ""),
        ("Wilbur", "wilbur_taxi_01", "Tranquilo, amigo. No vengo a buscar problemas… vengo a buscar a mi hermano.", false, ""),
        ("Taxista", "taxista_02", "¿Su hermano? ¿El menor, el de la casa de paja? …Ah. Ese muchacho.", true, ""),
        ("Taxista", "taxista_03", "Que Dios lo acompañe, señor. Y cuide la billetera.", true, ""),
        ("Wilbur", "wilbur_taxi_02", "Quédese con el cambio.", false, "sfx_monedas"),
    };
    private const string HouseLineText = "Porky… ¿qué te hicieron?";
    private const string AiThought = "Paja mojada, la puerta en el suelo… Esto no fue el viento, Wilbur. Alguien vino a cobrar.";

    private static readonly string[] AudioIds =
        { "amb_taxi_interior", "sfx_limpiaparabrisas", "sfx_taxi_frenar", "sfx_puerta_carro", "sfx_taxi_arranca", "sfx_monedas", "lluvia_loop" };

    // ==================================================================
    // Menú
    // ==================================================================

    [MenuItem("Plomo y Paja/Intro/Construir taxi y llegada")]
    public static void BuildMenu()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(LevelBuilder_NivelPorky.ScenePath))
        {
            EditorUtility.DisplayDialog("Construir taxi y llegada", "No existe " + LevelBuilder_NivelPorky.ScenePath +
                                        ". Corre primero Plomo y Paja → Generar Nivel Porky.", "Entendido");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != LevelBuilder_NivelPorky.ScenePath)
            scene = EditorSceneManager.OpenScene(LevelBuilder_NivelPorky.ScenePath, OpenSceneMode.Single);

        CartoonKit.BeginSession();
        var report = new StringBuilder();

        // Ruta nueva del guardia de la calle (vive en LevelBuilder_NivelPorky.ExtraGuards)
        GameObject markers = GameObject.Find(RootMarkers);
        report.AppendLine(LevelBuilder_NivelPorky.PlaceExtraGuards(markers != null ? markers.transform : null, null));
        BuildInto(report);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
#pragma warning disable 618 // mismo horneado que Generar Nivel Porky (el camión cambia el NavMesh)
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
#pragma warning restore 618
        EditorSceneManager.SaveScene(scene);
        report.AppendLine("✓ NavMesh horneado otra vez (camión nuevo)");
        LevelBuilder_NivelPorky.ValidateExtraGuards(report); // la ronda nueva del guardia de la calle es transitable
        AssetDatabase.SaveAssets();

        Debug.Log("[Intro] Taxi y llegada:\n" + report);
        EditorUtility.DisplayDialog("Construir taxi y llegada", "Listo.\n\n" + report, "Entendido");
    }

    private const string RootMarkers = "--- MARKERS ---";

    /// <summary>
    /// Arma todo en la escena abierta (NivelPorky). Lo llama Generar Nivel Porky antes de hornear el NavMesh.
    /// </summary>
    public static void BuildInto(StringBuilder report)
    {
        var missing = new List<string>();
        GameObject old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        Transform root = new GameObject(RootName).transform;

        GameObject prefab = BuildTaxiPrefab(missing, report);
        if (prefab == null)
        {
            report.AppendLine("✗ No se pudo guardar " + PrefabPath);
            return;
        }

        var taxiGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        taxiGo.transform.SetParent(root, false);
        taxiGo.transform.SetPositionAndRotation(TaxiStopPosition, Quaternion.Euler(0f, TaxiStopYaw, 0f));

        WaypointPath arrive = BuildPath("Taxi_Path", ArrivePoints, root);
        WaypointPath exit = BuildPath("Taxi_ExitPath", ExitPoints, root);
        Anchor("Anchor_TaxiStop", TaxiStopPosition, TaxiStopYaw, root);
        Transform curb = Anchor("Anchor_PlayerCurb", CurbPosition, CurbYaw, root);
        Transform house = Anchor("Anchor_HouseLook", HouseLookPosition, 0f, root);
        BuildTruck(root);
        BuildDrawTrigger(root);

        TMP_FontAsset font = InterrogationUIBuilder.EnsureFont(report);
        BuildDirector(root, taxiGo, arrive, exit, curb, house, font);

        EnsurePlayerHolster(report);
        MovePlayerToCurb(report);
        SetYardPatrolStart(report);
        EnsureBuildOrder(report);
        CheckAudio(missing);

        report.AppendLine($"✓ Taxi en la parada {TaxiStopPosition}, acera {CurbPosition}, casa {HouseLookPosition}");
        report.AppendLine($"✓ Camión estacionado en {TruckCenter} ({TruckSize.x}×{TruckSize.z} m, alto {TruckSize.y})");
        report.AppendLine($"✓ Trigger para sacar el arma frente a la puerta: {DrawTriggerCenter} ({DrawTriggerSize.x}×{DrawTriggerSize.z} m)");
        report.AppendLine(missing.Count == 0 ? "✓ No falta ningún asset del taxi." : "⚠ Faltan (placeholder): " + string.Join(", ", missing));
    }

    // ==================================================================
    // Prefab del taxi (local: +z adelante, +x derecha, origen en el piso al centro)
    // ==================================================================

    private static GameObject BuildTaxiPrefab(List<string> missing, StringBuilder report)
    {
        LevelBuilderUtils.EnsureFolder(PrefabFolder);
        Material yellow = LevelBuilderUtils.GetOrCreateMaterial("Kit_TaxiAmarillo", new Color32(0xF2, 0xB8, 0x1E, 0xFF), 0.35f, true);
        Material black = LevelBuilderUtils.GetOrCreateMaterial("Kit_TaxiNegro", new Color32(0x1C, 0x1A, 0x1E, 0xFF), 0.3f, true);
        Material white = CartoonKit.M("Yeso");
        Material chrome = CartoonKit.M("Cromo");
        Material seatMat = CartoonKit.M("Manta");
        Material dash = CartoonKit.M("MaderaOscura");
        Material glow = CartoonKit.M("VidrioCalido");
        Material window = WindowMaterial(report, missing);

        var root = new GameObject("Taxi_Intro");
        try
        {
            System.Random rng = CartoonKit.Rng("Taxi_Intro");
            float R(float a) => CartoonKit.Range(rng, -a, a);

            // Carrocería (primer hijo: su Renderer decide si el taxi "se ve")
            Mesh bodyMesh = CartoonKit.GetMesh("Taxi_Intro_Carroceria", b => AddCarBody(b, rng, true));
            CartoonKit.MeshObject("Carroceria", bodyMesh, new[] { yellow, black, white, chrome }, root.transform, Vector3.zero, Quaternion.identity);
            // Fondo negro detrás de la franja: una banda fina un poco más adentro
            CartoonKit.MeshObject("Franja_Fondo", CartoonKit.GetMesh("Taxi_Intro_FranjaFondo", b =>
            {
                foreach (float sx in new[] { -1f, 1f })
                    CartoonKit.AddBox(b, new Vector3(sx * 0.875f, 1.0f, -0.4f), new Vector3(0.02f, 0.1f, 2.2f), Quaternion.identity, 0.005f, 1);
            }), new[] { black }, root.transform, Vector3.zero, Quaternion.identity);

            // Letrero TAXI (TMP 3D, las dos caras)
            foreach (float side in new[] { 1f, -1f })
            {
                var sign = new GameObject(side > 0 ? "Letrero_Adelante" : "Letrero_Atras");
                sign.transform.SetParent(root.transform, false);
                sign.transform.localPosition = new Vector3(0f, 1.95f, 0.25f + side * 0.075f);
                sign.transform.localRotation = Quaternion.Euler(0f, side > 0 ? 180f : 0f, 0f);
                TextMeshPro text = sign.AddComponent<TextMeshPro>();
                text.text = "TAXI";
                text.fontSize = 1.6f;
                text.color = new Color32(0x1C, 0x1A, 0x1E, 0xFF);
                text.alignment = TextAlignmentOptions.Center;
                text.rectTransform.sizeDelta = new Vector2(0.52f, 0.18f);
                text.enableWordWrapping = false;
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Game/Fonts/SpecialElite SDF.asset");
                if (font != null) text.font = font;
            }

            // Ruedas (objetos aparte: giran)
            Mesh wheelMesh = CartoonKit.GetMesh("Taxi_Intro_Rueda", b =>
            {
                b.Submesh = 0;
                CartoonKit.AddLathe(b, Vector3.zero, Quaternion.Euler(0f, 0f, 90f), new[] { -0.13f, -0.1f, 0.1f, 0.13f }, new[] { 0.33f, 0.38f, 0.38f, 0.33f }, 16);
                b.Submesh = 1;
                CartoonKit.AddLathe(b, Vector3.zero, Quaternion.Euler(0f, 0f, 90f), new[] { -0.15f, 0.15f }, new[] { 0.15f, 0.15f }, 10);
            });
            var wheels = new List<Transform>();
            foreach (float sx in new[] { -0.84f, 0.84f })
                foreach (float sz in new[] { -1.4f, 1.4f })
                    wheels.Add(CartoonKit.MeshObject("Rueda", wheelMesh, new[] { black, chrome }, root.transform,
                        new Vector3(sx, 0.38f, sz), Quaternion.Euler(0f, 0f, R(2f))).transform);

            // Interior
            Mesh cabin = CartoonKit.GetMesh("Taxi_Intro_Interior", b =>
            {
                b.Submesh = 0; // asientos
                CartoonKit.AddBox(b, new Vector3(0f, 0.72f, 0.22f), new Vector3(1.5f, 0.24f, 0.5f), Quaternion.identity, 0.08f, 2);   // banco delantero
                CartoonKit.AddBox(b, new Vector3(0f, 1.05f, -0.05f), new Vector3(1.5f, 0.5f, 0.14f), Quaternion.Euler(-8f, 0f, 0f), 0.06f, 2);
                CartoonKit.AddBox(b, new Vector3(0f, 0.72f, -0.95f), new Vector3(1.5f, 0.26f, 0.6f), Quaternion.identity, 0.08f, 2);  // banco trasero
                CartoonKit.AddBox(b, new Vector3(0f, 1.1f, -1.32f), new Vector3(1.5f, 0.6f, 0.16f), Quaternion.Euler(-10f, 0f, 0f), 0.06f, 2);
                b.Submesh = 1; // tablero y volante
                CartoonKit.AddBox(b, new Vector3(0f, 1.1f, 0.78f), new Vector3(1.56f, 0.3f, 0.18f), Quaternion.Euler(-6f, 0f, 0f), 0.05f, 2);
                CartoonKit.AddLathe(b, new Vector3(-0.42f, 1.2f, 0.6f), Quaternion.Euler(-60f, 0f, 0f), new[] { -0.015f, 0.015f }, new[] { 0.19f, 0.19f }, 16);
                CartoonKit.AddBox(b, new Vector3(-0.42f, 1.1f, 0.68f), new Vector3(0.04f, 0.04f, 0.3f), Quaternion.Euler(-30f, 0f, 0f), 0.01f, 1);
                b.Submesh = 2; // instrumentos encendidos
                CartoonKit.AddBox(b, new Vector3(-0.3f, 1.2f, 0.68f), new Vector3(0.42f, 0.07f, 0.02f), Quaternion.Euler(-6f, 0f, 0f), 0.01f, 1);
            });
            CartoonKit.MeshObject("Interior", cabin, new[] { seatMat, dash, glow }, root.transform, Vector3.zero, Quaternion.identity);

            Light dashLight = LevelBuilderUtils.CreatePointLight("Tablero_Luz", Vector3.zero, new Color(1f, 0.68f, 0.38f), 0.5f, 1.6f, root.transform);
            dashLight.transform.localPosition = new Vector3(-0.2f, 1.25f, 0.55f);
            dashLight.shadows = LightShadows.None;

            // Ventanas con lluvia (quads sin collider)
            // INTRO.2.1: laterales del alféizar (1.15) al techo; antes 1.23..1.73
            float sideY = (WindowSill + WindowTop) * 0.5f, sideH = WindowTop - WindowSill;
            Window("Ventana_Der", root.transform, new Vector3(0.8f, sideY, -0.37f), Quaternion.Euler(0f, 90f, 0f), new Vector2(2.08f, sideH), window);
            Window("Ventana_Izq", root.transform, new Vector3(-0.8f, sideY, -0.37f), Quaternion.Euler(0f, -90f, 0f), new Vector2(2.08f, sideH), window);
            Window("Parabrisas", root.transform, new Vector3(0f, 1.47f, 0.74f), Quaternion.Euler(-12f, 0f, 0f), new Vector2(1.5f, 0.5f), window);
            Window("Luneta", root.transform, new Vector3(0f, 1.5f, -1.47f), Quaternion.Euler(0f, 180f, 0f), new Vector2(1.45f, 0.42f), window);

            // Taxista (billboard al volante; se lo ve de atrás desde el asiento trasero derecho)
            Sprite drive = LoadSprite("conduce_01", missing);
            Sprite talk1 = LoadSprite("habla_01", missing);
            Sprite talk2 = LoadSprite("habla_02", missing);
            var driverGo = new GameObject("Taxista");
            driverGo.transform.SetParent(root.transform, false);
            driverGo.transform.localPosition = new Vector3(-0.42f, 0.8f, 0.24f);
            driverGo.transform.localScale = Vector3.one * 0.47f; // lienzo de 2.05 m → ~0.96 m sentado
            SpriteRenderer driverRenderer = driverGo.AddComponent<SpriteRenderer>();
            driverRenderer.sprite = drive;
            if (drive == null)
            {
                // Placeholder: silueta oscura
                driverRenderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                driverGo.transform.localScale = new Vector3(0.5f, 0.9f, 1f);
            }
            LipSyncDriver lipSync = driverGo.AddComponent<LipSyncDriver>();
            LipSyncSpriteAdapter mouth = driverGo.AddComponent<LipSyncSpriteAdapter>();
            mouth.SetDriver(lipSync);

            // Barrido de faroles: pivote afuera a la derecha, mirando hacia adentro y abajo; la luz gira en Y local
            var pivot = new GameObject("Barrido_Pivote").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(2.2f, 2.6f, -0.3f);
            pivot.localRotation = Quaternion.Euler(28f, -90f, 0f);
            var sweepGo = new GameObject("Barrido_Farol");
            sweepGo.transform.SetParent(pivot, false);
            Light sweep = sweepGo.AddComponent<Light>();
            sweep.type = LightType.Spot;
            sweep.color = new Color(1f, 0.74f, 0.42f);
            sweep.range = 7f;
            sweep.spotAngle = 48f;
            sweep.intensity = 0f;
            sweep.shadows = LightShadows.Soft;

            // Techo: collider (capa TransparentFX) para que RainEmitter sepa que está bajo techo
            var roof = new GameObject("Techo_Collider") { layer = RoofLayer };
            roof.transform.SetParent(root.transform, false);
            roof.transform.localPosition = new Vector3(0f, 1.74f, -0.38f);
            roof.AddComponent<BoxCollider>().size = new Vector3(1.5f, 0.12f, 2.1f);
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Asiento de la cámara: atrás a la derecha
            var seatAnchor = new GameObject("Anchor_TaxiSeat").transform;
            seatAnchor.SetParent(root.transform, false);
            seatAnchor.localPosition = new Vector3(0.42f, SeatEyeHeight, -0.62f); // INTRO.2.1: antes y = 1.2

            // Motor (3D): frenada y arranque
            var engineGo = new GameObject("Motor");
            engineGo.transform.SetParent(root.transform, false);
            engineGo.transform.localPosition = new Vector3(0f, 0.8f, 1.4f);
            AudioSource engine = engineGo.AddComponent<AudioSource>();
            engine.playOnAwake = false;
            engine.spatialBlend = 1f;
            engine.minDistance = 3f;
            engine.maxDistance = 45f;
            engine.rolloffMode = AudioRolloffMode.Linear;
            engine.dopplerLevel = 0f;

            TaxiInterior interior = root.AddComponent<TaxiInterior>();
            interior.EditorSetup(sweep, driverRenderer, mouth, drive, talk1, talk2, wheels.ToArray(), 0.38f);

            CartoonKit.MakeNonStatic(root);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            report.AppendLine("✓ Prefab " + PrefabPath);
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// Carrocería de auto de los años 30 (local: +z adelante, origen en el piso). Submallas: 0 pintura, 1 negro, 2 blanco, 3 cromo.
    /// taxiTrim = letrero y franja a cuadros del taxi. N1.1: también la usan Car_Parked_30s y Car_Crashed_30s del kit de bordes.
    /// El orden de los números al azar no se cambia: el taxi sale idéntico.
    /// </summary>
    public static void AddCarBody(CartoonKit.KitMesh b, System.Random rng, bool taxiTrim)
    {
        float R(float a) => CartoonKit.Range(rng, -a, a);

        b.Submesh = 0; // pintura
        CartoonKit.AddBox(b, new Vector3(0f, 0.62f, -0.1f), new Vector3(1.72f, 0.42f, 3.9f), Quaternion.Euler(R(1f), 0f, R(1f)), 0.12f, 2);
        CartoonKit.AddBox(b, new Vector3(0f, 0.98f, 1.42f), new Vector3(1.12f, 0.46f, 1.55f), Quaternion.Euler(2.5f, R(1.5f), R(1.5f)), 0.16f, 2); // capó largo
        CartoonKit.AddBox(b, new Vector3(0f, 0.86f, -1.86f), new Vector3(1.55f, 0.5f, 0.75f), Quaternion.Euler(-4f, 0f, R(1f)), 0.18f, 2);  // baúl
        // puertas: de 0.81 al alféizar (INTRO.2.1: antes hasta 1.23)
        float doorH = WindowSill - 0.81f;
        CartoonKit.AddBox(b, new Vector3(0.84f, 0.81f + doorH * 0.5f, -0.35f), new Vector3(0.07f, doorH, 2.2f), Quaternion.Euler(0f, 0f, -3f), 0.03f, 1);
        CartoonKit.AddBox(b, new Vector3(-0.84f, 0.81f + doorH * 0.5f, -0.35f), new Vector3(0.07f, doorH, 2.2f), Quaternion.Euler(0f, 0f, 3f), 0.03f, 1);
        CartoonKit.AddBox(b, new Vector3(0f, 1.74f, -0.38f), new Vector3(1.62f, 0.09f, 2.3f), Quaternion.Euler(R(1.5f), 0f, R(2f)), 0.04f, 1); // techo
        if (taxiTrim)
            CartoonKit.AddBox(b, new Vector3(0f, 1.95f, 0.25f), new Vector3(0.56f, 0.2f, 0.14f), Quaternion.Euler(0f, R(4f), R(3f)), 0.05f, 1); // letrero
        b.Submesh = 1; // negro: guardabarros, estribos, parantes, paragolpes
        foreach (float sx in new[] { -1f, 1f })
        {
            foreach (float sz in new[] { -1f, 1f })
                CartoonKit.AddBox(b, new Vector3(sx * 0.86f, 0.74f, sz * 1.4f + (sz > 0 ? 0.05f : -0.05f)), new Vector3(0.42f, 0.34f, 1.05f),
                    Quaternion.Euler(sz * 6f + R(2f), 0f, sx * -4f), 0.16f, 2);
            CartoonKit.AddBox(b, new Vector3(sx * 0.95f, 0.42f, 0f), new Vector3(0.26f, 0.05f, 1.75f), Quaternion.Euler(0f, 0f, sx * 3f), 0.02f, 1);
            foreach (float pz in new[] { 0.72f, -0.38f, -1.46f })
                CartoonKit.AddBox(b, new Vector3(sx * 0.79f, (WindowSill + WindowTop) * 0.5f, pz), new Vector3(0.08f, WindowTop - WindowSill + 0.02f, 0.08f),
                    Quaternion.Euler(pz > 0 ? -12f : 0f, 0f, sx * 4f), 0.03f, 1); // parantes hasta el alféizar nuevo
        }
        CartoonKit.AddBox(b, new Vector3(0f, 0.55f, 2.3f), new Vector3(1.8f, 0.12f, 0.12f), Quaternion.Euler(0f, 0f, R(2f)), 0.05f, 1);
        CartoonKit.AddBox(b, new Vector3(0f, 0.55f, -2.3f), new Vector3(1.8f, 0.12f, 0.12f), Quaternion.Euler(0f, 0f, R(2f)), 0.05f, 1);
        b.Submesh = 2; // franja a cuadros (blanco; los negros salen del submesh 1 en los huecos)
        if (taxiTrim)
            foreach (float sx in new[] { -1f, 1f })
                for (int i = 0; i < 11; i++)
                {
                    if (i % 2 == 1) continue;
                    CartoonKit.AddBox(b, new Vector3(sx * 0.88f, 1.0f, -1.4f + i * 0.2f), new Vector3(0.02f, 0.1f, 0.2f), Quaternion.identity, 0.005f, 1);
                }
        b.Submesh = 3; // cromo: parrilla y faros
        CartoonKit.AddBox(b, new Vector3(0f, 0.98f, 2.2f), new Vector3(0.92f, 0.5f, 0.08f), Quaternion.Euler(4f, 0f, 0f), 0.04f, 1);
        foreach (float sx in new[] { -0.62f, 0.62f })
            CartoonKit.AddLathe(b, new Vector3(sx, 1.08f, 2.12f), Quaternion.Euler(90f, 0f, 0f), new[] { -0.08f, 0.02f, 0.08f }, new[] { 0.14f, 0.16f, 0.1f }, 12);
    }

    private static void Window(string name, Transform parent, Vector3 localPos, Quaternion localRot, Vector2 size, Material mat)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = localPos;
        quad.transform.localRotation = localRot;
        quad.transform.localScale = new Vector3(size.x, size.y, 1f);
        MeshRenderer r = quad.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static Material WindowMaterial(StringBuilder report, List<string> missing)
    {
        Shader shader = Shader.Find(WindowShader);
        if (shader == null || !shader.isSupported)
        {
            missing.Add(WindowShader + " (vidrio oscuro liso)");
            return LevelBuilderUtils.GetOrCreateMaterial("Kit_TaxiVidrio", new Color(0.08f, 0.1f, 0.16f), 0.6f, true);
        }
        LevelBuilderUtils.EnsureFolder(MaterialFolder);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(WindowMaterialPath);
        if (mat == null)
        {
            mat = new Material(shader) { name = "IntroTaxiVentana" };
            AssetDatabase.CreateAsset(mat, WindowMaterialPath);
        }
        else if (mat.shader != shader) mat.shader = shader;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Sprite LoadSprite(string name, List<string> missing)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TaxistaFolder + "/" + name + ".png");
        if (sprite == null) missing.Add("taxista/" + name);
        return sprite;
    }

    // ==================================================================
    // Escena
    // ==================================================================

    private static WaypointPath BuildPath(string name, Vector3[] points, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        for (int i = 0; i < points.Length; i++)
        {
            var p = new GameObject(name + "_" + i);
            p.transform.SetParent(go.transform, false);
            p.transform.position = points[i];
        }
        return go.AddComponent<WaypointPath>();
    }

    private static Transform Anchor(string name, Vector3 position, float yaw, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        return go.transform;
    }

    /// <summary>INTRO.3: caja trigger frente a la puerta (WeaponDrawTrigger). Capa Ignore Raycast: no tapa vistas ni balas.</summary>
    private static void BuildDrawTrigger(Transform parent)
    {
        var go = new GameObject("WeaponDraw_Puerta") { layer = 2 }; // Ignore Raycast
        go.transform.SetParent(parent, false);
        go.transform.position = DrawTriggerCenter;
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = DrawTriggerSize;
        go.AddComponent<WeaponDrawTrigger>();
    }

    /// <summary>
    /// INTRO.3: el Player.prefab (y el Player de la escena, si no fuera instancia) lleva PlayerWeaponHolster. Idempotente.
    /// </summary>
    private static void EnsurePlayerHolster(StringBuilder report)
    {
        string path = TestPrefabGenerator.PlayerPath;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            report.AppendLine("⚠ No existe " + path + ": no se pudo añadir PlayerWeaponHolster (corre Crear Prefabs de Prueba).");
        }
        else
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (contents.GetComponent<PlayerWeaponHolster>() == null)
                {
                    contents.AddComponent<PlayerWeaponHolster>();
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    report.AppendLine("✓ PlayerWeaponHolster añadido a " + path);
                }
                else report.AppendLine("✓ " + path + " ya tenía PlayerWeaponHolster");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        PlayerController player = Object.FindObjectOfType<PlayerController>(true);
        if (player != null && player.GetComponent<PlayerWeaponHolster>() == null)
        {
            player.gameObject.AddComponent<PlayerWeaponHolster>();
            EditorUtility.SetDirty(player.gameObject);
            report.AppendLine("✓ PlayerWeaponHolster añadido al Player de la escena (no era instancia del prefab)");
        }
    }

    /// <summary>Camión de reparto estacionado (primitivas del kit): estático, con collider (capa Default: tapa la vista).</summary>
    private static void BuildTruck(Transform parent)
    {
        Material green = LevelBuilderUtils.GetOrCreateMaterial("Kit_CamionVerde", new Color32(0x3E, 0x6B, 0x4A, 0xFF), 0.25f, true);
        Material black = LevelBuilderUtils.GetOrCreateMaterial("Kit_TaxiNegro", new Color32(0x1C, 0x1A, 0x1E, 0xFF), 0.3f, true);
        Material wood = CartoonKit.M("Madera");
        Mesh mesh = CartoonKit.GetMesh("Camion_Estacionado", b =>
        {
            b.Submesh = 0; // cabina y capó (verde)
            CartoonKit.AddBox(b, new Vector3(2.15f, 1.2f, 0f), new Vector3(1.3f, 1.4f, 2.1f), Quaternion.Euler(0f, 0f, -2f), 0.18f, 2);
            CartoonKit.AddBox(b, new Vector3(2.55f, 0.85f, 0f), new Vector3(0.9f, 0.7f, 1.5f), Quaternion.Euler(0f, 0f, 3f), 0.15f, 2);
            b.Submesh = 1; // caja de carga de madera
            CartoonKit.AddBox(b, new Vector3(-0.75f, 1.55f, 0f), new Vector3(3.9f, 2.2f, 2.35f), Quaternion.Euler(0f, 0f, 1.5f), 0.08f, 2);
            b.Submesh = 2; // ruedas y chasis
            CartoonKit.AddBox(b, new Vector3(0f, 0.42f, 0f), new Vector3(5.5f, 0.25f, 1.9f), Quaternion.identity, 0.05f, 1);
            foreach (float x in new[] { -1.9f, 1.95f })
                foreach (float z in new[] { -0.95f, 0.95f })
                    CartoonKit.AddLathe(b, new Vector3(x, 0.42f, z), Quaternion.Euler(90f, 0f, 0f), new[] { -0.14f, 0.14f }, new[] { 0.42f, 0.42f }, 14);
        });
        GameObject truck = CartoonKit.MeshObject("Camion_Estacionado", mesh, new[] { green, wood, black }, parent, TruckCenter, Quaternion.identity);
        BoxCollider box = truck.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, TruckSize.y * 0.5f, 0f);
        box.size = TruckSize;
        LevelBuilderUtils.MarkStaticRecursive(truck);
    }

    private static void BuildDirector(Transform root, GameObject taxiGo, WaypointPath arrive, WaypointPath exit, Transform curb,
        Transform house, TMP_FontAsset font)
    {
        // Franjas (orden 8: debajo del pensamiento de la IA, que va arriba en el canvas de orden 10)
        Canvas barsCanvas = IntroBookBuilder.OverlayCanvas("UI_Cine_Franjas", 8);
        barsCanvas.transform.SetParent(root, false);
        RectTransform top = Bar("Franja_Arriba", barsCanvas.transform);
        RectTransform bottom = Bar("Franja_Abajo", barsCanvas.transform);
        Letterbox letterbox = barsCanvas.gameObject.AddComponent<Letterbox>();
        IntroBookBuilder.SetRef(letterbox, "top", top);
        IntroBookBuilder.SetRef(letterbox, "bottom", bottom);

        // Subtítulos y cartel (orden 45)
        Canvas textCanvas = IntroBookBuilder.OverlayCanvas("UI_Cine_Texto", 45);
        textCanvas.transform.SetParent(root, false);
        RectTransform subRect = IntroBookBuilder.UiChild("Subtitulos", textCanvas.transform, new Vector2(0.1f, 0.015f), new Vector2(0.9f, 0.115f), Vector2.zero, Vector2.zero);
        CanvasGroup subGroup = subRect.gameObject.AddComponent<CanvasGroup>();
        subGroup.blocksRaycasts = false;
        Color cream = new Color32(0xF2, 0xE6, 0xC8, 0xFF);
        TMP_Text speaker = IntroBookBuilder.Label("Nombre", subRect, new Vector2(0f, 0.62f), Vector2.one, font, 26f, new Color32(0xE8, 0xB0, 0x4A, 0xFF));
        TMP_Text line = IntroBookBuilder.Label("Texto", subRect, Vector2.zero, new Vector2(1f, 0.66f), font, 30f, cream);
        line.enableWordWrapping = true;
        CinematicSubtitles subtitles = subRect.gameObject.AddComponent<CinematicSubtitles>();
        IntroBookBuilder.SetRef(subtitles, "group", subGroup);
        IntroBookBuilder.SetRef(subtitles, "speaker", speaker);
        IntroBookBuilder.SetRef(subtitles, "line", line);

        TMP_FontAsset titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Game/Fonts/IMFellEnglish SDF.asset");
        RectTransform titleRect = IntroBookBuilder.UiChild("Cartel_Titulo", textCanvas.transform, new Vector2(0.1f, 0.55f), new Vector2(0.9f, 0.85f), Vector2.zero, Vector2.zero);
        CanvasGroup titleGroup = titleRect.gameObject.AddComponent<CanvasGroup>();
        titleGroup.alpha = 0f;
        titleGroup.blocksRaycasts = false;
        TMP_Text title = IntroBookBuilder.Label("Titulo", titleRect, Vector2.zero, Vector2.one, titleFont != null ? titleFont : font, 150f, new Color32(0xF2, 0xC2, 0x4A, 0xFF));
        title.text = "PLOMO Y PAJA";
        title.enableWordWrapping = false;
        title.outlineWidth = 0.18f;
        title.outlineColor = new Color32(0x1A, 0x12, 0x0E, 0xFF);

        ScreenFade fade = IntroBookBuilder.BuildFade();
        fade.transform.root.SetParent(root, false);
        IntroBookBuilder.BuildSkipHint(font, out CanvasGroup hint, out Image radial);
        hint.transform.root.SetParent(root, false);

        // Director
        var go = new GameObject("IntroCinematic");
        go.transform.SetParent(root, false);
        HoldToSkip skip = go.AddComponent<HoldToSkip>();
        IntroBookBuilder.SetRef(skip, "hint", hint);
        IntroBookBuilder.SetRef(skip, "radialFill", radial);
        AudioSource taxistaVoice = Audio2D(go.transform, "Voz_Taxista");
        AudioSource wilburVoice = Audio2D(go.transform, "Voz_Wilbur");
        AudioSource ambience = Audio2D(go.transform, "Ambiente_Taxi");
        AudioSource wipers = Audio2D(go.transform, "Limpiaparabrisas");

        Transform seat = taxiGo.transform.Find("Anchor_TaxiSeat");
        Transform driver = taxiGo.transform.Find("Taxista");
        Transform engine = taxiGo.transform.Find("Motor");

        var director = go.AddComponent<IntroCinematicDirector>();
        var so = new SerializedObject(director);
        so.FindProperty("taxi").objectReferenceValue = taxiGo.transform;
        so.FindProperty("interior").objectReferenceValue = taxiGo.GetComponent<TaxiInterior>();
        so.FindProperty("seat").objectReferenceValue = seat;
        so.FindProperty("taxistaLipSync").objectReferenceValue = driver != null ? driver.GetComponent<LipSyncDriver>() : null;
        so.FindProperty("arrivePath").objectReferenceValue = arrive;
        so.FindProperty("exitPath").objectReferenceValue = exit;
        so.FindProperty("playerCurb").objectReferenceValue = curb;
        so.FindProperty("houseLook").objectReferenceValue = house;
        so.FindProperty("fade").objectReferenceValue = fade;
        so.FindProperty("letterbox").objectReferenceValue = letterbox;
        so.FindProperty("subtitles").objectReferenceValue = subtitles;
        so.FindProperty("titleCard").objectReferenceValue = titleGroup;
        so.FindProperty("skip").objectReferenceValue = skip;
        so.FindProperty("taxistaVoice").objectReferenceValue = taxistaVoice;
        so.FindProperty("wilburVoice").objectReferenceValue = wilburVoice;
        so.FindProperty("ambience").objectReferenceValue = ambience;
        so.FindProperty("wipers").objectReferenceValue = wipers;
        so.FindProperty("taxiEngine").objectReferenceValue = engine != null ? engine.GetComponent<AudioSource>() : null;

        SerializedProperty lines = so.FindProperty("taxiLines");
        lines.arraySize = TaxiLines.Length;
        for (int i = 0; i < TaxiLines.Length; i++)
            FillLine(lines.GetArrayElementAtIndex(i), TaxiLines[i].speaker, TaxiLines[i].id, TaxiLines[i].text, TaxiLines[i].taxista, TaxiLines[i].sfx);
        FillLine(so.FindProperty("houseLine"), "Wilbur", "wilbur_casa_01", HouseLineText, false, "");
        so.FindProperty("aiThought").stringValue = AiThought;
        so.FindProperty("curbStandYaw").floatValue = CurbStandYaw;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void FillLine(SerializedProperty p, string speaker, string id, string text, bool taxista, string sfx)
    {
        p.FindPropertyRelative("speaker").stringValue = speaker;
        p.FindPropertyRelative("voiceId").stringValue = id;
        p.FindPropertyRelative("text").stringValue = text;
        p.FindPropertyRelative("taxista").boolValue = taxista;
        p.FindPropertyRelative("sfx").stringValue = sfx;
    }

    private static RectTransform Bar(string name, Transform parent)
    {
        RectTransform rt = IntroBookBuilder.UiChild(name, parent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        Image image = rt.gameObject.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        return rt;
    }

    private static AudioSource Audio2D(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        return source;
    }

    // ==================================================================
    // Jugador, guardias, Build Settings, audio
    // ==================================================================

    /// <summary>El jugador (y el PlayerSpawn) quedan en la acera: así arranca un reintento sin cinemática (I6).</summary>
    private static void MovePlayerToCurb(StringBuilder report)
    {
        foreach (LevelMarker marker in Object.FindObjectsOfType<LevelMarker>(true))
            if (marker.Type == MarkerType.PlayerSpawn)
                marker.transform.SetPositionAndRotation(CurbPosition, Quaternion.Euler(0f, CurbYaw, 0f));

        PlayerController player = Object.FindObjectOfType<PlayerController>(true);
        if (player == null)
        {
            report.AppendLine("⚠ No hay Player en la escena (corre Generar Nivel Porky).");
            return;
        }
        player.transform.SetPositionAndRotation(CurbPosition, Quaternion.Euler(0f, CurbYaw, 0f));
        EditorUtility.SetDirty(player.transform);
        report.AppendLine($"✓ Player y PlayerSpawn en la acera {CurbPosition}, mirando la calle");
    }

    /// <summary>El patrullero del patio arranca detrás de la casa (no se ve desde la acera en la llegada).</summary>
    private static void SetYardPatrolStart(StringBuilder report)
    {
        GameObject guard = GameObject.Find("/--- ACTORS ---/Guard_Patrulla_A");
        if (guard == null || !guard.TryGetComponent(out EnemyAI ai))
        {
            report.AppendLine("⚠ No está Guard_Patrulla_A: no se cambió su punto de arranque.");
            return;
        }
        var so = new SerializedObject(ai);
        so.FindProperty("firstPatrolPoint").intValue = YardPatrolFirstPoint;
        so.ApplyModifiedPropertiesWithoutUndo();
        GameObject marker = GameObject.Find("/" + RootMarkers + "/Patrol_A_" + YardPatrolFirstPoint);
        if (marker != null) guard.transform.position = marker.transform.position;
        report.AppendLine($"✓ Guard_Patrulla_A arranca en Patrol_A_{YardPatrolFirstPoint} (detrás de la casa)");
    }

    /// <summary>Build Settings con el orden compartido (MenuPrincipal 0, Intro 1, NivelPorky 2). Ver BuildSceneOrder.</summary>
    public static void EnsureBuildOrder(StringBuilder report)
    {
        BuildSceneOrder.Ensure(report);
    }

    private static void CheckAudio(List<string> missing)
    {
        AudioLibrary library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AudioLibraryBuilder.LibraryPath);
        if (library == null)
        {
            missing.Add("AudioLibrary.asset (correr Reconstruir Librería de Audio)");
            return;
        }
        foreach (var l in TaxiLines)
            if (!library.ContainsVoice(l.id)) missing.Add(l.id);
        if (!library.ContainsVoice("wilbur_casa_01")) missing.Add("wilbur_casa_01");
        foreach (string id in AudioIds)
            if (!library.ContainsSfx(id)) missing.Add(id);
    }
}
