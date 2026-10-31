# Auditoría Viewmodel Fase 1.2 — Esquina, salto al disparar y orden de render

ESTADO: PENDIENTE DE REVISIÓN

Rama: `player` (local, **sin push**). Compila con 0 errores y 0 warnings nuevos (Runtime 59, Editor 0). **No se probó nada en Unity.**
Para activar todo hay que ejecutar **Plomo y Paja → Crear Prefabs de Prueba** (crea la capa `Viewmodel`, la ViewmodelCamera y el stack
en el prefab del Player).

## 1) Causa real del Bug B, con el log como prueba
**No pude medir la causa: sin Unity no hay log.** Lo que sí hice fue revisar las 5 sospechas contra el código y dejar listo el log para que
lo corra David. Lo que sigue está separado en "descartado por el código" y "sin confirmar".

| # | Sospecha | Resultado | Por qué |
|---|---|---|---|
| 1 | Resorte inestable con dt grande | Descartada como causa | El paso ya estaba recortado a 1/30 s. Con Euler semi-implícito y `w = 22`, `w·h = 0.73 < 2` es estable. Igual se cambió a sub-pasos de 1/120 (`w·h = 0.18`) |
| 2 | Grados y radianes mezclados | Descartada | `Quaternion.Euler` recibe grados en todos los casos: `kickRotation` 8°, `runTilt` −6°, giro por inercia = px × 0.06° |
| 3 | Escala negativa o cero | Descartada con los valores del perfil | La escala salía entre 0.9 y 1.1 (squash 1.10/0.90, golpecito 1.03, respiración ±1 %); no puede invertirse. Igual cada eje queda en [0.8, 1.2] |
| 4 | Sprite con otro tamaño, pivote o volteo | Descartada | Los 12 PNG miden 1920×1080 RGBA, todos con el mismo pivote (Center, FullRect) y la herramienta de recorte no voltea nada |
| 5 | Temblor de cámara de `ShotgunFeedback` | **Sospechosa más probable, sin confirmar** | Ver abajo |

**Sospecha 5.** `ShotgunFeedback`, en `beginCameraRendering` de la cámara principal, la inclina hasta 4° y, durante 0.08 s tras cada disparo, la mueve
hasta 0.05 m al azar (`shakeAmount`), y lo deshace en `endCameraRendering`. En la fase 1.1 el canvas de las manos estaba a **0.05 m** de esa
cámara (near 0.03 + 0.02): el temblor es del mismo tamaño que la distancia del canvas. Y la ventana de 0.08 s (unos pocos frames) coincide con
"unos milisegundos y vuelve".
- **Lo que no puedo afirmar:** el canvas era hijo de esa cámara, y no pude comprobar si hereda o no ese movimiento antes de dibujarse.
- **Lo que no encaja del todo:** "las manos aparecen al revés" no lo explica ninguna de las 5 sospechas, así que mi confianza es media.
  Por eso lo marco como sospecha y no como causa probada.

**Qué cambia en la fase 1.2 (aunque la causa no se confirme):**
- La ViewmodelCamera es hermana de la principal, así que no recibe el temblor.
- El canvas está a 1 m, no a 0.05 m.
- El resorte va en sub-pasos y la escala está limitada.
- Un solo reloj para todo: `Time.deltaTime` con escala, recortado a 1/30 s. Así el hit-stop de `CartoonFx` (timeScale 0) congela las manos junto con el mundo,
  y un tirón de FPS no mete un paso enorme. `ViewmodelLighting` pasó de `unscaledDeltaTime` al mismo reloj.

**Log de depuración (apagado por defecto).** En Play: seleccionar la Main Camera → componente *First Person Viewmodel* → marcar **Debug Fire Log**.
Cada disparo escribe durante 0.5 s, por frame:
`[VM-DEBUG] f03 pos=(x,y) rotZ=… scale=(x,y) sprite=… dt=… unscaledDt=… timeScale=…`
y, cuando cada cámara va a renderizar: `[VM-DEBUG] render '<cámara>': principal localPos=… localEuler=… | ViewmodelCamera localPos=…`
(ShotgunFeedback mueve la principal en ese mismo evento y el orden entre ambos no está garantizado: si `localPos` sale distinto de cero
en una línea de `render`, ese es el temblor). **Si el salto vuelve a verse con el flag encendido, pegar el log aquí y se cierra la causa.**

