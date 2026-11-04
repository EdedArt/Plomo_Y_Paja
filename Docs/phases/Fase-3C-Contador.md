# PHASE 3C — Centavo (the accountant): map, combat, interrogation scene, his revolver

> In the repo: `Docs/phases/Fase-3C-Contador.md`.
> **Branch:** `integracion/dia4` · **Owner:** David (Claude Code, orchestrated) · **Content source of truth:** `Docs/guiones/contador.md` (v1.2).
> **Deadline:** the game is delivered Wed 4 Nov 2026 → 3C should close by Mon 2 Nov night / Tue 3 Nov morning at the latest.
> Paths below are relative to `PLOMO_Y_PAJAV_1/Assets/_Game/` (the folder that contains `Scripts/` and `Art/`), unless they start with `docs/`, `Docs/`, `Tools/` or `_entrante_*`.

---

## 0. Rules for every subphase

1. **One subphase per session.** Do only the subphase you were asked for. At the end write its audit **in Spanish** to `Docs/audits/AUDIT_contador_3C<N>.md` using that subphase's audit template, then STOP. Do not start the next subphase.
2. **Recon first.** Before coding read `CLAUDE.md`, this file, the guion, and the previous 3C audits; query graphify (`Assets/_Game/Scripts`) for every system listed under *Touches*. In the audit, list which existing classes you reused and why.
3. **Never hand-edit `.unity` YAML.** Build scene content with Editor menu items under **Plomo y Paja → Contador → …** that create/update prefabs and place them **idempotently** (running twice does not duplicate). David runs the menu items and saves the scene. Report the exact menu items to run. Anything placed in `NivelPorky` must also survive **Plomo y Paja → Generar Nivel Porky** (hook into `ContadorYardBuilder` / `LevelBuilder_NivelPorky`).
4. **Existing contracts (from the Registro de fases — do not break):**
   - `PlayerAim` owns `Camera.fieldOfView`: never write FOV directly.
   - Layer 8 `Viewmodel` is reserved.
   - Weapons go through `IWeapon`; the crosshair is `UI/RubberHoseCrosshair` (don't create another).
   - `CartoonFx` hit-stop sets `Time.timeScale = 0`; the viewmodel uses `ViewmodelTime` (real time).
   - `UIManager` does not exist in any scene: do not depend on it.
   - Sprites are imported by `CharacterSpriteImporter.cs` and grouped by **Plomo y Paja → Reconstruir Sets de Sprites**; voices by `AudioImporter.cs` + **Plomo y Paja → Reconstruir Librería de Audio**. Extend these systems; do not create parallel ones.
   - Shed anchors come from `ContadorShed` (3C.1). Never hardcode world positions.
5. Code and identifiers in English. **Player-facing text in Spanish, exactly as in the guion** (keep ñ, ¿, ¡, …).
6. Centavo-specific code in `Scripts/Characters/Contador/`; reusable interrogation code in `Scripts/Interrogation/`; editor tools in `Scripts/Editor/` (CLAUDE.md). If recon shows a better existing place, use it and say so.
7. No silent TODOs in delivered paths. Debug helpers only behind a serialized `debug` flag that is **off** by default.
8. One commit (or a few) per subphase on `integracion/dia4`; list hashes in the audit.
9. There is no Unity Editor in the agent's environment: say clearly in the audit what was NOT compiled/tested and what David must check in the Editor.

---

## 1. Decisions (do not re-discuss)

| # | Decision |
|---|---|
| D1 | `registro_01` is obtainable **only** from Centavo (1A_v during the dialogue, or `perdon_02` when pardoned). Any other source is removed. *(Done in 3C.1: the level-2 "Contable" no longer gives it.)* |
| D2 | Centavo activates only when the yard guards (patroller + sentinel, assignable list) are **dead or surrendered**. Before that he idles in the shed and ignores the player. |
| D3 | Centavo never moves during combat. **Any** hit from the player (any weapon) downs him (no HP race); he cannot die from that first hit. |
| D4 | Shooting the wounded Centavo **before** the scene kills him: `death_01`, `contador = Dead`, interrogation = None, no `registro_01`, no interrogation counted. |
| D5 | Interrogation runs at `Time.timeScale = 0` with unscaled time for everything (camera, UI, typewriter, lip-sync). Voice AudioSources use `ignoreListenerPause = true`. |
| D6 | In the scene Centavo is a **UI portrait** (big, Mouse P.I. style); his world billboard is hidden during the scene and reappears in the resulting state. |
| D7 | Camera "crouch" moves/rotates the camera only. **No FOV change.** `PlayerAim` is released before the scene starts. |
| D8 | Lip-sync = per emotion pair `_01` (closed) / `_02` (open), switched by the voice RMS. Silence → `_01`. `blink_01` only for `talk`. |
| D9 | The interrogation UI is its own Canvas prefab, independent of `UIManager` and of any HUD. |
| D10 | A completed Centavo interrogation (reached the decision) counts toward `interrogationsCompleted` for Final C. |
| D11 | During the scene, the clue-pickup AI line and the kill AI line are suppressed (the scene already has its AI thought). |
| D12 | World size: Centavo's sprites (1024 canvas) use **PPU 500** (canvas ≈ 2.05 m, like the guards). The **seated** frames are scaled down in the world so he reads as someone sitting on the floor (≈ 1.0–1.1 m tall). |
| D13 | **Centavo's revolver becomes the player's second weapon.** The revolver he drops when hit is a pickup in **every** outcome (killed in the scene, pardoned, or killed before the scene per D4). It can be picked up as soon as it lies on the floor. |
| D14 | **Weapon roles:** shotgun = close range (wider spread + damage falloff with distance); revolver = long range, precise, one bullet. |
| D15 | Revolver damage is tuned as a **% of a guard's max HP** (≈ 40 % per body hit) so the surrender threshold (≤ 30 % HP) stays reachable; it must not one-shot guards. |

---

## 2. Subphases

### 3C.0 — Assets: chroma-key + import · ✅ CLOSED (1 nov)
Audit: `docs/audits/AUDIT_contador_assets.md`. `Tools/character_key.py` (magenta), 22 sprites 1024×1024 in `Art/Characters/contador/`, 21 + 8 voices complete, importer cases added. Originals in `_entrante_personajes/Contador/` (git-ignored). PPU fix moved to 3C.2 (D12).

### 3C.1 — Map: Porky's backyard + tool shed · ✅ CLOSED (2 nov)
Audit: `Docs/audits/AUDIT_contador_3C1.md`. Commit `68b8b53`.
- Menu **Plomo y Paja → Contador → Construir patio y cuarto** (`ContadorYardBuilder`), also run by *Generar Nivel Porky*.
- `ToolShed_Contador.prefab` at (-7, 0, 19), rot Y 180°, in `NivelPorky`: bench (cover), tools, barrels, oil lamp with `LightFlicker`.
- `ContadorShed` holds the anchors: `Anchor_CentavoStand`, `Anchor_CentavoSeated`, `Anchor_InterrogationCam`, `Exit_Path_0..3`, `Trigger_Activacion` (12 m, Ignore Raycast).
- Yard fence + gate; patroller crosses the gate (6 points), sentinel next to the gate. NavMesh baked by the menu.
- `registro_01` removed from the level-2 Contable (`DialogueDatabase`).

---

### 3C.2 — Centavo in the world: combat, wounded, death, escape
**Model:** Opus 5.5
**Touches:** `CharacterSpriteImporter` + the sprite sets built by "Reconstruir Sets de Sprites", damage pipeline used by the shotgun (`EnemyHealth` / damage interface), `EnemyAI` (guard dead/surrendered state), guard billboard sprite component, guard blood FX, `PlayerHealth`, `GameStateTracker`, AI kill hook, audio library + mixer groups, `ContadorShed`, `ContadorYardBuilder`, `DialogueDatabase` (Contable debt line).

0. Housekeeping: commit the pending 3C.0 files and the files generated by the 3C.1 menu as separate commits (nothing from `_entrante_*`). Level-2 Contable debt line → 3.200 pesos plus the week's interest, **only if** that line has no recorded clip (otherwise report and leave it).
1. **Sprites:** use the existing sprite-set system for Centavo (extend it if it can't express emotion pairs closed/open, `blink_01`, `death_01` and the limp cycle). Contador import case → **PPU 500** (D12), bump the importer version. Report missing frames.
2. **`LipSyncDriver`** (reusable, `Scripts/Interrogation/`): reads the voice `AudioSource` with `GetOutputData` (preallocated 256 buffer, no GC), RMS; open when RMS > threshold (threshold = 0.35 × running peak of the current clip, min 0.01) with hysteresis and min hold 0.06 s; outputs open/closed. Small adapters for `SpriteRenderer` and UI `Image`. Unscaled time.
3. **`CentavoController`** state machine: `Dormant → Alert → Combat → Hit → Wounded → (Interrogation, 3C.3) → Dead | Fleeing → Gone`. Placed by an idempotent menu (e.g. **Plomo y Paja → Contador → Colocar Centavo**) or inside `ContadorYardBuilder`.
   - **Dormant:** `idle_01/02` loop at `Anchor_CentavoStand`, ignores the player (D2).
   - **Alert:** guards neutralized + player inside `Trigger_Activacion` with line of sight → bark `contador_ver_01`, `peek_01`.
   - **Combat (never moves):** cover (1.0–1.8 s) → peek (0.4–0.7 s) → shoot (`shoot_01` + small shake, revolver "pop" SFX, small muzzle flash) → back to cover. 6 shots, then reload in cover 2 s (bark `contador_ver_02` once). Barks never overlap, ≥ 4 s apart; `ver_03` and `cubre_01` occasionally (cubre max once per 10 s). If there is no dedicated revolver SFX, reuse an existing gunshot with pitch ≈ 1.4.
   - **Aim:** hitscan toward the player, ≈ 20 % hit chance (lower if the player is running), 5 damage via `PlayerHealth`. Low threat by design.
   - **Hitbox:** capsule on a layer the shotgun hits; in cover it shrinks so the bench blocks pellets; full height when peeking/shooting.
   - **Hit (D3):** first player hit → `hit_01` 0.35 s, bark `contador_cae_01`, **revolver dropped as a separate world object on the floor** (3C.5 turns it into a pickup), cartoon blood burst (reuse guard FX), move to `Anchor_CentavoSeated` → Wounded. Integrate with the existing damage pipeline **without changing guard behaviour**.
   - **Wounded:** `herido` pair with lip-sync; small blood drip loop. `contador_herido_01` once when the player has LOS; `contador_herido_02` every 8–10 s while the player is between 3 and 12 m. Player ≤ 3 m with LOS → raise `OnInterrogationRequested` (3C.3 consumes it). Until 3C.3 exists: debug-only keys to force Dead/Fleeing.
   - **D4:** shot while wounded before the scene → Dead without interrogation.
   - **Dead:** `death_01` stays; collider no longer takes hits.
   - **Fleeing:** `limp_01/limp_02` alternating ≈ 4 fps, walks `Exit_Path` at ≈ 1.2 m/s, bark `contador_huye_01` at start, disabled at the end of the path (out of view).
   - Billboard: reuse the guard billboard component; bottom pivot; seated frames scaled per D12 (serialized `seatedScale`, default ≈ 0.6).
   - Audio: clips through the existing audio library; 3D `AudioSource` (spatialBlend 1, min 2 m, max 25 m), same mixer group as guard voices.
4. **`GameStateTracker`**: add `ContadorState { None, Pardoned, Dead }`, `ContadorInterrogation { None, Helpful, Misleading }`, `contadorLied` (bool), with events, and include them in the AI context builder. The kill in D4 fires the normal kill AI hook.

**Audit template (3C.2):**
```
- Commits de limpieza (3C.0 y menú de 3C.1):
- Línea del Contable: ¿se cambió? ¿tenía audio?
- Sistema de sprites: ¿se extendió el existente? ¿PPU 500 aplicado?
- Cómo se conecta el daño de la escopeta con Centavo (clase/método) y confirmación de que los guardias no cambiaron:
- Estados implementados y cómo se pasa de uno a otro:
- ¿Se activa solo con los guardias fuera de combate? (cómo lo probaste)
- ¿El banco bloquea los perdigones cuando está cubierto? (sí/no)
- Escala sentado (seatedScale) usada:
- Prueba del lip-sync en el mundo (herido_01/02):
- D4 (dispararle herido): resultado en GameStateTracker:
- Huida: ¿recorre el camino y desaparece? (sí/no)
- Revólver soltado: ¿queda en el piso como objeto aparte?
- Campos nuevos de GameStateTracker:
- Menús que David debe correr:
- Qué no se pudo compilar/probar y qué debe revisar David en el Editor:
- Archivos creados/modificados:
- Commits:
```

---

### 3C.3 — Interrogation scene: presentation (Mouse P.I. style)
**Model:** Opus 5.5
**Touches:** player input/movement, `PlayerAim`, the hook the existing guard dialogue uses to hide the viewmodel, `RubberHoseCrosshair`, existing `DialogueNPC` UI (reuse what helps, don't break guards), pause menu, URP Volumes, audio mixer.

1. **`InterrogationDirector`** (reusable): `Enter(target, data)` / `Exit(result)`. **Enter**, all unscaled:
   1. Lock player input, release `PlayerAim` (D7), hide viewmodel and crosshair (same mechanism as existing dialogue), unlock and show the cursor.
   2. Camera crouch: move/rotate the camera to `Anchor_InterrogationCam` over 0.6 s (ease in-out). No FOV writes.
   3. `Time.timeScale = 0` (store the previous value; handle an in-progress `CartoonFx` hit-stop). Duck world audio (mixer snapshot if a mixer exists; otherwise leave ambience). Voices always audible (D5).
   4. Blur + dim: dedicated high-priority Volume blended 0 → 1 in 0.35 s: Depth of Field Gaussian (start 0, end ≈ 1 m, max radius ≈ 1.5), Vignette +0.15, Color Adjustments postExposure −0.6.
   5. Hide the target's world billboard; show the portrait.
   **Exit:** reverse blend, restore timeScale and audio, camera back to the player's head in 0.4 s, restore input/viewmodel/crosshair/cursor lock, show the world billboard in the resulting state.
2. **`InterrogationCanvas.prefab`** (own Canvas, Screen Space Overlay, above crosshair/HUD), created by menu `Plomo y Paja → Contador → Crear UI de interrogatorio`:
   - **Portrait:** UI `Image`, bottom-center anchored, ≈ 78 % of screen height, preserveAspect. Entrance: scale 0.92 → 1 + fade 0.25 s. Alive motion: breathing squash/stretch (±1.5 % Y, 1.6 s period); for `scared`/`nervous` a stepped wobble (±2 px at 12 Hz, rubber-hose "boil").
   - **Lip-sync:** `LipSyncDriver` on the scene voice source → swaps `_01/_02` of the current emotion; `blink_01` every 2.5–5 s for 0.12 s, only for `talk` while silent.
   - **Dialogue paper** (bottom, over the lower part of the portrait): `Art/UI/ui_papel_dialogo.png` if present, otherwise a 9-sliced cream panel with a dark rough border. Speaker name (CENTAVO / WILBUR) + text with typewriter effect over ≈ 0.9 × clip length. Click/Space completes the text; a second click skips the clip.
   - **Options:** 2–3 buttons on the paper, keys 1/2/3 + mouse, hover highlight.
   - **AI thought band** (top): italic text, fade in, animated "…" while waiting.
   - **Decision:** two big buttons "Jalar el gatillo" / "Dejarlo ir".
   - **Font:** Special Elite (TMP dynamic font asset from `Fonts/SpecialElite-Regular.ttf` if present; otherwise project default and report). Must render ñ ¿ ¡ á é í ó ú.
3. **Pause menu** during the scene: either disabled or pauses/resumes cleanly; report which.
4. This subphase wires `OnInterrogationRequested` and plays a **short demo** (Ronda 0 only, with real clips) to validate presentation. Full content is 3C.4.

**Audit template (3C.3):**
```
- Cómo se ocultan manos y mira (mecanismo reutilizado):
- Entrada y salida: ¿se restaura todo? (timeScale, cursor, input, FOV intacto, manos, mira)
- ¿Funciona si entra durante un hit-stop de CartoonFx? (sí/no)
- Desenfoque: ¿aplica solo al mundo? (sí/no)
- Lip-sync en el retrato: ¿se ve natural? umbral usado:
- Fuente: ¿Special Elite? ¿se ven ñ, ¿, ¡ y tildes?
- Menú de pausa durante la escena: comportamiento elegido:
- Menús que David debe correr:
- Qué no se pudo compilar/probar y qué debe revisar David en el Editor:
- Archivos creados/modificados:
- Commits:
```

---

### 3C.4 — Content, logic, AI thought and consequences
**Model:** Opus 5.5
**Touches:** `GameStateTracker`, `AICompanion` (OpenAI) + fallback lines system, clue system (`registro_01` notification), `FinaleTrigger` conditions, audio library.

1. **`InterrogationData`** ScriptableObject + `Interrogation_Contador.asset` with every line of guion §5 (id, speaker, text, emotion, conditions, effects), built by menu `Plomo y Paja → Contador → Crear datos del interrogatorio`. Texts copied **exactly** from the guion. Clips resolved through the existing audio library; the menu lists any id without a clip.
2. **Flow:** `contador_int_00` → round 0 choice (play the `wilbur_int_0X` clip, then the response) → confidence → round 1 choice → response by condition → `contador_int_2` → AI thought → decision → decision lines → `Exit` → world consequence.
3. **Rules (guion §5):** confidence starts 0; 0A −1, 0B +1, 0C 0. Round 1: ≥ 0 → `_v` (Helpful), < 0 → `_m` (Misleading); 1C always (Helpful, no amount). `contadorLied` = chose 0A **or** got any `_m` response.
4. **`registro_01`:** given on `1A_v` during the dialogue, or by `perdon_02` when pardoned if not yet given. Use the same API as `CluePickup` (counts toward the clues, shows the clue notification) but suppress the clue AI line (D11).
5. **AI thought:** `AICompanion` with context (who Centavo is, wounded, the response texts he said, lied or not, confidence, registro given or not). Ask for 1–2 sentences, Spanish, noir, ambiguous, ≤ 25 words, never telling the player what to choose. Timeout 5 s real time → random fallback from guion §5 (the "colgaron de los pies" line only if he said `1B_m`; when it applies, pick it 50 % of the time). Never block input.
6. **Decision:**
   - **Jalar el gatillo:** `wilbur_dec_matar` → `contador_muerte_01` cut by a shotgun blast SFX ≈ 0.2 s before the clip ends → `Exit` → world `death_01` → `contador = Dead`. Kill AI line suppressed (D11).
   - **Dejarlo ir:** `wilbur_dec_perdonar` → `contador_perdon_01` (`relief`) → `contador_perdon_02` (`talk`, gives `registro_01`) only if not yet given → `Exit` → Fleeing → `contador = Pardoned`.
7. **`GameStateTracker`:** set `ContadorInterrogation`, `contadorLied`, `interrogationsCompleted++` (D10), no double counting. Verify the three finales still compute correctly.

**Audit template (3C.4):**
```
- Clips faltantes según el menú de validación:
- Tabla de los 9 caminos (ronda 0 × ronda 1): respuesta, Helpful/Misleading, ¿mintió?, ¿registro_01?
- Matar / perdonar: ¿registro_01 sale según el guion en todos los casos?
- IA: ejemplo de 2 pensamientos reales + prueba sin conexión (línea de respaldo usada):
- GameStateTracker al final (los dos caminos):
- ¿Siguen bien las condiciones de los 3 finales?
- Qué no se pudo compilar/probar y qué debe revisar David en el Editor:
- Archivos creados/modificados:
- Commits:
```

---

### 3C.5 — Centavo's revolver: pickup, second weapon, weapon balance
**Model:** Opus 5.5
**Touches:** `IWeapon`, the shotgun controller, `PlayerAim`, `ViewmodelProfile` + `VM_Revolver.asset` + revolver art/FX (VM 1.1–1.4), `ViewmodelAnimator`/`ViewmodelMotion`, `RubberHoseCrosshair`, player input, `docs/phases/PHASE-VM2.md`, `EnemyHealth` (surrender threshold), boss AI (Don Lobo HP), `CentavoController` (dropped revolver), clue notification UI, `ContadorYardBuilder`.

1. **Read `PHASE-VM2.md` first.** This subphase implements the **minimum of VM2** needed to play: a `RevolverController : IWeapon` using `VM_Revolver`, a small weapon inventory with switching, and the revolver reload. Anything in VM2 beyond that stays pending; list it in the audit.
   - Switching: keys **1** = shotgun, **2** = revolver, mouse wheel cycles. Lower/raise the hands ≈ 0.35 s (reuse `ViewmodelMotion`). Switching cancels a reload in progress. No switching during interrogation/dialogue/pause.
   - Revolver held with one hand; the left hand appears only to reload (Registro decision). Chamber bullets placed by code if the VM2 art supports it; otherwise a simple reload animation, reported.
   - Before the pickup the player only has the shotgun (key 2 does nothing).
2. **Pickup (D13):** the revolver Centavo drops in 3C.2 becomes a `WeaponPickup` on the floor (small prop or billboard of the revolver with a subtle glint), picked up with the project's existing interaction method (recon: key or walk-over; use what guards/clues use). Available as soon as it lies on the floor, in every outcome. Gives the revolver with **6 loaded + 12 reserve**, auto-switches to it the first time, shows a short notification "Revólver de Centavo" (reuse the clue notification if possible; it is NOT a clue). It must also work if Centavo was killed before the scene (D4).
3. **Balance (D14, D15).** Read the current values and put every number in serialized fields / ScriptableObjects. Starting targets:
   - **Revolver:** one hitscan bullet; spread ≈ 0.5° hip-fire, ≈ 0° aiming; range ≈ 80 m with no falloff; ≈ 0.45 s between shots; damage ≈ 40 % of a guard's max HP per body hit; reload ≈ 2.2 s.
   - **Shotgun:** base spread ≈ ×1.5 the current value (keep `PlayerAim`'s ×0.6 when aiming); damage falloff: 100 % up to 6 m → 25 % at 20 m → 0 beyond 30 m. Point-blank must feel as strong as now.
   - Check: guards can still be brought to surrender (≤ 30 % HP) with both weapons; Centavo D3 works with both; the Don Lobo fight is still doable with each weapon (report time-to-kill estimates).
4. **Crosshair:** `RubberHoseCrosshair` reflects each weapon's spread through `IWeapon` (shotgun wider, revolver tight). Verify or extend.
5. No ammo HUD exists (pending 3B Part 4): do not build a full HUD here; report how the player knows the revolver's ammo.
6. The pickup is placed/kept by the same menus, so *Generar Nivel Porky* doesn't lose it.

**Audit template (3C.5):**
```
- Qué parte de VM2 se hizo y qué quedó pendiente:
- Cambio de armas: teclas, tiempo, ¿cancela la recarga?
- Recogida: cómo se recoge, ¿funciona en los 3 desenlaces (matado en escena, perdonado, matado antes)?
- Tabla de balance antes/después (escopeta y revólver):
- ¿Los guardias se siguen rindiendo con ambas armas? ¿Don Lobo es vencible con cada una? (estimaciones)
- Mira: ¿cambia según el arma?
- Cómo sabe el jugador cuántas balas tiene:
- Menús que David debe correr:
- Qué no se pudo compilar/probar y qué debe revisar David en el Editor:
- Archivos creados/modificados:
- Commits:
```

---

### 3C.6 — Full playtest, polish and close
**Model:** Sonnet 5.5

1. Fix what David reports from this checklist (he plays; you fix):
   - [ ] Guards block the gate; Centavo idles until both are down/surrendered.
   - [ ] Combat: cover/peek/shoot reads well, barks don't overlap, low threat.
   - [ ] First hit downs him (shotgun and revolver); blood; seated pose aligned with the bench and correct size.
   - [ ] Wounded barks; approach ≤ 3 m starts the scene.
   - [ ] Scene: crouch, blur, portrait, paper, lip-sync on every line, options, AI thought, decision.
   - [ ] Kill branch and pardon branch (limp out through the gate).
   - [ ] D4: shooting him wounded before the scene.
   - [ ] Revolver pickup in the 3 outcomes; switching 1/2/wheel; reload.
   - [ ] Revolver precise at long range; shotgun strong close, weak far.
   - [ ] Player dies during the fight → normal restart flow, Centavo state resets correctly.
   - [ ] Esc during the scene.
   - [ ] Everything restored after the scene (move, aim, FOV, hands, crosshair, cursor, current weapon).
   - [ ] No GC spikes from lip-sync; no errors in the console.
2. Debug flags off. Remove dead code from earlier subphases.
3. Audit `Docs/audits/AUDIT_contador_3C6.md`, ending with a **block for the Registro de fases** (Resultado, Subfases table, Decisiones, Para los demás, Pendientes, Commits), in Spanish.

**Audit template (3C.6):**
```
- Checklist: estado de cada punto (OK / arreglado / pendiente):
- Bugs arreglados en esta subfase:
- Debug apagado: (sí/no)
- Bloque para el Registro de fases:
- Commits:
```
