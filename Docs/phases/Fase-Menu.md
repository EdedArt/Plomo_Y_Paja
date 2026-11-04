# PHASE MENU — Title screen: lightning reveal, film grain, intro music carried into the book

> In the repo: `Docs/phases/Fase-Menu.md` · content / assets: `Docs/guiones/menu.md` (Guion-Menu **v1.2**).
> **Version 1.4 (3 Nov):** adds MENU.1.1 = attract frames as a fast flipbook + new title (M3a and M11).
> **Version 1.3 (3 Nov, MENU.0 green-lit):** the sets grow: attract = every `menu_inicio_NN`, storm = every `menu_rayo_NN`, any image format, all cycled by `MenuFrameAnimator`, which never takes `pausa_*` or `boton_placa`. The pause menu is now Cristian's (merge `e8bfd47`: `PauseMenu`, `PauseMenuSpawner`, `GameManager.OnPauseChanged`): do not rebuild it, only verify M12 and report. The jazz tracks are not used.
> **Version 1.2 (3 Nov, after MENU.0):** keep **Cristian's existing screen** (`MenuPrincipal`, the three brothers in fighting poses at sunset, one flat image per frame). On key press **lightning strikes** and the image swaps to the storm version: Wilbur, in the same spot, aims the shotgun at Don Lobo's giant shadow. Music = **`musica_intro`** (not the jazz). Build on Cristian's scripts (`MainMenu`, `MenuFrameAnimator`, `OptionsPanel`, `BotonCartoon`, `CrearMenuInicio`) instead of creating parallel ones. No chroma key and no `character_key.py` changes.
> **Owner:** David (Claude Code, orchestrated) · **Branch:** `integracion/dia4`.
> **Deadline:** game delivered Wed 4 Nov 2026 → MENU.1 must close **Tue 3 Nov night**; MENU.2 goes into the shared playtest (3C.6 / INTRO.4 / HUD.4).
> Paths are relative to `PLOMO_Y_PAJAV_1/Assets/_Game/` unless they start with `Docs/`, `docs/`, `Tools/` or `_entrante_*`.
> Reference for the **feeling only**: the *Mouse: P.I. For Hire* title screen (cartoon life, soft music, subtle film grain). We do not copy its art, logo or layout.

## Scheduling
| Subphase | Status / can run… | Must NOT run at the same time as | Why |
|---|---|---|---|
| MENU.0 | ✅ CLOSED (3 Nov) — `Docs/audits/AUDIT_menu_0.md` | — | — |
| MENU.1 (build) | now (INTRO.1 is closed) | INTRO.4 if it touches `StoryBookDirector` | both edit the book's music |
| MENU.1.1 (flipbook + title) | after MENU.1 is integrated | INTRO.4 only if it touches `LightningFlash` or the menu | same scripts |
| MENU.2 (close) | merged into the shared playtest | — | — |

