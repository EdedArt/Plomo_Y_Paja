using UnityEngine;

/// <summary>
/// Disparador de respaldo para sacar la escopeta (INTRO.3, I8): una caja trigger frente a la puerta delantera de Porky.
/// Si el jugador llega ahí todavía con el arma enfundada (no vio a ningún guardia, no recibió daño), Wilbur la saca.
/// DÓNDE VA: "WeaponDraw_Puerta" dentro de "--- INTRO ---" en NivelPorky (lo pone Plomo y Paja → Intro → Construir taxi
/// y llegada, también desde Generar Nivel Porky). Capa Ignore Raycast: no tapa la vista de los guardias.
/// Sin PlayerWeaponHolster en el Player no hace nada.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class WeaponDrawTrigger : MonoBehaviour
{
    [SerializeField] private string reason = "puerta de Porky";

    private void OnTriggerEnter(Collider other)
    {
        PlayerWeaponHolster holster = PlayerWeaponHolster.Current;
        if (holster == null || holster.State != PlayerWeaponHolster.HolsterState.Holstered) return;
        if (!other.CompareTag("Player") && other.GetComponentInParent<PlayerWeaponHolster>() == null) return;
        if (PlayerAim.GameplayBlocked()) return; // durante la cinemática el jugador ya está en la acera, pero por si acaso
        holster.RequestDraw(reason);
    }
}
