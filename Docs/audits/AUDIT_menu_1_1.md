# AUDIT MENU.1.1 — Libro animado y título nuevo

ESTADO: LUZ VERDE (orquestador, 3 nov)

**Fecha:** 3 nov 2026 · **Rama:** `worktree-menu-1-1` (sale de `3352fec`, punta de `integracion/dia4`) · **Sin push y sin integrar.**

> **Nada se probó en Unity.** El C# compila con Roslyn. Ningún menú se corrió y el atlas todavía no existe: lo genera el builder.

## Lo que David debe saber primero

| # | Hallazgo | Qué se hizo |
|---|---|---|
| 1 | **No se cumplió "sin worktree".** Esta sesión corre en segundo plano y el harness rechaza escribir en el checkout compartido | Se trabajó en `worktree-menu-1-1`. Falta un merge (ver "Pasos manuales"). Para que futuras sesiones en segundo plano editen el checkout directamente, David puede poner `"worktree": {"bgIsolation": "none"}` en `.claude/settings.json` |
| 2 | **Incidente, ya revertido:** el script de parches entró por Bash al checkout principal en el mismo lote en que el harness rechazó las escrituras. Dejó cambios a medias en 6 archivos y 2 de fuente, sin `MenuTitleLogo`, con Unity abierto | Se restauraron con `git restore` y se borraron los 2 archivos de fuente. Antes estaban limpios en `git status`. No se tocaron la escena, el prefab ni los `.meta` de David. **Si Unity recompiló en ese momento, pudo mostrar errores CS y pudo crear `.meta` que ya se borraron; deben desaparecer al recompilar** |
| 3 | **Luckiest Guy no es OFL, es Apache 2.0** (`METADATA.pb`: `license: "APACHE2"`; el repo de Google la tiene en `apache/luckiestguy` y la ruta `ofl/` da 404) | Se usó igual. Su licencia real está en `Fonts/LuckiestGuy-LICENSE.txt`. Apache 2.0 pide conservar la licencia al redistribuir |
| 4 | **La v1.4 pegada no llegó:** el mensaje traía solo la descripción | La v1.4 se redactó desde esa descripción. M3a, M11, la subfase y la plantilla son texto mío; reemplazar si hay versión oficial |
| 5 | **Puerta de fase:** `AUDIT_menu_1.md` dice `INTEGRADA, PENDIENTE DE PRUEBA EN UNITY`, no `LUZ VERDE` | Se siguió porque el encargo pide esta subfase expresamente |
| 6 | **La "y" al 45 % queda muy pequeña:** unos 21 px de ancho en la simulación, junto a letras de 128 pt | Se dejó según la especificación. Para agrandarla: el factor `TamanoTitulo * 0.45f` en `CrearMenuInicio.CapaTitulo` |
| 7 | **Bug de MENU.1 corregido:** "Pulsa cualquier tecla" se salía por abajo a 21:9 | Ahora está anclado al borde inferior |

## Plantilla MENU.1.1

- **Menús que David debe correr (en orden):** **Plomo y Paja → Crear menu de inicio**, después del merge. Crea los dos atlas y regenera `MenuPrincipal`, que sobrescribe la escena sin commitear de ahora. No hace falta correr otro menú.

- **Libro animado: fps, variación, orden, ¿sin respiración? ¿la tormenta quedó igual?** En `MenuFrameAnimator`:
  - `modoEspera` = Hervido (cortes secos), `fpsEspera` = 8 (rango 6–12), `variacionEspera` = 0.15 (±15 % por cuadro), `respirarEspera` = false.
  - El orden 01→06 en bucle lo da el builder, que ordena por número.
  - La tormenta sigue en el campo `modo` = Fundido, con `respirar` y los mismos tiempos: no cambió. Por código; **no se ha visto en Play**.

