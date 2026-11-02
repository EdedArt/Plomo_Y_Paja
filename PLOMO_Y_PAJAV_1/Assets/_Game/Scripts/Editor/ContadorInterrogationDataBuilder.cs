using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// MENÚ: Plomo y Paja → Contador → Crear datos del interrogatorio (fase 3C.4)
/// Crea/actualiza Assets/_Game/Data/Interrogation_Contador.asset con TODAS las líneas del guion
/// (Docs/guiones/contador.md v1.2, §5) copiadas exactas, se lo asigna a CentavoInterrogation en Centavo.prefab y
/// lista los ids que no tienen clip en la librería de audio. Idempotente; no toca escenas.
/// </summary>
public static class ContadorInterrogationDataBuilder
{
    public const string DataFolder = "Assets/_Game/Data";
    public const string DataPath = DataFolder + "/Interrogation_Contador.asset";

    private const InterrogationLineSpeaker C = InterrogationLineSpeaker.Target;
    private const InterrogationLineSpeaker W = InterrogationLineSpeaker.Player;

    [MenuItem("Plomo y Paja/Contador/Crear datos del interrogatorio")]
    public static void BuildMenu()
    {
        if (EditorMenuGuard.BlockedInPlayMode()) return;
        var report = new StringBuilder();
        InterrogationData data = Build(report);
        Link(data, report);
        AssetDatabase.SaveAssets();
        Debug.Log("[Contador] Datos del interrogatorio:\n" + report);
        EditorUtility.DisplayDialog("Crear datos del interrogatorio", report.ToString(), "Entendido");
    }

    public static InterrogationData Build(StringBuilder report)
    {
        LevelBuilderUtils.EnsureFolder(DataFolder);
        var data = AssetDatabase.LoadAssetAtPath<InterrogationData>(DataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<InterrogationData>();
            AssetDatabase.CreateAsset(data, DataPath);
        }
        Fill(data);
        EditorUtility.SetDirty(data);
        report.AppendLine($"✓ {DataPath}: {data.lines.Length} líneas, {data.rounds.Length} rondas");
        ReportMissingClips(data, report);
        return data;
    }

