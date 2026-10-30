# Phase 3B — Informants, game state, clues, Level 1 complete

- **Day:** Fri 30 Oct
- **Model:** Opus 5.5
- **Commit tag:** `dia3b`
- **Gate:** `docs/audits/PHASE-3A2-audit.md` must start with `ESTADO: LUZ VERDE`. If not, stop.
- **Audit file:** `docs/audits/PHASE-3B-audit.md`

## Goal
Level 1 (NivelPorky) becomes a complete, playable investigation: a mix of normal guards and a
recognizable-but-not-obvious informant, two readable clues, a game state that remembers every
choice and feeds the AI, a HUD, balanced shotgun damage, and an exit to Level 2.

## Context
Read the "Deuda técnica" section of the 3A audit first. Relevant code (verify with the graph):
`Core/GameStateTracker.cs` (has `Register*`, `DetermineEnding`, `GetStatusSummary`),
`Core/GameManager.cs` (`LoadNextLevel`, `PlayerDied`, `TogglePause`), `Clues/CluePickup.cs`,
`Dialogue/DialogueNPC.cs` (static `OnInterrogationCompleted(npcId, result, clueId)`),
`Enemies/*`, `UI/UIManager.cs`, `AI/APISettings.cs`, `AI/AICompanion.cs` (**do not modify**;
use `AICompanion.ContextProvider` and `Speak`), `Editor/LevelBuilder_NivelPorky.cs`,
`Editor/TestPrefabGenerator.cs`, `Level/LevelMarker.cs`.

## Design decisions already made (do not reopen)
- **Informants = the interrogable guards.** Only informants can surrender and be interrogated.
  Normal guards fight to the death.
- Informants are recognizable by three combined cues: a red scarf (the only red item on a
  character), hesitant behavior, and the clues mentioning them. No icons, no outlines, no markers.
- The AI comments on kills **only when an informant dies**. Normal kills get no AI line.
- Ending rules (implemented in `DetermineEnding`, used in Phase 4/5):
  - **C:** wolf spared AND clues ≥ 3 AND interrogations completed ≥ 2
  - **A:** wolf killed AND clues < 2
  - **B:** every other case

---

## Part 1 — Informants and balance
Commit: `dia3b(edgar): informantes y balance de escopeta`

1. `EnemyAI` gets `public enum EnemyRole { Normal, Informant }` and field `role`.
   Informant behavior: tries to keep 8–12 m from the player, backs away when the player is
   within 6 m, accuracy ×0.5, fire interval ×1.6, sometimes hesitates (0.5–1 s pause before
   shooting). Normal behavior unchanged.
2. `EnemyHealth`: informants `canSurrender = true`, surrender threshold 50%, HP 80.
   Normal guards `canSurrender = false`, HP 100.
3. Barks: informants use `voiceSet = "informante"`.
4. Kill hook: `AICompanion.Instance.Speak(AITrigger.EnemyKilled, "era un informante que sabía
   algo de Porky")` only when an informant dies; remove the call for normal guards.
5. Placeholder look: informants get a small red scarf mesh at the neck (`#B3001B`) on the
   capsule, and the `informante` accessory sprite set when sprites exist.
6. Shotgun damage falloff per pellet: 15 at ≤ 5 m, linear down to 5 at 25 m, 0 beyond range.
   Expose the curve values as serialized fields.
7. `TestPrefabGenerator`: `Enemy_Guard` = Normal; `Enemy_Interrogable` renamed in content to the
   informant prefab (keep the asset file name to avoid broken references) with role Informant,
   DialogueNPC, red scarf.

**Acceptance**
- 1.1 A point-blank shot kills a normal guard; 2–3 shots at mid range (8–12 m) make an informant
  surrender most of the time.
- 1.2 Informants never charge; normal guards do.
- 1.3 Killing a normal guard never triggers an AI line; killing an informant does.

## Part 2 — Game state wiring
Commit: `dia3b(david): estado del juego conectado a pistas, interrogatorios y la IA`

1. `GameStateTracker`: persistent singleton (`DontDestroyOnLoad`, root object, created on demand
   if missing). Tracks: collected clue ids (set), interrogations `npcId → InterrogationResult`,
   informants killed, enemies killed, `wolfKilled`, `wolfSpared`.
   API: `RegisterClue(id)`, `RegisterInterrogation(npcId, result)`, `RegisterKill(bool informant)`,
   `RegisterWolfOutcome(bool killed)`, `ResetForNewGame()`, `Snapshot()` / `Restore(snapshot)`
   (used by retry on death in Phase 5), `DetermineEnding()` → `EndingType { A, B, C }` per the
   rules above.
2. Subscribe to `DialogueNPC.OnInterrogationCompleted`: register the interrogation, and the clue
   if `clueId` is not null.
3. `AICompanion.ContextProvider` = a Spanish summary, max ~40 words, e.g.
   `"Pistas: 2 de 5 (nota de Porky, contrato). Interrogatorios: 1 (Colmillo: útil).
   Informantes muertos: 0. Sospecha de Wilbur sobre Porky: media."`
   Suspicion = baja (0–1 clues), media (2–3), alta (4–5).
4. Remove dead code: `UIManager.ShowAISubtitle` and `UIManager.ShowDialogue` (and their panels)
   if no caller remains; delete `AI/APISettings.cs` and any asset of that type if nothing
   references it (check GUIDs). Report what was removed.

