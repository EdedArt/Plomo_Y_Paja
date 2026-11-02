# Phase 5 — Intro storybook, 3 endings, menus, death and retry

- **Day:** Sun 1 Nov
- **Model:** Sonnet 5.5
- **Commit tag:** `dia5`
- **Gate:** `docs/audits/PHASE-4-audit.md` must start with `ESTADO: LUZ VERDE`. If not, stop.
- **Audit file:** `docs/audits/PHASE-5-audit.md`

## Goal
The game becomes a complete loop: main menu → intro → Level 1 → Level 2 → one of three endings →
credits → menu. Dying lets you retry the level. Everything works with placeholder art and gets
better automatically when Edgar's illustrations and Cristian's audio arrive.

## Context
Read the "Deuda técnica" of the 3B and 4 audits. Relevant: `Core/GameManager.cs`,
`Core/GameStateTracker.cs` (`DetermineEnding`, `Snapshot/Restore`, `ResetForNewGame`),
`AudioLibrary`, `CharacterSpriteSet` (from 3A), `DialogueUI`, `PlayerHealth.OnDeath`.

Final Build Settings order: `MainMenu`, `Intro`, `NivelPorky`, `Campamento`, `Final`.
`SampleScene` stays out of the build.

## Shared component: StorybookPlayer
Commit: `dia5(david): reproductor tipo libro de cuentos`

Runtime component used by Intro and Final. Plays a list of panels; each panel =
`image` (Sprite from a set, optional), `text` (Spanish), `speaker` (optional), `lineId` (optional
voice), `duration`. Behavior:
- Image fills the screen with a slow pan/zoom (Ken Burns, 1.00 → 1.08 scale).
- Text as a 1930s title card: cream card with black ornamental border at the bottom third,
  typewriter reveal; with a speaker, shows the name in bold above the line.
- Voice clip plays if it exists; the panel lasts `max(duration, clip length + 0.5 s)`.
- Without an image: a flat Technicolor color card per panel (palette from Phase 3A).
- Space/click = next panel; Esc = skip all.
- Background music `musica_jazz_01` (if present), fades between scenes.

## Part 1 — Main menu, pause, options, death
Commit: `dia5(cristian): menus, pausa y pantalla de muerte`

1. Scene **MainMenu** (generator menu **Plomo y Paja/Generar Menús**): title "Plomo y Paja" in a
   big 1930s cartoon logo style (TMP, cream with red outline), subtitle
   "Una historia de los tres cerditos", buttons: **Jugar** (`ResetForNewGame` → Intro),
   **Opciones**, **Créditos**, **Salir**. Background: Technicolor color card with slow animated
   rain; `intro_01` sprite if present.
2. **Opciones** panel (shared with pause): master, music, SFX and voices volume (AudioMixer with
   groups Music/SFX/Voices created by an editor script; AudioLibrary routes to them), mouse
   sensitivity, fullscreen toggle. Persist with PlayerPrefs.
3. **Pause** (Esc during gameplay): Continuar, Opciones, Reiniciar nivel, Menú principal.
   Uses `Time.timeScale = 0`; must coexist with hit-stop and the boss slow motion
   (one owner of timeScale; record how).
4. **Death screen** on `PlayerHealth.OnDeath`: big title "¡TE LLENARON DE PLOMO!", buttons
   Reintentar (reload current level and `Restore` the state snapshot taken when the level loaded)
   and Menú principal.

**Acceptance**
- 1.1 Full loop menu → game → pause → menu works without errors or a frozen timeScale.
- 1.2 Retry restores clues/interrogations to the level-start values (no duplicated clues).
- 1.3 Settings survive a restart of the game.

## Part 2 — Intro
Commit: `dia5(edgar): intro de los tres hermanos`

Scene **Intro**: StorybookPlayer with these 6 panels (images `intro_01..06` from the `intro`
sprite set when present; voice `narrador_intro_0N`). Then loads NivelPorky.
```
1 | Érase una vez, en la ciudad de Porcópolis, tres hermanos cerditos.
2 | Wilbur, el mayor, trabajaba de sol a sol. Su casa era de ladrillo, firme como su palabra.
3 | Max, el del medio, trabajaba... a ratos. Lo que ganaba se le iba en joyas, y su casa de madera brillaba más de lo que debía.
4 | Y Porky, el menor... Porky tenía un problema: los dados, las cartas y las deudas.
5 | Su casa de paja nunca resistió mucho. Y una noche de lluvia, ya no había casa. Ni Porky.
6 | Wilbur tomó su escopeta. Alguien iba a pagar por esto. Lo que no sabía era quién le debía a quién.
```

