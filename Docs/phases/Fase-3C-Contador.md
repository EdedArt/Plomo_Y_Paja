# PHASE 3C — Centavo (the accountant): map, combat, interrogation scene

> Copy to the repo as `docs/phases/PHASE-3C-contador.md`.
> **Branch:** `integracion/dia4` · **Owner:** David (Claude Code, orchestrated) · **Content source of truth:** `Docs/guiones/contador.md` (v1.2).
> **Deadline:** the game is delivered Wed 4 Nov 2026 → 3C should close by Mon 2 Nov night.
> Paths below are relative to `PLOMO_Y_PAJAV_1/Assets/_Game/` (the folder that contains `Scripts/` and `Art/`), unless they start with `docs/`, `Docs/`, `Tools/` or `_entrante_*`.

---

## 0. Rules for every subphase

1. **One subphase per session.** Do only the subphase you were asked for. At the end write its audit **in Spanish** to `docs/audits/AUDIT_contador_3C<N>.md` using that subphase's audit template, then STOP. Do not start the next subphase.
2. **Recon first.** Before coding read `CLAUDE.md`, this file, the guion, and the previous 3C audits; query graphify (`Assets/_Game/Scripts`) for every system listed under *Touches*. In the audit, list which existing classes you reused and why.
3. **Never hand-edit `.unity` YAML.** Build scene content with Editor menu items under **Plomo y Paja → Contador → …** that create/update prefabs and place them **idempotently** (running twice does not duplicate). David runs the menu items and saves the scene. Report the exact menu items to run.
4. **Existing contracts (from the Registro de fases — do not break):**
   - `PlayerAim` owns `Camera.fieldOfView`: never write FOV directly.
   - Layer 8 `Viewmodel` is reserved.
   - Weapons go through `IWeapon`; the crosshair is `UI/RubberHoseCrosshair` (don't create another).
   - `CartoonFx` hit-stop sets `Time.timeScale = 0`; the viewmodel uses `ViewmodelTime` (real time).
   - `UIManager` does not exist in any scene: do not depend on it.
   - Sprites are imported by `CharacterSpriteImporter.cs` and grouped by the menu **Plomo y Paja → Reconstruir Sets de Sprites**; voices by `AudioImporter.cs` + **Plomo y Paja → Reconstruir Librería de Audio**. Extend these systems; do not create parallel ones.
5. Code and identifiers in English. **Player-facing text in Spanish, exactly as in the guion** (keep ñ, ¿, ¡, …).
6. Centavo-specific code in `Scripts/Characters/Contador/`; reusable interrogation code in `Scripts/Interrogation/` (guards may reuse it later). If recon shows a better existing place, use it and say so.
7. No silent TODOs in delivered paths. Debug helpers only behind a serialized `debug` flag that is **off** by default.
8. One commit (or a few) per subphase on `integracion/dia4`; list hashes in the audit.
9. There is no Unity Editor in the agent's environment: say clearly in the audit what was NOT compiled/tested and what David must check in the Editor.

---

## 1. Decisions (do not re-discuss)

| # | Decision |
|---|---|
| D1 | `registro_01` is obtainable **only** from Centavo (1A_v during the dialogue, or `perdon_02` when pardoned). Any other source is removed. |
| D2 | Centavo activates only when the yard guards (patroller + sentinel, assignable list) are **dead or surrendered**. Before that he idles in the shed and ignores the player. |
| D3 | Centavo never moves during combat. **Any** hit from the player downs him (no HP race); he cannot die from that first hit. |
| D4 | Shooting the wounded Centavo **before** the scene kills him: `death_01`, `contador = Dead`, interrogation = None, no `registro_01`, no interrogation counted. |
| D5 | Interrogation runs at `Time.timeScale = 0` with unscaled time for everything (camera, UI, typewriter, lip-sync). Voice AudioSources use `ignoreListenerPause = true`. |
| D6 | In the scene Centavo is a **UI portrait** (big, Mouse P.I. style); his world billboard is hidden during the scene and reappears in the resulting state. |
| D7 | Camera "crouch" moves/rotates the camera only. **No FOV change.** `PlayerAim` is released before the scene starts. |
| D8 | Lip-sync = per emotion pair `_01` (closed) / `_02` (open), switched by the voice RMS. Silence → `_01`. `blink_01` only for `talk`. |
| D9 | The interrogation UI is its own Canvas prefab, independent of `UIManager` and of any HUD. |
| D10 | A completed Centavo interrogation (reached the decision) counts toward `interrogationsCompleted` for Final C. |
| D11 | During the scene, the clue-pickup AI line and the kill AI line are suppressed (the scene already has its AI thought). |
| D12 | World size: Centavo's sprites (1024 canvas) use **PPU 500** (canvas ≈ 2.05 m, same as the guards' 512 canvas at PPU 250). The **seated** frames fill the canvas, so in the world they are scaled down so he reads as a person sitting on the floor (≈ 1.0–1.1 m tall), not a giant. |

---

