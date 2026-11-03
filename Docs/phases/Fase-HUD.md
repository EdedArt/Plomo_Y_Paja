# PHASE HUD — Health & ammo HUD, enemy gunfire from the muzzle, pickups (medkits & ammo)

> In the repo: `Docs/phases/Fase-HUD.md` · content / assets: `Docs/guiones/Guion-HUD.md` (v1.0).
> **Version 1.1 (3 Nov):** HUD.1 closed (`51f4e42`); status block below; **guards now shoot (H18)**; H9 and HUD.2/HUD.3/HUD.4 updated.
> **Owner:** David (Claude Code, orchestrated) · **Branch:** `integracion/dia4`.
> **Deadline:** game delivered Wed 4 Nov 2026 → HUD should close by **Tue 3 Nov night** (playtest merged with 3C.6 / INTRO.4).
> Paths are relative to `PLOMO_Y_PAJAV_1/Assets/_Game/` unless they start with `Docs/`, `docs/`, `Tools/` or `_entrante_*`.

## Status after HUD.1 (read this first)
- **HUD.1 closed after 3C.5 / 3C.5.2**, so the contract was inverted: **both** reserves live in `PlayerAmmo`. Revolver = **5 loaded + 15 reserve** on pickup (3C.5.2, five chambers in the art), cap 24. The crosshair's provisional ammo row was removed; ammo is shown **only** by `HUD_Wilbur`.
- **`UiHideReasons`** (`Scripts/UI/HUD/`) is the reason-based hide system (H6). INTRO.3 must use `UiHideReasons.Set(Reason.Cinematic / Holstered, on)`.
- `PlayerHealth.TakeDamage(damage, sourcePosition)` and `OnDamaged(amount, Vector3?)` exist. Guards (melee) and Centavo already send a source position.
- **Guards are melee-only today** (`EnemyAI.PerformAttack`). Only Centavo shoots. → H18.
- **Level 2 (Campamento) and Don Lobo do not exist yet** (Fase 4). Every Level 2 / Don Lobo item in this phase applies **only if they exist when the subphase runs**; otherwise leave a documented hook (e.g. `HudBuilder.PlaceInOpenScene(null)`, `EnemyGunfire` ready to add) and list it in the audit.