    private static void Fill(InterrogationData d)
    {
        d.speakerName = "CENTAVO";
        d.npcName = "Centavo";
        d.initialEmotion = "scared";

        // registro_01: solo lo da Centavo (D1). Nombre y descripción del GDD; texto de la hoja según 1A_v.
        d.clueId = "registro_01";
        d.clueName = "Registro de deudas";
        d.clueDescription = "Hoja del libro de Centavo: Porky sacó 3.200 pesos de la caja fuerte de Don Lobo";
        d.clueFullText = "Hoja arrancada del libro de cuentas de Don Lobo. Al margen, una P. " +
                         "3.200 pesos y los intereses de la semana, sacados de la caja fuerte del jefe. Con fecha y todo.";

        d.lines = new[]
        {
            // Ronda 0
            L("contador_int_00", C, "¡No me remate, señor! ¡Yo no soy de los que pegan! Soy el contador. Números, solo números.", "scared"),
            L("wilbur_int_0A", W, "Habla rápido o la próxima no va al costado.", null, trust: -1),
            L("wilbur_int_0B", W, "Tranquilo. Si cooperas, sales de aquí con vida.", null, trust: +1),
            L("wilbur_int_0C", W, "¿Qué hace el contador de Don Lobo escondido en el patio de mi hermano?", null),
            L("contador_int_0A", C, "¡S-sí, señor! ¡Lo secuestraron! ¡Se lo llevaron a la fuerza, pataleando y todo!", "scared", lie: true),
            L("contador_int_0B", C, "G-gracias… ay… El jefe me mandó a revisar la casa. A ver si quedaba algo de valor para cobrarse.", "nervous"),
            L("contador_int_0C", C, "Vine por las cuentas, señor. Su hermano tiene una cuenta abierta con el jefe. Muy, muy abierta.", "talk"),

            // Ronda 1
            L("wilbur_int_1A", W, "¿Cuánto le debe Porky?", null),
            L("wilbur_int_1B", W, "¿Dónde tienen a mi hermano?", null),
            L("wilbur_int_1C", W, "Mírame a los ojos. Si me mientes, lo voy a saber.", null),
            L("contador_int_1A_v", C, "Tres mil doscientos pesos… y los intereses de la semana. Pero no es un préstamo, señor. Él los sacó de la caja fuerte del jefe. Aquí está anotado, con fecha y todo.",
                "talk", InterrogationCondition.TrustAtLeastZero, result: InterrogationLineResult.Helpful, clue: true),
            L("contador_int_1A_m", C, "¿Deber? ¡Nada, nada! Su hermano es un santo. El jefe es el malo, se lo juro.",
                "nervous", InterrogationCondition.TrustBelowZero, result: InterrogationLineResult.Misleading, lie: true),
            L("contador_int_1B_v", C, "En la guarida del jefe, al otro lado del campamento. Nadie le ha tocado un pelo… El señor Lobo solo quiere lo suyo.",
                "talk", InterrogationCondition.TrustAtLeastZero, result: InterrogationLineResult.Helpful),
            L("contador_int_1B_m", C, "Lo… lo tienen colgado de los pies en el sótano. Sí. Muy feo. Vaya rápido y no pregunte más.",
                "nervous", InterrogationCondition.TrustBelowZero, result: InterrogationLineResult.Misleading, lie: true),
            L("contador_int_1C", C, "¡Está bien, está bien! Nadie lo secuestró. Él solito tocó la puerta del jefe… y el jefe todavía está esperando su plata.",
                "scared", result: InterrogationLineResult.Helpful),

            // Ronda 2: súplica
            L("contador_int_2", C, "Ya le dije todo lo que sé… ay… Tengo esposa, tres cachorros… y una calculadora que todavía estoy pagando a plazos.", "plead"),

            // Decisión
            L("wilbur_dec_matar", W, "Las cuentas se saldan, Centavo.", null),
            L("contador_muerte_01", C, "¡Espere, todavía no he cuadrado el…!", "scared", cut: true),
            L("wilbur_dec_perdonar", W, "Lárgate. Y no vuelvas a esta casa.", null),
            L("contador_perdon_01", C, "¡Gracias, señor! ¡Que Dios se lo pague… con intereses bajitos!", "relief"),
            L("contador_perdon_02", C, "Tome… la hoja del libro. Yo no la vi, usted no me la dio.", "talk",
                InterrogationCondition.ClueNotGiven, clue: true),
        };

        d.rounds = new[]
        {
            new InterrogationRound
            {
                openingLineId = "contador_int_00",
                options = new[]
                {
                    O("wilbur_int_0A", "contador_int_0A"),
                    O("wilbur_int_0B", "contador_int_0B"),
                    O("wilbur_int_0C", "contador_int_0C"),
                },
            },
            new InterrogationRound
            {
                openingLineId = "",
                options = new[]
                {
                    O("wilbur_int_1A", "contador_int_1A_v", "contador_int_1A_m"),
                    O("wilbur_int_1B", "contador_int_1B_v", "contador_int_1B_m"),
                    O("wilbur_int_1C", "contador_int_1C"),
                },
            },
        };
        d.pleaLineId = "contador_int_2";

        d.killLabel = "Jalar el gatillo";
        d.spareLabel = "Dejarlo ir";
        d.killLineIds = new[] { "wilbur_dec_matar", "contador_muerte_01" };
        d.spareLineIds = new[] { "wilbur_dec_perdonar", "contador_perdon_01", "contador_perdon_02" };

        d.thoughtWho = "Centavo, el contador flaco y nervioso de Don Lobo, al que mandaron a la casa de Porky a ver si quedaba algo de valor para cobrarse la deuda.";
        d.thoughtTimeoutSeconds = 5f;
        d.fallbackThoughts = new[]
        {
            T("Un contador vivo lleva la cuenta de quién lo dejó ir. Uno muerto no le cuenta nada a nadie."),
            T("Tiembla como hoja. ¿Miente por miedo… o el miedo es lo único que no está fingiendo?"),
            T("Porky nunca supo sumar. ¿Y si este es el único que tiene las cuentas claras?"),
            T("Ya está en el suelo, Wilbur. Lo difícil no es disparar… es saber a quién le estás apretando el gatillo."),
            // Solo si dijo 1B_m, y entonces la mitad de las veces (fase 3C.4 punto 5)
            new InterrogationThought
            {
                text = "Dijo que lo colgaron de los pies. Guarda bien esas palabras… y compáralas después.",
                condition = InterrogationThoughtCondition.SaidLine,
                requiresLineId = "contador_int_1B_m",
                chance = 0.5f,
            },
        };

        // Pensamiento después de la decisión (guion §5, fase 3C.4.1). Los condicionados salen la mitad de las veces.
        d.afterThoughtTimeoutSeconds = 4f;
        d.spareThoughts = new[]
        {
            T("Se va cojeando con tus secretos en el bolsillo. Ojalá la gratitud le dure más que el miedo."),
            T("Dejaste ir al único que lleva las cuentas. Algún día alguien te va a cobrar este favor."),
            T("Un lobo menos en el suelo. ¿Fue piedad, Wilbur… o solo cansancio?"),
        };
        d.killThoughts = new[]
        {
            T("Las cuentas se saldaron. Lo que nadie te dijo es quién paga los intereses."),
            T("Ya no tiembla. Tú sí."),
            T("Un contador menos. Los números de Porky siguen sin cuadrar."),
            new InterrogationThought
            {
                text = "Murió mintiendo… o eso quieres creer, para que pese menos.",
                condition = InterrogationThoughtCondition.Lied,
                requiresLineId = "",
                chance = 0.5f,
            },
            new InterrogationThought
            {
                text = "Te dijo la verdad y le pagaste con plomo.",
                condition = InterrogationThoughtCondition.ToldTruth,
                requiresLineId = "",
                chance = 0.5f,
            },
        };
    }