**Parallel-session git rule** (same as INTRO/HUD): never `git add -A` / `git add .`; stage by explicit path. Before editing a shared file (`StoryBookDirector.cs`, `IntroBookBuilder.cs`, `AudioImporter.cs`, `AudioLibrary.cs`, `EditorBuildSettings.asset`, `PlayerController.cs`, `UIManager.cs`, `GameManager.cs`, `OldFilmFilter.cs`, Cristian's menu scripts, `CLAUDE.md`) run `git status`; if it has uncommitted changes you did not make, **don't edit it**: report what it needs.

---

## 0. Rules for every subphase
Rules 1–9 of `Docs/phases/Fase-3C-Contador.md` §0 apply **verbatim**, with these substitutions:
- Audits go to `Docs/audits/AUDIT_menu_<N>.md` (in Spanish), using the subphase's template.
- Recon uses graphify (`Assets/_Game/Scripts`) plus a file search of `Assets/`.
- **The menu builder is Cristian's `Plomo y Paja → Crear menu de inicio` (`Editor/CrearMenuInicio.cs`)**: extend it; it stays the single source of truth and must stay idempotent. Don't add a second builder for the same scene.
- **Extend existing scripts, don't duplicate** (CLAUDE.md): `UI/MainMenu.cs`, `UI/MenuFrameAnimator.cs`, `UI/OptionsPanel.cs`, `UI/BotonCartoon.cs`, `Intro/OldFilmFilter.cs`, `Cinematics/ScreenFade.cs`, `Cinematics/CarryOverAudio.cs`. New files only where nothing exists (`Cinematics/MusicPlayer.cs`, a lightning-flash component).
- Existing contracts in 3C §0.4 still hold (`PlayerAim` owns FOV, layer 8 reserved, existing importers and libraries).
- **Missing assets never block.** Placeholders + a warning; rerunning the menu picks up the real files. The audit lists every missing id.
- **Player-facing text in Spanish, exactly as in the guion.**
- All menu animation on **unscaled time**; no per-frame GC.

---

## 1. Decisions (do not re-discuss)
| # | Decision |
|---|---|
| M1 | Flow: **`MenuPrincipal` → `Intro` → `NivelPorky`** (already the Build Settings order). Keep the scene name `MenuPrincipal` (`GameManager.GoToMainMenu` loads it by name). |
| M2 | Layers on Cristian's `CanvasMenu` (1920×1080, Scale With Screen Size, match 0.5), back to front: `Fondo` (full image frames, **scale-to-cover**, never stretched: works at 16:9, 16:10, 21:9), lightning flash overlay (white full-screen Image, raycasts off), logo, UI, film overlay. Mouse parallax: the whole `Fondo` ±12 px, smoothed (the image is flat; no per-layer parallax). |
| M3 | **Two states.** *Attract*: every `menu_inicio_NN` frame, cycled as `MenuFrameAnimator` plays them today (crossfade + breathing); "Pulsa cualquier tecla" pulsing (1.6 s). → any key or click → **lightning strike** (M4) → *Main*: prompt gone, logo eases down a little, buttons fade/slide in staggered 0.08 s, starting 0.6 s after the strike. Input ignored during the strike. |
| M3a | **Attract frames are a fast flipbook** (`menu_inicio_01..06`): hard cuts, in order 01→06, looping, 4 fps by default (serialized, range 2–8), ±15 % random variation in each frame's duration, a 0.05 s micro-crossfade (serialized; 0 = hard cut), no breathing scale on top. The storm set (`menu_rayo_01..02`) stays as in M4: slow crossfade with breathing. This overrides the crossfade + breathing of M3 for attract; the logo now **rises** to its menu position on the strike instead of easing down. |
| M4 | **Lightning strike:** flash overlay alpha 0 → 0.9 (0.04 s) → 0.2 (0.06 s) → 0.8 (0.04 s) → 0 (0.35 s); at the first peak `MenuFrameAnimator` **cuts** (no crossfade) to the storm set, every `menu_rayo_NN` frame (same crossfade + breathing loop afterwards); small screen shake ±6 px for 0.25 s; `sfx_trueno` 2D. **Ambient lightning in Main:** every 12–20 s a soft single flash (peak 0.35) + `sfx_trueno_lejos` (or `sfx_trueno` at 40 % volume). If `menu_rayo_01` is missing: the flash and thunder still play and the attract frames stay (log a warning). |
| M5 | **Film look, subtle:** reuse/extract `Intro/OldFilmFilter` (grain ~24 fps, dust specks, vertical scratches) — if it can't run over a Screen Space Overlay canvas, extract its core into a reusable `Cinematics/FilmOverlay` that both the book and the menu use (same look, separate settings: the menu's is **weaker** than the book's, grain alpha ≈ 0.06, exposure flicker ±2 %). Every value serialized. **Rule: it must be felt, not seen.** |
| M6 | **Music = `musica_intro`.** One persistent `MusicPlayer` (`Cinematics/`, DontDestroyOnLoad, single instance) loads that **exact clip** (not `AudioLibrary.GetSfx`, which picks random variants), fades it in over 1.5 s on the menu and **keeps playing into the book without restarting**. `StoryBookDirector`: if a `MusicPlayer` is playing, it does not start its own music (keep the current `StartLoop` as fallback when the book scene is opened directly); its −6 dB narrator duck and its final fade (`RampAudio`) go through the `MusicPlayer`; after the fade the player stops and stays silent in `NivelPorky`. If `musica_intro` clicks at the loop point, the `MusicPlayer` may loop with a short self-crossfade (serialized, default off; report). Optional vinyl crackle on the menu only. |
| M7 | **Buttons** (Cristian's `boton_placa` + `BotonCartoon`): Jugar · Opciones · Créditos · Salir. Add keyboard (↑/↓ or W/S, Enter, Esc = back) and `sfx_ui_hover` / `sfx_ui_click` (silent if missing). Cursor visible and unlocked on the menu. |
| M8 | **Jugar:** `sfx_ui_jugar` (or `sfx_escopeta_bombeo`) → fade to black 0.8 s (`ScreenFade` if its API fits) → `LoadSceneAsync("Intro")`; music keeps going. Input ignored while loading. Change `MainMenu.escenaJuego`'s code default to `Intro` too. |
| M9 | **Opciones** (extend `OptionsPanel`, shared with pause): keep Volumen general + Pantalla completa; add **Música** (`MusicPlayer` volume, `PlayerPrefs`) and **Sensibilidad del ratón** (multiplier read in `PlayerController` — the only place the player reads it — and applied to the taxi look in `IntroCinematicDirector` if trivial). |
| M10 | **Créditos:** the existing panel, text exactly as the guion; Esc/Volver closes it. |
| M11 | **Title logo (`UI/MenuTitleLogo.cs`, built by `CrearMenuInicio`, idempotent):** `Art/UI/Menu/menu_logo.png` if present. Otherwise "PLOMO y PAJA" in **Luckiest Guy** (Google Fonts, Apache 2.0, in `Fonts/` with its license; static TMP atlas with the title glyphs only; IM Fell English if unavailable), editing the TMP mesh letter by letter with no per-frame GC. PLOMO gradient #C9CED6 → #4A5260; PAJA gradient #F6E39A → #C8902E; small "y" (~45 %) in IM Fell italic, red #B3261E with a cream outline, a little lower. Arc ~6°; each letter rotation ±4°, scale ±5 %, height ±6 px with a fixed seed. Outline #2A1A10 (~0.3), hard shadow ~8 px down-right (a second TMP behind), cream shine on top. Idle: wave ±3 px, period 2.2 s, phased per letter, plus overall wobble ±1° and ±1 %. On the strike: one-frame white flash and staggered jump (0.03 s between letters; squash 0.85/1.15, stretch, settle with a bounce, 0.35 s), and the logo rises to its menu position. On each far flash: cold white-blue tint for 0.15 s. Wired through `LightningFlash` events, no polling. All values serialized. Must not collide with Wilbur or the buttons at 16:9, 16:10 and 21:9. Replace LiberationSans in the rest of the menu with IM Fell English. |
| M12 | **Pause (Cristian's, merge `e8bfd47`):** `PauseMenu`, `PauseMenuSpawner`, `GameManager.OnPauseChanged`. Do not rebuild it. Only verify and report: the button label is exactly "Salir al menú", the cursor and `AudioListener.pause` end up right, and `MusicPlayer` does not survive into a second copy. |
| M13 | **Art import:** frames live in `Art/UI/Menu/` with the existing `MenuSpriteImporter` (any image format): `menu_inicio_01..06` (attract) and `menu_rayo_01..02` (storm). The old `menu_inicio_01/02.png` are removed (copy in `_entrante_menu/actuales/`). All frames of a set must share one size; if sizes differ, the builder warns and scale-to-cover keeps each frame filling the screen. `MenuFrameAnimator` cycles all the frames of each set and never takes `pausa_*` or `boton_placa`. |

---

## 2. Subphases

### MENU.0 — Recon and asset inventory · ✅ CLOSED (3 Nov)
Audit `Docs/audits/AUDIT_menu_0.md`. Findings that changed the plan: the screen is Cristian's (`MenuPrincipal`, `CrearMenuInicio`), one flat 1672×941 image per frame, fighting poses at sunset; no music in the menu; jazz tracks don't loop cleanly; book plays `musica_intro` itself; pause already has "Salir al menú"; sensitivity lives only in `PlayerController`; `OldFilmFilter` already does grain/dust/scratches.

---

### MENU.1 — Build the title screen
**Model:** Opus 5.5
**Touches:** `MenuPrincipal` + `CrearMenuInicio`, `MainMenu`, `MenuFrameAnimator`, `OptionsPanel`, `BotonCartoon`, `MenuSpriteImporter`, `OldFilmFilter`, `ScreenFade`, `StoryBookDirector` + `IntroBookBuilder` (music hand-off only), `AudioLibrary` / `AudioImporter`, `PlayerController` (sensitivity only), `IntroCinematicDirector` (sensitivity only, optional), `PauseMenu` / `CrearMenuPausa` (verify M12 only, no changes).

0. **Coordination:** `CrearMenuInicio` and the menu scripts are Cristian's. Extend them; don't rename or delete his fields. Keep his visual style (wood plaques).
1. **`MenuFrameAnimator`:** support two frame sets (attract / storm) and a hard cut between them (M4), keeping the current crossfade + breathing for each set; scale-to-cover for `Fondo`; parallax per M2.
2. **`MainMenu`:** Attract → strike → Main states (M3/M4), keyboard navigation (M7), async Jugar with fade (M8), ambient lightning.
3. **Lightning:** flash overlay + shake + thunder per M4 (small component, `UI/` or `Cinematics/`).
4. **`MusicPlayer`** (M6) + the `StoryBookDirector` hand-off (three points from the MENU.0 audit: `StartLoop` of the music, the duck in `Update`, `RampAudio`).
5. **Film look** (M5).
6. **Options** (M9), **Credits** (M10), **Logo + fonts** (M11), **pause check** (M12).
7. **`CrearMenuInicio`** builds all of the above idempotently and lists missing ids (`menu_rayo_01/02`, `menu_logo`, `sfx_trueno`, `sfx_trueno_lejos`, UI SFX).

**Audit template (MENU.1):**
```
- Menús que David debe correr (en orden):
- Scripts de Cristian extendidos (qué se agregó a cada uno) y archivos nuevos:
- Estados (pulsa cualquier tecla → rayo → botones): ¿funcionan con ratón y teclado?
- Rayo: secuencia del destello, ¿corta a menu_rayo? ¿qué pasa si falta el arte o el trueno?
- Relámpagos de ambiente: intervalo y volumen:
- Grano/ruido: ¿se reutilizó OldFilmFilter o se extrajo FilmOverlay? valores usados:
- Música: ¿musica_intro sigue del menú al libro sin reiniciar? ¿se baja con el narrador? ¿se apaga al final del libro? ¿hay clic en el loop?
- Abrir Intro directo en el Editor: ¿el libro sigue sonando con su propia música?
- Jugar → Intro: ¿sin tirón ni silencio?
- Opciones: ¿se guardan música y sensibilidad? ¿se aplica la sensibilidad?
- 16:9 / 16:10 / 21:9: ¿la imagen cubre la pantalla sin deformarse?
- Pausa → Salir al menú: texto exacto, cursor, música (sin duplicar MusicPlayer):
- Ids faltantes y placeholders:
- Archivos compartidos tocados (y si estaban limpios en git status):
- Qué no se pudo compilar/probar y qué debe revisar David en el Editor:
- Archivos creados/modificados:
- Commits:
```

---

### MENU.1.1 — Flipbook attract frames + new title
**Touches:** `MenuFrameAnimator`, `MainMenu`, `LightningFlash`, `CrearMenuInicio`, `InterrogationUIBuilder` (font overload only), new `UI/MenuTitleLogo.cs`, `Fonts/`.

1. **`MenuFrameAnimator`:** playback mode per set (M3a). The storm set does not change.
2. **Title:** font + static atlas, `MenuTitleLogo`, events and builder (M11).

**Audit template (MENU.1.1):**
```
- Menús que David debe correr (en orden):
- Libro animado: fps, variación, orden, ¿sin respiración? ¿la tormenta quedó igual?
- Fuente: ¿se descargó? licencia, atlas (glifos, tamaño), respaldo:
- MenuTitleLogo: cada especificación de M11 y dónde está su valor serializado:
- Eventos agregados y quién los escucha:
- GC por cuadro: qué se hizo y qué debe medir David:
- 16:9 / 16:10 / 21:9: ¿choca con Wilbur o con los botones? cómo se comprobó:
- menu_logo.png: ¿qué pasa si existe?
- Archivos compartidos tocados (y si estaban limpios en git status):
- Qué no se pudo compilar/probar y qué debe revisar David en el Editor:
- Archivos creados/modificados:
- Commits:
```

---

### MENU.2 — Playtest and close
**Model:** Sonnet 5.5 · Merged into the shared playtest (3C.6 / INTRO.4 / HUD.4).
1. Checklist:
   - [ ] The game opens on the menu; `musica_intro` fades in; grain and noise subtle (felt, not seen).
   - [ ] Attract frames play as a fast flipbook; the title waves; "Pulsa cualquier tecla" pulses.
   - [ ] Key press → double flash, thunder, shake, cut to the storm image (Wilbur aiming at Don Lobo's shadow); buttons appear.
   - [ ] Ambient lightning every 12–20 s, soft.
   - [ ] Buttons: hover, click, keyboard; Options saved after restarting the game (volume, music, sensitivity, fullscreen); Credits; Salir closes the build.
   - [ ] Jugar → Intro with the same music, ducked under the narrator, faded into the rain → NivelPorky with no music left over.
   - [ ] Pause → Salir al menú → music starts again once, cursor free.
   - [ ] 16:9 / 16:10 / 21:9; no console errors; no GC spikes.
2. Debug flags off; dead code out.
3. Audit `Docs/audits/AUDIT_menu_2.md` with a **block for the Registro de fases** (Resultado, Subfases, Decisiones, Para los demás, Pendientes, Commits), in Spanish.

---

## 3. Plan B (in this order)
1. Without `menu_rayo_*`: flash + thunder over the current image.
2. Without new attract frames: keep Cristian's current `menu_inicio_01/02`.
3. Drop ambient lightning and screen shake.
4. Drop dust and scratches; keep grain + flicker.
5. Drop the parallax.
