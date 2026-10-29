# Phase 3A — Technicolor look, asset pipeline, combat juice, barks, UI split

- **Day:** Thu 29 Oct (afternoon)
- **Model:** Opus 5.5
- **Commit tag:** `dia3a`
- **Gate:** first phase — no previous audit required. Instead verify that `main`, `dev` and
  `player` exist on origin (`git ls-remote --heads origin`). If not, stop and report.
- **Audit file:** `docs/audits/PHASE-3A-audit.md` (Spanish, from `docs/phases/AUDIT_TEMPLATE.md`)

## Goal
The game stops looking like a grey prototype. It gets a full-color 1930s Technicolor look, combat
that feels heavy and cartoonishly violent, guards that talk, and a pipeline where Edgar's sprites
and Cristian's audio can be dropped into folders and replace the placeholders without code changes.

## Context (verify with the graph, don't re-explore)
All paths under `PLOMO_Y_PAJAV_1/Assets/_Game/Scripts/`:
`Player/ShotgunController.cs`, `Player/PlayerHealth.cs`, `Player/PlayerController.cs`,
`Enemies/EnemyAI.cs`, `Enemies/EnemyHealth.cs`, `Enemies/BillboardSprite.cs`,
`Core/NoirPostProcess.cs`, `AI/AICompanion.cs` (**do not modify**), `Dialogue/DialogueUI.cs`,
`Editor/LevelBuilder_NivelPorky.cs`, `Editor/LevelBuilderUtils.cs`,
`Editor/TestPrefabGenerator.cs`, `Editor/NoirVolumeBuilder.cs`.

Shotgun facts: 8 pellets × 15 damage, raycast with no layer mask, finds enemies with
`GetComponentInParent<EnemyHealth>()`. Guards: 60 HP, surrender at ≤30% with 0.4 s grace.

## Out of scope (later phases)
Informants, GameStateTracker wiring, clues, Level 2, Don Lobo, intro, endings, menus, balance.

---

## Part 0 — Graph commit
- Add to `.gitignore`: `graphify-out/cache/`, `graphify-out/.graphify_python`,
  `graphify-out/.graphify_root`.
- Append to `CLAUDE.md` a section `### Phases` with two lines: "The project runs in gated phases
  defined in `docs/phases/`. Read `docs/phases/README.md` before executing any phase file."
- Commit `graphify-out/GRAPH_REPORT.md`, `graph.json`, `graph.html`, `docs/` and `CLAUDE.md` →
  `chore: grafo de scripts y plan de fases`.

## Part 1 — Technicolor look and lighting
Commit: `dia3a(david): look Technicolor e iluminacion`

1. `NoirVolumeBuilder` also creates `Assets/_Game/Settings/TechnicolorVolumeProfile.asset` if
   missing: Tonemapping Neutral; Color Adjustments postExposure 0.4, contrast 10, saturation 20,
   colorFilter `#FFF4E6`; White Balance temperature 8; Bloom threshold 0.9, intensity 0.6;
   Vignette 0.2 black; Film Grain Thin1 0.12. `LevelBuilder_NivelPorky` uses it instead of the noir one.
2. `NoirPostProcess` overwrites the profile at runtime with noir values. Make its base values
   match the Technicolor profile. `TriggerDamagePulse` becomes a short red vignette
   (`#B3001B`, intensity 0.45, 0.25 s). Keep the class name and public API.
3. `LevelBuilder_NivelPorky`:
   - Ambient Gradient: sky `#3A3F8F`, equator `#6B4A7A`, ground `#4A3326`.
   - Moon directional light `#BFD4FF`, intensity 0.5, soft shadows.
   - 3 more warm lamps `#FFB060` (street, yard, back door); `LightFlicker` on one lamp only.
   - Interior lights warmer and brighter.
   - Greybox palette: Porky's walls straw `#E8C35A`, wood `#8A5A33`, front door `#C0392B`,
     window frames `#2A9D8F`, ground moss `#5E8C3A`, street cobblestone `#6C7A96`, debris `#7A6A55`.
4. `TestPrefabGenerator`, Player prefab: child of the camera named `FillLight`, point light,
   range 6, intensity 0.35, no shadows, `#FFE9C7`.

**Acceptance**
- 1.1 Regenerating NivelPorky produces the new palette, lights and volume.
- 1.2 No area of the playable path is near-black; the player can always see walls and enemies.
- 1.3 Damage pulse is red; no runtime code re-applies negative saturation.

## Part 2 — Asset drop-in pipeline
Commit: `dia3a(edgar): pipeline de sprites y audio`

