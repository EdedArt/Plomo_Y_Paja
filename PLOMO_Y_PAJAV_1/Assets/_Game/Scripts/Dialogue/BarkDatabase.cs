using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Frases cortas ("barks") de los enemigos. Cristian graba el audio con los mismos ids.
/// Formato del id: &lt;voiceSet&gt;_&lt;evento&gt;_&lt;NN&gt;. Eventos: ver, dano, rendir, morir, patrulla.
/// </summary>
public static class BarkDatabase
{
    public const string DefaultSet = "guardia";

    private static readonly string[] Raw =
    {
        "guardia_ver_01|¡Eh, tú, cerdo!",
        "guardia_ver_02|¡Tenemos visita, muchachos!",
        "guardia_ver_03|¡Alto ahí, jamón con patas!",
        "guardia_ver_04|¡El jefe quiere tu tocino!",
        "guardia_dano_01|¡Ay, mi pellejo!",
        "guardia_dano_02|¡Eso dolió, marrano!",
        "guardia_dano_03|¡Auch! ¡Mi cola!",
        "guardia_rendir_01|¡No dispares! ¡Me rindo!",
        "guardia_rendir_02|¡Está bien, está bien! ¡Hablo!",
        "guardia_rendir_03|¡Tengo familia, cerdo!",
        "guardia_morir_01|¡Aaaay, mamá!",
        "guardia_morir_02|¡Dile al jefe... que lo intenté!",
        "guardia_morir_03|¡Uuuuf!",
        "guardia_patrulla_01|Maldita lluvia...",
        "guardia_patrulla_02|Otra noche cuidando paja.",
        "guardia_patrulla_03|¿Quién se comió mi sándwich?",
        "informante_ver_01|¡Ay no, ay no, ay no!",
        "informante_ver_02|¡N-no quiero problemas!",
        "informante_dano_01|¡Yo solo llevo las cuentas!",
        "informante_rendir_01|¡Sé cosas! ¡Sé cosas de tu hermano!",
        "informante_rendir_02|¡No me mates, te sirvo vivo!",
        "informante_morir_01|¡Yo... sabía... algo!",
    };

    private static Dictionary<string, List<KeyValuePair<string, string>>> _byKey;
    private static readonly Dictionary<string, int> LastPicked = new Dictionary<string, int>();

    private static void Build()
    {
        _byKey = new Dictionary<string, List<KeyValuePair<string, string>>>();
        foreach (string line in Raw)
        {
            int bar = line.IndexOf('|');
            string id = line.Substring(0, bar);
            string text = line.Substring(bar + 1);
            // "guardia_ver_01" -> clave "guardia_ver"
            string key = id.Substring(0, id.LastIndexOf('_'));
            if (!_byKey.TryGetValue(key, out var list))
                _byKey[key] = list = new List<KeyValuePair<string, string>>();
            list.Add(new KeyValuePair<string, string>(id, text));
        }
    }

    /// <summary>Todos los ids (para pruebas).</summary>
    public static IEnumerable<string> AllIds()
    {
        foreach (string line in Raw) yield return line.Substring(0, line.IndexOf('|'));
    }

    /// <summary>Frase al azar (sin repetir la anterior). Si el set no tiene ese evento usa "guardia".</summary>
    public static bool GetRandom(string voiceSet, string evt, out string lineId, out string text)
    {
        if (_byKey == null) Build();
        lineId = null;
        text = null;

        string key = (string.IsNullOrEmpty(voiceSet) ? DefaultSet : voiceSet) + "_" + evt;
        if (!_byKey.TryGetValue(key, out var list))
        {
            key = DefaultSet + "_" + evt;
            if (!_byKey.TryGetValue(key, out list)) return false;
        }

        int index = Random.Range(0, list.Count);
        if (list.Count > 1 && LastPicked.TryGetValue(key, out int last) && last == index)
            index = (index + 1) % list.Count;
        LastPicked[key] = index;

        lineId = list[index].Key;
        text = list[index].Value;
        return true;
    }
}