## Scheduling with Phase 3C and INTRO
| Subphase | Can run… | Must NOT run at the same time as | Why |
|---|---|---|---|
| HUD.1 | ✅ CLOSED (3 Nov) | — | — |
| HUD.2 (enemy gunfire) | now (3C.2–3C.5 closed) | 3C.6, INTRO.3 | touches `EnemyAI`, `CentavoController`, `ShotgunFeedback` |
| HUD.3 (pickups) | after HUD.2 (or in parallel only if HUD.2 isn't touching `EnemyAI`/`EnemyHealth`) | INTRO.2, HUD.2 | Level 1 builders; guard death event |
| HUD.4 (close) | together with 3C.6 / INTRO.4 | — | one shared playtest session |

**Parallel-session git rule** (same as INTRO): never `git add -A` / `git add .`; stage by explicit path. Before editing a shared file (`IWeapon.cs`, `ShotgunController.cs`, `RevolverController.cs`, `PlayerHealth.cs`, `PlayerAmmo.cs`, `EnemyAI.cs`, `EnemyHealth.cs`, `CentavoController.cs`, `GameStateTracker.cs`, `CharacterSpriteImporter.cs`, `AudioImporter.cs`, level builders, `CLAUDE.md`) run `git status`; if it has uncommitted changes you did not make, **don't edit it**: report in the audit what it needs.

---

## 0. Rules for every subphase
Rules 1–9 of `Docs/phases/Fase-3C-Contador.md` §0 apply **verbatim**, with these substitutions:
- Audits go to `Docs/audits/AUDIT_hud_<N>.md` (in Spanish), using the subphase's template.
- Editor menus live under **Plomo y Paja → HUD → …** and are idempotent.
- Code: HUD in `Scripts/UI/HUD/`; enemy gunfire and shared combat FX in `Scripts/Combat/`; pickups in `Scripts/Pickups/`; editor tools in `Scripts/Editor/`. If recon shows a better existing place, use it and say so.
- The existing contracts in 3C §0.4 still hold: `PlayerAim` owns FOV, layer 8 reserved, `IWeapon`, `RubberHoseCrosshair` (no second crosshair), `CartoonFx` hit-stop (`timeScale` 0), no `UIManager`, existing sprite/audio importers and libraries. New since HUD.1: `PlayerAmmo` owns every reserve; `UiHideReasons` owns HUD visibility.
- **Missing assets never block.** Every builder uses a labelled placeholder and logs a warning; rerunning the menu picks up the real files. The audit lists every missing id.
- **Player-facing text in Spanish, exactly as in `Docs/guiones/Guion-HUD.md`.**
- **No per-frame GC** anywhere in this phase. All pools preallocated.

---

## 1. Decisions (do not re-discuss)
| # | Decision |
|---|---|
| H1 | The HUD is its own Canvas prefab `Prefabs/UI/HUD_Wilbur.prefab` (Screen Space Overlay), independent of `UIManager`. Sorting: world < **HUD** < `RubberHoseCrosshair` < interrogation / cinematic canvases. Placed idempotently in every gameplay scene by a menu, and it survives *Generar Nivel Porky*. |
| H2 | **Layout: one cluster bottom-left** (the hands own the bottom-right): Wilbur's face, the health bar, and under it the loaded rounds as icons plus the reserve "×18". |
| H3 | The HUD only listens to **events** (`PlayerHealth`, `IWeapon`, `PlayerAmmo`); the three legacy bools (pause, dialogue, interrogation) are synced once per frame by `UiHideReasons.SyncLegacyStates()`. |
| H4 | **The ammo reserve lives in one place: `PlayerAmmo`** (`AmmoType { Shotgun, Revolver }`). Weapons hold only the loaded rounds. |
| H5 | `IWeapon` ammo members: `AmmoType`, `LoadedRounds`, `MagazineSize`, `ReserveRounds`, `IsReloading`, `DisplayStyle { Shells, Cylinder }`, `OnAmmoChanged`. |
| H6 | **Visibility composes reasons** through `UiHideReasons` (Death, Pause, Dialogue, Interrogation, Cinematic, Holstered). Holstered hides only the ammo row. |
| H7 | All HUD animation runs on **unscaled time**. |
| H8 | **Feedback:** face flinch, bar shake, red edge flash, direction wedge (when the source is known), low-health pulse + heartbeat, heal flash; rounds ≤ 2 red; "¡RECARGA!" / "SIN MUNICIÓN". |
| H9 | **Enemy shots come out of the gun.** Muzzle flash, tracer **and the damage ray** start at the muzzle point of the sprite currently shown. If something blocks muzzle → player, the shot hits the blocker. Centavo's hit chance / damage / rate stay as they are; guards' ranged numbers are the new ones in H18. |
| H10 | Muzzle points are authored **by clicking on the sprite** in an Editor window and saved in a `MuzzlePointSet` asset per character (per frame, normalized). A character-level fallback point covers frames without one. |
| H11 | Enemy FX are pooled (no `Instantiate` per shot). Enemy muzzle flashes reuse the player's `fx_` flash art (scaled, slightly tinted) unless dedicated art exists. |
| H12 | **Near-miss:** a missed shot whose ray passes within 1.5 m of the camera plays a whiz SFX (max one per 0.3 s). |
| H13 | **Pickups use the project's existing interaction** (recon: what the revolver pickup / clues / dialogue use) with a prompt "[E] Tomar botiquín". Used **immediately** (no inventory). If the player is full the item is **not** consumed and a toast says so. |
| H14 | Quantities (all serialized, see table below). Revolver ammo can be picked up before having the revolver (stored in `PlayerAmmo`). |
| H15 | Pickup positions per level live in a ScriptableObject `PickupLayout_<Scene>`; the builder places them idempotently. Menu **Guardar posiciones de objetos** writes the markers' scene positions back to the asset. |
| H16 | **Guard drops:** a guard that **dies** (not surrendered) drops a shell box with 35 % chance (max 1 per guard). Never Centavo, never Don Lobo. |
| H17 | Restart after death reloads the scene (HUD.1 recon): health, ammo, pickups and drops reset with it. |
| H18 | **Guards get a pistol (ranged attack)**, keeping their current melee hit when the player is inside melee range. Ranged rules (all serialized): only with line of sight **from the muzzle**, range ≤ 18 m; they stop at 6–14 m on the NavMesh to shoot (no cover logic); **0.35 s aim telegraph** (aim frame, before the shot) so the player can react; one shot every 1.4–2.2 s; hit chance 30 % (15 % if the player is running); **8 damage**. Surrendered, dying or in-dialogue guards never shoot. The surrender behaviour (≤ 30 % HP) and patrol/chase logic stay as they are. |

**Quantities (starting values):**
| Item | Effect | Notes |
|---|---|---|
| Botiquín | +40 % of max HP | not consumed at full HP |
| Caja de cartuchos | +6 shotgun reserve | shotgun reserve cap 30 |
| Caja de balas de revólver | +6 revolver reserve | revolver reserve cap 24 |
| Drop of a dead guard | +4 shotgun reserve | 35 % chance |

---

## 2. Subphases

### HUD.1 — Health & ammo HUD + `PlayerAmmo` · ✅ CLOSED (3 Nov)
Audit: `Docs/audits/AUDIT_hud_1.md`. Commit `51f4e42`. Menus: *Crear Prefabs de Prueba* (adds `PlayerAmmo` to the Player) → **Plomo y Paja → HUD → Crear HUD**. Icons: `python Tools/character_key.py --center` → `Art/UI/HUD/` (256×256). Leftovers moved to HUD.4.

---

### HUD.2 — Enemy gunfire from the muzzle + guards with pistols
**Model:** Opus 5.5
**Touches:** `EnemyAI` (attack, chase, surrender), `EnemyHealth`, `CentavoController` (Combat shooting only), boss AI only if it exists, the guard billboard sprite component and the guard sprite sets, `PlayerHealth` (source position), `ShotgunFeedback` and `RevolverController` FX (player), `CartoonFx`, audio library + mixer groups, layers / physics matrix, NavMesh agents.

0. **Recon table first** (put it in the audit): every character that deals damage, class, melee or ranged, how damage is dealt today (origin, hit chance, damage, rate), which sprite frames exist for attacking / aiming / shooting, and its attack SFX.
1. **`MuzzlePointSet`** ScriptableObject (sprite → normalized point, plus fallback) and the Editor window **Plomo y Paja → HUD → Puntos de cañón**: pick a character's sprite set, click on each shooting frame to place the point (cross gizmo, undo, "copy to next frame"). The menu pre-creates an asset per shooter with a reasonable fallback (right-hand height). List which frames still have no point.
2. **`EnemyGunfire`** (reusable component, `Scripts/Combat/`): `Fire(Transform target, float hitChance, float damage, float spreadDeg)`.
   - Muzzle world point = billboard transform + (normalized point − pivot) × rendered size, along the billboard's right/up; respect `flipX`, the per-object scale (Centavo's `seatedScale`) and the current frame. Show the shooting frame the same frame the flash appears.
   - Ray from the muzzle: on hit (by hit chance) aim at the player's chest with tiny jitter; on miss aim at a random point 0.6–1.5 m beside the player. Layer mask so the shooter never hits itself.
   - **FX:** flash at the muzzle (2 frames, 0.05 s, plus a short warm point light), **tracer** muzzle → hit point (stretched cartoon streak, 0.06 s), **impact puff** + **bullet hole** decal on world hits (pool 32, fade after 10 s), 3D shot SFX at the muzzle (same mixer group as guard voices; `sfx_disparo_guardia` if present, otherwise reuse an existing pistol/revolver shot). Near-miss whiz per H12.
   - Player hit → `PlayerHealth.TakeDamage(damage, muzzlePos)`.
   - Debug gizmo (flag off by default) drawing muzzle point and ray.
