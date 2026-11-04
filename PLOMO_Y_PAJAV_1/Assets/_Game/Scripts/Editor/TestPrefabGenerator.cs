using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Crea los prefabs de prueba (IA, guardias, jugador) para poder probar el juego en el Editor.
/// MENÚ: Plomo y Paja → Crear Prefabs de Prueba
///
/// Todo se hace por código (nada de YAML a mano). Volver a ejecutarlo regenera
/// AICompanion, Enemy_Guard y Enemy_Interrogable; Player.prefab solo se crea si falta
/// (se saca del objeto "Player" de SampleScene) y después solo se le completan componentes.
/// </summary>
public static class TestPrefabGenerator
{
    public const string PrefabFolder = "Assets/_Game/Prefabs";
    public const string AICompanionPath = PrefabFolder + "/AICompanion.prefab";
    public const string GuardPath = PrefabFolder + "/Enemy_Guard.prefab";
    public const string InterrogablePath = PrefabFolder + "/Enemy_Interrogable.prefab";
    public const string PlayerPath = PrefabFolder + "/Player.prefab";

    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    /// <summary>Fuente del globo de los guardias (EnemyBarks). Padding 10: deja sitio al contorno grueso.</summary>
    public const string BarkFontPath = "Assets/_Game/Fonts/LuckiestGuy SDF.asset";
    private static readonly string[] BarkFontSources = { "Assets/_Game/Fonts/LuckiestGuy-Regular.ttf" };
    private static readonly Color Cream = new Color32(0xE8, 0xE2, 0xD0, 0xFF);

