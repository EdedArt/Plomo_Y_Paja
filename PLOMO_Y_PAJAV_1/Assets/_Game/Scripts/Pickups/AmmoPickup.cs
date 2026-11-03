using UnityEngine;

/// <summary>
/// Caja de munición (HUD.3): suma a la reserva de PlayerAmmo hasta el tope. Con la reserva llena no se gasta; si solo entra
/// una parte, se gasta y el aviso dice lo que entró. La de revólver se guarda aunque todavía no se tenga el arma (H14).
/// </summary>
public class AmmoPickup : Pickup
{
    [SerializeField] private AmmoType ammoType = AmmoType.Shotgun;
    [SerializeField, Min(1)] private int amount = 6;

    public override PickupKind Kind => ammoType == AmmoType.Shotgun ? PickupKind.Cartuchos : PickupKind.BalasRevolver;
    protected override string UseSfx => "sfx_municion_recoger";
    protected override string UseVoice => "wilbur_municion_01";

    /// <summary>Lo usa GuardDropSpawner: el drop de un guardia trae menos que la caja del mapa.</summary>
    public void SetAmount(int value) => amount = Mathf.Max(1, value);

    protected override bool TryApply(GameObject player, out string toast)
    {
        PlayerAmmo ammo = PlayerAmmo.FindOnPlayer();
        if (ammo == null || !ammo.TryAdd(ammoType, amount, out int added))
        {
            toast = "Munición llena";
            return false;
        }
        toast = ammoType == AmmoType.Shotgun ? $"+{added} cartuchos" : $"+{added} balas de revólver";
        return true;
    }
}
