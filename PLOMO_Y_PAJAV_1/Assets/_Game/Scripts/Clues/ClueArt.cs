using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PISTAS.1 (P5): los dibujos de las pistas (dibujo en el mundo e imagen grande de la hoja), por clueId.
/// Asset en Resources/Clues/ClueArt.asset, lo arma "Plomo y Paja → Pistas → Crear hoja de pista" con los PNG de
/// Art/Props/pistas/ que existan (nombres en ClueCatalog). Sin el asset o sin un PNG: placeholder (la hoja dibujada) y hoja
/// sin imagen. Se carga una vez.
/// </summary>
[CreateAssetMenu(menuName = "Plomo y Paja/Pistas/Dibujos de pistas", fileName = "ClueArt")]
public class ClueArt : ScriptableObject
{
    public const string ResourcePath = "Clues/ClueArt";

    [Serializable]
    public class Entry
    {
        public string clueId;
        [Tooltip("Dibujo en el mundo (billboard). Vacío = la hoja dibujada de siempre")]
        public Sprite worldSprite;
        [Tooltip("Imagen grande de la hoja. Vacío = hoja sin imagen")]
        public Sprite cardImage;
    }

    public List<Entry> entries = new List<Entry>();

    private static ClueArt _loaded;
    private static bool _triedLoad;

    public static Sprite WorldSprite(string clueId)
    {
        Entry e = Find(clueId);
        return e != null ? e.worldSprite : null;
    }

    public static Sprite CardImage(string clueId)
    {
        Entry e = Find(clueId);
        return e != null ? e.cardImage : null;
    }

    private static Entry Find(string clueId)
    {
        if (!_triedLoad || _loaded == null)
        {
            if (!_triedLoad) _loaded = Resources.Load<ClueArt>(ResourcePath);
            _triedLoad = true;
        }
        if (_loaded == null || string.IsNullOrEmpty(clueId)) return null;
        for (int i = 0; i < _loaded.entries.Count; i++)
            if (_loaded.entries[i] != null && _loaded.entries[i].clueId == clueId) return _loaded.entries[i];
        return null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _loaded = null;
        _triedLoad = false;
    }
}