3. **Guards (H18):** add the ranged attack to `EnemyAI` through `EnemyGunfire` without changing patrol, chase, surrender or dialogue. Frames: use the guard's aim/shoot frames if they exist; otherwise reuse the closest existing frame (attack or idle) with the fallback muzzle point, and list the missing frames as `guardia_apunta_01` / `guardia_dispara_01` for Edgar. Check that the yard guards still let the Centavo sequence (D2) and the surrender → dialogue flow work.
4. **Centavo:** only replace how his shot is produced in Combat with `EnemyGunfire` (same numbers); his states and the 3C.3/3C.4 hooks stay untouched. Cover must still block his shots and the player's pellets.
5. **Don Lobo:** only if a boss AI exists; otherwise say so and leave nothing half-done.
6. **Player side (pending from VM 1.4):** `ShotgunFeedback`'s light and onomatopoeia follow the viewmodel's muzzle (same for the revolver if it uses it); player pellet / bullet impacts use the same impact-puff/decal pool.
7. Performance: no GC per shot; report pool sizes.

**Audit template (HUD.2):**
```
- Tabla de quienes hacen daño (clase, cuerpo a cuerpo o disparo, antes y ahora):
- Guardias con pistola: números usados, ¿se siguen rindiendo y abriendo el diálogo? ¿el patio de Centavo sigue funcionando?
- Cuadros de los guardias para apuntar/disparar: ¿existían? ¿cuáles faltan para Edgar?
- Puntos de cañón: ¿qué cuadros quedaron sin punto? (personaje / sprite)
- ¿El disparo sale del arma en todos? ¿también con Centavo agachado/asomado?
- ¿El rayo de daño sale del cañón? ¿lo bloquean el banco y las paredes?
- Zumbido de bala cercana: ¿funciona?
- ShotgunFeedback: ¿la luz y la onomatopeya siguen la boca del cañón?
- Tamaños de pool y ¿hay GC por disparo?
- Menús que David debe correr:
- Archivos compartidos tocados (y si estaban limpios en git status):
- Qué no se pudo compilar/probar y qué debe revisar David en el Editor:
- Archivos creados/modificados:
- Commits:
```

