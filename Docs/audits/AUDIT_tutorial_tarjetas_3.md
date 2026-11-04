# AUDIT tutorial tarjetas 3

**Commits de la copia principal (hash + qué incluye cada uno):**
- `7f050a8` — `.meta` de scripts y carpetas de N1.4 y N2.2 (10 archivos: `Characters/Lobo`, `LoboBossController.cs`, `FootprintTrail.cs`, `LoboBossBuilder.cs`, `Finale`, `FinaleResolver.cs`, `Level/Footprints`, `FootprintsSceneDirector.cs`, `ForestGate.cs`, `LevelExit.cs`).
- `5657a2c` — N1 regenerado (18 archivos: `Scenes/NivelPorky.unity`, `Prefabs/Level1/Centavo.prefab`, `Prefabs/Level1/Taxi_Intro.prefab`, `Data/PickupLayout_NivelPorky.asset` + `.meta`, `Art/Props/huellas/` con `gen_huella_cerdo_01.png` y `gen_huella_lobo_01.png` + `.meta`, materiales `Huella_huella_cerdo_01.mat` y `Huella_huella_lobo_01.mat` + `.meta`, y mallas `Huellas_Bosque_Cerdo.asset` y `Huellas_Bosque_Lobo.asset` + `.meta`).
- `a29b7a9` — N2 escena Campamento, DonLobo y assets generados (424 archivos: `Scenes/Campamento.unity` + `Campamento/NavMesh.asset` + `.meta`, `Prefabs/Level2/DonLobo.prefab` + `.meta`, `Art/SpriteSets/lobo.asset` + `.meta`, `Data/PickupLayout_Campamento.asset` + `.meta`, 9 materiales `Greybox/Camp_*.mat` + `.meta`, y 197 mallas en `Art/Environment/Generated/` + `.meta`).
- `5a58dd9` — Build settings y fuentes (2 archivos: `ProjectSettings/EditorBuildSettings.asset` con `Campamento.unity` habilitada y `Assets/_Game/Fonts/LuckiestGuy SDF.asset`).

**¿Algo quedó sin commit? ¿por qué?**
- Sí, 2 `.meta` de carpetas vacías: `PLOMO_Y_PAJAV_1/Assets/_Game/Art/Characters/lobo.meta` y `PLOMO_Y_PAJAV_1/Assets/_Game/Audio/Voices/lobo.meta`. `LoboBossBuilder.EnsureAssetFolders()` creó las carpetas `Art/Characters/lobo/` y `Audio/Voices/lobo/` vacías en disco; como Git no versiona directorios vacíos, commitear esos `.meta` sin archivos dentro dejaría `.meta` huérfanos en el repo. Se subirán junto con sus PNG/WAV cuando llegue el arte y audio de Don Lobo.

**¿Se añadió algo al .gitignore?**
- No. `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`, `*.csproj`, `*.sln` y `_entrante_*` ya estaban ignorados y ninguno apareció como untracked.

**Merge: hash / conflictos y cómo se resolvieron:**
- Hash: `50b67e3` (`Merge branch 'worktree-tut-tarjetas' into integracion/dia4`).
- Conflictos: 1 en código (`PLOMO_Y_PAJAV_1/Assets/_Game/Scripts/Editor/CharacterSpriteImporter.cs`, líneas 35–43: N2.2 por `lobo` y `worktree-tut-tarjetas` por `Art/UI/Tutorial/` subieron `GetVersion()` de 14 a 15). Tras detenerse y confirmar con David, se conservaron ambos cambios (`lobo` + `TutorialFolder` / `ApplyTutorialCard`) y se subió `GetVersion()` a `16`. `TutorialDirector.cs` y `Tools/character_key.py` auto-mergearon sin conflicto; 0 conflictos en archivos generados.
- Worktree `.claude/worktrees/tut-tarjetas` eliminado y rama `worktree-tut-tarjetas` (`d484178`) borrada.

**¿Están las 8 tarjetas y el importador en la copia principal?**
- Sí: las 8 tarjetas en `PLOMO_Y_PAJAV_1/Assets/_Game/Art/UI/Tutorial/` (`tut_apuntar.png`, `tut_cambiar_arma.png`, `tut_caminar.png`, `tut_correr.png`, `tut_disparar.png`, `tut_examinar.png`, `tut_pensamientos.png`, `tut_recargar.png`; aún sin `.meta`, Unity los creará al abrir con `CharacterSpriteImporter` v16), `Editor/TutorialCardImporter.cs` (+ `.meta`), los cambios en `TutorialDirector.cs` y `TutorialBuilder.cs`, y `--defringe` en `Tools/character_key.py`.

**Compile: errores:**
- **0 errores** (0 avisos; Roslyn de Unity 2022.3.62f3: `Assembly-CSharp` 125 fuentes, `Assembly-CSharp` sin `UNITY_EDITOR` 125 fuentes, `Assembly-CSharp-Editor` 46 fuentes).