    [MenuItem("Plomo y Paja/Crear Prefabs de Prueba")]
    public static void CreateAll()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsurePrefabs(true);
        EditorUtility.DisplayDialog("Prefabs de prueba", "Prefabs listos en " + PrefabFolder, "OK");
    }

    /// <summary>
    /// Crea los prefabs que falten (o todos si regenerate = true).
    /// Lo usa también el generador de nivel.
    /// </summary>
    public static void EnsurePrefabs(bool regenerate)
    {
        LevelBuilderUtils.EnsureFolder(PrefabFolder);
        CharacterLookBuilder.CreateAll(); // materiales sin iluminación de personajes (fase 3A2)
        // Los sets se rellenan con los PNG de Art/Characters ANTES de crear prefabs: así el guardia nunca
        // vuelve al placeholder por haber olvidado "Reconstruir Sets de Sprites" (fase 3A3)
        CharacterSpriteSetBuilder.RebuildAll();
        ViewmodelLayerSetup.EnsureLayer(); // capa "Viewmodel": la necesita el stack de cámaras de las manos (fase 1.2)

        if (regenerate || !File.Exists(AICompanionPath)) CreateAICompanionPrefab();
        if (regenerate || !File.Exists(GuardPath)) CreateGuardPrefab();
        if (regenerate || !File.Exists(InterrogablePath)) CreateInterrogablePrefab();
        UpdateGuardPrefab(new StringBuilder());
        EnsurePlayerPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Plomo y Paja/Enemigos/Actualizar guardias (combate y globo)")]
    public static void UpdateGuardsMenu()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        var report = new StringBuilder();
        UpdateGuardPrefab(report);
        AssetDatabase.SaveAssets();
        Debug.Log("[Prefabs] Guardias actualizados:\n" + report);
        EditorUtility.DisplayDialog("Actualizar guardias", report.ToString(), "OK");
    }

    /// <summary>
    /// Idempotente, sin regenerar el prefab: (1) dificultad del tiro de los guardias, cada valor solo si sigue en el
    /// de antes (un ajuste a mano en el Inspector no se pisa); (2) globo: bubbleHeight 2.3 → 2.0 con la misma regla y la
    /// fuente Luckiest Guy SDF (se crea si falta; sin el TTF el globo usa la fuente por defecto de TMP).
    /// Enemy_Interrogable es variante de Enemy_Guard: lo hereda.
    /// </summary>
    private static void UpdateGuardPrefab(StringBuilder report)
    {
        if (!File.Exists(GuardPath))
        {
            report.AppendLine("✗ No existe " + GuardPath + " (Crear Prefabs de Prueba)");
            return;
        }
        TMP_FontAsset barkFont = InterrogationUIBuilder.EnsureFont(report, BarkFontSources, BarkFontPath, "Luckiest Guy",
            InterrogationUIBuilder.RequiredCharacters(), 56, 10, 1024);

        GameObject contents = PrefabUtility.LoadPrefabContents(GuardPath);
        try
        {
            EnemyAI ai = contents.GetComponent<EnemyAI>();
            if (ai != null)
            {
                var so = new SerializedObject(ai);
                // Cadena de valores (original → 1.ª subida → demo): cualquiera de los anteriores pasa al último.
                // El aviso (aimTelegraph 0.45 s) y su sonido NO cambian en la subida de la demo.
                MigrateFloat(so, "rangedHitChance", report, 0.3f, 0.45f, 0.62f);
                MigrateFloat(so, "rangedHitChanceRunning", report, 0.15f, 0.25f, 0.38f);
                MigrateFloat(so, "rangedDamage", report, 8f, 10f, 15f);
                MigrateFloat(so, "aimTelegraph", report, 0.35f, 0.45f);
                MigrateVector2(so, "shotInterval", report, new Vector2(1.4f, 2.2f), new Vector2(1f, 1.6f), new Vector2(0.8f, 1.3f));
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EnemyBarks barks = contents.GetComponent<EnemyBarks>();
            if (barks != null)
            {
                var so = new SerializedObject(barks);
                MigrateFloat(so, "bubbleHeight", report, 2.3f, 2f);
                MigrateFloat(so, "textOutline", report, 0.28f, 0f); // texto negro: el contorno negro solo engrosaba la letra
                SerializedProperty fontProp = so.FindProperty("font");
                if (fontProp != null && barkFont != null && barkFont != TMP_Settings.defaultFontAsset)
                {
                    fontProp.objectReferenceValue = barkFont;
                    report.AppendLine("✓ Globo con Luckiest Guy");
                }
                else
                {
                    report.AppendLine("⚠ Globo sin Luckiest Guy: usa la fuente por defecto de TMP");
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(contents, GuardPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    /// <summary>
    /// Lleva un float serializado al ÚLTIMO valor de la cadena si todavía tiene uno de los anteriores (versiones viejas del
    /// código); cualquier otro valor es un ajuste a mano y no se pisa.
    /// </summary>
    private static void MigrateFloat(SerializedObject so, string property, StringBuilder report, params float[] chain)
    {
        SerializedProperty p = so.FindProperty(property);
        if (p == null || chain.Length < 2) return;
        float target = chain[chain.Length - 1];
        if (Mathf.Approximately(p.floatValue, target)) return;
        for (int i = 0; i < chain.Length - 1; i++)
        {
            if (!Mathf.Approximately(p.floatValue, chain[i])) continue;
            report.AppendLine($"✓ {property}: {p.floatValue} → {target}");
            p.floatValue = target;
            return;
        }
        report.AppendLine($"⚠ {property} = {p.floatValue} (ajustado a mano): no se toca");
    }

    private static void MigrateVector2(SerializedObject so, string property, StringBuilder report, params Vector2[] chain)
    {
        SerializedProperty p = so.FindProperty(property);
        if (p == null || chain.Length < 2) return;
        Vector2 target = chain[chain.Length - 1];
        if ((p.vector2Value - target).sqrMagnitude < 0.0001f) return;
        for (int i = 0; i < chain.Length - 1; i++)
        {
            if ((p.vector2Value - chain[i]).sqrMagnitude >= 0.0001f) continue;
            report.AppendLine($"✓ {property}: {p.vector2Value} → {target}");
            p.vector2Value = target;
            return;
        }
        report.AppendLine($"⚠ {property} = {p.vector2Value} (ajustado a mano): no se toca");
    }

    // ==================================================================
    // AICompanion: raíz con el script + Canvas hijo con el subtítulo
    // ==================================================================
    private static void CreateAICompanionPrefab()
    {
        var root = new GameObject("AICompanion");
        AICompanion companion = root.AddComponent<AICompanion>();

        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(root.transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10; // por debajo del diálogo (100)

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var subtitleGo = new GameObject("Subtitle", typeof(RectTransform));
        subtitleGo.transform.SetParent(canvasGo.transform, false);
        RectTransform rect = (RectTransform)subtitleGo.transform;
        // Pensamientos de Wilbur: arriba al centro, 90 px desde el borde (el diálogo va abajo)
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -90f);
        rect.sizeDelta = new Vector2(1200f, 120f);

        TextMeshProUGUI text = subtitleGo.AddComponent<TextMeshProUGUI>();
        text.fontSize = 32f;
        text.fontStyle = FontStyles.Italic;
        text.color = new Color32(0xF2, 0xE6, 0xC8, 0xFF);
        text.fontSharedMaterial = GetUnderlayMaterial(text.font, text.fontSharedMaterial);
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        text.text = "";
        CanvasGroup group = subtitleGo.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var so = new SerializedObject(companion);
        so.FindProperty("subtitleText").objectReferenceValue = text;
        so.FindProperty("subtitleGroup").objectReferenceValue = group;
        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAndDestroy(root, AICompanionPath);
    }

    // ==================================================================
    // Enemy_Guard: raíz en los pies, un solo CapsuleCollider, cuerpo y "nariz" solo visuales
    // ==================================================================
    private static void CreateGuardPrefab()
    {
        Material bodyMat = LevelBuilderUtils.GetOrCreateMaterial("Enemigo_Guardia", new Color(0.16f, 0.16f, 0.17f));
        Material noseMat = LevelBuilderUtils.GetOrCreateMaterial("Enemigo_Marca", new Color(0.55f, 0.15f, 0.1f));

        var root = new GameObject("Enemy_Guard");

        // El disparo de la escopeta usa Physics.Raycast sin capa y busca EnemyHealth en el padre,
        // así que basta con un collider en la raíz (capa Default, sin tag)
        CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 1f, 0f);
        capsule.radius = 0.4f;
        capsule.height = 2f;

        NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.4f;
        agent.height = 2f;
        agent.speed = 2f;

        root.AddComponent<EnemyHealth>(); // canSurrender = false por defecto
        EnemyAI ai = root.AddComponent<EnemyAI>();

        // Todo lo visible cuelga de "Visual" (sin collider): EnemyCombatFx lo hace volar al morir
        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(visual.transform, false);
        body.transform.localPosition = new Vector3(0f, 1f, 0f);
        body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

        // "Cabeza" cuadrada adelantada para ver hacia dónde mira
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Head";
        head.transform.SetParent(visual.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.55f, 0.35f);
        head.transform.localScale = new Vector3(0.3f, 0.3f, 0.5f);
        Object.DestroyImmediate(head.GetComponent<Collider>());
        head.GetComponent<MeshRenderer>().sharedMaterial = noseMat;

        // Sprite 2D del set "guardia": oculto (y placeholder visible) mientras el set esté vacío
        var spriteHolder = new GameObject("SpriteHolder");
        spriteHolder.transform.SetParent(visual.transform, false);
        spriteHolder.AddComponent<SpriteRenderer>();
        BillboardSprite billboard = spriteHolder.AddComponent<BillboardSprite>();
        var billboardSo = new SerializedObject(billboard);
        billboardSo.FindProperty("spriteSet").objectReferenceValue = CharacterSpriteSetBuilder.GetOrCreateSet("guardia");
        SerializedProperty placeholders = billboardSo.FindProperty("placeholderRenderers");
        placeholders.arraySize = 2;
        placeholders.GetArrayElementAtIndex(0).objectReferenceValue = body.GetComponent<MeshRenderer>();
        placeholders.GetArrayElementAtIndex(1).objectReferenceValue = head.GetComponent<MeshRenderer>();
        billboardSo.FindProperty("anchorFeet").boolValue = false;       // el importador ya pone el pivote en los pies
        billboardSo.FindProperty("flipWithMovement").boolValue = false; // sombra pintada: nunca se voltea
        billboardSo.ApplyModifiedPropertiesWithoutUndo();

        // Goma de Edgar en el mismo objeto que el sprite: su origen son los pies (raíz en los pies + pivote del
        // importador), así que estira/aplasta desde el piso y el ancho va en el eje de pantalla del billboard.
        // Escucha sola a EnemyAI.OnStateChanged y EnemyHealth.OnDamaged (los busca en los padres).
        spriteHolder.AddComponent<RubberHoseAnimator>();

        var aiSo = new SerializedObject(ai);
        aiSo.FindProperty("billboardSprite").objectReferenceValue = billboard;
        aiSo.ApplyModifiedPropertiesWithoutUndo();

        // HUD.2 (H9/H18): pistola; el tiro sale del cañón del dibujo (puntos en Data/MuzzlePoints/Muzzle_guardia)
        EnemyGunfire gunfire = root.AddComponent<EnemyGunfire>();
        MuzzlePointsBuilder.ConfigureGunfire(gunfire, "guardia");
        var gunfireSo = new SerializedObject(gunfire);
        gunfireSo.FindProperty("spriteRenderer").objectReferenceValue = spriteHolder.GetComponent<SpriteRenderer>();
        gunfireSo.ApplyModifiedPropertiesWithoutUndo();

        root.AddComponent<EnemyCombatFx>();
        root.AddComponent<EnemyBarks>();

        SaveAndDestroy(root, GuardPath);
    }

    // ==================================================================
    // Enemy_Interrogable: variante del guardia que se rinde y se puede interrogar
    // ==================================================================
    private static void CreateInterrogablePrefab()
    {
        if (!File.Exists(GuardPath)) CreateGuardPrefab();

        Material brown = LevelBuilderUtils.GetOrCreateMaterial("Enemigo_Interrogable", new Color(0.24f, 0.15f, 0.09f));

        var guardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GuardPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(guardPrefab);
        instance.name = "Enemy_Interrogable";

        var healthSo = new SerializedObject(instance.GetComponent<EnemyHealth>());
        healthSo.FindProperty("canSurrender").boolValue = true;
        healthSo.ApplyModifiedPropertiesWithoutUndo();

        // Se queda quieto hasta ver al jugador: así se le puede encontrar y rendir
        var aiSo = new SerializedObject(instance.GetComponent<EnemyAI>());
        aiSo.FindProperty("enemyType").enumValueIndex = (int)EnemyAI.EnemyType.Centinela;
        aiSo.ApplyModifiedPropertiesWithoutUndo();

        instance.AddComponent<DialogueNPC>();
        instance.transform.Find("Visual/Body").GetComponent<MeshRenderer>().sharedMaterial = brown;

        // Guardar una instancia de otro prefab como asset nuevo crea una variante
        SaveAndDestroy(instance, InterrogablePath);
    }

    // ==================================================================
    // Player: sale del objeto "Player" de SampleScene y se completa si falta algo
    // ==================================================================
    private static void EnsurePlayerPrefab()
    {
        if (!File.Exists(PlayerPath))
        {
            if (!File.Exists(SampleScenePath))
            {
                Debug.LogWarning($"[Prefabs] No existe {SampleScenePath}; no se puede crear Player.prefab.");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(SampleScenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Additive);

            GameObject player = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Player") { player = root; break; }
            }

            if (player == null)
            {
                Debug.LogWarning("[Prefabs] No hay un objeto raíz 'Player' en SampleScene; no se crea Player.prefab.");
            }
            else
            {
                PrefabUtility.SaveAsPrefabAssetAndConnect(player, PlayerPath, InteractionMode.AutomatedAction);
                EditorSceneManager.SaveScene(scene); // el objeto de la escena queda conectado al prefab
            }

            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            if (player == null) return;
        }

        CompletePlayerPrefab();
    }

    /// <summary>
    /// Fase 3C.5 (idempotente): GameObject "Revolver" (RevolverController + AudioSource, componente apagado: sin dueño hasta
    /// recoger el de Centavo) junto a la escopeta, WeaponSwitcher en el Player con los dos perfiles, y la dispersión de la
    /// escopeta × 1.5 (D14) solo si sigue en el valor viejo (5): un ajuste a mano en el Inspector no se pisa.
    /// 3C.6: la reserva de la escopeta pasa de 18 a 24 con la misma regla (solo si sigue en 18).
    /// </summary>
    private static void EnsureRevolverAndSwitcher(GameObject contents, Camera cam, ShotgunController shotgun)
    {
        if (shotgun != null)
        {
            var shotgunSo = new SerializedObject(shotgun);
            SerializedProperty spread = shotgunSo.FindProperty("spreadAngle");
            if (Mathf.Approximately(spread.floatValue, 5f)) spread.floatValue = 7.5f;
            MigrateInt(shotgunSo, "reserveAmmo", 18, 24); // 3C.6: AUDIT_contador_3C5, opción (a)
            shotgunSo.ApplyModifiedPropertiesWithoutUndo();
        }

        RevolverController revolver = contents.GetComponentInChildren<RevolverController>(true);
        if (revolver == null)
        {
            Transform parent = cam != null ? cam.transform : contents.transform;
            var revolverGo = new GameObject("Revolver");
            revolverGo.transform.SetParent(parent, false);
            revolverGo.transform.localPosition = new Vector3(0.25f, -0.25f, 0.5f);
            revolver = revolverGo.AddComponent<RevolverController>();
            AudioSource audio = revolverGo.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
        }
        revolver.enabled = false; // WeaponSwitcher lo enciende cuando es el arma activa
        var revolverSo = new SerializedObject(revolver);
        if (cam != null) revolverSo.FindProperty("playerCamera").objectReferenceValue = cam;
        // 3C.5.2: tambor de 5 + 15 de reserva. Solo se pisa si sigue con los valores de la 3C.5 (6 / 6 / 12)
        MigrateInt(revolverSo, "magazineSize", 6, 5);
        MigrateInt(revolverSo, "loadedRounds", 6, 5);
        MigrateInt(revolverSo, "reserveRounds", 12, 15);
        revolverSo.ApplyModifiedPropertiesWithoutUndo();

        WeaponSwitcher switcher = contents.GetComponent<WeaponSwitcher>();
        if (switcher == null) switcher = contents.AddComponent<WeaponSwitcher>();
        var switcherSo = new SerializedObject(switcher);
        switcherSo.FindProperty("shotgun").objectReferenceValue = shotgun;
        switcherSo.FindProperty("revolver").objectReferenceValue = revolver;
        switcherSo.FindProperty("shotgunProfile").objectReferenceValue = ViewmodelProfileBuilder.GetOrCreateShotgunProfile();
        switcherSo.FindProperty("revolverProfile").objectReferenceValue = ViewmodelProfileBuilder.GetOrCreateRevolverProfile();
        MigrateInt(switcherSo, "revolverLoaded", 6, 5);
        MigrateInt(switcherSo, "revolverReserve", 12, 15);
        switcherSo.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Cambia un entero serializado solo si todavía tiene el valor viejo (un ajuste a mano no se pisa).</summary>
    private static void MigrateInt(SerializedObject so, string property, int oldValue, int newValue)
    {
        SerializedProperty p = so.FindProperty(property);
        if (p != null && p.intValue == oldValue) p.intValue = newValue;
    }

    /// <summary>
    /// SampleScene solo trae movimiento y cámara: se añade lo que falta para poder probar
    /// (vida, escopeta) y se activa el post-procesado de la cámara. No se quita nada.
    /// </summary>
    private static void CompletePlayerPrefab()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            if (contents.GetComponent<PlayerHealth>() == null)
                contents.AddComponent<PlayerHealth>();

            // HUD.1 (H4): la reserva de munición vive en un solo lugar (escopeta 24 / tope 30, revólver 0 / tope 24)
            PlayerAmmo playerAmmo = contents.GetComponent<PlayerAmmo>();
            if (playerAmmo == null)
                playerAmmo = contents.AddComponent<PlayerAmmo>();
            var ammoSo = new SerializedObject(playerAmmo);
            MigrateInt(ammoSo, "shotgunReserve", 18, 24); // 3C.6: reserva de la escopeta 18 → 24 (solo si sigue en 18)
            ammoSo.ApplyModifiedPropertiesWithoutUndo();

            // Apuntar con clic derecho (viewmodel, dispersión, velocidad, FOV y mira)
            if (contents.GetComponent<PlayerAim>() == null)
                contents.AddComponent<PlayerAim>();

            // Mira rubber-hose del HUD (construye su propio canvas Overlay; UIManager no existe en las escenas)
            if (contents.GetComponent<RubberHoseCrosshair>() == null)
                contents.AddComponent<RubberHoseCrosshair>();

            // INTRO.3: arranque sin arma tras el taxi (sin cinemática empieza lista: no cambia nada en otras escenas)
            if (contents.GetComponent<PlayerWeaponHolster>() == null)
                contents.AddComponent<PlayerWeaponHolster>();

            // Mantiene cargada la librería de audio (sin referencia no entraría en el build)
            AudioLibraryLoader loader = contents.GetComponent<AudioLibraryLoader>();
            if (loader == null) loader = contents.AddComponent<AudioLibraryLoader>();
            var loaderSo = new SerializedObject(loader);
            loaderSo.FindProperty("library").objectReferenceValue = AudioLibraryBuilder.GetOrCreateLibrary();
            loaderSo.ApplyModifiedPropertiesWithoutUndo();

            Camera cam = contents.GetComponentInChildren<Camera>();
            if (cam != null)
            {
                cam.gameObject.tag = "MainCamera";
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

                // Manos en su propia cámara Overlay (URP camera stacking): nada del mundo se dibuja encima. La cámara principal
                // deja de dibujar la capa Viewmodel y el near clip se queda como está en el prefab.
                if (ViewmodelCameraRig.Ensure(cam) == null)
                    Debug.LogWarning("[Viewmodel] No se pudo armar la ViewmodelCamera (¿falta la capa 'Viewmodel'?).");

                // Sin skybox el fondo quedaba negro: cielo nocturno índigo (fase 3A)
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color32(0x2A, 0x26, 0x55, 0xFF);

                // Manos con escopeta en 2D: perfil VM_Shotgun con los PNG de Art/Viewmodel (sin cuadros no se ven)
                FirstPersonViewmodel viewmodel = cam.GetComponent<FirstPersonViewmodel>();
                if (viewmodel == null) viewmodel = cam.gameObject.AddComponent<FirstPersonViewmodel>();
                var viewmodelSo = new SerializedObject(viewmodel);
                viewmodelSo.FindProperty("profile").objectReferenceValue = ViewmodelProfileBuilder.GetOrCreateShotgunProfile();
                viewmodelSo.ApplyModifiedPropertiesWithoutUndo();

                // Luz de relleno suave que acompaña al jugador: nunca hay negro total delante
                if (cam.transform.Find("FillLight") == null)
                {
                    var fillGo = new GameObject("FillLight");
                    fillGo.transform.SetParent(cam.transform, false);
                    Light fill = fillGo.AddComponent<Light>();
                    fill.type = LightType.Point;
                    fill.range = 6f;
                    fill.intensity = 0.35f;
                    fill.shadows = LightShadows.None;
                    fill.color = new Color32(0xFF, 0xE9, 0xC7, 0xFF);
                }
            }

            if (contents.GetComponentInChildren<ShotgunController>() == null)
            {
                Transform parent = cam != null ? cam.transform : contents.transform;
                var shotgunGo = new GameObject("Shotgun");
                shotgunGo.transform.SetParent(parent, false);
                shotgunGo.transform.localPosition = new Vector3(0.25f, -0.25f, 0.5f);

                ShotgunController shotgun = shotgunGo.AddComponent<ShotgunController>();
                AudioSource audio = shotgunGo.AddComponent<AudioSource>();
                audio.playOnAwake = false;

                if (cam != null)
                {
                    var so = new SerializedObject(shotgun);
                    so.FindProperty("playerCamera").objectReferenceValue = cam;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // Impacto caricaturesco (fase 3A, parte 3)
            ShotgunController gun = contents.GetComponentInChildren<ShotgunController>();
            if (gun != null && gun.GetComponent<ShotgunFeedback>() == null)
                gun.gameObject.AddComponent<ShotgunFeedback>();
            EnsureCartoonFx(contents);

            // Fase 3C.5: revólver de Centavo (segunda arma) + cambio de armas + balance de la escopeta
            EnsureRevolverAndSwitcher(contents, cam, gun);

            // Lluvia ligera que sigue a la cámara (se apaga bajo techo)
            RainEmitter rain = contents.GetComponent<RainEmitter>();
            if (rain == null) rain = contents.AddComponent<RainEmitter>();
            var rainSo = new SerializedObject(rain);
            rainSo.FindProperty("material").objectReferenceValue = RainBuilder.GetRainMaterial();
            rainSo.ApplyModifiedPropertiesWithoutUndo();

            // Pasos madera/barro según haya techo (usa el estado de RainEmitter)
            if (contents.GetComponent<PlayerFootsteps>() == null) contents.AddComponent<PlayerFootsteps>();

            PrefabUtility.SaveAsPrefabAsset(contents, PlayerPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    /// <summary>Copia del material TMP con sombra oscura (underlay), guardada como asset para que sobreviva en el prefab.</summary>
    private static Material GetUnderlayMaterial(TMP_FontAsset font, Material fallback)
    {
        if (font == null || fallback == null) return fallback;
        LevelBuilderUtils.EnsureFolder(LevelBuilderUtils.MaterialFolder);
        string path = LevelBuilderUtils.MaterialFolder + "/Pensamiento_Underlay.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        mat = new Material(fallback) { name = "Pensamiento_Underlay" };
        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.85f));
        mat.SetFloat("_UnderlayOffsetX", 0.6f);
        mat.SetFloat("_UnderlayOffsetY", -0.6f);
        mat.SetFloat("_UnderlayDilate", 0.4f);
        mat.SetFloat("_UnderlaySoftness", 0.3f);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void EnsureCartoonFx(GameObject playerRoot)
    {
        CartoonFx fx = playerRoot.GetComponent<CartoonFx>();
        if (fx == null) fx = playerRoot.AddComponent<CartoonFx>();

        var so = new SerializedObject(fx);
        so.FindProperty("bloodMaterial").objectReferenceValue = FxMaterial("Fx_Sangre", CartoonFx.BloodColor, false);
        so.FindProperty("decalMaterial").objectReferenceValue = FxMaterial("Fx_Mancha", CartoonFx.BloodColor, true);
        AssignDecalTextures(so.FindProperty("decalTextures"));
        so.FindProperty("smokeMaterial").objectReferenceValue = FxMaterial("Fx_Humo", new Color32(0xE6, 0xE6, 0xE6, 0xFF), false);
        so.FindProperty("starMaterial").objectReferenceValue = FxMaterial("Fx_Estrella", new Color32(0xFF, 0xD2, 0x3F, 0xFF), false);
        so.FindProperty("toothMaterial").objectReferenceValue = FxMaterial("Fx_Diente", new Color32(0xFF, 0xFA, 0xE8, 0xFF), false);
        so.FindProperty("flashMeshMaterial").objectReferenceValue = FxMaterial("Fx_DestelloMalla", Color.white, false);
        so.FindProperty("flashSpriteMaterial").objectReferenceValue = SpriteFlashMaterial();
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Formas de mancha para CartoonFx: sangre_01..04 de Art/Characters/fx. Las que falten se saltan
    // (sin ninguna, CartoonFx vuelve al quad de color plano).
    private static void AssignDecalTextures(SerializedProperty prop)
    {
        if (prop == null) return;
        var found = new System.Collections.Generic.List<Texture2D>();
        for (int i = 1; i <= 4; i++)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/_Game/Art/Characters/fx/sangre_{i:00}.png");
            if (tex != null) found.Add(tex);
        }
        prop.arraySize = found.Count;
        for (int i = 0; i < found.Count; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
    }

    private static Material FxMaterial(string name, Color color, bool transparent)
    {
        Material mat = LevelBuilderUtils.GetOrCreateMaterial(name, color, 0f, true);
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit != null) mat.shader = unlit;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (transparent)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material SpriteFlashMaterial()
    {
        string path = LevelBuilderUtils.MaterialFolder + "/Fx_DestelloSprite.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;
        mat = new Material(shader) { name = "Fx_DestelloSprite" };
        mat.SetColor("_Color", new Color(4f, 4f, 4f, 1f)); // sobreexpuesto: el sprite se ve blanco
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void SaveAndDestroy(GameObject go, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }
}
