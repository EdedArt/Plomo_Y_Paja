using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Qué objeto va en cada lugar del mapa (HUD.3).</summary>
public enum PickupKind { Botiquin, Cartuchos, BalasRevolver }

/// <summary>
/// Dónde van los objetos recogibles de una escena (fase HUD.3, H15). Un asset por escena: Data/PickupLayout_&lt;Escena&gt;.asset.
/// Lo crea y lo lee PickupBuilder (menú "Plomo y Paja → HUD → Colocar objetos"); "Guardar posiciones de objetos" escribe aquí
/// lo que se haya movido en la escena, así regenerar el nivel no lo pierde.
/// NIVEL 2: la Fase 4 solo llama PickupBuilder.PlaceInOpenScene desde su generador; el asset PickupLayout_Campamento se crea
/// vacío la primera vez y se llena colocando objetos en la escena y corriendo "Guardar posiciones de objetos".
/// </summary>
public class PickupLayout : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Nombre único en la escena: el objeto se llama Pickup_<id>")]
        public string id;
        public PickupKind kind;
        [Tooltip("Posición de mundo; al colocarlo, la altura se ajusta al piso")]
        public Vector3 position;
    }

    public List<Entry> entries = new List<Entry>();
}
