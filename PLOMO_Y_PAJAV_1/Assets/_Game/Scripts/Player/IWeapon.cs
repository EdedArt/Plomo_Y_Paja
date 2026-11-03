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

    /// <summary>Corta una recarga en curso (cambio de arma). Lo ya cargado se queda. No dispara OnReloadFinished.</summary>
    void CancelReload();
}

/// <summary>
/// Recarga por etapas, bala por bala (fase 3C.5.2, VM2): abrir → meter una bala por cada una que falta → cerrar.
/// La implementa RevolverController; FirstPersonViewmodel la usa para los cuadros de recarga y las balas visibles del tambor.
/// Va entre OnReloadStarted y OnReloadFinished de IWeapon.
/// </summary>
public interface IStagedReloadWeapon
{
    /// <summary>El tambor se abrió (cuadro "abrir").</summary>
    event System.Action OnReloadOpened;
    /// <summary>Empieza a meter una bala (cuadro "meter"); dura RoundInsertSeconds.</summary>
    event System.Action OnRoundInserting;
    /// <summary>Entró una bala: balas cargadas ahora.</summary>
    event System.Action<int> OnRoundLoaded;
    /// <summary>Se cierra el tambor (cuadro "cerrar").</summary>
    event System.Action OnReloadClosing;

    float RoundInsertSeconds { get; }
}
