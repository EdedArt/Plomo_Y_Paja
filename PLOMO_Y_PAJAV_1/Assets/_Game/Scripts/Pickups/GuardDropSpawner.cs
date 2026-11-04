using UnityEngine;

/// <summary>
/// Drops de los guardias (fase HUD.3, H16): el guardia que MUERE suelta a sus pies una caja de cartuchos con 35 % de chance
/// (máximo una por guardia: EnemyHealth muere una sola vez). Rendirse no suelta nada; rematar a un rendido sí cuenta como morir.
/// NUNCA Centavo: no usa EnemyHealth (CentavoController es IDamageable). NUNCA Don Lobo: la Fase 4 le apaga dropsLoot en su EnemyHealth.
/// DÓNDE VA: un objeto por escena, dentro de "--- PICKUPS ---" (lo crea PickupBuilder). Sin él, no hay drops.
/// N1.2 (regla N7): APAGADOS por defecto; los objetos están colocados en el mapa. Se prenden con dropsEnabled.
/// </summary>
public class GuardDropSpawner : MonoBehaviour
{
    [Tooltip("N7: apagado por defecto. Prendido, vuelve a funcionar como en H16")]
    [SerializeField] private bool dropsEnabled;
    [Tooltip("Pickup_Cartuchos")]
    [SerializeField] private GameObject dropPrefab;
    [SerializeField, Range(0f, 1f)] private float dropChance = 0.35f;
    [SerializeField, Min(1)] private int dropAmount = 4;

    private void OnEnable()
    {
        EnemyHealth.AnyDied += HandleEnemyDied;
    }

    private void OnDisable()
    {
        EnemyHealth.AnyDied -= HandleEnemyDied;
    }

    private void HandleEnemyDied(EnemyHealth enemy)
    {
        if (!dropsEnabled || dropPrefab == null || !enemy.DropsLoot || !enemy.TryGetComponent(out EnemyAI _)) return;
        if (Random.value >= dropChance) return;

        GameObject drop = Instantiate(dropPrefab, Pickup.SnapToGround(enemy.transform.position), Quaternion.identity, transform);
        if (drop.TryGetComponent(out AmmoPickup ammo)) ammo.SetAmount(dropAmount);
    }
}
