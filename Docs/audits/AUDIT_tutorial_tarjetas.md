# AUDIT tutorial — tarjetas ilustradas

**Archivos encontrados / faltantes**
- Encontrados (7): tut_pensamientos, tut_caminar, tut_correr, tut_examinar, tut_apuntar, tut_recargar, tut_cambiar_arma (todos .png, en `PLOMO_Y_PAJAV_1/_entrante_ui/tutorial/`, carpeta ignorada por git).
- Faltantes (2): **tut_disparar** y **tut_sello_listo** (opcional).
- Paso con chip viejo: **T3 (escopeta)** completo, porque le falta una de sus 3 tarjetas (regla: paso con arte incompleto = etiquetas de texto). T1, T2 y T4 usan arte. Sin sello real se dibuja el visto rojo. Al agregar `tut_disparar.png` y correr el menú, T3 pasa solo a tarjetas.

**Recorte**
- `character_key.py --single --crop 2` (lo 20 / hi 90 / dilate 4, los de siempre) dejaba halo rosado en el borde (~100–1450 px con m>25 por imagen, casi todo en el borde). Se agregó la opción `--defringe` (despill COMPLETO, no la mitad, en la franja de borde de 3 px); con ella el borde quedó en 0–40 px por imagen (el resto "rosado" es la piel del cerdo, interior). Revisado sobre fondo verde: borde crema limpio.
- Tamaños recortados ≈ 1500×880 (se recorta a lo opaco + 2 px de margen).

**Tarjeta Pensamientos**
- Rect del papel vacío (imagen recortada 1500×884): x 0.513–0.967, y 0.272–0.945 desde arriba.
- Rects usados (normalizados, y hacia arriba, con margen): cuerpo x 0.54–0.94 / y 0.25–0.70; nota gris y 0.145–0.24; "Clic para continuar" y 0.07–0.135. Special Elite, #3B2A1E, auto-size (cuerpo 12–32, nota 10–20, cierre 12–26), overflow Truncate. Tarjeta centrada, 756 px de alto (70 % de 1080), preserveAspect, oscurecido alpha 0.6. Título de texto eliminado (ya está en la imagen).
- ¿Cabe? Estimado a mano (≈ 513×340 px de caja, el cuerpo ocupa ~13 líneas a 20–22 px): sí, sin desbordar. **No se pudo verificar dentro de Unity** (ver pasos manuales).

**Etiquetas**
- Alto 170 px a 1080p (ancho por aspecto ≈ 275–297 px), separación 16 px, fila abajo al centro (y=60), canvas 5 (igual que antes). Pantalla angosta: la fila se escala para caber en 96 % del ancho.
- Marcado: igual que antes (mover / Shift+mover / E; clic der. / clic izq. / R). T4 es UNA tarjeta y se marca con `WeaponSwitcher.OnWeaponChanged` (cualquier forma de cambiar de arma: 1, 2, rueda o lo que llame al switcher). No ejecutado en Unity.
- Sello: dibujado (visto rojo con dos trazos) mientras falte `tut_sello_listo`; con el archivo, se usa el sprite. Rotación aleatoria ±12°, pop 0.15 s; la tarjeta se oscurece (tinte 0.62). Entrada 0.6→1.08→1 en 0.25 s escalonada 0.08 s, balanceo ±1.5° / 2 s, salida con escala+fundido 0.25 s. Todo con tiempo real y sin asignaciones por frame.

**¿Algo aparece sobre interrogatorio / cinemática / pausa?** No. Se conservó sin cambios la lógica: `Blocked()` pone el alpha del grupo (que contiene ambas filas) en 0 y el tiempo no corre; los pasos en cola esperan.

**Menú que David debe correr**
- `Plomo y Paja → Nivel 1 → Importar tarjetas de tutorial` (recorta lo que falte, importa y reconstruye el tutorial de NivelPorky). Generar Nivel Porky / Colocar tutorial también lo hacen. Necesita `python` en el PATH.

**Archivos**
- Creados: `Editor/TutorialCardImporter.cs` (+ .meta), `Art/UI/Tutorial/tut_*.png` (7; los .meta los crea Unity al importar), este audit.
- Modificados: `Scripts/Tutorial/TutorialDirector.cs` (modo tarjetas), `Editor/TutorialBuilder.cs` (tarjeta ilustrada + fila de tarjetas), `Editor/CharacterSpriteImporter.cs` (regla Art/UI/Tutorial: Sprite UI, sin mipmaps, 2048, CompressedHQ, Clamp; versión 15), `Tools/character_key.py` (`--defringe`).
- Compile check: Roslyn de Unity 2022.3.62f3 contra los DLL del proyecto, ensamblado de runtime y de Editor: 0 errores.
- Commits: ver `git log` de la rama `worktree-tut-tarjetas` (basada en `integracion/dia4`).