## 2) Archivos tocados
**Creados**
- `Scripts/Player/ViewmodelCameraRig.cs` — arma la ViewmodelCamera, el culling y el stack (lo usan el generador y FirstPersonViewmodel; es idempotente).
- `Scripts/Editor/ViewmodelLayerSetup.cs` — crea la capa `Viewmodel` en TagManager por SerializedObject; menú "Crear Capa Viewmodel".

**Modificados**
- `Scripts/Player/FirstPersonViewmodel.cs` — canvas en Screen Space - Camera sobre la ViewmodelCamera (planeDistance 1, capa Viewmodel en todo), CanvasScaler `match = 1`,
  HandsImage con ancla y pivote (1,0) y `overscan` (1.08), sin el near clip ni el ZTest Always, y el log de depuración.
- `Scripts/Player/ViewmodelMotion.cs` — dt recortado, resorte en sub-pasos de 1/120, escala limitada a [0.8, 1.2] y `KeepEdgesOffscreen` (ver Bug A).
- `Scripts/Player/ViewmodelLighting.cs` — tinte base por defecto (0.95, 0.92, 0.88) y reloj único.
- `Scripts/Player/PlayerAim.cs` — elige la cámara del mundo (salta la ViewmodelCamera) para el zoom del FOV.
- `Scripts/Editor/TestPrefabGenerator.cs` — crea la capa y llama a `ViewmodelCameraRig.Ensure(cam)` al armar el Player.

**Bug A (esquina).** El tamaño sale del alto de la pantalla (`CanvasScaler match = 1`) × `overscan`; siempre 16:9 como el arte. HandsImage tiene ancla y pivote abajo a
la derecha y se pasa de la pantalla por la derecha (153.6 px) y por abajo (86.4 px) en un canvas de 1080 de alto. En pantallas más anchas queda pegado a
la derecha (a la izquierda no hay arte, es transparente); en las más angostas se recorta por la izquierda. Después de sumar balanceo, inercia, culatazo, rotación y
escala, `KeepEdgesOffscreen` calcula dónde quedan 3 esquinas del dibujo y mueve la posición lo justo para que el borde derecho y el inferior nunca entren
en pantalla. Con el culatazo a 8° eso desplaza las manos ~60 px más hacia abajo de lo que pide el perfil. Al apuntar sin dibujo de apuntar (`aimOffsetNoSprite`) el
desplazamiento a la izquierda también queda recortado por esta regla.

**Bug C (orden de render).** Ver sección 3.

## 3) Cómo quedó el stack de cámaras
```
Player
└── CameraHolder
    ├── Main Camera        Base · culling mask SIN la capa "Viewmodel" · tag MainCamera · zoom de apuntar (solo esta cámara)
    │   ├── FirstPersonViewmodel, ShotgunController/Feedback…
    │   └── ViewmodelCanvas (capa Viewmodel, Screen Space - Camera → ViewmodelCamera, planeDistance 1)
    └── ViewmodelCamera    Overlay · culling mask = solo "Viewmodel" · FOV fijo 60 · near 0.1 / far 10 · Post Processing ON · sin tag
Main Camera → Camera Stack: [ViewmodelCamera]
HUD: canvases Screen Space - Overlay (sin tocar)
```
- La capa `Viewmodel` va en el primer hueco libre desde la 8 (hoy el índice 8). Se crea por SerializedObject al ejecutar "Crear Prefabs de Prueba" (o el menú
  "Crear Capa Viewmodel"); eso modifica `ProjectSettings/TagManager.asset`, que hay que commitear junto al prefab.
- **Desviación del pedido:** la ViewmodelCamera es **hermana** de la cámara principal (hija del mismo `CameraHolder`), no hija de ella. Como solo ve la capa
  Viewmodel su posición en el mundo no importa, y así no hereda el temblor y la inclinación de `ShotgunFeedback`. Misma pose local que la principal.
- Se eliminó el `nearClipPlane` bajado en runtime y el material con ZTest Always. La cámara principal conserva el near clip del prefab (0.3).
- **Post-procesado:** en URP el post-procesado de un stack lo aplica la **última** cámara (la ViewmodelCamera, con Post Processing ON) sobre mundo + manos juntos, con el
  volumen del juego (grano, viñeta, color, bloom). El HUD en Overlay se dibuja después y no lo recibe. *Esto sale de la documentación y del código de URP 14,
  no de verlo en pantalla; es el paso 3 de la prueba.*