---

### HUD.3 — Pickups: medkits, ammo boxes, guard drops
**Model:** Opus 5.5 · **After HUD.2; not in parallel with INTRO.2.**
**Touches:** the existing interaction system (the 3C.5 revolver pickup, `CluePickup`, `DialogueNPC`), `PlayerHealth` (`Heal`, `IsFullHealth`), `PlayerAmmo` (`TryAdd`, `IsFull`), `HudWilbur.ShowToast`, `EnemyAI` / `EnemyHealth` (death event, surrender), level builders (`LevelBuilder_NivelPorky`, `ContadorYardBuilder`; Level 2 builder only if it exists), audio library, `CharacterSpriteImporter`.

0. **Assets:** pickup sprites from `_entrante_hud/Objetos/` (magenta) → `python Tools/character_key.py Objetos` → `Art/Props/pickups/` (bottom-aligned, they rest on the floor). Import case PPU 500, pivot bottom-center, mipmaps on (bump the importer version). SFX through the library.
1. **`Pickup`** base + `MedkitPickup`, `AmmoPickup(AmmoType, amount)`: billboard sprite (reuse the guard billboard component, or what the revolver pickup uses), slow bob (±3 cm, 1.4 s) and a glint every 2–3 s, small warm point light (optional, serialized). Interaction per H13 reusing the prompt the revolver pickup shows on the crosshair. On use: effect, SFX, HUD toast, disappears. Full → not consumed + toast. Prefabs `Prefabs/Pickups/Pickup_Botiquin`, `Pickup_Cartuchos`, `Pickup_BalasRevolver`.
2. **Layout** (H15): `PickupLayout_NivelPorky` + menu **Plomo y Paja → HUD → Colocar objetos** (idempotent, also run by *Generar Nivel Porky*) and **Guardar posiciones de objetos**. Choose concrete spots from the level geometry and report them:
   - **Level 1:** 1 shell box in the house, 1 medkit before the yard gate, 1 shell box + 1 medkit inside or next to Centavo's shed (reward after the fight), 1 revolver box on the path out of the yard.
   - **Level 2** (only if the scene exists): 3 medkits, 4 shell boxes, 3 revolver boxes along the route; **before Don Lobo: 1 medkit + 1 of each ammo**. Otherwise: the layout asset type supports it and the audit says how Fase 4 hooks it.
