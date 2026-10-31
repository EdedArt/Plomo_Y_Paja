# Auditoría Viewmodel Fase 1.4 — Mira centrada al apuntar, mira estilo Mouse, onomatopeya rara y limpieza de Git

ESTADO: PENDIENTE DE REVISIÓN

Rama: `player`, publicada en `origin/player`. Compila con 0 errores y 0 warnings nuevos (Runtime 59, Editor 0). **No se probó nada en Unity.**
La medición del error de centrado en Unity (sección 1) está pendiente: el flag **Debug Sight Log** ya está listo.

## 1) Cómo se centra la mirilla y el error medido en cada formato

**Punto de mira (`aimSightPoint`).** Nuevo campo de `ViewmodelProfile`: píxeles del arte 1920×1080 con el origen abajo a la izquierda. Se mide solo del PNG `aimidle`:
opaco = alpha ≥ 128, franja central (30–70 % del ancho), fila opaca más alta y promedio de x de sus 3 filas superiores (la punta del "bead"). Lo hacen dos programas con el mismo algoritmo:
`Tools/viewmodel_sight.py` (para reportar) y `ViewmodelProfileBuilder.MeasureSightPoint` (en el Editor, para rellenar el asset). Comprobé a ojo que la cruz cae sobre la punta de la mira delantera.

| Arma | `aimSightPoint` |
|---|---|
| Escopeta (`wilbur_vm_shotgun_aimidle_01`) | **(960.5, 575.0)** |
| Revólver (`wilbur_vm_revolver_aimidle_01`) | **(964.3, 723.0)** |

Los assets `VM_*.asset` existentes reciben el valor medido al abrir Unity (campo `sightVersion`, igual que la migración de tiempos de la 1.3); hasta entonces usan el valor por defecto de la clase (el de la escopeta).

**Cómo se centra.** Cada frame, `ViewmodelMotion.SightCenterOffset` pasa `aimSightPoint` del arte al espacio de `ViewmodelRoot` usando el rect real de HandsImage (esquina inferior derecha + overscan) y devuelve `centro de la pantalla − punto`.
Ese desplazamiento se mezcla con el apuntado (`aimT`) en lugar de las posiciones de cadera. Al apuntar del todo, el balanceo, la inercia y la respiración valen 0 (posición, rotación y escala); solo el culatazo puede moverla y vuelve al centro exacto.
`KeepEdgesOffscreen` conserva solo el borde inferior mientras se apunta (el derecho pesa `1 − aimT`).

**Error por formato — IMPORTANTE: es un cálculo, no una medición en Unity.** Con la fórmula del código en un modelo numérico (canvas de 1080 de alto con `match = 1`), el punto de mira cae en el centro con error 0.0000 px en todos los formatos
(es cero por construcción, porque la fórmula es `centro − punto`). Lo que sí aporta el modelo es el desplazamiento que resulta y que el dibujo no se corta:

| Formato | Desplazamiento X (escopeta) | Borde inferior del arte | ¿El contenido toca el borde derecho? |
|---|---|---|---|
| 16:9 (1920×1080) | −77 | 81 px bajo la pantalla | No (el contenido llega a x = 1521 de 1920) |
| 16:10 (1280×800) | +19 | 81 px bajo la pantalla | No |
| 21:9 (2560×1080) | −397 | 81 px bajo la pantalla | No (1841 de 2560) |
| 4:3 (1024×768) | +163 | 81 px bajo la pantalla | No; el lado izquierdo queda en x = 65, sin recorte |
| 32:9 (3840×1080) | −1037 | 81 px bajo la pantalla | No |

(El contenido opaco de `aimidle` va de x = 354 a 1479 en la escopeta y de 783 a 1253 en el revólver, así que el borde derecho del arte nunca entra con contenido.)

**Para medirlo de verdad:** en Play, Main Camera → *First Person Viewmodel* → marcar **Debug Sight Log**, apuntar quieto en cada formato del Game view (16:9, 16:10, 21:9, 4:3, Free Aspect). Cada 0.5 s sale
`[VM-SIGHT] pantalla WxH (aspecto) · mira=(x,y) centro=(x,y) error=…px`, que debe ser < 2 px. Convierte el punto del arte a pantalla con `RectTransformUtility.WorldToScreenPoint` sobre la ViewmodelCamera, así que mide el dibujo real, no el modelo.
No vale mientras el culatazo está activo.