- **Zoom de apuntar:** `PlayerAim` solo cambia el FOV de la cámara principal; la ViewmodelCamera tiene FOV fijo, así que las manos no cambian de tamaño.
- **Sin la capa:** si falta (prefab sin regenerar), FirstPersonViewmodel avisa y dibuja las manos en Overlay (sin post-procesado), pero siguen viéndose.
- El prefab queda con todo guardado; el Player de una escena que sea instancia del prefab lo hereda al regenerar. Además `FirstPersonViewmodel` lo arma en runtime si falta
  (en ese caso el culling de la principal se cambia solo en memoria).

## 4) Cómo probar en 5 pasos
Antes: **Crear Prefabs de Prueba**. En consola debe salir `[Viewmodel] Capa 'Viewmodel' creada…` (solo la primera vez), sin errores.
1. **5 formatos de pantalla** (Game view: 16:9, 16:10, 21:9, 4:3 y Free Aspect arrastrando el borde). Con la escopeta quieta, caminando y disparando: las manos
   quedan pegadas a la esquina inferior derecha, sin franja de mundo a la derecha de la manga ni borde de arte abajo. En 4:3 se recorta por la izquierda.
2. **Disparar 10 veces seguidas** (con y sin apuntar). Las manos no saltan ni se invierten. Si pasa, activar **Debug Fire Log** (sección 1) y pegar el log.
3. **Pasillo con lluvia** (y cerca de un cono de luz o de sangre): nada del mundo se dibuja encima de las manos y no se ven lavadas. Las manos tienen el mismo grano,
   viñeta y color que el mundo; el HUD (munición, vida, mira) no.
4. **Pegado a una pared** y en una esquina: la pared no tapa las manos ni el fogonazo.
5. **Apuntar** (clic derecho): el FOV baja 15° y las manos no cambian de tamaño; al soltar vuelven. Disparar apuntando también.

## 5) Riesgos
- **Causa del Bug B sin confirmar** (sección 1). Si tras esta fase el salto sigue, hace falta el log.
- **SSAO se ejecuta también para la ViewmodelCamera.** El feature de SSAO de URP 14 no distingue cámaras Overlay: añade un prepass de profundidad/normales y el
  paso de AO sobre una cámara que no ve nada de geometría. Es un costo extra de pantalla completa en GPUs débiles (PC de Edgar, presentación). Si se nota, la salida es
  un segundo renderer sin SSAO para la ViewmodelCamera (`SetRenderer(i)`), que no hice por ser un asset nuevo.
- **`TagManager.asset` cambia** (capa nueva en el 8). Hay que commitearlo junto con el prefab; si alguien más añade capas en el mismo hueco habrá que resolver el conflicto a mano.
- **El prefab del Player cambia** (hijo ViewmodelCamera, culling mask de la principal, stack): `Player.prefab` tiene cambios locales de David sin commitear.
- **Overscan y bordes:** el recorte de bordes tiene prioridad sobre el movimiento. En el culatazo y al apuntar sin dibujo de apuntar, las manos se mueven menos de lo que pide el perfil.
  Si se siente poco, subir `overscan` (hasta 1.3) en el inspector.
- **El pivote de ViewmodelRoot (0.65, 0.1) está lejos de la esquina**, y una rotación de 8° mueve la esquina ~100 px: es lo que hace trabajar al recorte de bordes. Si molesta, mover el pivote hacia la esquina.
- **FitHands corre en LateUpdate y el recorte de bordes lee ese tamaño en Update:** al cambiar el tamaño de la ventana hay un frame de desfase (imperceptible).
- **Fallback sin capa:** vuelve a Overlay, donde el mundo no tapa las manos pero tampoco hay post-procesado.
- **La ViewmodelCamera ya no hereda el near clip:** si algún día se vuelve a bajar el near de la principal por otra razón, no afecta a las manos (near 0.1, plano a 1 m).
- **Lo que se mantiene de la fase 1.1:** importación sin compresión, revólver, `IWeapon`, `ViewmodelLighting` (solo cambia el tinte base).
