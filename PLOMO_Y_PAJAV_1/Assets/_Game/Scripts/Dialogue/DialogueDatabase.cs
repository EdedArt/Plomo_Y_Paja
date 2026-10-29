using System.Collections.Generic;

/// <summary>
/// Árbol de interrogatorio de un guardia. Datos en C# (sin .asset) para que los merges sean fáciles.
/// </summary>
public class DialogueTree
{
    public string npcId;
    public string displayName;
    public string startNodeId;
    public DialogueNode[] nodes;

    public DialogueNode GetNode(string id)
    {
        foreach (var node in nodes)
            if (node.id == id) return node;
        return null;
    }
}

/// <summary>
/// Una línea del NPC. Sin opciones = nodo final (usa endResult y awardClueId).
/// </summary>
public class DialogueNode
{
    public string id;
    public string npcLine;
    public DialogueOption[] options = new DialogueOption[0];
    public InterrogationResult endResult = InterrogationResult.Refused;
    public string awardClueId;

    public bool IsEnd => options == null || options.Length == 0;
}

/// <summary>
/// Respuesta del jugador que lleva a otro nodo.
/// </summary>
public class DialogueOption
{
    public string text;
    public string nextNodeId;
}

/// <summary>
/// Todos los interrogatorios del juego.
/// REGLA DE ESCRITURA: máximo 3 nodos por camino, 2-3 opciones por nodo,
/// líneas del NPC de ~25 palabras, tono gángster de los años 30.
/// Nunca contar la verdad completa: solo pistas.
///
/// USO: DialogueDatabase.Get("guardia_porky")
/// </summary>
public static class DialogueDatabase
{
    private static readonly Dictionary<string, DialogueTree> Trees = new Dictionary<string, DialogueTree>();

    static DialogueDatabase()
    {
        Add(BuildColmillo());
        Add(BuildTuerto());
        Add(BuildContable());
    }

    /// <summary>Devuelve el árbol del npcId, o null si no existe.</summary>
    public static DialogueTree Get(string npcId)
    {
        if (string.IsNullOrEmpty(npcId)) return null;
        Trees.TryGetValue(npcId, out DialogueTree tree);
        return tree;
    }

    private static void Add(DialogueTree tree) => Trees[tree.npcId] = tree;

    // ============ Helpers para escribir los árboles cortos ============
    private static DialogueNode Line(string id, string npcLine, params DialogueOption[] options)
        => new DialogueNode { id = id, npcLine = npcLine, options = options };

    private static DialogueNode End(string id, string npcLine, InterrogationResult result, string clueId = null)
        => new DialogueNode { id = id, npcLine = npcLine, endResult = result, awardClueId = clueId };

    private static DialogueOption Opt(string text, string next)
        => new DialogueOption { text = text, nextNodeId = next };

    // ==================================================================
    // Nivel 1 — Colmillo: lobo joven y nervioso. Le teme más a Don Lobo que a Wilbur.
    // ==================================================================
    private static DialogueTree BuildColmillo() => new DialogueTree
    {
        npcId = "guardia_porky",
        displayName = "Colmillo",
        startNodeId = "inicio",
        nodes = new[]
        {
            Line("inicio", "¡N-no dispare, señor! Yo solo cuido la puerta. No sé nada de nada, se lo juro por mi madrecita.",
                Opt("Habla o te dejo como un colador.", "amenaza"),
                Opt("Tranquilo, chico. Contesta y te dejo ir.", "trato"),
                Opt("¿Para quién trabajas? ¿Don Lobo?", "jefe")),

            Line("amenaza", "Dispare si quiere, señor. Lo que Don Lobo les hace a los soplones es mucho, mucho peor.",
                Opt("Te lo pregunto por última vez.", "fin_negado"),
                Opt("Como quieras. Tú te lo pierdes.", "fin_negado")),
            End("fin_negado", "No diré ni pío. Haga lo que tenga que hacer, pero hágalo rápido, por favor.",
                InterrogationResult.Refused),

            Line("trato", "¿De verdad me deja ir? Mire... su hermanito le debía plata al jefe. Mucha plata, señor.",
                Opt("¿Y se lo llevaron a la fuerza?", "fin_ayuda_rastras"),
                Opt("¿Cuánto debía?", "fin_ayuda_deuda")),
            End("fin_ayuda_rastras", "Eso es lo raro, señor. Nadie se lo llevó a rastras. Salió por esa puerta por su propio pie.",
                InterrogationResult.Helpful),
            End("fin_ayuda_deuda", "No sé de cifras. Pero nadie se lo llevó a rastras, eso se lo aseguro. Ahora déjeme largarme.",
                InterrogationResult.Helpful),

            Line("jefe", "¿Don Lobo? Ja. Don Lobo desayuna cerdos, señor. Con huevos y tostadas. Su hermano ya es tocino.",
                Opt("Mientes.", "fin_engano"),
                Opt("¿Dónde lo tienen?", "fin_engano")),
            End("fin_engano", "Créame o no. Llega tarde, señor. Mejor dé media vuelta y vuelva a casa antes de acabar en la sartén.",
                InterrogationResult.Misleading),
        }
    };