## 2) La mira nueva
**Forma:** punto central redondo (Ø 10 px con contorno) y 4 gotas (extremo gordo y redondo hacia afuera, punta fina apuntando al centro), relleno blanco y contorno gris casi negro (2 px), antialias de medio píxel de textura (6 texels por píxel).
**Tamaño en reposo:** 2 × (7.6 + 14.4) = **44 px a 1080p** (canvas Overlay con `match = 1`). **Se ve en el HUD, sin grano ni viñeta.**
Las gotas son una cápsula con dos radios generada por código (distancia con signo); comprobé la forma renderizando esa misma matemática aparte (no en Unity).

**Constantes** (arriba de `RubberHoseCrosshair.cs`, en px a 1080p): `DotWhiteRadius` 3.0 · `TickTipRadius` 0.7 · `TickFatRadius` 3.8 · `TickWhiteLength` 10.4 · `OutlineThickness` 2.0 · `OutlineLevel` 22 · `PopStretch` 0.16 · `PopSquash` 0.12.
**Inspector:** `baseRadius` 7.6 · `spreadVisualScale` 0.3 · `movingSpread` 6 · `popAmount` 0.4 · `popDuration` 0.12 · `hitDuration` 0.1 · `fillColor` · `hitColor`.
**Se mantiene todo el comportamiento:** apertura con la dispersión, rebote hacia afuera de un 40 % (ahora con un poco de "squash and stretch": la gota se alarga un 16 % y se adelgaza un 12 %) que vuelve en 0.12 s, rojo 0.1 s al acertar, y oculta al apuntar, en diálogo, en pausa y al morir.

## 3) Onomatopeya: cómo funciona el contador
`ShotgunFeedback` tiene un contador de disparos y un umbral. Al empezar se sortea un umbral entero entre `minShotsBetweenWords` y `maxShotsBetweenWords` (ambos incluidos, 15 y 20 por defecto, serializados).
Cada disparo suma 1. Cuando el contador llega al umbral sale la palabra, el contador vuelve a 0 y se sortea otro umbral. Entre medias no sale nada (la luz del fogonazo y el temblor de cámara no cambiaron).
Palabra al azar de la lista serializada `words` (`¡BANG!`, `¡BLAM!`, `¡POW!`, `¡KABOOM!`), sin repetir la anterior, con giro al azar de ±12° (`popupRotationDegrees`) y el pop de escala que ya tenía. El contador se reinicia al cargar la escena (la primera palabra sale entre el disparo 15 y el 20).
Si `max < min` se usa `min`; si la lista está vacía no sale nada.

## 4) Cambio de la dispersión
`ShotgunController.ShootPellet` (única línea de la escopeta que cambió):

Antes (ejes del mundo):
```csharp
shootDirection += new Vector3(
    Random.Range(-spread, spread) * 0.01f,
    Random.Range(-spread, spread) * 0.01f,
    0f
);
```
Después (ejes de la cámara):
```csharp
shootDirection += playerCamera.transform.right * (Random.Range(-spread, spread) * 0.01f)
                + playerCamera.transform.up * (Random.Range(-spread, spread) * 0.01f);
```
Misma magnitud (`spreadAngle` × multiplicadores × 0.01 por eje). Efecto: el cono queda centrado en la mira mirando hacia cualquier lado; antes se deformaba según hacia dónde mirara el jugador.

**Revólver FX (parte 5).** `fx_casing_revolver.jpg` y `fx_round_rim.jpg` (2752×1536, fondo verde) pasaron a `_entrante_viewmodel/FX/` (fuera de `Assets/`, `.meta` borrados), se convirtieron a PNG y salieron de `viewmodel_key.py FX` a **256×256** centrados:
`fx_casing_revolver_01.png` y `fx_round_rim_01.png`. **El recorte quedó limpio:** 0 píxeles de verde residual, ~800 píxeles de antialias en el borde y sin artefactos de JPEG visibles a 256 px (el contorno negro y los reflejos están intactos). Aún no los usa ningún perfil ni efecto.

