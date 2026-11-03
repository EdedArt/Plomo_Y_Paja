# PHASE MENU — Title screen: the three brothers animated, film grain, Cristian's jazz into the intro

> In the repo: `Docs/phases/Fase-Menu.md` · content / assets: `Docs/guiones/menu.md` (Guion-Menu **v1.1**).
> **Version 1.1 (3 Nov):** the title screen is **Edgar's existing screen (the three pig brothers hugging), improved and animated** — not Wilbur's study. M2, M3, M4, M6 and MENU.0/1/2 updated.
> **Owner:** David (Claude Code, orchestrated) · **Branch:** `integracion/dia4`.
> **Deadline:** game delivered Wed 4 Nov 2026 → MENU.1 must close **Tue 3 Nov night**; MENU.2 goes into the shared playtest (3C.6 / INTRO.4 / HUD.4).
> Paths are relative to `PLOMO_Y_PAJAV_1/Assets/_Game/` unless they start with `Docs/`, `docs/`, `Tools/` or `_entrante_*`.
> Reference for the **feeling only**: the *Mouse: P.I. For Hire* title screen (characters idling like a cartoon, soft jazz, subtle film grain). We do not copy its art, logo, layout or composition.

## Scheduling
| Subphase | Can run… | Must NOT run at the same time as | Why |
|---|---|---|---|
| MENU.0 (recon + inventory) | **now**, in parallel with anything | — | read-only; only copies files into a git-ignored folder |
| MENU.1 (build) | after INTRO.1 is closed (if INTRO.1 is not done yet, MENU.1 still runs but leaves the Intro hook documented) | INTRO.1 | both edit Build Settings, the menu "Jugar" flow and the intro music |
| MENU.2 (close) | merged into the shared playtest | — | — |

**Parallel-session git rule** (same as INTRO/HUD): never `git add -A` / `git add .`; stage by explicit path. Before editing a shared file (`AudioImporter.cs`, `CharacterSpriteImporter.cs`, `EditorBuildSettings.asset`, `StoryBookDirector.cs`, the pause menu script, `Tools/character_key.py`, `CLAUDE.md`) run `git status`; if it has uncommitted changes you did not make, **don't edit it**: report what it needs.

---

## 0. Rules for every subphase
Rules 1–9 of `Docs/phases/Fase-3C-Contador.md` §0 apply **verbatim**, with these substitutions:
- Audits go to `Docs/audits/AUDIT_menu_<N>.md` (in Spanish), using the subphase's template.
- **Recon uses graphify** (`Assets/_Game/Scripts`) for every system under *Touches*, plus a file search of `Assets/` for scenes, images and audio (graphify only covers scripts).
- Editor menus live under **Plomo y Paja → Menú → …** and are idempotent.
- Code: `Scripts/Menu/`; reusable film look and music player in `Scripts/Cinematics/` (next to `HoldToSkip`); editor tools in `Scripts/Editor/`.
- Existing contracts in 3C §0.4 still hold (no `UIManager`, existing sprite/audio importers and libraries, `PlayerAim` owns FOV, layer 8 reserved).
- **Missing assets never block.** Placeholders + a warning; rerunning the menu picks up the real files. The audit lists every missing id.
- **Player-facing text in Spanish, exactly as in the guion.**
- All menu animation on **unscaled time**; no per-frame GC.

---

