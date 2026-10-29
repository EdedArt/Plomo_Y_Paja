# CLAUDE.md — Plomo y Paja

## Project
- Plomo y Paja: first-person shooter, Unity 2022.3.62f3 LTS, URP, C#, old Input Manager. Unity project = `PLOMO_Y_PAJAV_1/`.
- Story: Wilbur (pig, shotgun) hunts mob boss Don Lobo (wolf) to rescue his little brother Porky, who actually stole from Don Lobo and is held as collateral.
- Scope: 2 levels (NivelPorky, Campamento), 5 clues, 3 interrogable guards, 3 endings. Deadline 2026-11-04.
- Art direction: FULL COLOR 1930s Technicolor cartoon, rubber-hose 3D world + 2D billboard sprites, exaggerated Popeye-era cartoon violence.
- All in-game text in Spanish.

## Where things are
- Code: `Assets/_Game/Scripts/<area>/`; editor tools: `Assets/_Game/Scripts/Editor/`
- Sprites: `Assets/_Game/Art/Characters/<characterId>/<anim>_<NN>.png`
- Voices: `Assets/_Game/Audio/Voices/<characterId>/<lineId>.wav`; SFX: `Assets/_Game/Audio/SFX/<name>_<NN>.wav`
- Generated scenes come from "Plomo y Paja/..." editor menus; never edit them by hand.

## Hard rules
- Code must compile against Unity 2022.3: compile-check both assemblies with the Editor's bundled Roslyn against the project DLLs before every commit.
- Never hand-edit .unity/.prefab/.asset YAML; use Editor scripts (AssetDatabase/PrefabUtility).
- Never `?.` or `??` on UnityEngine.Object; use explicit `!= null` / TryGetComponent.
- Never read, print, log or stage `Assets/StreamingAssets/openai_key.txt`.
- Extend existing scripts; don't duplicate systems; don't rename public members used elsewhere.
- Every system must keep working when an art/audio asset is missing (placeholders stay).
- Move files with `git mv` together with their .meta.
- One local commit per part, message `<dayTag>(<owner>): <spanish summary>`. Never push unless the prompt says so.

## How to work (token budget)
- Before exploring code, read the graph report in `graphify-out/` to locate classes and callers. Open only the files you will change or whose API you must call. No full-repo greps when the graph answers it.
- Don't re-read a file you just edited. Don't paste file contents into the report.
- Unity can't be run here: list manual Editor steps instead of guessing they happened.

## Report format (end of every task, nothing else)
```
AUDIT <TASK>
1. Archivos creados
2. Archivos modificados (file - one line)
3. Menús a ejecutar (in order)
4. Compile check
5. APIs dudosas
6. Pasos manuales
7. Commits (hash - message)
```