## 5) Commits y resultado del push
| Hash | Mensaje |
|---|---|
| fb0ab45 | chore(player): Player prefab with viewmodel, rain and footsteps |
| 231a0be | chore: generated assets from phases 3A–3A8 |
| 06139dd | docs: move viewmodel audits to Docs/audits + 3A7/3A8 audit status |
| a73815a | fix(viewmodel): center the front sight on screen when aiming |
| 502394d | feat(hud): mouse-style teardrop crosshair |
| 0a44ff7 | feat(feedback): show the onomatopoeia only every 15-20 shots |
| bb1f0ef | fix(shotgun): apply pellet spread on the camera's right and up axes |
| 339f2ab | chore(viewmodel): revolver FX PNGs and VM profiles migrated to the 1.3 timing |
| (este commit) | docs: viewmodel phase 1.4 audit |

Push: `origin/player` `fe07c02..339f2ab` (avance rápido, sin force, sin PR).
Notas de los commits de limpieza: el de "generated assets" son 629 archivos (491 nuevos, 138 modificados, ~34 MB; el mayor pesa 11 MB). Además de lo listado incluí tres assets generados del mismo tipo que estaban modificados:
`Enemy_Guard.prefab`, `PersonajePlaceholder.mat` y `TechnicolorVolumeProfile.asset`. No se commiteó `_entrante_viewmodel/`, ni `Tools/__pycache__/` (ahora ignorado), ni `StreamingAssets`.

## 6) Cómo probar en 5 pasos
Antes: abrir Unity (debe salir `[Viewmodel] VM_Shotgun actualizado · aimSightPoint = (960.5, 575)` y lo mismo para `VM_Revolver` con (964.3, 723)) y ejecutar **Crear Prefabs de Prueba**. Commitear los `VM_*.asset` que Unity actualice.
1. **Centrado:** activar **Debug Sight Log** y apuntar quieto en 16:9, 16:10, 21:9, 4:3 y Free Aspect; el error del log debe ser < 2 px y, a ojo, la punta de la mira delantera queda justo en el centro de la pantalla.
2. **Apuntar y soltar** en 21:9: las manos se desplazan al centro sin saltos raros al cambiar de dibujo y vuelven a la esquina. Quieto, sin balanceo ni respiración; al caminar mientras se apunta tampoco se mueve.
3. **Disparar apuntando:** el culatazo la mueve y vuelve al centro exacto (repetir el log tras 1 s sin disparar).
4. **Mira:** gotas con el extremo gordo hacia afuera, ~44 px a 1080p, rebote del 40 % con squash al disparar, rojo al acertar, y oculta al apuntar, en diálogo, pausa y muerte. Disparar 30 veces: la palabra sale una sola vez cada 15–20 disparos.
5. **Dispersión:** disparar la escopeta desde la cadera a una pared mirando hacia el norte, el este y el techo: el patrón de impactos queda con la misma forma en todas las direcciones.

## 7) Riesgos
- **Sin probar en Unity:** el error de centrado de la sección 1 es un cálculo hasta que se use Debug Sight Log; el recorte de las gotas y el contorno se comprobaron solo con una copia de la matemática en Python.
- **Salto al cambiar de dibujo:** el dibujo de apuntar entra al 50 % del apuntado, y la raíz ya se desplazó hacia el centro (hasta ~190 px a medio camino en 21:9). Si se nota un salto en pantallas anchas, hay que suavizar el cambio (por ejemplo, desvanecer entre los dos dibujos) o retrasar el desplazamiento.
- **`VM_*.asset`:** el commit lleva solo la migración de tiempos de la 1.3. La del `aimSightPoint` ocurre al abrir Unity con este código y hay que commitearla.
- **Mira centrada = el arma tapa el centro:** es lo pedido (la punta de la mira en el centro), pero la escopeta cubre parte de lo que hay detrás de esa zona al apuntar.
- **Parámetros sin uso:** con mira centrada, `aimBobMultiplier`, `aimSwayMultiplier` y `aimBreathMultiplier` no se usan (valen 0 al apuntar del todo); solo cuentan en perfiles sin dibujo de apuntar.
- **La dispersión cambió:** es un cambio de jugabilidad aprobado. La forma del cono ahora es simétrica respecto de la cámara; a la misma distancia los impactos pueden caer en otros sitios que antes.
- **Las gotas son indicativas:** abren el 30 % de la dispersión real. Con la dispersión corregida, el punto central sigue siendo lo exacto.
- **Prefab:** `ShotgunFeedback` tiene campos nuevos que el `Player.prefab` commiteado no trae; Unity usa los valores por defecto del código hasta que se regenere el prefab.
- **FX nuevos sin usar:** `fx_round_rim_01` y `fx_casing_revolver_01` existen pero ningún perfil ni script los usa todavía.
