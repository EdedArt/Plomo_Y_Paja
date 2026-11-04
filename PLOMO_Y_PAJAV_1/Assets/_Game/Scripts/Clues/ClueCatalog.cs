using System.Collections.Generic;

/// <summary>Una pista del juego: textos (guion de pistas §2) y nombres de sus dibujos.</summary>
public sealed class ClueInfo
{
    public readonly string Id;
    /// <summary>Título de la hoja y del aviso "Pista: título".</summary>
    public readonly string Title;
    /// <summary>Resumen para GameStateTracker y para el pensamiento de la IA.</summary>
    public readonly string ShortDescription;
    /// <summary>Texto de la hoja (vacío = la pista no abre hoja).</summary>
    public readonly string Body;
    /// <summary>PNG del dibujo en el mundo en Art/Props/pistas/ (vacío = placeholder).</summary>
    public readonly string WorldSprite;
    /// <summary>PNG de la imagen grande de la hoja en Art/Props/pistas/ (vacío = sin imagen).</summary>
    public readonly string CardImage;

    public ClueInfo(string id, string title, string shortDescription, string body, string worldSprite, string cardImage)
    {
        Id = id;
        Title = title;
        ShortDescription = shortDescription;
        Body = body;
        WorldSprite = worldSprite;
        CardImage = cardImage;
    }
}

/// <summary>
/// PISTAS.1 (P4): los textos de TODAS las pistas en un solo lugar, copiados del guion (Docs/guiones/pistas.md §2).
/// Datos en código, como DialogueDatabase: siempre están, aunque falte un asset. Los dibujos van aparte (ClueArt, opcional).
/// Los textos de CluePickup en la escena (los pone LevelBuilder_Campamento) solo se usan si una pista no está aquí.
/// testimonio_puerta_01 y testimonio_socios_01 conservan su texto de DialogueDatabase (no se tocan): aquí solo tienen
/// título para el aviso.
/// </summary>
public static class ClueCatalog
{
    private static readonly Dictionary<string, ClueInfo> Clues = new Dictionary<string, ClueInfo>();

    public static IEnumerable<ClueInfo> All => Clues.Values;

    public static bool TryGet(string clueId, out ClueInfo info)
    {
        info = null;
        return !string.IsNullOrEmpty(clueId) && Clues.TryGetValue(clueId, out info);
    }

    private static void Add(ClueInfo info) => Clues[info.Id] = info;

    static ClueCatalog()
    {
        // ===== Nivel 1 =====
        Add(new ClueInfo("nota_porky_01", "Nota de Porky",
            "Porky le escribe a «L.»: que su gente no vaya a la casa, que va solo, y que no le diga nada a Wilbur",
            "L.:\n" +
            "Esta noche no puedo. Dile a tu gente que no venga a la casa, que yo voy solo.\n" +
            "Llevo lo que pueda.\n" +
            "Y no le digas nada a Wilbur.\n" +
            "—P.",
            "pista_nota_01", ""));

        Add(new ClueInfo("contrato_01", "Pagaré",
            "Porky le debe quinientos pesos al señor Lobo por un préstamo de juego; respondía con su casa y su trabajo",
            "Yo, Porky, reconozco deberle al señor Lobo la suma de QUINIENTOS PESOS por préstamo de juego, pagaderos el primero del mes.\n" +
            "Si no pago, respondo con mi casa y con mi trabajo.\n" +
            "\n" +
            "Firma: Porky  (la firma está toda temblorosa)",
            "pista_contrato_01", ""));

        // Centavo (interrogatorio o al perdonarlo): no abre hoja, solo el aviso. El texto queda para un diario de pistas.
        Add(new ClueInfo("registro_01", "Hoja del libro de cuentas",
            "Faltan $3.200 de la caja fuerte del club; el último en entrar fue P. (el menor)",
            "Caja fuerte, oficina del club. Faltante: $3.200.\n" +
            "Último en entrar: P. (el menor). Testigo: el portero.\n" +
            "Intereses de la semana: $40.\n" +
            "Nota del jefe: que nadie lo toque. Que pague trabajando.",
            "", ""));

        // Colmillo (interrogatorio, DialogueNPC): texto intacto en DialogueDatabase; aquí solo el título del aviso
        Add(new ClueInfo("testimonio_puerta_01", "Testimonio de Colmillo", "", "", "", ""));

        // ===== Nivel 2 =====
        // Tuerto (interrogatorio, DialogueNPC): no está en el guion; mismo trato que Colmillo
        Add(new ClueInfo("testimonio_socios_01", "Testimonio de Tuerto", "", "", "", ""));

        Add(new ClueInfo("foto_reunion_01", "Foto de la reunión",
            "Foto en el Club del Lobo: Porky en la mesa del jefe, sonriendo, con un fajo de billetes; «el socio nuevo»",
            "Club del Lobo, hace dos semanas. Porky sentado en la mesa del jefe, sonriendo, con un fajo de billetes en la mano.\n" +
            "\n" +
            "Al reverso, a lápiz: «El socio nuevo. Que no se nos olvide la cara.»",
            "pista_foto_01", "pista_foto_reunion_grande"));

        Add(new ClueInfo("carta_lobo_01", "Carta de Don Lobo",
            "Don Lobo dice que Porky vino solo, que le robó tres mil doscientos pesos y que Max sabe",
            "A quien venga por el cerdito:\n" +
            "\n" +
            "No lo tengo encerrado. Vino solo, con la cola entre las patas, porque los que le prestaron después de mí no son tan caballeros como yo.\n" +
            "Me robó tres mil doscientos pesos y el orgullo. La plata la puede pagar trabajando. El orgullo… ya veremos.\n" +
            "\n" +
            "Le escribí a su hermano Max. Max sabe.\n" +
            "—L.",
            "pista_carta_01", ""));
    }
}
