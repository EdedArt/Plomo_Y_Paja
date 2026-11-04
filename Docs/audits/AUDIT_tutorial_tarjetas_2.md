# AUDIT tutorial tarjetas 2

**¿Merge hecho? hash / conflictos**
- **NO.** La copia principal (`integracion/dia4`) no está limpia: 451 entradas (NivelPorky.unity, Centavo.prefab, Taxi_Intro.prefab, EditorBuildSettings.asset, LuckiestGuy SDF.asset modificados + Campamento y materiales sin seguimiento). Según la instrucción, me detuve. No hubo conflictos porque no se intentó. El worktree sigue en `.claude/worktrees/tut-tarjetas` (rama `worktree-tut-tarjetas`) y NO se eliminó.
- Pendiente: David commitea o guarda lo de la copia principal y luego `git merge --no-ff worktree-tut-tarjetas` en `integracion/dia4`; después `git worktree remove .claude/worktrees/tut-tarjetas`.

**¿Las 8 tarjetas en Art/UI/Tutorial?**
- Sí, las 8 (pensamientos, caminar, correr, examinar, apuntar, disparar, recargar, cambiar_arma), recortadas con `character_key.py --single --crop 2 --defringe` (mismos valores). `tut_disparar` salió 1480×918. Solo falta el sello (opcional, se dibuja). Quedan en la rama del worktree; llegan a la copia principal con el merge.

**¿Generar Nivel Porky hace todo? orden de llamadas**
- Sí: `LevelBuilder_NivelPorky` → … HUD → taxi/llegada → `TutorialBuilder.BuildInto` (primero `TutorialCardImporter.ImportAll`, luego tarjeta, filas de tarjetas y TutorialDirector con sprites asignados) → pickups → `SaveScene` → NavMesh → `SaveScene`. Es decir, después de armar el nivel y antes de guardar. Termina con una línea: `Tutorial: X/8 tarjetas asignadas (faltan: …)`.

**¿Qué pasa si no hay python?**
- Python solo se llama para PNG que NO existan ya en `Art/UI/Tutorial/` (con los 8 en el repo no se usa nunca). Si hace falta y no hay Python o falla: se loguea UN error con el comando a mano, no se reintenta con los demás archivos y se sigue con lo que exista (los pasos sin arte conservan las etiquetas de texto).

**Commits**
- Ver `git log worktree-tut-tarjetas`: `c11c869` (tarjetas) y el commit de este paso (disparar + ImportAll sin Python si ya existen los PNG). Compile check (runtime y Editor, Roslyn de Unity 2022.3.62f3): 0 errores.