    private static InterrogationLine L(string id, InterrogationLineSpeaker speaker, string text, string emotion,
        InterrogationCondition condition = InterrogationCondition.Always, int trust = 0,
        InterrogationLineResult result = InterrogationLineResult.None, bool lie = false, bool clue = false, bool cut = false)
    {
        return new InterrogationLine
        {
            id = id, speaker = speaker, text = text, emotion = emotion ?? "", condition = condition,
            trustDelta = trust, result = result, lie = lie, givesClue = clue, cutByShot = cut,
        };
    }

    private static InterrogationOption O(string playerLineId, params string[] responses)
        => new InterrogationOption { playerLineId = playerLineId, responseLineIds = responses };

    private static InterrogationThought T(string text) => new InterrogationThought
        { text = text, condition = InterrogationThoughtCondition.Always, requiresLineId = "", chance = 1f };

    private static void ReportMissingClips(InterrogationData data, StringBuilder report)
    {
        AudioLibrary library = null;
        foreach (string guid in AssetDatabase.FindAssets("t:AudioLibrary"))
        {
            library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AssetDatabase.GUIDToAssetPath(guid));
            if (library != null) break;
        }
        if (library == null)
        {
            report.AppendLine("⚠ No hay AudioLibrary.asset: no se pudieron comprobar los clips.");
            return;
        }

        var missing = new List<string>();
        foreach (InterrogationLine line in data.lines)
            if (!library.ContainsVoice(line.id)) missing.Add(line.id);
        if (missing.Count == 0) report.AppendLine($"✓ Las {data.lines.Length} líneas tienen clip");
        else report.AppendLine($"⚠ Sin clip ({missing.Count}): {string.Join(", ", missing)} — correr Reconstruir Librería de Audio; sin clip solo se ve el texto");
    }

    /// <summary>Asigna los datos a CentavoInterrogation de Centavo.prefab (edición en su lugar: conserva los IDs).</summary>
    public static void Link(InterrogationData data, StringBuilder report)
    {
        if (data == null) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ContadorCentavoBuilder.PrefabPath) == null)
        {
            report.AppendLine("⚠ Todavía no hay Centavo.prefab: correr Plomo y Paja → Contador → Colocar Centavo (lo enlaza solo).");
            return;
        }
        GameObject contents = PrefabUtility.LoadPrefabContents(ContadorCentavoBuilder.PrefabPath);
        try
        {
            if (!contents.TryGetComponent(out CentavoInterrogation link)) link = contents.AddComponent<CentavoInterrogation>();
            var so = new SerializedObject(link);
            so.FindProperty("data").objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, ContadorCentavoBuilder.PrefabPath);
            report.AppendLine("✓ Centavo.prefab usa estos datos (CentavoInterrogation)");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }
}