Folder conventions (must match the team asset guide exactly):
- Sprites: `Assets/_Game/Art/Characters/<characterId>/<anim>_<NN>.png`
  characterIds: `guardia`, `informante` (accessory overlay: red scarf), `donlobo`, `porky`, `max`,
  `wilbur_hands`, `fx`, `intro`. Anims: `idle walk shoot hit surrender death talk fire reload`.
- Voices: `Assets/_Game/Audio/Voices/<characterId>/<lineId>.wav`
- SFX: `Assets/_Game/Audio/SFX/<name>_<NN>.wav`

1. `Editor/CharacterSpriteImporter.cs` (AssetPostprocessor scoped to the Characters folder):
   Sprite Single, pivot BottomCenter (`wilbur_hands`: BottomRight; `intro`: Center, PPU 100),
   PPU 250 (450 px ≈ 1.8 m), Bilinear, compression None, alphaIsTransparency.
2. Runtime `CharacterSpriteSet : ScriptableObject { string characterId; AnimClip[] clips; }`,
   `AnimClip { string name; Sprite[] frames; float fps; }` + `TryGet(string anim, out AnimClip)`.
   Menu **Plomo y Paja/Reconstruir Sets de Sprites** builds or updates one asset per character
   folder in `Assets/_Game/Art/SpriteSets/`. Default fps: idle 4, walk 8, shoot 10, hit 1,
   surrender 4, death 8, talk 6, fire 12, reload 8.
3. `BillboardSprite`: optional `CharacterSpriteSet` + optional accessory set (same frame index,
   rendered on top, sortingOrder +1). If the set is null or has no frames for an anim, the
   capsule placeholder stays visible and the sprite renderer is hidden. Existing
   `PlayWalk/PlayAlert/PlayDeath/SetFacingDirection` keep working.
4. `RubberHoseMotion` component (works on the placeholder too): idle bob ±3% height at 1.5 Hz,
   squash & stretch per step while moving, hit squash 0.15 s, surrender wobble.
5. `FirstPersonViewmodel` on the Player camera: overlay UI Image bottom-right fed by the
   `wilbur_hands` set; idle/fire/reload frames; walk sway, fire kick, reload dip. Hidden if no
   sprites. Add to `ShotgunController`: `public event Action OnFired, OnReloadStarted,
   OnReloadFinished, OnDryFire`.
6. `Editor/AudioImporter.cs`: everything under Voices and SFX forced mono; Voices
   DecompressOnLoad; names starting with `lluvia_` or `musica_` → Streaming.
7. Runtime `AudioLibrary : ScriptableObject` (lineId → clip; sfx name → variants grouped by the
   `_NN` suffix). Menu **Plomo y Paja/Reconstruir Librería de Audio** →
   `Assets/_Game/Audio/AudioLibrary.asset`. Static API: `AudioLibrary.PlayVoice(string lineId,
   Vector3 pos)`, `PlaySfx(string name, Vector3 pos)`, `PlaySfx2D(string name)`. Random variant,
   never the same one twice in a row. Missing clip = silent no-op, logged once per id.
8. Hook SFX: `escopeta_disparo` (fire), `escopeta_bombeo` (0.35 s after fire), `escopeta_cargar`
   (per shell on reload), `escopeta_vacia` (dry fire), `impacto_carne`, `sangre_chorro`,
   `golpe_cartoon`, `caida_silbato`, `estrellas`, `humo_puf` (death), `pista_papel` (CluePickup).

**Acceptance**
- 2.1 With empty Art/Audio folders the game behaves exactly as before (placeholders, no errors).
- 2.2 Dropping `guardia_idle_01.png` + running the rebuild menu makes guards show that sprite.
- 2.3 Dropping `escopeta_disparo_01.wav` + rebuild makes the shotgun play it.
- 2.4 The viewmodel is invisible without sprites and does not block the crosshair.

## Part 3 — Combat juice (placeholders are fine)
Commit: `dia3a(edgar): impacto y muertes caricaturescas`

Everything built in code, no particle prefabs required. Blood color `#B3001B`.
1. **Fire:** camera recoil kick 4° up with 0.15 s recovery; screen shake 0.08 s; muzzle point
   light 0.05 s; comic word popup (`¡BANG!`, `¡BLAM!`, `¡POW!`, yellow `#FFD23F` with black
   outline, scale-pop 0.4 s) near the muzzle.
2. **Hit:** knockback 1.5 m/s via `agent.Move` over 0.15 s; white flash 0.08 s on the renderer;
   blood burst of 20–30 big droplets with gravity; floor decal quad where droplets land
   (pool max 40, fade after 30 s).
