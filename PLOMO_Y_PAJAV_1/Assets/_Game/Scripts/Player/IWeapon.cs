/// <summary>
/// Lo mínimo que el viewmodel, PlayerAim, la mira y el cambio de armas necesitan saber de un arma.
/// Las armas leen PlayerAim.IsAiming por su cuenta para su propia dispersión (ShotgunController ya lo hace).
/// FirstPersonViewmodel.SetWeapon(IWeapon) conecta el arma activa; ViewmodelProfile decide cómo se ve.
/// Implementan: ShotgunController y RevolverController (fase 3C.5). WeaponSwitcher elige la activa.
/// </summary>
public interface IWeapon
{
    event System.Action OnFired;
    event System.Action OnReloadStarted;
    event System.Action OnReloadFinished;

    bool IsReloading { get; }

    /// <summary>Dispersión actual del arma en las unidades de ShootPellet: cada eje suma rand(−s, s)·0.01 al forward.
    /// Solo lectura: la mira del HUD lo usa para abrir sus marcas.</summary>
    float SpreadAngle { get; }

    /// <summary>Segundos que dura la recarga (el viewmodel baja las manos o pasa los cuadros de recarga en ese tiempo).</summary>
    float ReloadDuration { get; }

    /// <summary>Balas cargadas ahora mismo (cartuchos en la escopeta, balas en el tambor).</summary>
    int LoadedRounds { get; }

    /// <summary>Capacidad del cargador / tambor.</summary>
    int MagazineSize { get; }

    /// <summary>Balas de reserva.</summary>
    int ReserveRounds { get; }

    /// <summary>Corta una recarga en curso sin cargar nada (cambio de arma). No dispara OnReloadFinished.</summary>
    void CancelReload();
}
