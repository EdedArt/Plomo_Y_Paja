/// <summary>
/// Lo mínimo que el viewmodel y PlayerAim necesitan saber de un arma: cuándo dispara, cuándo recarga y cuánto dura.
/// Las armas leen PlayerAim.IsAiming por su cuenta para su propia dispersión (ShotgunController ya lo hace).
/// FirstPersonViewmodel.SetWeapon(IWeapon) conecta el arma activa; ViewmodelProfile decide cómo se ve.
/// </summary>
public interface IWeapon
{
    event System.Action OnFired;
    event System.Action OnReloadStarted;
    event System.Action OnReloadFinished;

    bool IsReloading { get; }

    /// <summary>Dispersión actual del arma (el valor de ShootPellet: cada perdigón suma rand(−s, s)·0.01 al forward). Solo lectura:
    /// la mira del HUD lo usa para abrir sus marcas.</summary>
    float SpreadAngle { get; }

    /// <summary>Segundos que dura la recarga (el viewmodel baja las manos durante todo ese tiempo).</summary>
    float ReloadDuration { get; }
}