**Acceptance**
- 2.1 Every interrogation and clue changes the context string the AI receives. Since
  AICompanion can't be modified, GameStateTracker logs the new context each time it changes,
  behind `[SerializeField] bool logState = true`.
- 2.2 `DetermineEnding()` has a unit-style editor test or a menu
  **Plomo y Paja/Probar Finales** that prints the ending for the 6 edge cases
  (0 clues killed, 1 killed, 2 killed, 3+2 spared, 3+1 spared, 5+3 killed).

## Part 3 — Clues and reading UI
Commit: `dia3b(cristian): pistas legibles en el Nivel Porky`

1. Static `ClueDatabase` (Spanish text, exactly as below). Each entry: `id`, `title`, `body`.
   ```
   nota_porky_01 | Nota de Porky |
   "Lobito: el viernes te llevo lo tuyo, palabra de cerdo. Dile al de la bufanda roja que deje
   de anotarme en su libreta. Y ni una palabra a Wilbur. — P."

   contrato_01 | Contrato de deuda |
   "Yo, Porky, reconozco deber a Don Lobo 1.000 billetes, prestados para negocios de cartas.
   Si no pago antes del día 30, quedo a su disposición como garantía.
   Firma: P.  Testigo: M."

   foto_reunion_01 | Foto de reunión |
   "Una foto de Porky y Don Lobo brindando en una mesa de póker, muy sonrientes.
   Al reverso, a lápiz: «Socios. Mayo.»"

   registro_01 | Registro de deudas |
   "Libreta del Contable: «Porky — 1.000 prestados, sin pagar. Además se llevó 4.300 de la caja
   la noche del póker y se rió del jefe delante de todos. Retenido hasta que pague.»"

   carta_lobo_01 | Carta de Don Lobo |
   "Monsieur Wilbur: si lees esto, entraste a mi casa a balazos. Tu hermano no está secuestrado:
   está pagando. Me robó y me humilló delante de mi familia, y aun así no le toqué un pelo.
   Pregúntale a Max. Él lo vio todo. — D. L."
   ```
2. `CluePickup` uses the database by id; on pickup: register in GameStateTracker, play
   `pista_papel`, open the reader, call `AICompanion.Speak(AITrigger.ClueFound, title + ": " +
   first sentence of body)`.
3. `ClueReader` overlay (built at runtime): aged-paper card centered, title + body in a
   typewriter-style font (use `Special Elite` if a TMP font asset with that name exists, else the
   default), `[E] Cerrar`. Player movement and shotgun are locked while open (reuse the same
   lock DialogueNPC uses; if both need it, extract one `PlayerInputLock` helper).
4. One interaction prompt system for clues and interrogations (`[E] Leer`, `[E] Interrogar`),
   same position and style.
5. `LevelBuilder_NivelPorky` spawns `CluePickup` objects at the `nota_porky_01` and `contrato_01`
   markers (small paper placeholder, slight glow so they're findable in the dark).

**Acceptance**
- 3.1 Both Level 1 clues can be found, read, closed, and are registered once each.
- 3.2 The AI speaks after reading a clue, and its line reflects the clue.

## Part 4 — Level 1 population, HUD and exit
Commit: `dia3b(cristian): Nivel Porky completo con HUD y salida`

1. Population via the generator: 1 informant (`guardia_porky`, kitchen) + 4 normal guards:
   patrol A around the house (existing 4 waypoints), new patrol B on the porch/street
   (3 waypoints), sentinel in the backyard (existing), sentinel under a street lamp.
   Add the missing markers to the generator.
2. HUD (runtime-built or prefab via the generator; reuse UIManager's HUD only if wiring is
   cheap, otherwise a new `HUD` component and mark the HUD parts of UIManager obsolete — record
   the decision): HUD in the style of 1930s cartoon badges: health = round portrait medallion
   bottom-left with a number on a ribbon; ammo = round medallion bottom-right with loaded/reserve
   numbers; objective text top-left in a rounded cartoon font; no bars.
3. `LevelExit` trigger: if Build Settings has a next scene, `GameManager.LoadNextLevel()`;
   otherwise show a centered title card `Continuará...` for 3 s. GameStateTracker survives the
   scene change.
4. Remove `AICompanionTester` from every generated scene and prefab (keep the script file).

**Acceptance**
- 4.1 Level 1 can be played start to exit: 5 enemies, 1 informant, 2 clues, HUD always visible.
- 4.2 Walking into the exit with 0 clues and with 2 clues both work; state persists.

---

## Unity test checklist for David (copy into audit section 9, in Spanish)
1. Menús en orden; consola limpia. Ejecutar **Probar Finales** y pegar su salida.
2. Recorrer el Nivel Porky completo: ¿cuántos guardias? ¿se distingue el informante sin que sea obvio?
3. Disparo a quemarropa mata a un guardia normal; el informante se rinde a media distancia.
4. Matar un guardia normal: la IA no dice nada. Matar al informante: la IA comenta.
5. Encontrar y leer las 2 pistas; la IA reacciona a cada una.
6. Interrogar al informante; el contador de pistas y el contexto de la IA cambian.
7. HUD visible: vida, balas, pistas, mira.
8. Salida del nivel: aparece "Continuará..." o carga el siguiente nivel.

## Finish
Write `docs/audits/PHASE-3B-audit.md` (Spanish), commit with the last part, `git push origin player`.
Stop. Do not start Phase 4.
