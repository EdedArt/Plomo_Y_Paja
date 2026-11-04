# PHASE PISTAS — Clues: readable without UIManager, final texts, art, Level 2 clues

> In the repo: `Docs/phases/Fase-Pistas.md` · content: `Docs/guiones/pistas.md` (Guion-Pistas v1.0).
> **Owner: Edgar** (Claude Code) · **Branch:** `pistas`, created from the tip of `origin/integracion/dia4`; push `pistas` and David merges it.
> **Deadline:** tonight, Tue 3 Nov.
> Paths are relative to `PLOMO_Y_PAJAV_1/Assets/_Game/` unless they start with `Docs/`, `docs/`, `Tools/` or `_entrante_*`.

## Context (read first)
- `UIManager` does not exist in any scene. Today `CluePickup` shows its prompt and its text through `UIManager`, so **the player can pick a clue up but never sees "[E]" nor reads it** (`Docs/audits/AUDIT_n2_1.md`, item 4).
- Other systems in place: `RubberHoseCrosshair.SetPrompt` (pickups use it, E, ≤ 2.2 m, line of sight), `HudWilbur.ShowToast`, `UiHideReasons`, `CluePickup.Award` (clues given by interrogations), `AICompanion` clue line, `GameStateTracker` (clues count for the finales), the interrogation paper look + Special Elite font (`InterrogationUIBuilder.EnsureFont`).
- Level 2 (`Campamento`) has anchors `Anchor_Clue_foto_reunion_01` and `Anchor_Clue_carta_lobo_01`; its builder auto-places a `Pista_Auto_*` `CluePickup` there when the anchor is empty.

## Ownership tonight (two other Claude Code sessions are working)
| Edgar owns | Edgar must NOT edit |
|---|---|
| `CluePickup.cs` and any new `Scripts/Clues/*`, clue data assets, `Art/Props/pistas/`, clue prefabs, the clue reading card | Any `.unity` scene, `LevelBuilder_NivelPorky`, `ContadorYardBuilder`, `Nivel1MapBuilder`, `LevelBuilder_Campamento*`, `GameStateTracker.cs`, `GameManager.cs`, interrogation scripts, `AICompanion` |

If something needs a change in a file you don't own, **don't do it**: write in the audit exactly what change is needed and where; David passes it to the right session. **No scene edits**: the clues must look right through data, prefabs and `CluePickup` alone, so the level builders keep working when they run again.

## 0. Rules
Rules 1–9 of `Docs/phases/Fase-3C-Contador.md` §0 apply, with these substitutions:
- Audits go to `Docs/audits/AUDIT_pistas_<N>.md` (in Spanish).
- Editor menus under **Plomo y Paja → Pistas → …**, idempotent.
- Code in `Scripts/Clues/` (or where `CluePickup` already lives; say which).
- Contracts: `PlayerAim` owns FOV · layer 8 reserved · no `UIManager` · `RubberHoseCrosshair` is the only crosshair · `UiHideReasons` owns HUD/hands visibility · existing importers (`CharacterSpriteImporter`; add a `pistas` case, git status first and commit it right away).
- Player-facing text **exactly** as in the guion. No per-frame GC. Never `git add -A` / `git add .`.

## 1. Decisions
| # | Decision |
|---|---|
| P1 | Prompt "[E] Examinar" through `RubberHoseCrosshair.SetPrompt`, same rules as pickups (E, ≤ 2.2 m horizontal, line of sight). No more picking clues through walls. |
| P2 | Picking a physical clue opens the **clue card**: own Canvas (above HUD, below interrogation/cinematic), paper look of the interrogation, Special Elite; title, optional image, text; `timeScale` 0, unscaled time, hands/HUD hidden via `UiHideReasons`; closes with E / click / Esc after a 0.5 s minimum (Esc must not open the pause on the same press). |
| P3 | After closing: `HudWilbur.ShowToast("Pista: <título>")`, then the usual AI clue line. Clues given by interrogation (`Award`) only show the toast (the interrogation scene already has its AI thought). |
| P4 | Texts and titles come from data (one place), filled from the guion. `testimonio_puerta_01` keeps its current text. |
| P5 | World visual: billboard sprite from `Art/Props/pistas/` per clue (placeholder = the current drawn sheet), resting on its surface, subtle glint like the pickups, no bob. Chosen by data, so the builders' `Pista_Auto_*` objects get the right look with no builder change. |
| P6 | A clue already collected (restart, snapshot restore) does not appear again; one collected after the level entry reappears after a restart (the tracker decides; just read it on Start). |

## 2. Subphases

### PISTAS.1 — Readable clues + final texts
**Model:** Opus 5.5 · ~45 min
0. Recon: every clue in the game (id, where it is placed, which builder places it, how it's awarded), every `UIManager` call in the clue path, current texts.
1. Clue data (reuse what exists; extend it with title, body, optional image, world sprite) with the texts of guion §2.
2. `CluePickup`: P1, P2, P3, P5, P6, without breaking `CluePickup.Award` and the counting for the finales.
3. Clue card prefab built by menu **Plomo y Paja → Pistas → Crear hoja de pista**; the card finds/creates itself at runtime if missing in a scene (no scene edits).
4. Menu **Plomo y Paja → Pistas → Validar pistas**: lists every clue, its data, sprite (real or placeholder) and where it is in the open scene.

**Audit template (PISTAS.1):**
```
- Lista de pistas (id, dónde está, quién la coloca, cómo se obtiene):
- Llamadas a UIManager que había y por qué se reemplazaron:
- Aviso [E]: ¿con línea de vista? ¿ya no se toman a través de paredes?
- Hoja: ¿pausa y restaura todo? ¿Esc no abre la pausa?
- Aviso "Pista: …" y pensamiento de la IA: ¿salen en orden?
- Textos: ¿iguales al guion? ¿testimonio_puerta_01 intacto?
- Cambios que hacen falta en archivos que no son míos (si hay):
- Qué no se pudo compilar/probar y qué debe revisar David en Unity:
- Archivos creados/modificados:
- Commits (rama pistas):
```

### PISTAS.2 — Art and close
**Model:** Sonnet 5.5 · ~20 min (when the art exists)
1. `python Tools/character_key.py` for the 4 world sprites (recon the tool's input folder; originals in an `_entrante_*` folder, never committed) → `Art/Props/pistas/`; `pista_foto_reunion_grande` imported as UI sprite, no compression.
2. Importer case `pistas` (PPU so a clue reads ≈ 0.3 m in the world; pivot bottom-center; mipmaps on).
3. Run *Validar pistas*; play Level 1 clues and, if `Campamento` exists, Level 2 clues.
4. Audit `Docs/audits/AUDIT_pistas_2.md` with a block for the Registro de fases (Resultado, Decisiones, Para los demás, Pendientes, Commits), in Spanish.

## 3. Plan B
1. No big photo image (text only).
2. Placeholder sheet for every clue.
**Never drop:** the [E] prompt and the readable card.
