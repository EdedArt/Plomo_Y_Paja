/// <summary>
/// Algo que recibe los disparos del jugador y NO es un guardia (los guardias siguen usando EnemyHealth).
/// QUIÉN LO USA: ShotgunController, solo si el collider tocado no tiene EnemyHealth en sus padres
/// (así el camino de los guardias no cambia). Primer usuario: CentavoController (fase 3C.2).
/// DÓNDE VA: en el collider tocado o en cualquiera de sus padres.
/// </summary>
public interface IDamageable
{
    /// <summary>Recibir un perdigón/bala. Devuelve true si este golpe lo mató.</summary>
    bool TakeDamage(float damage);
}
