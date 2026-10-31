# Phase VM2 — Revolver, weapon switching, visible-rounds reload

- **Day:** when time allows (recommended after Phase 4; it is polish, not core)
- **Model:** Opus 5.5
- **Commit tag:** `vm2`
- **Gate:** `docs/audits/AUDIT_viewmodel_phase1_4.md` must start with `ESTADO: LUZ VERDE`. If not, stop.
- **Audit file:** `docs/audits/PHASE-VM2-audit.md` (use `docs/phases/AUDIT_TEMPLATE.md`, in Spanish)

## Context
Read `CLAUDE.md`, `graphify-out/GRAPH_REPORT.md`, the Viewmodel entry of `docs/phases/REGISTRO.md` and
`docs/audits/AUDIT_viewmodel_phase1_4.md`. Branch `player` only; push `origin player` at the end; no PR, no force.

## Already in place (do not rebuild)
- `IWeapon` (`OnFired`, `OnReloadStarted`, `OnReloadFinished`, `IsReloading`, `ReloadDuration`, `SpreadAngle`);
  `ShotgunController` implements it.
- `FirstPersonViewmodel.SetProfile` / `SetWeapon`, `PlayerAim.SetWeapon`.
- `VM_Revolver.asset` + keyed PNGs in `Art/Viewmodel/Revolver/`: idle, fire, aimidle, `reload_01` (cylinder open,
  5 empty chambers), `reload_02` (left hand inserting a round), `reload_03` (close). `aimSightPoint` (964.3, 723).
- FX: `fx_round_rim_01.png`, `fx_casing_revolver_01.png` (256x256, centered).
- `ViewmodelCamera` stack, layer `Viewmodel`, unscaled clock `ViewmodelTime`, `RubberHoseCrosshair` (reads
  `IWeapon.SpreadAngle`, red hit marker via `EnemyHealth.OnDamaged`).
- If Phase 3B already ran: the HUD ammo medallion exists and must read the ACTIVE `IWeapon`.

## Scope
Add a revolver weapon, weapon switching and the revolver reload sequence. Do NOT change shotgun behavior,
enemy logic, dialogue, AI companion or levels.

## Part 0 — Investigate (audit "Decisiones tomadas")
- How `ShotgunController` applies damage (raycast, enemy component/method, falloff if 3B ran) and how guard
  surrender is triggered. The revolver MUST use the exact same damage path.
- Whether a HUD ammo display exists.

## Part 1 — Weapon abstraction and switching
Commit: `vm2(david): cambio de armas`
1. Extend `IWeapon` (do not create a new interface): `ViewmodelProfile Profile`, `int LoadedRounds`,
   `int MagazineSize`, `int ReserveRounds`, `void CancelReload()`. `ShotgunController` implements them with its
   existing fields (no behavior change).
2. `WeaponSwitcher` on the player: keys 1 (shotgun) / 2 (revolver) and mouse scroll. Switch = lower the viewmodel
   off-screen ~0.2 s, `SetWeapon`/`SetProfile`, raise ~0.2 s; firing and aiming blocked during the switch;
   switching cancels reload. Only the active weapon component is enabled. Both weapons available from start
   (serialized toggle); default shotgun.
3. `PlayerAim`, `RubberHoseCrosshair` and the HUD ammo display (if any) follow the active `IWeapon`.

## Part 2 — RevolverController (implements IWeapon)
Commit: `vm2(david): revolver`
1. Hitscan single raycast from the main camera center, spread on the camera's right/up axes (same as the shotgun).
   Semi-auto (one shot per click). Magazine 5, reserve 20, damage 35, range 60 m, fire interval 0.35 s,
   hip spread 1.5°, aim spread 0.2° (all serialized).
2. Reload (R, or automatically when firing empty with reserve > 0), round by round: open 0.35 s → eject →
   0.30 s per missing round (consumes reserve) → close 0.30 s. Events `OnReloadOpened`,
   `OnRoundLoaded(int loadedCount)`, `OnReloadClosed`. Pressing fire during reload: finish the current round,
   close, then allow firing, only if `LoadedRounds >= 1`.
3. Dry-fire click when empty with no reserve. Optional AudioClip slots: fire, dry fire, open, eject, insert, close.
4. `ShotgunFeedback`-style onomatopoeia counter shared across weapons (one counter, not one per weapon).

## Part 3 — Revolver viewmodel and visible rounds
Commit: `vm2(david): recarga del revolver con balas visibles`
1. `VM_Revolver`: map `reload_01/02/03` to open / insert / close. Do NOT loop the reload clip; the sequence below
   drives it. Smaller base flash size than the shotgun.
2. `ChamberOverlay` under `ViewmodelCanvas` (layer `Viewmodel`), visible only while open/insert is showing:
   5 child RectTransforms `Chamber_1..Chamber_5`, each an Image with `fx_round_rim_01`. Enabled rims =
   `LoadedRounds`, filled in order 1→5. David positions them in the inspector over the 5 holes; add a context-menu
   "Show all rims" to help.
3. Sequence (unscaled `ViewmodelTime`): swap to open (scale punch) → rims for the current rounds → spawn
   (5 − LoadedRounds) casings (`fx_casing_revolver_01`) at the cylinder that fall with gravity + spin and fade in
   ~0.6 s → per round: insert 0.15 s, back to open, enable the next rim with a 1.15 scale punch → close frame
   (or a procedural wrist flick: rotate root −12° and snap back) → idle.
4. ADS uses the existing centering with the revolver's `aimSightPoint`.

## Unity test checklist for David (copy into audit section 9, in Spanish)
1. Teclas 1 y 2 y rueda del mouse cambian de arma con bajada y subida de manos; no se puede disparar en medio.
2. Revólver: un disparo por clic, la mira se cierra más que con la escopeta; un guardia normal muere y el
   informante se rinde igual que con la escopeta.
3. 5 balas, disparar 2 y recargar: el tambor muestra 3 balas y 2 huecos, caen 2 casquillos, entran 2 balas.
4. Vaciar el tambor y recargar: 0 balas, 5 casquillos, 5 inserciones.
5. Disparar durante la recarga: termina la bala actual, cierra y dispara.
6. Apuntar con el revólver: la mirilla queda en el centro exacto (Debug Sight Log < 2 px).
7. Cambiar de arma durante la recarga la cancela sin errores.

## Finish
Write `docs/audits/PHASE-VM2-audit.md` (Spanish), commit it with the last part, `git push origin player`. Stop.
