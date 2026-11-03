using UnityEngine;

/// <summary>Botiquín (HUD.3): cura un porcentaje de la vida máxima. Con la vida llena no se gasta.</summary>
public class MedkitPickup : Pickup
{
    [Tooltip("Cuánto cura, como parte de la vida máxima (0.4 = 40 %)")]
    [SerializeField, Range(0f, 1f)] private float healFraction = 0.4f;

    public override PickupKind Kind => PickupKind.Botiquin;
    protected override string UseSfx => "sfx_botiquin";
    protected override string UseVoice => "wilbur_botiquin_01";

    protected override bool TryApply(GameObject player, out string toast)
    {
        PlayerHealth health = player.GetComponentInParent<PlayerHealth>();
        if (health == null) health = player.GetComponentInChildren<PlayerHealth>();
        if (health == null || health.IsDead || health.IsFullHealth)
        {
            toast = "Salud completa";
            return false;
        }
        health.Heal(health.MaxHealth * healFraction);
        toast = "+ Botiquín";
        return true;
    }
}