## 2. Subphases

### 3C.0 — Assets: chroma-key + import · ✅ CLOSED (1 nov)
Audit: `docs/audits/AUDIT_contador_assets.md`.
- `Tools/character_key.py` (magenta key, reuses `viewmodel_key.py` helpers; viewmodel tool untouched).
- 22 sprites 1024×1024 RGBA in `Art/Characters/contador/`, single scale factor, bottom-aligned.
- 21 + 8 voices complete and correctly named (mono, 44.1 kHz) in `Audio/Voices/contador/` and `Audio/Voices/wilbur/` (wilbur also has the older `wilbur_dano_01..03`, `wilbur_pocavida_01`).
- `CharacterSpriteImporter.cs`: contador case (pivot bottom-center, mipmaps on). `AudioImporter.cs`: preloadAudioData for voices.
- Originals live in `_entrante_personajes/Contador/` (git-ignored).
- Known leftover: PPU still 250 → fixed in 3C.2 (D12).

---

### 3C.1 — Map: Porky's backyard + tool shed
**Model:** Opus 5.5
**Touches:** Level 1 scene (Porky's house), guard patrol waypoints / sentinel placement, NavMesh, `CluePickup` + any source of `registro_01`, scene lighting.

1. **Builder menu** `Plomo y Paja → Contador → Construir patio y cuarto` that creates/updates `Prefabs/Level1/ToolShed_Contador.prefab` and places it in the backyard:
   - Shed ≈ 4 × 3 m of crooked planks (primitives with ±3° rotations from a **fixed seed**, deterministic), tilted roof, open doorway facing the yard gate. Rubber-hose cartoon feel: slightly bent, chunky, readable. Reuse project materials if any fit.
   - **Workbench** just inside the doorway, ≈ 1.0 m high, solid `BoxCollider` → Centavo's cover. Hanging tools on the back wall (simple shapes), 2–3 barrels, **oil lamp** on the bench: warm orange Point Light (range ≈ 6 m, soft shadows) with a gentle flicker script.
   - Child anchors (empty GameObjects with gizmos): `Anchor_CentavoStand` (behind the bench, facing the doorway), `Anchor_CentavoSeated` (floor, in front of the bench, leaning on it), `Anchor_InterrogationCam` (≈ 1.5 m in front of the seated anchor, ≈ 0.9 m high, looking at him), `Exit_Path` (2–4 waypoints from the shed through the yard gate to a point out of view).
   - Activation trigger: sphere radius 12 m centered on the doorway (gizmo).
2. **Backyard:** fence + **yard gate** between the house's back area and the shed. The gate is on the guards' route: adjust the patroller's waypoints so he crosses the gate and place the sentinel guarding it. Fence colliders closed: the shed is unreachable without passing the gate.
3. **NavMesh:** rebake (or give David the exact manual steps if it must be done by hand). Guards still patrol correctly.
4. **D1:** find every source of `registro_01` (CluePickup, guard dialogue, etc.) and remove/disable it. Report where it was.
5. **Lighting:** the yard must be readable (the level was reported too dark). Lamp + one cold moon/street accent. Do not change global post-processing.

**Audit template (3C.1):**
```
- Menús que David debe correr (en orden):
- Prefab creado y dónde quedó en la escena:
- Anclas creadas (nombre + posición):
- Cambios a la ruta del patrullero y al centinela:
- ¿Se puede llegar al cuarto sin pasar por el portón? (sí/no + cómo lo comprobaste)
- NavMesh: ¿rebake hecho? (pasos si es manual)
- registro_01: ¿dónde estaba antes y qué se quitó?
- Qué no se pudo compilar/probar y qué debe revisar David en el Editor:
- Archivos creados/modificados:
- Commits:
```

---

### 3C.2 — Centavo in the world: combat, wounded, death, escape
**Model:** Opus 5.5
**Touches:** `CharacterSpriteImporter` + the sprite sets built by "Reconstruir Sets de Sprites", damage pipeline used by the shotgun (`EnemyHealth` / damage interface), `EnemyAI` (guard dead/surrendered state), guard billboard sprite component, guard blood FX, `PlayerHealth`, `GameStateTracker`, AI kill hook, audio library + mixer groups.

1. **Sprites:** use the existing sprite-set system for Centavo (extend it if it can't express emotion pairs closed/open, `blink_01`, `death_01` and the limp cycle). Set the contador import case to **PPU 500** (D12) and bump the importer version. Report missing frames.
2. **`LipSyncDriver`** (reusable, `Scripts/Interrogation/`): reads the voice `AudioSource` with `GetOutputData` (preallocated 256 buffer, no GC), RMS; open when RMS > threshold (threshold = 0.35 × running peak of the current clip, min 0.01) with hysteresis and min hold 0.06 s; outputs open/closed. Small adapters for `SpriteRenderer` and UI `Image`. Unscaled time.
3. **`CentavoController`** state machine: `Dormant → Alert → Combat → Hit → Wounded → (Interrogation, 3C.3) → Dead | Fleeing → Gone`.
   - **Dormant:** `idle_01/02` loop at `Anchor_CentavoStand`, ignores the player (D2).
   - **Alert:** guards neutralized + player inside 12 m with line of sight → bark `contador_ver_01`, `peek_01`.
   - **Combat (never moves):** cover (1.0–1.8 s) → peek (0.4–0.7 s) → shoot (`shoot_01` + small shake, revolver "pop" SFX, small muzzle flash) → back to cover. 6 shots, then reload in cover 2 s (bark `contador_ver_02` once). Barks never overlap, ≥ 4 s apart; `ver_03` and `cubre_01` occasionally (cubre max once per 10 s). If there is no dedicated revolver SFX, reuse an existing gunshot with pitch ≈ 1.4.
   - **Aim:** hitscan toward the player, ≈ 20 % hit chance (lower if the player is running), 5 damage via `PlayerHealth`. Low threat by design.
   - **Hitbox:** capsule on a layer the shotgun hits; in cover it shrinks so the bench blocks pellets; full height when peeking/shooting.
   - **Hit (D3):** first player hit → `hit_01` 0.35 s, bark `contador_cae_01`, revolver dropped, cartoon blood burst (reuse guard FX), move to `Anchor_CentavoSeated` → Wounded. Integrate with the existing damage pipeline **without changing guard behaviour**.
   - **Wounded:** `herido` pair with lip-sync; small blood drip loop. `contador_herido_01` once when the player has LOS; `contador_herido_02` every 8–10 s while the player is between 3 and 12 m. Player ≤ 3 m with LOS → raise `OnInterrogationRequested` (3C.3 consumes it). Until 3C.3 exists: debug-only keys to force Dead/Fleeing.
   - **D4:** shot while wounded before the scene → Dead without interrogation.
   - **Dead:** `death_01` stays; collider no longer takes hits.
   - **Fleeing:** `limp_01/limp_02` alternating ≈ 4 fps, walks `Exit_Path` at ≈ 1.2 m/s, bark `contador_huye_01` at start, disabled at the end of the path (out of view).
   - Billboard: reuse the guard billboard component; bottom pivot; seated frames scaled per D12 (serialized `seatedScale`, default ≈ 0.6, tuned so he looks seated against the bench).
   - Audio: clips through the existing audio library; 3D `AudioSource` (spatialBlend 1, min 2 m, max 25 m), same mixer group as guard voices.
4. **`GameStateTracker`**: add `ContadorState { None, Pardoned, Dead }`, `ContadorInterrogation { None, Helpful, Misleading }`, `contadorLied` (bool), with events, and include them in the AI context builder. The kill in D4 fires the normal kill AI hook.

**Audit template (3C.2):**
```
- Sistema de sprites: ¿se extendió el existente? ¿PPU 500 aplicado?
- Cómo se conecta el daño de la escopeta con Centavo (clase/método) y confirmación de que los guardias no cambiaron:
- Estados implementados y cómo se pasa de uno a otro:
- ¿Se activa solo con los guardias fuera de combate? (cómo lo probaste)
- ¿El banco bloquea los perdigones cuando está cubierto? (sí/no)
- Escala sentado (seatedScale) usada:
- Prueba del lip-sync en el mundo (herido_01/02):
- D4 (dispararle herido): resultado en GameStateTracker:
- Huida: ¿recorre el camino y desaparece? (sí/no)
- Campos nuevos de GameStateTracker:
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

### 3C.5 — Full playtest, polish and close
**Model:** Sonnet 5.5

1. Fix what David reports from this checklist (he plays; you fix):
   - [ ] Guards block the gate; Centavo idles until both are down/surrendered.
   - [ ] Combat: cover/peek/shoot reads well, barks don't overlap, low threat.
   - [ ] First hit downs him; blood; seated pose aligned with the bench and correct size.
   - [ ] Wounded barks; approach ≤ 3 m starts the scene.
   - [ ] Scene: crouch, blur, portrait, paper, lip-sync on every line, options, AI thought, decision.
   - [ ] Kill branch and pardon branch (limp out through the gate).
   - [ ] D4: shooting him wounded before the scene.
   - [ ] Player dies during the fight → normal restart flow, Centavo state resets correctly.
   - [ ] Esc during the scene.
   - [ ] Everything restored after the scene (move, aim, FOV, hands, crosshair, cursor).
   - [ ] No GC spikes from lip-sync; no errors in the console.
2. Debug flags off. Remove dead code from earlier subphases.
3. Audit `docs/audits/AUDIT_contador_3C5.md`, ending with a **block for the Registro de fases** (Resultado, Subfases table, Decisiones, Para los demás, Pendientes, Commits), in Spanish.

**Audit template (3C.5):**
```
- Checklist: estado de cada punto (OK / arreglado / pendiente):
- Bugs arreglados en esta subfase:
- Debug apagado: (sí/no)
- Bloque para el Registro de fases:
- Commits:
```
