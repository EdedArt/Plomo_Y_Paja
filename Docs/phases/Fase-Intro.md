# PHASE INTRO — Storybook intro, taxi arrival, unarmed start

> Copy to the repo as `docs/phases/PHASE-INTRO.md`.
> **Owner:** David (Claude Code, orchestrated) · **Content source of truth:** `Docs/guiones/intro.md` (v1.0).
> **Deadline:** game delivered Wed 4 Nov 2026 → INTRO should close by Tue 3 Nov midday.
> **Branch:** `integracion/dia4` (same as Phase 3C). Paths are relative to `PLOMO_Y_PAJAV_1/Assets/_Game/` unless they start with `docs/`, `Docs/`, `Tools/` or `_entrante_*`.

## Scheduling with Phase 3C (read this first)
| Subphase | Can run… | Why |
|---|---|---|
| INTRO.1 (book) | **in parallel** with any 3C session | new scene, new scripts, new folders only |
| INTRO.2 (taxi + arrival) | **after 3C.1 is closed** | both edit the Level 1 scene and its builders |
| INTRO.3 (unarmed + draw) | **after 3C.2 is closed** | both touch the weapon, viewmodel hiding and enemy events |
| INTRO.4 (close) | together with 3C.5 if time is short | same playtest session |

**Parallel-session git rule:** two sessions may share the same working copy. Therefore **never** `git add -A` / `git add .`: stage only the files you created or changed, by explicit path. Before editing a shared file (`AudioImporter.cs`, `CharacterSpriteImporter.cs`, `EditorBuildSettings.asset`, `GameStateTracker.cs`, `CLAUDE.md`), run `git status`. If that file already has uncommitted changes you did not make, **don't edit it**: report in the audit what change it needs.

---

## 0. Rules for every subphase
Rules 1–9 of `docs/phases/PHASE-3C-contador.md` §0 apply here **verbatim**, with these substitutions:
- Audits go to `docs/audits/AUDIT_intro_<N>.md` (in Spanish), using the template of the subphase.
- Editor menus live under **Plomo y Paja → Intro → …** and are idempotent.
- Intro code goes in `Scripts/Intro/`. Reusable cinematic bits (letterbox, subtitles, hold-to-skip) go in `Scripts/Cinematics/`.
- The existing contracts in 3C §0.4 still hold: `PlayerAim` owns FOV, layer 8 is reserved, `IWeapon`, `RubberHoseCrosshair`, `CartoonFx` hit-stop, no `UIManager`, existing importers and libraries.

**Missing assets never block.** Art and audio may arrive after the code. Every builder uses a clearly labelled placeholder (a grey quad reading "ILUSTRACIÓN 01", a 0.5 s silent clip, etc.) and logs a warning. When David drops the real files in and reruns the menu, the builder picks them up. The audit lists every missing id.

---

