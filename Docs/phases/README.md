# Plomo y Paja — Phase Plan

Each phase is a self-contained task for Claude Code. Phases run in order and are **gated**:
a phase may only start when the previous phase's audit file carries the green-light line.

| Phase | Day | Title | Model |
|---|---|---|---|
| [3A](PHASE-3A.md) | Thu 29 Oct | Technicolor look, asset pipeline, combat juice, barks, UI split | Opus 5.5 |
| [3A2](PHASE-3A2.md) | Fri 30 Oct | "Mouse P.I." look: unlit 2D characters, painted 3D cartoon environment | Opus 5.5 |
| 3A3 ([auditoría](../audits/PHASE-3A3-audit.md): PENDIENTE DE REVISIÓN) | Fri 30 Oct | First real guard sprite, interior contrast, menus blocked in Play mode | Sonnet 5.5 |
| 3A4 ([auditoría](../audits/PHASE-3A4-audit.md): PENDIENTE DE REVISIÓN) | Fri 30 Oct | Feet grounding, "worst house" crime scene, rain, broken street lamp, frame guide | Sonnet 5.5 |
| [3B](PHASE-3B.md) | Fri 30 Oct | Informants, GameStateTracker wiring, clues, Level 1 complete | Opus 5.5 |
| [4](PHASE-4.md) | Sat 31 Oct | Level 2 (Campamento), Don Lobo boss, final decision | Opus 5.5 |
| [5](PHASE-5.md) | Sun 1 Nov | Intro storybook, 3 endings, menus, death/retry | Sonnet 5.5 |
| [6](PHASE-6.md) | Mon 2 Nov | Asset integration, optional live AI voice, polish, FEATURE FREEZE | Sonnet 5.5 |
| [7](PHASE-7.md) | Tue 3 Nov | Windows build, README, release checklist | Sonnet 5.5 |

Delivery: **Wed 4 Nov 2026**.

## Workflow per phase (David)

1. `git checkout player && git pull`
2. Refresh the code graph (fast, cached, zero tokens):
   `/graphify ./PLOMO_Y_PAJAV_1/Assets/_Game/Scripts`
3. `/clear`
4. Prompt: `Execute docs/phases/PHASE-<X>.md`
5. When the agent finishes, open Unity, run the menus listed in the audit, play-test, and fill in
   the **"Pruebas en Unity (David)"** section of `docs/audits/PHASE-<X>-audit.md`.
6. Paste the audit file to the orchestrator (Claude chat). If approved, add this line at the very
   top of the audit file and commit it:
   `ESTADO: LUZ VERDE — aprobado por el orquestador`
7. Open the PR `player` -> `dev` on GitHub.

## Rules every phase follows (in addition to CLAUDE.md)

- **Gate check first.** If the previous phase's audit file does not start with
  `ESTADO: LUZ VERDE`, stop immediately and say which file is missing the line.
- **Context budget.** Read `CLAUDE.md` and `graphify-out/GRAPH_REPORT.md` first. Use the graph to
  find classes, callers and events; open only files you will change or whose API you must call.
  Never re-read a file you just edited. Never paste file contents into the audit.
- **Branch.** Work only on `player`. At the end, `git push origin player`. Never push, merge or
  rebase `dev` or `main`. Never open PRs.
- **Commits.** One commit per part, tagged as the phase says.
- **Unity can't run here.** Anything that needs the Editor goes into "Menús a ejecutar" /
  "Pasos manuales" and into the test checklist for David. Never claim something was tested in Play.
- **Audit.** Write `docs/audits/PHASE-<X>-audit.md` **in Spanish** using
  `docs/phases/AUDIT_TEMPLATE.md`, commit it in the last commit of the phase.
- **Ambiguity.** If the spec is ambiguous, pick the option that keeps scope smallest, implement it,
  and list it under "Decisiones tomadas" in the audit. Questions that block correctness go under
  "Preguntas para el orquestador".
