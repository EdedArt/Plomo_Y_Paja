# Phase 4 — Level 2 (Campamento), Don Lobo boss, final decision

- **Day:** Sat 31 Oct
- **Model:** Opus 5.5
- **Commit tag:** `dia4`
- **Gate:** `docs/audits/PHASE-3B-audit.md` must start with `ESTADO: LUZ VERDE`. If not, stop.
- **Audit file:** `docs/audits/PHASE-4-audit.md`

## Goal
The second and last level: Don Lobo's camp, with the remaining clues, two informants, a boss
fight that always ends in a clear moral choice (shoot or lower the weapon), and the hand-off to
the ending scene.

## Context
Read the "Deuda técnica" sections of the 3A and 3B audits. Reuse `Editor/LevelBuilderUtils.cs`
and copy the structure of `Editor/LevelBuilder_NivelPorky.cs`. Relevant code:
`Core/FinaleTrigger.cs` (`OnWolfKilled`), `Core/GameStateTracker.cs` (from 3B),
`Core/GameManager.cs`, `Enemies/*`, `Clues/*`, `Dialogue/DialogueDatabase.cs`
(already has `guardia_barracon` and `guardia_registro` trees; `guardia_registro` awards
`registro_01`), `AI/AICompanion.cs` (**do not modify**).

## Out of scope
Ending scene content, intro, menus (Phase 5). Final art and audio (Phase 6).

---

## Part 1 — Campamento level generator
Commit: `dia4(cristian): generador del Campamento de Don Lobo`

Menu **Plomo y Paja/Generar Campamento** → `Assets/_Game/Scenes/Campamento.unity`, added to
Build Settings right after NivelPorky. Same Technicolor volume, materials helper and lighting
approach as Level 1, but with fog (`#3A3F8F`, density low) and more neon/warm light.

Layout (~80 × 60 m), from entrance to boss:
1. **Entrance alley** (PlayerSpawn), narrow, crates and barrels as cover.
2. **Courtyard** with two **barracks** (small buildings). Barrack 1 holds clue
   `foto_reunion_01` and informant `guardia_barracon`.
3. **Contaduría**: a small office shack with informant `guardia_registro` (gives `registro_01`
   only through interrogation; no physical pickup).
4. **La Guarida** (main building), three spaces:
   - Foyer with two guards.
   - **Office** (side room) with clue `carta_lobo_01` on a desk — reachable before the boss hall.
   - **Boss hall**: casino room with poker tables (cover), Don Lobo's desk at the back,
     a placeholder Porky sitting in a back corner (non-interactive, `porky` sprite set when it
     exists), radio audio source playing `musica_jazz_*`.
   - Trigger at the hall entrance = `BossIntroTrigger`.
5. A broken neon sign "LOBO'S" over the Guarida door (emissive material, flicker).

Population: **8 normal guards + 2 informants**: 2 patrol routes in the courtyard, 1 patrol in
the alley, 3 sentinels (alley end, contaduría door, guarida door), 2 guards in the foyer.
NavMesh baked; doors at least 1.6 m wide.

**Acceptance**
- 1.1 Generating twice produces the same scene without duplicates.
- 1.2 A path exists from spawn to the boss hall; every room is reachable on the NavMesh.
- 1.3 Leaving Level 1 loads Campamento with GameStateTracker intact.

## Part 2 — Don Lobo boss
Commit: `dia4(edgar): Don Lobo, jefe final`

`BossAI` (new) on a Don Lobo prefab built by `TestPrefabGenerator` (placeholder: bigger capsule,
dark purple suit `#4B2A6B`, `donlobo` sprite set when it exists, `voiceSet = "donlobo"`).