- **Fuente: ¿se descargó? licencia, atlas (glifos, tamaño), respaldo:**
  - Sí: `Fonts/LuckiestGuy-Regular.ttf`, del repo `google/fonts`, con licencia Apache 2.0 (hallazgo 3).
  - Atlas estático `Fonts/LuckiestGuy Titulo SDF.asset`: glifos `PLOMAJ`, 512×512, muestreo 96, padding 12.
  - Atlas estático `Fonts/IMFellEnglish-Italic Titulo SDF.asset`: glifo `y`, 256×256.
  - Se crean extendiendo `InterrogationUIBuilder.EnsureFont` (sobrecarga nueva; las llamadas existentes no cambian).
  - Respaldo: IM Fell English para el título y cursiva simulada para la "y", con aviso en consola.

- **MenuTitleLogo: cada especificación de M11 y dónde está su valor serializado:**

| Especificación | Dónde |
|---|---|
| PLOMO #C9CED6 → #4A5260, PAJA #F6E39A → #C8902E | `words[].top/bottom` |
| "y" ~45 %, IM Fell cursiva, roja #B3261E, borde crema, más abajo | builder (tamaño y borde #F3E6C4, contorno 0.2), `words[1].top/bottom`, `verticalOffset` = −18 |
| Arco ~6° | `arcDegrees` (total, de punta a punta) |
| Letra: rotación ±4°, escala ±5 %, altura ±6 px, semilla fija | `letterRotation`, `letterScale`, `letterHeight`, `seed` = 1930 |
| Contorno #2A1A10 ~0.3 | material de cada TMP, puesto por el builder |
| Sombra dura ~8 px abajo a la derecha, un segundo TMP detrás | `shadowOffset`, `shadowColor` |
| Brillo crema arriba | tercer TMP con alfa de arriba hacia abajo: `shineColor`, `shineAlpha` = 0.55 |
| Ola ±3 px, 2.2 s, desfasada por letra | `wavePixels`, `wavePeriod`, `wavePhasePerLetter` |
| Bamboleo ±1° y ±1 % | `wobbleDegrees`, `wobbleScale`, `wobblePeriod` = 3 s (el encargo no fijó el período) |
| Destello blanco de un cuadro | `whiteFrames` = 1 |
| Salto: 0.03 s entre letras, 0.85/1.15, estirar, rebote, 0.35 s | `jumpStagger`, `squash`, `stretch`, `jumpSeconds`, `bounceDamping`, `bounceCycles`, `jumpHeight` = 28 |
| El logo sube a su posición del menú | `MainMenu.subidaLogo` = 45, `duracionSubidaLogo` = 0.6; empieza con el rayo |
| Tinte frío 0.15 s | `coldTint` = #D6E6FF, `coldTintSeconds` |

  El **brillo** es una interpretación: un TMP crema encima, transparente hacia abajo. Juzgarlo a ojo. El bamboleo pasó de `MainMenu` a este componente, dentro de la malla.

- **Eventos agregados y quién los escucha:** `LightningFlash.StrikeStarted` y `LightningFlash.AmbientFlashStarted`, escuchados por `MenuTitleLogo`. No hizo falta evento en `MainMenu`: mueve el logo directamente al revelar. Además, `TMPro_EventManager.TEXT_CHANGED_EVENT` para volver a tomar la malla cuando TMP la regenera.

- **GC por cuadro: qué se hizo y qué debe medir David:** arreglos reservados en `Awake`; sin LINQ, cadenas ni clausuras en `Update`; el bamboleo no cambia la escala del `RectTransform`. No se pudo medir: mirar **GC Alloc** de `MenuTitleLogo.Update` en el Profiler. Son 8 TMP actualizados por cuadro (3 sombras, 3 letras, 2 brillos).

- **16:9 / 16:10 / 21:9: ¿choca con Wilbur o con los botones? cómo se comprobó:** no choca en la **simulación**: se replicó el `CanvasScaler` (match 0.5) y la cobertura del fondo con PIL, usando la fuente y los cuadros reales, y se revisó la imagen. Logo centrado en (400, 345), 128 pt. No es un render de Unity.