## 1. Decisions (do not re-discuss)
| # | Decision |
|---|---|
| M1 | Flow: **Menu → Intro → Level 1.** Reuse the existing main menu scene (Edgar's) if MENU.0 finds it (rebuild its content with the builder); otherwise create `MainMenu.unity`. It is **scene 0** in Build Settings. Level 1 and Intro can still be opened directly in the Editor. |
| M2 | The title screen is a **layered 2D illustration** on its own Canvas (reference 1920×1080, Scale With Screen Size, match 0.5), back to front: background (scale-to-cover, important content inside the central 16:9 band), optional wolf shadow (masked to a serialized wall rect), **the three brothers as one group sprite**, optional foreground, logo, UI. Must read at 16:9, 16:10 and 21:9. |
| M3 | **Two states:** *Attract* ("Pulsa cualquier tecla", slow fade pulse 1.6 s) → any key or click → *Main* (prompt fades, logo eases up a little, buttons fade/slide in staggered 0.08 s). The brothers react to the key press: `cerditos_mira_01` (all three look at the camera, Wilbur waves) for 1.4 s, then back to idle. |
| M4 | **The brothers move like a cartoon** (one group sprite, whole-frame swaps): idle alternates `cerditos_idle_01`/`_02` "on twos" (every 0.5–0.7 s, slight random); breathing squash/stretch ±1.5 % Y on a 1.8 s period (reuse `InterrogationPortrait`'s breathing if reusable); **independent blinks** — `cerditos_parpadeo_wilbur` / `_max` / `_porky` for 0.12 s, each brother on his own random 3–6 s timer, never two blinks in the same frame; every 10–16 s `cerditos_porky_mira` for 1.2 s (Porky's nervous glance). Pivot bottom-center, feet anchored to a serialized normalized point of the background. Any missing frame is simply skipped. |
| M5 | **Film look, subtle** (`FilmOverlay`, reusable, its own top Canvas, raycasts off): grain = code-generated noise texture (no asset), UV jump at 24 fps, alpha ≈ 0.06; exposure flicker ±2 % at 24 fps; dust specks (pooled, 0–3 on screen, 1–2 frames each); a thin vertical scratch for 0.08 s every 5–9 s; soft vignette. Every value serialized. **Rule: it must be felt, not seen** — if the grain pattern is noticeable at a glance, it is too strong. |
| M6 | **Light life:** the street-lamp glow over the background flickers gently (reuse the lamp flicker curve from 3C.1 / INTRO.1 if it exists, applied to a UI glow image's alpha); optional rain streaks (scrolling UI image, serialized rect); mouse parallax: background ±12 px, brothers ±24 px, foreground ±40 px, smoothed. **Wolf shadow (optional, only if `menu_sombra_lobo` exists):** every 20–35 s it slides across the wall rect in ≈ 3 s at alpha ≈ 0.35 with a slight walk bob; when it starts, Porky's glance (`cerditos_porky_mira`) plays. |
| M7 | **Music:** Cristian's jazz (MENU.0 finds it). One persistent `MusicPlayer` (DontDestroyOnLoad, single instance) starts it on the menu with a 1.5 s fade-in and **keeps playing into the Intro book without restarting**. The book's narrator ducking (−6 dB) goes through this player, and it fades out with the book's final fade-to-black while the rain comes in (INTRO I11). No double music if the Intro director already plays one: it hands off to the `MusicPlayer`. Optional low vinyl crackle loop on the menu only. |
| M8 | **Buttons:** Jugar · Opciones · Créditos · Salir. Mouse hover + keyboard (↑/↓ or W/S, Enter, Esc = back). Hover: slight scale 1.06 with an elastic ease and a small tilt wobble; `sfx_ui_hover` / `sfx_ui_click`. Cursor visible and unlocked on the menu; Level 1 locks it again as today. |
| M9 | **Jugar:** `sfx_ui_jugar` (shotgun pump, reuse `sfx_escopeta_bombeo` if absent) → fade to black 0.8 s → load `Intro` async (music keeps going). If `Intro` isn't in Build Settings, load Level 1 and say so. Input ignored while loading. |
| M10 | **Opciones:** Volumen general, Música, Sensibilidad del ratón, Pantalla completa, Volver. Saved with `PlayerPrefs` through a small `GameSettings` static. Volumes through the existing mixer if one exists, otherwise `AudioListener.volume` and the `MusicPlayer` volume. Sensitivity only if recon finds **one** place where the player reads it; otherwise hide the row and report. |
| M11 | **Créditos:** a paper panel with the text from the guion, Esc/Volver closes it. |
| M12 | **Logo:** `Art/Menu/menu_logo.png` if present (hand-made, transparent). Otherwise TMP "PLOMO Y PAJA" big, cream with a thick dark outline and drop shadow, with a slow rubber-hose wobble (±1° rotation, ±1 % scale). Fonts: a cartoon display font in `Fonts/` if MENU.0 finds one, otherwise Special Elite. Must render ñ ¿ ¡ á é í ó ú. |
| M13 | **Pause menu (only if it exists):** add "Salir al menú" (timeScale back to 1, cursor unlocked, load the menu). If the pause script is dirty in git, report instead. |

---

## 2. Subphases

### MENU.0 — Recon and asset inventory (no gameplay code)
**Model:** Sonnet 5.5
**Touches (read only):** scenes and Build Settings, any main menu / title / scene-loader / fade scripts, pause menu, `AudioImporter` + audio library, the music folder, URP Volumes/profiles, `Fonts/`, `Scripts/Intro/` and `Scripts/Cinematics/` (INTRO.1 status), `Tools/character_key.py`, `.gitignore`.

1. Graphify for the scripts; file search for scenes, images and audio. Don't modify anything in `Assets/`.
2. **Inventory of Edgar's current title screen** (the three brothers hugging): every image it uses (asset path, pixel size, what it shows, where it is referenced: scene/prefab + component). Say explicitly whether the brothers and the background are **one flat image or separate layers**, and whether it has transparency. Same for its fonts, its music and its scripts.
3. **Copy** every one of those images (originals, untouched) to `_entrante_menu/actuales/` (next to `_entrante_viewmodel/`; git-ignored — verify `**/_entrante_*/` covers it) so David can use them as references to remake them.
4. **Music:** every jazz / music track in the project: path, length, sample rate, whether it loops cleanly, how it is referenced, and which one is Cristian's newest. Is there an audio mixer? Which groups?
5. **Intro status:** is INTRO.1 done? How does the book play music and duck it? How does the menu reach the Intro today?
6. Pause menu, current film grain values in Volumes, available fonts and whether they have ñ/accents, whether `character_key.py` can take a new `Menu` set **with 1536×1024 (non-square) canvases**, and whether a player mouse-sensitivity value exists (where).
7. Write the audit and stop.

**Audit template (MENU.0):**
```
- Escenas y orden en Build Settings:
- Pantalla de inicio actual (la de Edgar): escena, scripts y cómo funciona hoy:
- Imágenes de la pantalla de inicio (ruta · tamaño · qué muestra · dónde se usa):
- ¿Cerditos y fondo están en una sola imagen o en capas separadas? ¿tienen transparencia?:
- Copiadas a _entrante_menu/actuales/: (sí/no, lista) · ¿lo ignora git?:
- Música encontrada (ruta · duración · ¿loop limpio? · ¿cuál es la de Cristian?):
- Mixer y grupos:
- Estado de INTRO.1 y cómo maneja la música:
- Menú de pausa: ¿existe? ¿tiene salir?
- Grano actual (Volumes) y fuentes disponibles (¿ñ y tildes?):
- character_key.py: ¿acepta un set Menu con lienzos 1536×1024?:
- Sensibilidad del ratón: ¿dónde se lee?
- Riesgos / archivos compartidos que MENU.1 va a tocar:
```

---

### MENU.1 — Build the title screen
**Model:** Opus 5.5
**Touches:** the menu scene and Build Settings, the scripts found in MENU.0, `AudioImporter` + library, `CharacterSpriteImporter`, `Tools/character_key.py`, `StoryBookDirector` (music hand-off only), pause menu (M13 only), `InterrogationPortrait` (reuse only), lamp flicker script (reuse only).

0. **Assets:** `python Tools/character_key.py Menu` → group frames from `_entrante_menu/Cerditos/` (magenta, 1536×1024) to `Art/Menu/cerditos/` (keep canvas size, bottom-aligned) and `_entrante_menu/Sombra/` → `Art/Menu/menu_sombra_lobo.png`. Add the `Menu` set (and non-square support) if the tool lacks it. Importer case: Sprite, pivot bottom-center, mipmaps on, no compression (bump version). Background `Art/Menu/menu_fondo.png` (no chroma), optional `menu_frente.png` (magenta, keyed) and `menu_logo.png`. Audio ids from the guion through the library.
1. **Builder** `Plomo y Paja → Menú → Construir pantalla de inicio` (idempotent): the scene per M1, `Prefabs/Menu/TitleScreen.prefab` (layers per M2, lamp glow, rain rect, wall rect + wolf shadow, brothers, logo, Attract prompt, buttons, Options and Credits panels), `FilmOverlay` per M5, the `MusicPlayer` bootstrap, Build Settings order Menu → Intro → Level 1. The menu also lists missing ids.
2. **`TitleScreenDirector`**: states per M3, buttons M8–M11, scene load M9.
3. **`MenuGroupAnimator`** (UI `Image`): M4 + the wolf-shadow trigger of M6. Frames from a small ScriptableObject `MenuGroupFrames` (idle pair, named blink frames, glance, look-at-camera); missing frames are skipped.
4. **`FilmOverlay`** (M5) and **`MusicPlayer`** (M7) in `Scripts/Cinematics/`. If INTRO.1 is closed: add the same `FilmOverlay` to the Intro book (same settings asset) and hand the book's music to `MusicPlayer`. If not: document the hook.
5. **`GameSettings`** (M10) applied at load in every scene; M13 if the pause menu exists.
6. **Placeholders:** if the new art isn't there, use Edgar's current images found in MENU.0. If his screen is one flat image, it becomes the background and the group layer stays empty (breathing applies to nothing); if the brothers are a separate layer, that layer is used as the only idle frame (breathing + bob only).

**Audit template (MENU.1):**
```
- Menús que David debe correr (en orden):
- Escena del menú y orden en Build Settings:
- Estados (pulsa cualquier tecla → opciones): ¿funcionan con ratón y teclado?
- Cerditos: cuadros usados, tiempos, parpadeos independientes, mirada de Porky, ¿miran a cámara al pulsar?
- Sombra del lobo: ¿existe el arte? ¿pasa y Porky mira?
- Grano/ruido: valores usados (alpha, flicker, rayas):
- Música: ¿sigue del menú al libro sin cortarse? ¿se baja con el narrador? ¿se apaga al final del libro?
- Jugar → Intro (o Nivel 1): ¿sin tirón ni silencio?
- Opciones: ¿se guardan? ¿sensibilidad aplicada o fila oculta?
- 16:9 / 16:10 / 21:9: ¿se ve bien?
- Pausa → Salir al menú: (hecho / no existe / reportado)
- Ids faltantes y placeholders:
- Archivos compartidos tocados (y si estaban limpios en git status):
- Qué no se pudo compilar/probar y qué debe revisar David en el Editor:
- Archivos creados/modificados:
- Commits:
```

---

### MENU.2 — Playtest and close
**Model:** Sonnet 5.5 · Merged into the shared playtest (3C.6 / INTRO.4 / HUD.4).
1. Checklist:
   - [ ] The game opens on the menu; jazz fades in; grain and noise subtle (felt, not seen).
   - [ ] The brothers breathe, each blinks on his own, Porky glances nervously; on the key press all three look at the camera and Wilbur waves.
   - [ ] Wolf shadow (if present) crosses the wall and Porky glances at the same time.
   - [ ] Parallax and lamp flicker smooth; nothing stutters.
   - [ ] Buttons: hover, click, keyboard; Options saved after restarting the game; Credits; Salir closes the build.
   - [ ] Jugar → Intro with the same music, ducked under the narrator, faded into the rain → Level 1.
   - [ ] Pause → Salir al menú (if it exists) → music and cursor correct.
   - [ ] 16:9 / 16:10 / 21:9; no console errors; no GC spikes.
2. Debug flags off; dead code out.
3. Audit `Docs/audits/AUDIT_menu_2.md` with a **block for the Registro de fases** (Resultado, Subfases, Decisiones, Para los demás, Pendientes, Commits), in Spanish.

---

## 3. Plan B (in this order)
1. Without the new group frames: keep Edgar's current images; brothers = breathing + bob on one frame only (or static if his screen is one flat image).
2. Drop the wolf shadow.
3. Drop the scratches and dust; keep grain + flicker.
4. Drop Options (keep Jugar, Créditos, Salir).
5. Drop the parallax and the rain rect.