3. **Death:** launched back and up with a spin, squash on landing, smoke puff + 3 orbiting stars
   + 2–4 flying teeth, blood puddle decal, fade out after 2 s. 50 ms hit-stop on kill
   (`Time.timeScale`, always restored, also if the object is destroyed mid hit-stop).

**Acceptance**
- 3.1 One shot at a placeholder guard shows recoil, popup, blood, knockback.
- 3.2 A kill shows the full cartoon death and the body disappears after ~2 s.
- 3.3 `Time.timeScale` is never left at a value other than 1 (also when pausing).
- 3.4 No per-shot allocations that grow unbounded (decals and popups are pooled).

## Part 4 — Barks and UI split
Commit: `dia3a(cristian): globos de dialogo y voces`

1. Runtime static `BarkDatabase` with exactly these `id|text` pairs (Cristian records audio with
   the same ids):
   ```
   guardia_ver_01|¡Eh, tú, cerdo!
   guardia_ver_02|¡Tenemos visita, muchachos!
   guardia_ver_03|¡Alto ahí, jamón con patas!
   guardia_ver_04|¡El jefe quiere tu tocino!
   guardia_dano_01|¡Ay, mi pellejo!
   guardia_dano_02|¡Eso dolió, marrano!
   guardia_dano_03|¡Auch! ¡Mi cola!
   guardia_rendir_01|¡No dispares! ¡Me rindo!
   guardia_rendir_02|¡Está bien, está bien! ¡Hablo!
   guardia_rendir_03|¡Tengo familia, cerdo!
   guardia_morir_01|¡Aaaay, mamá!
   guardia_morir_02|¡Dile al jefe... que lo intenté!
   guardia_morir_03|¡Uuuuf!
   guardia_patrulla_01|Maldita lluvia...
   guardia_patrulla_02|Otra noche cuidando paja.
   guardia_patrulla_03|¿Quién se comió mi sándwich?
   informante_ver_01|¡Ay no, ay no, ay no!
   informante_ver_02|¡N-no quiero problemas!
   informante_dano_01|¡Yo solo llevo las cuentas!
   informante_rendir_01|¡Sé cosas! ¡Sé cosas de tu hermano!
   informante_rendir_02|¡No me mates, te sirvo vivo!
   informante_morir_01|¡Yo... sabía... algo!
   ```
   API: `BarkDatabase.GetRandom(string voiceSet, string evt, out string lineId, out string text)`;
   `evt ∈ ver, dano, rendir, morir, patrulla`. Falls back to `guardia` if the set has no lines
   for that event.
2. `EnemyBarks` component, added by `TestPrefabGenerator` to both enemy prefabs. Field
   `voiceSet` (default `guardia`). Triggers: `ver` on first detection; `dano` on hit, max once
   per 3 s; `rendir`; `morir`; `patrulla` every 15–30 s while patrolling and the player is
   within 15 m. Shows a world-space speech bubble above the head (cream `#F2E6C8` panel, black
   outline and text, 2 s, faces the camera, one bubble per enemy at a time) and calls
   `AudioLibrary.PlayVoice(lineId, position)`.
3. `TestPrefabGenerator`, AICompanion prefab: subtitle moves to **top-center** (anchor top, 90 px
   from the top), italic, color `#F2E6C8`, dark TMP underlay, no panel. These are Wilbur's
   thoughts. `DialogueUI` stays at the bottom with the speaker name.

**Acceptance**
- 4.1 Guards show a bubble when they see the player, get hit, surrender and die.
- 4.2 AI thoughts appear at the top; character dialogue at the bottom; they never overlap.
- 4.3 All 22 ids exist exactly as listed (copy-paste check).

---

## Unity test checklist for David (copy into audit section 9, in Spanish)
1. Ejecutar los menús en el orden del audit. Consola sin errores rojos.
2. Generar Nivel Porky → Play: se ve a color, de noche pero legible, con faroles cálidos.
3. Disparar a un guardia: retroceso, "¡BANG!", sangre, empujón, mancha en el piso.
4. Matar a un guardia: muerte caricaturesca con humo, estrellas y dientes; desaparece.
5. El guardia muestra globos al verte, al recibir daño, al rendirse y al morir.
6. Los pensamientos de la IA salen arriba; el diálogo del interrogatorio abajo.
7. Recibir daño: el borde de la pantalla parpadea en rojo.
8. Con Pausa o al morir, el juego no se queda congelado ni acelerado.

## Finish
1. Write `docs/audits/PHASE-3A-audit.md` (Spanish, template) with `ESTADO: PENDIENTE DE REVISIÓN`.
2. Commit it with the last part, then `git push origin player`.
3. Stop. Do not start Phase 3B.