| Aspecto | Lienzo | Límite visible (x / y) | Caja del título en el menú (x / y) |
|---|---|---|---|
| 16:9 | 1920×1080 | ±960 / ±540 | 14..794 / 257..451 |
| 16:10 | 1821×1138 | ±911 / ±569 | igual |
| 21:9 | 2217×935 | ±1109 / ±468 | igual |

  La caja incluye el margen de salto, sombra, ola y arco. El botón superior llega a y = 95; la cabeza de Wilbur queda muy por debajo y a la izquierda. En espera la caja baja a y = 212..406. **El título tapa parte del tejado y la chimenea** en 21:9; los ojos del lobo quedan libres.

- **menu_logo.png: ¿qué pasa si existe?** Se usa esa imagen con bamboleo, salto del rayo (de toda la imagen) y tinte frío, sin efectos por letra. No se crean los atlas. El destello blanco no se aplica a la imagen, porque ya se dibuja a color pleno.

- **Archivos compartidos tocados (y si estaban limpios en git status):** `MenuFrameAnimator.cs`, `MainMenu.cs`, `LightningFlash.cs`, `CrearMenuInicio.cs`, `InterrogationUIBuilder.cs`, `Fase-Menu.md`: **limpios** antes de empezar. En ese checkout quedan sin commitear `MenuPrincipal.unity`, `PauseMenu_Canvas.prefab` y `.meta` generados, que esta rama no toca.

- **Qué no se pudo compilar/probar y qué debe revisar David en el Editor:** todo en Play. Sobre todo:
  - que el atlas se genere sin el aviso "No se pudieron generar";
  - que el contorno 0.3 no se corte con padding 12;
  - la línea base (`baselineY` = −46) con las métricas reales de TMP;
  - que la malla no salte al volver de Opciones;
  - el brillo y el tamaño de la "y".

- **Archivos creados/modificados:**
  - Creados: `UI/MenuTitleLogo.cs` (+ `.meta`), `Fonts/LuckiestGuy-Regular.ttf`, `Fonts/LuckiestGuy-LICENSE.txt`, esta auditoría.
  - Modificados: los 6 de "Archivos compartidos".

- **Compile check:** Roslyn de Unity 2022.3.62f3: `Assembly-CSharp` 107 fuentes y `Assembly-CSharp-Editor` 32 fuentes, **0 errores, 0 avisos**. Las 37 propiedades de `FindProperty` del builder tienen su campo.

- **Commits:**

| Hash | Mensaje |
|---|---|
| `ff7001f` | `dia4(david): Fase-Menu v1.4 con MENU.1.1 (M3a y M11)` |
| `d76b49f` | `dia4(david): MENU.1.1 cuadros de espera como libro animado rapido; la tormenta no cambia` |
| `e8bdb11` | `dia4(david): MENU.1.1 titulo por letras con Luckiest Guy, salto con el rayo y tinte con los relampagos` |
| (este) | `dia4(david): MENU.1.1 auditoria` |

## Pasos manuales (David)
1. En el checkout principal, en `integracion/dia4`: `git merge --ff-only worktree-menu-1-1`. Tus cambios sin commitear no chocan, porque esta rama no toca esos archivos.
2. Con Unity abierto, deja que recompile y corre **Plomo y Paja → Crear menu de inicio**.
3. Commitea lo generado, junto con lo que ya tienes pendiente: `MenuPrincipal.unity`, `PauseMenu_Canvas.prefab`, los `.meta` de los cuadros, los 2 atlas nuevos y los `.meta` de `LuckiestGuy-*`.
4. El resto de `.meta` sin trackear (`Art/FX`, `Art/Props`, HUD, `Pickups`) no son de esta fase; revisa si van en otro commit.