3. **Drops** (H16): on a guard's death, 35 % chance to spawn `Pickup_Cartuchos` (+4) at its feet, snapped to the ground.
4. Restart per H17.

**Audit template (HUD.3):**
```
- Cómo se recoge (tecla / aviso reutilizado):
- Botiquín: ¿cura 40 %? ¿no se gasta con la vida llena?
- Munición: ¿respeta los topes? ¿la de revólver se guarda sin tener el revólver?
- Posiciones elegidas (nivel 1; nivel 2 si existe):
- Drops de guardias: ¿solo muertos? ¿nunca Centavo ni Don Lobo?
- ¿Sobreviven a "Generar Nivel Porky"? (sí/no)
- Menús que David debe correr (en orden):
- Ids faltantes y placeholders:
- Archivos compartidos tocados (y si estaban limpios en git status):
- Qué no se pudo compilar/probar y qué debe revisar David en el Editor:
- Archivos creados/modificados:
- Commits:
```

---

### HUD.4 — Playtest, polish and close
**Model:** Sonnet 5.5 · Merged into the 3C.6 / INTRO.4 session.

1. **Leftovers from HUD.1:** add `×` (U+00D7) to the Special Elite character list in `InterrogationUIBuilder.EnsureFont` and drop the LiberationSans `<font>` tag; decide one red damage flash (HUD edge flash vs. `NoirPostProcess` red vignette) and remove the other; 16:10 overlap with the shotgun's left arm (shrink the cluster ~10 % on aspect < 1.7 if it reads badly).
2. Fix what David reports from this checklist:
   - [ ] HUD visible in Level 1 (and Level 2 if it exists); readable at 16:9 / 16:10 / 21:9.
   - [ ] Health: face by band, flinch, ghost bar, low-health pulse + heartbeat, heal animation.
   - [ ] Damage direction wedge points to the real attacker.
   - [ ] Ammo: shells pop on fire, refill on reload, "¡RECARGA!" / "SIN MUNICIÓN"; revolver row (5); switching swaps the row.
   - [ ] HUD hides in interrogation (and epilogue), dialogue, pause, death; ammo hidden while holstered (INTRO.3).
   - [ ] Guards shoot with telegraph, flash + tracer from the gun, impacts on walls, whiz on near misses; they still surrender and talk.
   - [ ] Centavo shoots from his revolver; the bench blocks shots.
   - [ ] Player shotgun: light and onomatopoeia follow the muzzle.
   - [ ] Medkits and ammo: prompt, pickup, toast, caps, "full" case; drops from dead guards.
   - [ ] Die → restart: everything back to the initial state.
   - [ ] No GC spikes on firing; no console errors.
3. Debug flags off; remove dead code.
4. Audit `Docs/audits/AUDIT_hud_4.md` ending with a **block for the Registro de fases** (Resultado, Subfases, Decisiones, Para los demás, Pendientes, Commits), in Spanish.

**Audit template (HUD.4):**
```
- Checklist: estado de cada punto (OK / arreglado / pendiente):
- Restos de HUD.1 (×, viñeta roja doble, 16:10):
- Bugs arreglados:
- Debug apagado: (sí/no)
- Bloque para el Registro de fases:
- Commits:
```

---

## 3. Plan B (if time runs out), in this order
1. Muzzle points: fallback point only (no per-frame clicking).
2. Drop bullet-hole decals (keep the impact puff).
3. Drop guard drops; keep placed pickups.
4. Drop the direction wedge and the ghost bar.
5. Placeholders instead of missing art.