Add to `BarkDatabase`:
```
donlobo_encuentro_01|Ah, monsieur Wilbur... por fin. Pasa, pasa, la lluvia no es buena para el tocino.
donlobo_encuentro_02|Has hecho mucho ruido para ser un cerdo tan pequeño.
donlobo_combate_01|¡Qué maleducado, mon ami!
donlobo_combate_02|¡Muchachos, ocúpense de él!
donlobo_combate_03|No me obligues a ensuciarme los guantes.
donlobo_dano_01|¡Mon Dieu!
donlobo_dano_02|¡Sacrebleu! ¡Mi traje!
donlobo_rendir_01|Basta. Baja el arma y escucha la verdad.
donlobo_rendir_02|¿Quieres a tu hermano? Pregúntale cuánto me debe.
donlobo_morir_01|Idiota... nunca... supiste... la verdad.
```
Don Lobo's spoken lines appear in the **bottom DialogueUI style with speaker name "Don Lobo"**
(not as a bubble) and play the voice clip.

States:
1. **Idle** — seated at the desk.
2. **Intro** — on `BossIntroTrigger`: `AICompanion.Speak(AITrigger.FinalConfrontation)`, then
   `donlobo_encuentro_01`, then `_02`. Hall doors close behind the player.
3. **Combat** — HP 400 (shotgun falloff applies). Moves between poker-table cover points, fires
   3-shot bursts (10 damage each, hit chance falls with distance), barks `combate_*` and `dano_*`.
   At 60% HP: `combate_02` and 2 guards enter from side doors (once).
4. **Surrender** — at ≤ 25% HP. Damage is floored at the threshold until then, so the decision
   always happens. `donlobo_rendir_01`. Remaining guards in the hall surrender too (stop
   attacking).
5. **Decision** — see Part 3.

**Acceptance**
- 2.1 The fight cannot skip the decision (no one-shot kill, no falling through the floor).
- 2.2 Reinforcements spawn exactly once.
- 2.3 Lines are shown in the bottom dialogue style with the speaker name.

## Part 3 — Final decision and hand-off
Commit: `dia4(david): decision final y paso al final`

1. On Surrender: cinematic-light moment — slow motion `Time.timeScale = 0.6` (always restored),
   camera FOV narrows slightly, prompt at the bottom: `[Clic] Disparar     [E] Bajar el arma`.
   No timer.
2. **Shoot** (any shotgun hit on Don Lobo while in Decision): `donlobo_morir_01`, cartoon death,
   `GameStateTracker.RegisterWolfOutcome(killed: true)`.
3. **Lower the weapon** (E): weapon viewmodel lowers, `donlobo_rendir_02`,
   `RegisterWolfOutcome(killed: false)`.
4. After the last line + 2 s: load scene `Final` if it exists in Build Settings, else show the
   computed ending letter as a title card (`Final A / B / C`) for 4 s.
5. `FinaleTrigger`: either adapt it to drive this flow or mark it obsolete if BossAI owns it;
   record the decision.

**Acceptance**
- 3.1 Both choices register the correct outcome and reach the hand-off.
- 3.2 `DetermineEnding()` result at hand-off matches the rules from Phase 3B (log it).
- 3.3 Clues read during the level (including the letter) count toward the ending.

---

## Unity test checklist for David (copy into audit section 9, in Spanish)
1. Menús en orden; consola limpia.
2. Pasar del Nivel Porky al Campamento: las pistas del nivel 1 se conservan (ver contador).
3. Recorrer el Campamento: 10 enemigos, 2 informantes reconocibles, foto y carta encontrables.
4. Interrogar al Contable con la opción correcta: obtienes la pista del registro.
5. Jefe: diálogo de entrada, combate, refuerzos una sola vez, se rinde al 25%.
6. Elegir **Disparar** en una partida y **Bajar el arma** en otra: ambos llegan al final.
7. Anotar qué final (A/B/C) mostró cada partida y con cuántas pistas/interrogatorios.

## Finish
Write `docs/audits/PHASE-4-audit.md` (Spanish), commit with the last part, `git push origin player`.
Stop. Do not start Phase 5.
