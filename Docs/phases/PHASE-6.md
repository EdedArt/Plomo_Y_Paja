# Phase 6 — Asset integration, optional live AI voice, polish, FEATURE FREEZE

- **Day:** Mon 2 Nov
- **Model:** Sonnet 5.5
- **Commit tag:** `dia6`
- **Gate:** `docs/audits/PHASE-5-audit.md` must start with `ESTADO: LUZ VERDE`. If not, stop.
- **Audit file:** `docs/audits/PHASE-6-audit.md`

## Goal
Edgar's sprites and Cristian's audio go into the game, everything is checked against the asset
guide, the game gets its final sound mix and polish, and the optional live voice for the AI is
added behind a switch. **After this phase no new features: only bug fixes.**

## Context
Read the "Deuda técnica" of every previous audit. If `docs/phases/BALANCE_NOTES.md` exists,
apply its notes (written by the orchestrator from David's playtests). Relevant: everything from
3A (`CharacterSpriteImporter`, `CharacterSpriteSet`, `AudioLibrary`, `BarkDatabase`),
`AI/AICompanion.cs` (**do not modify**; it exposes `event Action<string> OnLineSpoken`).

## Part 1 — Bring in the assets
Commit: `dia6(david): integracion de sprites y audio`

1. `git fetch origin` and `git merge origin/dev` into `player` (Edgar's and Cristian's PRs land in
   `dev`). Conflicts in binary or YAML assets: keep `origin/dev` for anything under
   `Assets/_Game/Art/` and `Assets/_Game/Audio/`, keep `player` for everything else; list every
   conflicted file in the audit.
2. Editor menu **Plomo y Paja/Verificar Assets** that writes `docs/audits/ASSET_REPORT.md`
   (Spanish) with:
   - per characterId: animations found, frame counts, wrong sizes (not 512×512), wrong names;
   - every line id from `BarkDatabase`, the intro and the endings: clip present yes/no;
   - every SFX name from 3A: variants found;
   - files that don't follow the naming convention.
   Run it (it can only run in Unity: list it as a menu for David) and leave the report path in
   the audit. The agent **also** produces the same check by scanning the file system, so the
   audit has a first version without Unity.
3. Assign sprite sets and accessory sets in the prefab generator automatically by characterId.
4. Rename-only fixes are allowed (wrong case, missing zero padding) with `git mv` + `.meta`;
   never edit image or audio content.

**Acceptance**
- 1.1 ASSET_REPORT lists every expected asset with present/missing.
- 1.2 Characters with complete sets show sprites; incomplete ones keep the placeholder for the
  missing animations only.

## Part 2 — Sound mix and ambience
Commit: `dia6(cristian): mezcla de sonido y ambiente`

1. Per scene music: MainMenu `musica_jazz_01`, NivelPorky `musica_jazz_02` low, Campamento
   `musica_jazz_03` low; boss hall: the radio source only. Crossfade 1 s between scenes.
2. `lluvia_loop` ambience in both levels (2D, low), occasional `trueno` (every 40–90 s).
3. Footsteps for the player (`pasos_madera` indoors, `pasos_barro` outdoors, by ground material
   or by trigger volumes from the generators).
4. Mixer defaults: Voices 0 dB, SFX −3 dB, Music −12 dB; music ducks −6 dB while a voice line or
   an AI thought is on screen.

## Part 3 — Optional live voice for the AI (behind a switch)
Commit: `dia6(david): voz en vivo opcional para la IA`

1. New `AICompanionVoice` component next to AICompanion, subscribed to `OnLineSpoken`. Default
   **off**; toggle in Opciones ("Voz de la IA en vivo"), saved in PlayerPrefs.
2. When on: POST to OpenAI's speech endpoint (`/v1/audio/speech`) with the same key loading
   rules as AICompanion (env var `OPENAI_API_KEY`, then `StreamingAssets/openai_key.txt`),
   `response_format: "wav"`, and style instructions: whispering, mysterious, female, Spanish,
   1930s radio-drama tone. Model and voice are serialized strings; verify the current model name
   and supported parameters in OpenAI's docs before choosing defaults, and write what you used
   in the audit. If you can't verify, default the switch to off and leave the fields empty.
3. Play through the Voices mixer group as 2D audio. If the request fails or takes more than 4 s,
   skip the audio silently (the text is already on screen). Never queue more than one request.

**Acceptance**
- 3.1 With the switch off, zero extra network calls.
- 3.2 With it on and no key / no network, no errors and no stalls.

## Part 4 — Polish and performance
Commit: `dia6(edgar): pulido final`

1. Light pass from David's notes; make sure every clue has a visible warm light nearby.
2. Pools for popups, decals, blood and bubbles verified (no growth after 5 minutes of combat).
3. Remove leftover debug logs except those behind serialized flags.
4. Resolve every item in the previous audits' "Deuda técnica" that is cheap (< 15 min);
   list the rest as "pendiente, no bloqueante".
5. Write `docs/FEATURE_FREEZE.md`: one line, `Congelado el 2 de noviembre de 2026. Solo
   correcciones de errores a partir de aquí.`

---

## Unity test checklist for David (copy into audit section 9, in Spanish)
1. Ejecutar **Reconstruir Sets de Sprites**, **Reconstruir Librería de Audio**, **Verificar Assets**.
2. Revisar `ASSET_REPORT.md`: ¿qué falta? Avisar a Edgar/Cristian.
3. Partida completa con assets reales: sprites, voces, música y lluvia suenan bien.
4. La música baja cuando habla un personaje o la IA.
5. Activar "Voz de la IA en vivo" en Opciones: la IA habla en voz alta sin trabar el juego.
6. 5 minutos de combate seguidos: el juego no se pone lento.

## Finish
Write `docs/audits/PHASE-6-audit.md` (Spanish), commit with the last part, `git push origin player`.
Stop. Do not start Phase 7.
