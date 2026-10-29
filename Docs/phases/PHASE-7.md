# Phase 7 — Windows build, README, release

- **Day:** Tue 3 Nov
- **Model:** Sonnet 5.5
- **Commit tag:** `dia7`
- **Gate:** `docs/audits/PHASE-6-audit.md` must start with `ESTADO: LUZ VERDE`. If not, stop.
- **Audit file:** `docs/audits/PHASE-7-audit.md`

## Goal
A Windows build that runs on another PC, a README the professor can follow, and a release
checklist. Bug fixes only: **no new features** (see `docs/FEATURE_FREEZE.md`).

## Part 1 — Build pipeline
Commit: `dia7(david): build de Windows`

1. Player Settings via an editor script (not by hand): company `UCC Pasto`, product
   `Plomo y Paja`, version `1.0.0`, Windows x86_64, fullscreen window, default 1920×1080,
   resizable, icon from `Assets/_Game/Art/icon.png` if it exists.
2. `Editor/BuildScript.cs` with menu **Plomo y Paja/Build Windows** and a static method usable
   from batch mode: `BuildScript.BuildWindows`. Output `Builds/Windows/PlomoYPaja/` (add
   `Builds/` to `.gitignore`). Scenes = the 5 in Build Settings order; fail the build if any is
   missing.
3. **API key policy:** the build must NOT contain `openai_key.txt`. Before building, move it out
   of `StreamingAssets` to a temp folder and restore it in a `finally` block, even if the build
   fails. After the build, verify `PlomoYPaja_Data/StreamingAssets/` has no key file.
   The README explains how to add the key to the built game.
4. Optional: if David closes the Unity Editor and confirms, run the build in batch mode:
   `"<Unity 2022.3.62f3 path>\Editor\Unity.exe" -batchmode -quit -projectPath PLOMO_Y_PAJAV_1
   -executeMethod BuildScript.BuildWindows -logFile Builds/build.log`
   and report the result and the last 30 lines of the log. Find the Unity path from Unity Hub's
   default location; never guess silently.
5. Zip the build to `Builds/PlomoYPaja_v1.0.0_Windows.zip`.

## Part 2 — Final cleanup
Commit: `dia7(cristian): limpieza final`

1. `AICompanionTester` not present in any scene of the build (keep the file, or move it under an
   `Editor`-excluded `Debug` folder; record the choice).
2. Debug menus (`Ver Final`, `Probar Finales`) are editor-only (inside `Editor` folders).
3. No compile warnings that are new since Phase 3A.

## Part 3 — README and release checklist
Commit: `dia7(edgar): README y checklist de entrega`

1. `README.md` at the repo root, **in Spanish**:
   - Title, one-paragraph pitch, team and roles.
   - Requisitos (Windows 10/11, 64 bits).
   - Cómo jugar: controles (WASD, mouse, clic, R, E, Shift, Esc), objetivo, la IA.
   - **Cómo activar la IA:** crear `PlomoYPaja_Data/StreamingAssets/openai_key.txt` con la key, o
     la variable de entorno `OPENAI_API_KEY`. Sin key, el juego usa líneas de respaldo.
   - Los tres finales (sin spoilers detallados: "tus decisiones importan").
   - Cómo abrir el proyecto en Unity 2022.3.62f3 y regenerar escenas con los menús "Plomo y Paja".
   - Créditos de assets: herramientas de IA usadas para arte y voz, música y licencias.
   - Capturas: `docs/screenshots/*.png` (David las agrega).
2. `docs/RELEASE_CHECKLIST.md` (Spanish, checkboxes): build abre en otra PC, sin key en el build,
   los 3 finales alcanzables, intro y créditos, sin errores en el log del jugador, README
   revisado, capturas, zip subido, PR `dev → main` y tag `v1.0.0`.

---

## Unity test checklist for David (copy into audit section 9, in Spanish)
1. Ejecutar **Build Windows** (o confirmar que el agente lo hizo en modo batch).
2. Abrir el `.exe` en tu PC sin key: el juego funciona con líneas de respaldo.
3. Copiar la key a `StreamingAssets` del build: la IA responde.
4. Probar el zip en la PC de Edgar o Cristian (otra máquina).
5. Jugar de principio a fin una vez por cada final.
6. Revisar el README con ojos de profesor.

## Finish
Write `docs/audits/PHASE-7-audit.md` (Spanish), commit, `git push origin player`.
The PRs `player → dev` and `dev → main` and the `v1.0.0` tag are done by David on GitHub.
