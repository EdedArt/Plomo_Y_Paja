using System;
using UnityEngine;

/// <summary>
/// Todas las animaciones 2D de un personaje (frames sueltos, sin Animator).
/// ASSETS: Assets/_Game/Art/SpriteSets/&lt;characterId&gt;.asset
/// MENÚ: Plomo y Paja → Reconstruir Sets de Sprites (los crea o actualiza desde
///       Assets/_Game/Art/Characters/&lt;characterId&gt;/&lt;anim&gt;_&lt;NN&gt;.png)
/// </summary>
[CreateAssetMenu(fileName = "SpriteSet", menuName = "Plomo y Paja/Character Sprite Set")]
public class CharacterSpriteSet : ScriptableObject
{
    [Serializable]
    public class AnimClip
    {
        public string name;
        public Sprite[] frames = new Sprite[0];
        public float fps = 8f;
    }

    public string characterId;
    public AnimClip[] clips = new AnimClip[0];

    /// <summary>Busca una animación con al menos un frame. Devuelve false si no hay.</summary>
    public bool TryGet(string anim, out AnimClip clip)
    {
        if (clips != null)
        {
            foreach (AnimClip c in clips)
            {
                if (c != null && c.name == anim && c.frames != null && c.frames.Length > 0)
                {
                    clip = c;
                    return true;
                }
            }
        }
        clip = null;
        return false;
    }

    /// <summary>¿Hay al menos un frame en alguna animación?</summary>
    public bool HasAnyFrames
    {
        get
        {
            if (clips == null) return false;
            foreach (AnimClip c in clips)
                if (c != null && c.frames != null && c.frames.Length > 0) return true;
            return false;
        }
    }
}