**Acceptance**
- 2.1 Intro plays fully, can be advanced and skipped, and always ends in NivelPorky.

## Part 3 — Endings and credits
Commit: `dia5(david): tres finales y creditos`

Scene **Final**: reads `GameStateTracker.DetermineEnding()` and plays the matching panel list,
then credits, then MainMenu. Speaker panels use the bottom-dialogue look; narrator panels the
title-card look. Voice line ids in brackets (play if present).

**Final A — "La bala fácil"**
```
narrador | Wilbur salió de la guarida con el humo todavía saliendo del cañón. Porky lo esperaba afuera, temblando.
Porky [porky_mentira_01] | ¡Hermano! ¡Me tenían secuestrado, te lo juro!
narrador | Wilbur lo abrazó. Esa noche, Porcópolis tuvo un héroe... o eso creyó él.
```
After credits, a 5 s post-credit panel: `Mientras tanto...` → Porky at a card table counting a
pile of money and laughing (`porky` set, `talk` anim if present). Closing line:
`A veces la justicia ciega solo ciega.`

**Final B — "Confianza equivocada"**
```
narrador | Wilbur había leído lo suficiente para dudar, pero no lo bastante para detenerse.
Porky [porky_confiesa_01] | Está bien... le robé. ¡Pero iba a devolverlo!
narrador | Porky se puso delante del Lobo. Y Wilbur, sin entender nada, apuntó a su propio hermano.
```
Hard cut to black for 2 s, then: `Seguir creyendo lo que queremos creer tiene un precio.`

**Final C — "La verdad pesa más"**
```
narrador | Wilbur bajó el arma. Don Lobo se sacudió el polvo del traje.
Don Lobo [donlobo_rendir_02] | ¿Quieres a tu hermano? Pregúntale cuánto me debe.
Max [max_testigo_01] | Wilbur... yo sabía algo. Debí decírtelo.
Max [max_testigo_02] | Porky le debía al Lobo hasta las orejas.
Porky [porky_nervios_01] | Ehh... puedo explicarlo.
Porky [porky_confiesa_01] | Está bien... le robé. ¡Pero iba a devolverlo!
Don Lobo [donlobo_finalc_01] | Tu hermano trabajará para mí hasta pagar el último centavo. Es lo justo, non?
narrador | Y así fue. Todos en paz... más o menos.
```
Closing line: `La verdad duele más que una bala, pero sana mejor.`

**Credits** (scrolling, cream on deep blue): "Plomo y Paja", then
`David Cabezas — Programación e integración`, `Edgar Arteaga — Arte y personajes`,
`Cristian — Escenarios, voces y sonido`, `Universidad Cooperativa de Colombia — Pasto, 2026`,
`Gracias por jugar`.

Add a debug menu **Plomo y Paja/Ver Final A|B|C** that opens Final with a forced ending
(editor only) so David can test all three without playing.

**Acceptance**
- 3.1 Each ending plays its exact text and closing line.
- 3.2 Final A shows the post-credit panel; Final B has the hard cut; Final C has Max.
- 3.3 After credits the game returns to MainMenu and a new game starts with clean state.

---

## Unity test checklist for David (copy into audit section 9, in Spanish)
1. Menús en orden; consola limpia.
2. Menú principal → Jugar → intro completa → Nivel Porky.
3. Saltar la intro con Esc funciona.
4. Pausa, opciones (volumen y sensibilidad), reiniciar nivel, volver al menú.
5. Morir y reintentar: las pistas no se duplican.
6. Ver Final A, B y C con el menú de depuración: textos, cierre, créditos y regreso al menú.
7. Cerrar y abrir el juego: las opciones se conservan.

## Finish
Write `docs/audits/PHASE-5-audit.md` (Spanish), commit with the last part, `git push origin player`.
Stop. Do not start Phase 6.