## 1. Decisions (do not re-discuss)
| # | Decision |
|---|---|
| I1 | The book lives in its own scene, `Intro.unity` (in the project's scenes folder; recon). Flow: main menu "Jugar" (if a menu exists) → `Intro` → Level 1. Level 1 can still be opened and played directly in the Editor. |
| I2 | The book is **3D** (real perspective, lit by the lamp). Cover and leaves are **rigid two-sided quads hinged at the spine** that rotate 0 → 180°. No bending page meshes. |
| I3 | Page text is **TextMeshPro** (Special Elite if present in `Fonts/`; otherwise the project default, and report it), never baked into images. It types on over 0.85 × the clip length and must render ñ ¿ ¡ á é í ó ú …. |
| I4 | The page turns automatically 0.8 s after its clip ends. A click or a tap of Space completes the text and turns the page at once. |
| I5 | **Skip:** hold Space or Esc for 1 s (radial hint "Mantén ESPACIO para saltar"). In the book it goes straight to Level 1; in the taxi or arrival it jumps to the end state: player at the curb, taxi gone, control returned, weapon still holstered. |
| I6 | The taxi and arrival happen in the **Level 1 scene** and play **once per game session**. A restart after death spawns the player at the curb with no cinematic. A serialized `playIntroCinematic` (default on) turns it off for testing. |
| I7 | Cinematics **never write FOV** (`PlayerAim` contract). Input is locked, and guards cannot detect or damage the player while a cinematic runs. |
| I8 | **Unarmed start:** the weapon is holstered (viewmodel and crosshair hidden; fire, aim and reload ignored). The first trigger that fires draws it: a guard is visible (inside the camera frustum, ≤ 30 m, raycast line of sight), the player takes damage, any guard goes alert or starts chasing, or the player enters the fallback trigger at Porky's door. Once drawn it is never holstered again, and restarts begin armed. |
| I9 | The first AI thought at the reveal is the **fixed line** from guion §4, shown through the existing AI-thought display (no API call). |
| I10 | Cinematic letterbox (2.35:1, animated over 0.4 s) and subtitles come from a small reusable `CinematicSubtitles` / `Letterbox` on their own Canvas: speaker name in caps plus text, Special Elite, at the bottom. Independent of `UIManager` and the HUD. |
| I11 | There is no silent gap between the book and Level 1. Level 1 loads async (start loading during page 5); the rain ambience fades in over the final fade-to-black and Level 1 opens with the same rain already playing. |
| I12 | Holstering is one **hide reason** among others (interrogation, dialogue, pause, death). Viewmodel and crosshair visibility must compose reasons (a set or counter), never a single shared bool that one system can clear for another. |

---

## 1b. Amendments after INTRO.0 (A1–A5)
Agreed in the INTRO.0 review (3 Nov). **They override the rest of this file where they conflict.**

| # | Amendment |
|---|---|
| A1 | **Spread layout** (replaces "illustration left / text right" in INTRO.1 step 2). Edgar's illustrations (`Art/Intro/ilus_intro_01..06`) are 16:9, 1920×1080. Each story spread shows the illustration **across both pages** (Cuphead style), in the upper ~60–65 % of the spread, inset a little from the paper edges, with a thin dark ink frame. The page text goes in a band below it, centred across the spread, Special Elite, dark ink colour, typewriter per I3/I4. **Implementation:** each spread (paper + illustration + text) is composed into a RenderTexture by an offscreen camera/canvas. The left half of the RT is UV-mapped onto the back of leaf *i* and the right half onto the front of leaf *i+1*, so the content is continuous across the spine and turns with the rigid leaves. The current spread's RT updates live while typing; the turning leaf shows the outgoing spread on one face and the incoming spread on the other. No GC per frame, reasonable RT size (≈ 2048 wide). The title spread (`libro_guardas` left, title page right) may use the same system or direct textures, whichever is simpler. |
| A2 | **Book textures:** copy `libro_portada`, `libro_guardas` and `libro_papel` from `F:\Descargas` (root, **not** `sonidos_final`; never move or delete the originals) into `Art/Intro/` with the INTRO.0 import rule. Page quads take their aspect from `libro_papel` (1086×1448, ~3:4); the cover takes its aspect from `libro_portada`. Ignore the square `ilus_intro_*` set in `F:\Descargas`. |
| A3 | **Ambience stereo:** `AudioImporter` keeps `amb_*` and `musica_*` stereo (played 2D). Voices stay mono. `git status` first (shared-file rule); bump the importer version. |
| A4 | **Voice silence:** trim leading/trailing silence of the intro voices (`narrador_*`, `taxista_*`, `wilbur_taxi_*`, `wilbur_casa_01`, `wilbur_arma_01`) to ≤ 0.1 s with a small Python script in `Tools/` (stdlib `wave` + `audioop` or numpy; ffmpeg is not installed), threshold ≈ −45 dBFS, run on the project copies only. List before/after durations in the audit. Do not touch the 3C voices. |
| A5 | The fixed AI line (I9) and the taxi do **not** belong to INTRO.1; they stay in INTRO.2. |
| A7 | **Book redesign — 2D full screen, 16:9 (subphase INTRO.1.1, after David tested INTRO.1).** Replaces **I2** and **A1** for the book; INTRO.1 steps 2–3 change accordingly. Reference: the Cuphead intro — ONE page fills the screen (illustration on top, text below), the page turns with a curl, an old-film filter sits on top. (1) **Remove the 3D book:** room, table, lamp, 3D leaves, `StoryBook` prefab, title-page spread; delete dead code (`StoryBook.cs`, `IntroBookBuilder.Book.cs`, unused materials, lamp mesh asset). **Keep/reuse:** `IntroBookData`/`IntroBook.asset`, `StoryBookDirector` flow (adapted), the `SpreadCompositor` idea, `HoldToSkip`, `ScreenFade`, `CarryOverAudio`, `CinematicEase`, async load, typewriter via `maxVisibleCharacters`, the mixing values and A3/A4. (2) **Opening:** `Art/Intro/libro_cerrado` (closed book on a table, title painted in) full screen, cover-fit 16:9; fade from black 0.8 s, hold 1.2 s, push-in scale 1 → 1.12 over 2 s (ease), then a page-open transition into page 1 with `sfx_libro_abrir`. Placeholder: dark red rectangle with the title in TMP. (3) **Pages:** each page composed into a 1920×1080 RT: `Art/Intro/hoja_papel` background (cover-fit; cream placeholder) + `ilus_intro_0N` (16:9) in the upper ~68 %, centred, ~86 % of page width, thin dark ink frame + text below, centred, 2–3 lines, ink `#2A1F1A`, typewriter as before. Font **IM Fell English** (`Fonts/IMFellEnglish-Regular.ttf`) as a static TMP asset with ñ ¿ ¡ á é í ó ú É …; fallback Special Elite (report it). The longest page (`narrador_intro_03`) fits in 3 lines at 1080p. (4) **Page turn:** full-screen RawImage with an unlit URP-compatible page-curl shader (current RT + next RT); the curl edge sweeps right → left over 0.8 s (ease in-out), lighter paper underside (`hoja_papel` mirrored and tinted), soft shadow on the next page by the fold, highlight on the fold; random `sfx_pagina_0X`. Fallback if a cylinder curl is not reliable: rigid flip with gradient shade (explain in the audit). (5) **Old-film filter** (book only, on top, own Canvas/pass, independent of the in-game Volume): animated grain ~24 fps (stronger than the game's), dust specks and random vertical scratches, gate weave (1–2 px at 12 fps), warm flicker (exposure ±3 %, replaces the lamp), dark vignette, slight warm sepia. Procedural, no textures, all values serialized, zero GC per frame. (6) **Ending:** after page 6, push into the illustration (1.5 s) + fade to black (1.0 s) + rain hand-off exactly as INTRO.1. (7) Skip/advance exactly as INTRO.1. (8) The menu *Plomo y Paja → Intro → Construir escena del libro* regenerates `Intro.unity` for this design (idempotent) and lists missing assets. Build Settings: `Intro`, `NivelPorky`; `SampleScene` is removed from the list (scene not deleted). |

---

## 2. Subphases

### INTRO.1 — The storybook (scene `Intro`)
**Model:** Opus 5.5 · **Can run in parallel with 3C.**
**Touches:** build settings / scene list, main menu and its play button (if any), any existing scene loader or fade, `AudioImporter` + "Reconstruir Librería de Audio", fonts / TMP font assets, the project's global Volume profile, the 3C.1 lamp flicker script (reuse it if it already exists).

0. **Assets:** narrator clips in `Audio/Voices/narrador/` (make sure `AudioImporter` and the library pick that folder up; voices preload). Book textures in `Art/Intro/`: sRGB, mipmaps on, max size 2048, high quality / no compression. Book SFX and music through the existing audio library.
1. **`IntroBookData`** ScriptableObject + `Data/Intro/IntroBook.asset` built by the menu below: pages in order (clip id, text, illustration), with the texts copied **exactly** from guion §2.
2. **Builder menu** `Plomo y Paja → Intro → Construir escena del libro`: creates or updates `Intro.unity` through `EditorSceneManager` (not hand-edited YAML) and `Prefabs/Intro/StoryBook.prefab`:
   - A wooden table (chunky box, reuse a wood material if one exists), an oil lamp (warm Point Light with soft shadows and flicker), dim cold fill light, optional slow dust particles. The project's Volume, plus a stronger vignette.
   - **Book:** back cover, spine, cover leaf and **7 page leaves** (leaf 1 = title page, leaves 2–7 = text pages). Each leaf has front and back quads, a hinge pivot on the spine, small per-leaf thickness offsets (no z-fighting) and a thickening page stack on the right.
   - **Spread mapping:** after the cover opens, the left page is `libro_guardas` (inside of the cover) and the right page is the title page (front of leaf 1). Spread *i* (1–6) shows `ilus_intro_i` on the left (the back of leaf *i*) and `text_i` on the right (the front of leaf *i+1*). The cover's front shows `libro_portada` with the TMP title "PLOMO Y PAJA" in gold.
   - Camera rig anchors: `Cam_Wide` (closed book on the table), `Cam_Spread` (open spread fills ~85 % of the frame), `Cam_Push` (close to the left page for the ending).
3. **`StoryBookDirector`** (runtime):
   - Fade from black 0.8 s on `Cam_Wide`, hold 1.2 s → cover opens (1.0 s, ease in-out, `sfx_libro_abrir`) while the camera dollies to `Cam_Spread` (1.0 s) → title page 2.0 s → per page: turn the leaf (0.7 s rotation, ease in-out, small arc lift and tiny settle overshoot, random `sfx_pagina_0X`) → play the narrator clip and typewriter the text → I4.
   - Gentle handheld drift on the camera (≤ 0.3°, slow noise). Music low under the narrator (duck it about −6 dB while a clip plays). Room ambience loop.
   - Ending: after page 6, push to `Cam_Push` (1.5 s) + fade to black (1.0 s) while the rain fades in → activate the async-loaded Level 1 (I11).
   - Skip and advance per I4 and I5. Hold-to-skip is the reusable `Scripts/Cinematics/HoldToSkip`.
4. **Flow:** if a main menu exists, its play button loads `Intro`; add `Intro` to Build Settings in the right order. Report how the flow was before and after.

**Audit template (INTRO.1):**
```
- Menús que David debe correr (en orden):
- Escena y prefab creados (rutas):
- Flujo de escenas antes/después (menú → intro → nivel 1):
- Fuente usada y ¿se ven ñ, ¿, ¡, tildes y …?
- Ids faltantes (audio/arte) y qué placeholder se usa:
- Saltar (mantener ESPACIO/Esc) y avanzar con clic: ¿funcionan?
- ¿Hay silencio o tirón al pasar al Nivel 1? (carga async)
- Archivos compartidos tocados (y si estaban limpios en git status):
- Qué no se pudo compilar/probar y qué debe revisar David en el Editor:
- Archivos creados/modificados:
- Commits:
```

---

### INTRO.2 — Taxi, arrival and house reveal (Level 1)
**Model:** Opus 5.5 · **Only after 3C.1 is closed.**
**Touches:** Level 1 scene + its builders (including 3C.1's yard builder), player spawn / respawn / restart flow, player input lock (whatever `DialogueNPC` uses), viewmodel and crosshair hide mechanism, `EnemyAI` detection, `AICompanion` display path, `GameStateTracker`, audio library, `Tools/character_key.py`, `CharacterSpriteImporter`, `LipSyncDriver` (3C.2, if it already exists).

0. **Assets:** taxista sprites with `python Tools/character_key.py Taxista` → `Art/Characters/taxista/`; add a `taxista` case to `CharacterSpriteImporter` (PPU 500, pivot bottom-center, mipmaps on; bump the importer version). Voices `taxista_*` and `wilbur_*` plus the taxi SFX through the library.
1. **Builder menu** `Plomo y Paja → Intro → Construir taxi y llegada` creates or updates `Prefabs/Level1/Taxi_Intro.prefab` and places it idempotently:
   - **1930s cab from primitives:** long hood, separate rounded fenders (scaled capsules/cylinders), 4 chunky wheels, running boards, a roof sign "TAXI" (TMP), a checker stripe and yellow/black paint. Slightly bent and chunky (rubber-hose), deterministic with a fixed seed.
   - **Interior seen from the back-right seat:** front bench, dashboard with a dim warm glow, steering wheel, the taxista billboard at the wheel (`conduce_01`; `habla_01/02` when talking), and window quads with a scrolling rain-streak material. Very dark. A spot light sweeps across the interior every 2.5–4 s (street lamps passing).
   - Anchors and paths: `Anchor_TaxiSeat` (camera seat), `Taxi_Path` (from out of view, down the street, to `Anchor_TaxiStop` at the curb near Porky's house), `Taxi_ExitPath` (from the stop to out of view), `Anchor_PlayerCurb` (spawn after getting out, facing along the street), `Anchor_HouseLook` (the house front, for the reveal).
2. **`IntroCinematicDirector`** (Level 1):
   1. Letterbox in. Fade from black 0.8 s, already driving: rain, engine, `amb_taxi_interior`, optional wipers. The camera is parented to `Anchor_TaxiSeat`, with limited mouse look (yaw ±60°, pitch ±30°) and no FOV writes.
   2. Drive `Taxi_Path` for ~8–10 s with smooth acceleration, decelerate, `sfx_taxi_frenar`, stop.
   3. Dialogue from guion §3 in order, with subtitles (I10) and 0.3–0.5 s gaps. The taxista turns his head for his lines (`habla_01/02`, lip-sync with `LipSyncDriver` if it exists; otherwise toggle by RMS with a simple threshold and report) and goes back to `conduce_01` after `taxista_03`. `sfx_monedas` plays on `wilbur_taxi_02`.
   4. `sfx_puerta_carro`, fade to black 0.5 s, player placed at `Anchor_PlayerCurb`, fade in. The taxi drives off along `Taxi_ExitPath` with 3D `sfx_taxi_arranca` and is disabled once out of view.
   5. **Reveal:** input still locked. The camera rotates (yaw/pitch slerp, ease, 1.8 s) to look at `Anchor_HouseLook` → `wilbur_casa_01` → the fixed AI thought (I9) → optional title card "PLOMO Y PAJA" (fade in 0.6 s, hold 2 s, fade out; serialized bool, default on).
   6. Letterbox out, subtitles off, control returned **from the current view direction (no snap)**. The weapon stays holstered (INTRO.3; until then, leave the weapon as it is today).
3. Play once per session and skip per I5 and I6. Store the session flag in `GameStateTracker` (or a static if the tracker resets on reload; recon and say which).
4. **I7:** guards can't detect or damage the player during the cinematic. Check that patrol routes don't see the curb during it; adjust the first guard's waypoints if needed (without undoing 3C.1's gate route).
5. **Pause menu during the cinematic:** disabled, or pauses and resumes cleanly. Report which.

**Audit template (INTRO.2):**
```
- Menús que David debe correr (en orden):
- Prefab del taxi y anclas (nombre + posición):
- Lip-sync del taxista: ¿LipSyncDriver o alternativa?
- ¿El taxi se va y desaparece fuera de vista? (sí/no)
- Giro hacia la casa: ¿FOV intacto? ¿control vuelve sin salto?
- Una vez por sesión: ¿dónde se guarda la bandera? ¿al morir se salta?
- ¿Los guardias detectan/dañan al jugador durante la cinemática? (sí/no + cómo)
- Saltar: ¿deja el estado final correcto?
- Menú de pausa durante la cinemática:
- Ids faltantes y placeholders:
- Archivos compartidos tocados (y si estaban limpios en git status):
- Qué no se pudo compilar/probar y qué debe revisar David en el Editor:
- Archivos creados/modificados:
- Commits:
```

---

### INTRO.3 — Unarmed start and shotgun draw
**Model:** Opus 5.5 · **Only after 3C.2 is closed.**
**Touches:** `ShotgunController`, `IWeapon`, `ViewmodelMotion` / `ViewmodelAnimator` / `ViewmodelTime`, `PlayerAim`, `RubberHoseCrosshair`, the viewmodel/crosshair hide mechanism (and its users: dialogue, interrogation 3C.3 if present, pause, death), `EnemyAI` alert/chase events, `PlayerHealth` damage event, `GameStateTracker`, `CentavoController` (only to verify it doesn't break).

1. **`PlayerWeaponHolster`** (`Scripts/Player/`): states `Holstered → Drawing → Ready`.
   - **Holstered:** viewmodel and crosshair hidden through a hide reason (I12); fire, aim and reload input blocked at the `IWeapon` level; `PlayerAim` held off; walking and running allowed; ammo untouched.
2. **Draw triggers** per I8. Check guard visibility every 0.1 s with no GC (cached guard list, preallocated buffers). The fallback trigger is a box at Porky's front door, placed by the INTRO.2 builder menu (extend it).
3. **Draw:** `wilbur_arma_01` (2D voice) + `sfx_escopeta_bombeo` timed to the raise. The viewmodel rises from below over 0.5 s (reuse `ViewmodelMotion`'s reload-lowering offset in reverse, on `ViewmodelTime`). The crosshair fades in at the end, and input unlocks only at `Ready`. If the trigger is damage, the same line plays. A draw during a `CartoonFx` hit-stop still completes (real time).
4. **Persistence:** `weaponDrawn` survives restarts (same place as the INTRO.2 flag). Start holstered **only** if the intro cinematic played (or was skipped) in this load; with `playIntroCinematic` off, start `Ready`.
5. **Do not break:** Centavo's combat and wounded state (3C.2), the interrogation hiding and restoring the viewmodel (3C.3, if present), the pause menu, death and restart. Verify by reading code paths and list them in the audit.

**Audit template (INTRO.3):**
```
- Cómo se ocultan manos/mira (razones de ocultado) y quién más las usa:
- Disparadores implementados y cuál gana primero:
- Animación de sacar el arma: ¿reutiliza ViewmodelMotion? duración:
- ¿Funciona durante un hit-stop? (sí/no)
- Reinicio tras morir: ¿empieza armado? ¿dónde se guarda weaponDrawn?
- Con playIntroCinematic apagado: ¿empieza armado?
- Rutas revisadas para no romper (Centavo, interrogatorio, pausa, muerte):
- Qué no se pudo compilar/probar y qué debe revisar David en el Editor:
- Archivos creados/modificados:
- Commits:
```

---

### INTRO.4 — Playtest, polish and close
**Model:** Sonnet 5.5 · Can be merged into the 3C.5 session.

1. Fix what David reports from this checklist:
   - [ ] Menu → Intro → Level 1 with no silence or hitch.
   - [ ] Book: cover opens, 6 pages, texts exact with accents, each clip in sync, click advances, hold skips.
   - [ ] Taxi: dark, light sweeps, rain; dialogue with subtitles; taxista turns his head and talks.
   - [ ] Gets out, taxi drives away and vanishes; reveal of the house, Wilbur line, AI thought, title.
   - [ ] Control returns with no snap and FOV intact; walks unarmed (no hands, no crosshair, can't shoot).
   - [ ] First guard in sight → draw with line and pump; also by damage and by the door trigger.
   - [ ] Guards don't attack during the cinematic.
   - [ ] Die → restart at the curb, armed, no cinematic.
   - [ ] Esc / pause during the book, the taxi and the reveal.
   - [ ] Centavo and the interrogation still hide and restore hands and crosshair correctly.
   - [ ] No errors in the console.
2. Debug flags off; remove dead code.
3. Audit `docs/audits/AUDIT_intro_4.md` ending with a **block for the Registro de fases** (Resultado, Subfases, Decisiones, Para los demás, Pendientes, Commits), in Spanish.

**Audit template (INTRO.4):**
```
- Checklist: estado de cada punto (OK / arreglado / pendiente):
- Bugs arreglados:
- Debug apagado: (sí/no)
- Bloque para el Registro de fases:
- Commits:
```