    // ==================================================================
    // Nivel 2 — Tuerto: veterano cínico. Respeta el respeto; desprecia las amenazas.
    // ==================================================================
    private static DialogueTree BuildTuerto() => new DialogueTree
    {
        npcId = "guardia_barracon",
        displayName = "Tuerto",
        startNodeId = "inicio",
        nodes = new[]
        {
            Line("inicio", "Vaya, vaya. Un cerdo con escopeta. Veinte años en el negocio y nunca había visto algo tan ridículo.",
                Opt("Solo quiero hablar. Con respeto.", "calma"),
                Opt("Habla, tuerto, o te quito el otro ojo.", "amenaza"),
                Opt("Cincuenta billetes por la verdad.", "soborno")),

            Line("calma", "Respeto. Hace años que nadie me ofrece eso. Pregunte, cerdo, antes de que me arrepienta.",
                Opt("¿Conocías a mi hermano Porky?", "fin_ayuda_visitas"),
                Opt("¿Qué quiere Don Lobo con él?", "fin_ayuda_socios")),
            End("fin_ayuda_visitas", "Lo vi entrar al despacho del jefe varias veces, mucho antes de todo esto. Salían riéndose, como socios.",
                InterrogationResult.Helpful),
            End("fin_ayuda_socios", "Pregúntese por qué se veían tan seguido. Esos dos eran socios, cerdo. Hasta que dejaron de serlo.",
                InterrogationResult.Helpful),

            Line("amenaza", "Amenazas. Qué original. Está bien, se lo digo: su hermanito duerme en el fondo del río.",
                Opt("¡Mientes!", "fin_engano"),
                Opt("¿Quién lo hizo?", "fin_engano")),
            End("fin_engano", "Con zapatos de cemento, como manda la tradición. Vaya a llorarle al río, a ver si le contesta.",
                InterrogationResult.Misleading),

            Line("soborno", "¿Cincuenta? Con eso no compra ni mi ojo de vidrio. Pruebe con algo mejor, o con un chiste.",
                Opt("¿Sabes el del lobo y los tres cerditos?", "fin_negado"),
                Opt("Cien, entonces.", "fin_negado")),
            End("fin_negado", "Ni chistes ni billetes, cerdo. Este viejo lobo no está en venta. Se acabó la charla.",
                InterrogationResult.Refused),
        }
    };

    // ==================================================================
    // Nivel 2 — Contable: lobo de los números. Los libros son lo único que respeta.
    // ==================================================================
    private static DialogueTree BuildContable() => new DialogueTree
    {
        npcId = "guardia_registro",
        displayName = "Contable",
        startNodeId = "inicio",
        nodes = new[]
        {
            Line("inicio", "¡Cuidado con los papeles, por favor! Yo solo soy el contable. Números, señor. Únicamente números.",
                Opt("Háblame del dinero. ¿Qué dicen tus libros?", "libros"),
                Opt("¿Dónde está Porky?", "donde"),
                Opt("Suelta todo o quemo tus libros contigo.", "amenaza")),

            Line("libros", "Los libros nunca mienten, señor. Cada billete que entra y sale queda anotado con mi mejor letra.",
                Opt("¿Falta dinero?", "fin_ayuda"),
                Opt("¿Aparece mi hermano en ellos?", "fin_ayuda")),
            End("fin_ayuda", "Faltan 4.300 billetes, justo la semana en que Porky desapareció. Una sola inicial al margen: P. Llévese la hoja.",
                InterrogationResult.Helpful, "registro_01"),

            Line("donde", "¿El muchacho? Lo trasladaron al almacén del puerto, creo. O al sótano del club. No me pagan por saber.",
                Opt("¿Cuál de los dos?", "fin_engano"),
                Opt("No me mientas.", "fin_engano")),
            End("fin_engano", "El puerto, seguro. Muelle siete. Corra, señor, antes de que lo muevan otra vez.",
                InterrogationResult.Misleading),

            Line("amenaza", "¿Mis libros? ¡Jamás! Prefiero sus balas a que Don Lobo encuentre sus cuentas hechas ceniza.",
                Opt("Tú lo pediste.", "fin_negado"),
                Opt("Piénsalo bien, contable.", "fin_negado")),
            End("fin_negado", "Mi boca está cerrada como caja fuerte, señor. Y la combinación solo la tiene Don Lobo.",
                InterrogationResult.Refused),
        }
    };
}
