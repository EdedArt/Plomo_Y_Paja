using System;
using UnityEngine;

/// <summary>
/// La reserva de munición de Wilbur, en UN solo lugar (fase HUD.1, H4). Las armas guardan solo lo cargado.
/// DÓNDE VA: GameObject "Player" (lo añade "Crear Prefabs de Prueba").
///
/// - Reserva y tope por AmmoType. Escopeta: 18 al empezar (lo que tenía ShotgunController), tope 30 (el de su AddAmmo).
///   Revólver: 0 al empezar (no se tiene el revólver, D13), tope 24. La de revólver se puede juntar sin tener el arma (H14).
/// - TryAdd(type, amount, out added): suma hasta el tope; false si ya estaba lleno (HUD.3: el objeto no se gasta).
/// - Take(type, amount): saca hasta lo que haya; devuelve cuánto sacó (las armas al recargar).
/// - OnReserveChanged(type, reserve): lo escucha el HUD (H3).
/// Cada escena trae su propio Player: la reserva empieza de nuevo en cada nivel (como antes la de la escopeta).
/// </summary>
public class PlayerAmmo : MonoBehaviour
{
    [Header("Escopeta")]
    [SerializeField, Min(0)] private int shotgunReserve = 18;
    [SerializeField, Min(0)] private int shotgunCap = 30;

    [Header("Revólver")]
    [SerializeField, Min(0)] private int revolverReserve = 0;
    [SerializeField, Min(0)] private int revolverCap = 24;

    /// <summary>Cambió la reserva de un tipo: (tipo, reserva nueva).</summary>
    public event Action<AmmoType, int> OnReserveChanged;

    public int GetReserve(AmmoType type) => type == AmmoType.Shotgun ? shotgunReserve : revolverReserve;
    public int GetCap(AmmoType type) => type == AmmoType.Shotgun ? shotgunCap : revolverCap;
    public bool IsFull(AmmoType type) => GetReserve(type) >= GetCap(type);

    /// <summary>Suma munición hasta el tope. added = lo que entró. false si no entró nada (estaba lleno o amount ≤ 0).</summary>
    public bool TryAdd(AmmoType type, int amount, out int added)
    {
        added = Mathf.Clamp(amount, 0, Mathf.Max(0, GetCap(type) - GetReserve(type)));
        if (added <= 0) return false;
        SetReserve(type, GetReserve(type) + added);
        return true;
    }

    /// <summary>Saca hasta amount de la reserva. Devuelve cuánto sacó.</summary>
    public int Take(AmmoType type, int amount)
    {
        int taken = Mathf.Clamp(amount, 0, GetReserve(type));
        if (taken > 0) SetReserve(type, GetReserve(type) - taken);
        return taken;
    }

    private void SetReserve(AmmoType type, int value)
    {
        if (type == AmmoType.Shotgun) shotgunReserve = value;
        else revolverReserve = value;
        OnReserveChanged?.Invoke(type, value);
    }

    /// <summary>La del jugador de la escena (el que tiene tag Player), o null. Para quien no cuelga del Player.</summary>
    public static PlayerAmmo FindOnPlayer()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return null;
        PlayerAmmo ammo = player.GetComponentInParent<PlayerAmmo>();
        if (ammo == null) ammo = player.GetComponentInChildren<PlayerAmmo>();
        return ammo;
    }
}
