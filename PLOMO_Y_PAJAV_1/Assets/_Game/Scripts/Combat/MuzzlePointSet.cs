using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puntos del cañón por cuadro de un personaje (fase HUD.2, H10). Asset: Assets/_Game/Data/MuzzlePoints/Muzzle_&lt;id&gt;.asset.
/// - Cada entrada = un Sprite y su punto normalizado (0..1 dentro del rect del sprite, origen abajo-izquierda).
/// - fallback: el punto para los cuadros sin entrada (a la altura de la mano que dispara).
/// Se marcan haciendo clic sobre el dibujo en Plomo y Paja → HUD → Puntos de cañón. Los lee EnemyGunfire.
/// </summary>
[CreateAssetMenu(fileName = "Muzzle_personaje", menuName = "Plomo y Paja/Puntos de cañón")]
public class MuzzlePointSet : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public Sprite sprite;
        [Tooltip("0..1 dentro del sprite, origen abajo a la izquierda")]
        public Vector2 point;
    }

    public string characterId;
    [Tooltip("Punto para los cuadros sin entrada (normalizado)")]
    public Vector2 fallback = new Vector2(0.5f, 0.55f);
    public List<Entry> points = new List<Entry>();

    /// <summary>¿Tiene punto propio este cuadro? (búsqueda lineal: son pocos cuadros, sin basura)</summary>
    public bool TryGet(Sprite sprite, out Vector2 point)
    {
        if (sprite != null)
            for (int i = 0; i < points.Count; i++)
                if (points[i] != null && points[i].sprite == sprite) { point = points[i].point; return true; }
        point = fallback;
        return false;
    }

    /// <summary>Punto del cuadro, o el de respaldo.</summary>
    public Vector2 Get(Sprite sprite)
    {
        TryGet(sprite, out Vector2 point);
        return point;
    }

    /// <summary>Pone (o cambia) el punto de un cuadro. Lo usa la ventana del Editor.</summary>
    public void Set(Sprite sprite, Vector2 point)
    {
        if (sprite == null) return;
        for (int i = 0; i < points.Count; i++)
            if (points[i] != null && points[i].sprite == sprite) { points[i].point = point; return; }
        points.Add(new Entry { sprite = sprite, point = point });
    }

    public void Remove(Sprite sprite)
    {
        points.RemoveAll(e => e == null || e.sprite == sprite);
    }
}
