# AUDIT contador 3C.1: patio trasero y cuarto de herramientas

Fase: `Docs/phases/Fase-3C-Contador.md` (en el repo se llama así, no `PHASE-3C-contador.md`), subfase 3C.1. Guion v1.2 (`Docs/guiones/contador.md`) y auditoría 3C.0 leídos.
Rama: `integracion/dia4`, creada desde `player` @ `66a4dfb` (ya incluye `integracion/dia3`).

> **Importante:** Unity no se abrió en esta sesión. El código compila (ver el final), pero el menú **no se ejecutó**: el prefab, las mallas, la escena y el NavMesh todavía no existen. Todo lo que dice "comprobado" se refiere a la geometría y al código; la prueba real la hace el menú y la tiene que leer David.

## Menús que David debe correr (en orden)
1. Abrir Unity, dejar que recompile y confirmar que la consola no tiene errores rojos.
2. **Plomo y Paja → Contador → Construir patio y cuarto.** Si no está abierta `Assets/Scenes/NivelPorky.unity`, la abre (antes pregunta si guardar la escena actual). Al terminar muestra un cuadro con las comprobaciones (✓/✗), que también quedan en la consola.
3. **Ctrl+S** para guardar la escena (el menú no la guarda).
4. Hacer commit de lo que generó el menú: `Assets/_Game/Prefabs/Level1/` (prefab y carpeta con su `.meta`), las mallas nuevas en `Assets/_Game/Art/Environment/Generated/` (`Cuarto_Contador_*`, `Porton_Contador_Hoja`, `Cerca_Valla_Cuarto_*`, `Tablas_Piso_Cuarto_*`, `RBox_*` nuevas), `NivelPorky.unity` y `Scenes/NivelPorky/NavMesh.asset`.

Se puede correr las veces que haga falta: no duplica nada. **Plomo y Paja → Generar Nivel Porky** también construye el cuarto, así que regenerar el nivel no lo borra.

## Prefab creado y dónde quedó en la escena
- `Assets/_Game/Prefabs/Level1/ToolShed_Contador.prefab`, raíz con el componente `ContadorShed`.
- Instancia `ToolShed_Contador` bajo `--- ENVIRONMENT ---`, en **(-7, 0, 19)** con rotación Y = **180°**: esquina del fondo a la izquierda del patio trasero. Ocupa x -9..-5, z 17.5..20.5, con la puerta en z = 17.5 mirando a -Z, hacia el portón (4 m) y la puerta trasera de la casa.
- Contenido:
  - Piso de tablas a 0.22 m sobre un zócalo oscuro (tapa el terreno, que sube y baja ±0.16 m).
  - Paredes de tablas verticales con ±3° de inclinación, semilla fija `Rng("Cuarto_Contador_Paredes")` (determinista), un poco abombadas.
  - Techo de una sola agua (2.75 m adelante, 2.15 m atrás), combado y con dos parches de lata.
  - Puerta de 1.5 × 2.1 m con dintel chueco y un escalón suelto.
  - Colisiones con cajas invisibles sin rendijas (paredes, dintel y techo; el techo también tapa la lluvia).
  - **Banco de trabajo** de 1.9 × 1.0 × 0.62 m con `BoxCollider` sólido (la cobertura), frente tapado con tablas, morsa y lata de clavos.
  - Herramientas colgadas en la pared del fondo (serrucho, martillo, llave, pala, rastrillo, soga).
  - 3 barriles y 1 caja: un barril y la caja cierran los lados del banco, así no se puede pasar por detrás.
  - **Lámpara de aceite** sobre el banco: Point Light naranja (1, 0.58, 0.24), intensidad 2.4, rango 6 m, sombras suaves, con `LightFlicker` suave (tiembla, nunca se apaga).
- **Zona de activación:** `Trigger_Activacion`, `SphereCollider` trigger de radio **12 m** centrada en la puerta, en la capa *Ignore Raycast*. Así no la tocan el raycast de la escopeta (máscara por defecto) ni la vista de los guardias (solo capa Default). Su gizmo es naranja.
- Escena (grupo `--- ENVIRONMENT ---/Patio_Contador`, se rehace en cada corrida):
  - Cerca de frente en z = 13.5 (x -10.1..-3.5) con el **portón** (x -8..-6, 2 m).
  - Cerca lateral en x = -3.6 (z 13.4..21.1).
  - Dos postes gruesos y una viga chueca a 2.3 m.
  - Hoja del portón abierta hacia adentro, decorativa: sin collider y fuera del NavMesh.
- Luz (grupo `--- LIGHTING ---`): `Luz_Luna_Patio_Contador`, Spot frío (#9EBDFF), intensidad 8, rango 16, ángulo 80°, sombras suaves, desde (-4.5, 6.5, 11) hacia el portón y la fachada del cuarto. El farol que ya existía (`Farol_Patio`, en -8.8, 15) quedó dentro del rincón e ilumina el frente del cuarto. **No se tocó el post-procesado global.**
- Accesorios corridos porque quedaban dentro del cuarto:
  - `Fardo_Patio`: (-6.5, 0, 19.6) a 85° → **(-4.3, 0, 19.4)** a -5°.
  - `Escombro_09`: (-8, 18) → **(-9.35, 16.8)**.

## Anclas creadas (nombre + posición)
Son marcadores `LevelMarker` del tipo nuevo `Anchor` (gizmo verde con etiqueta). `ContadorShed` las referencia y dibuja la zona, el camino y el encuadre.

| Ancla | Local (prefab) | Mundo | Orientación |
|---|---|---|---|
| `Anchor_CentavoStand` | (0, 0.22, -0.35) | (-7, 0.22, 19.35) | mira a la puerta (-Z mundo), 0.45 m detrás del banco |
| `Anchor_CentavoSeated` | (0, 0.22, 1.00) | (-7, 0.22, 18.00) | en el piso, apoyado en el frente del banco, mira a la puerta |
| `Anchor_InterrogationCam` | (0, 0.90, 2.50) | (-7, 0.90, 16.50) | 1.5 m delante de Centavo sentado, a 0.9 m de alto, mira 0.55 m sobre el ancla (≈5° hacia abajo) |
| `Exit_Path_0` | (0, 0, 2.7) | (-7, 0, 16.3) | delante de la puerta |
| `Exit_Path_1` | (0, 0, 6.6) | (-7, 0, 12.4) | ya pasó el portón |
| `Exit_Path_2` | (2, 0, 10.4) | (-9, 0, 8.6) | patio, hacia el pasillo izquierdo |
| `Exit_Path_3` | (2, 0, 22) | (-9, 0, -3) | pasillo izquierdo, detrás de la casa (fuera de la vista desde el patio) |
| `Trigger_Activacion` | (0, 1, 1.5) | (-7, 1, 17.5) | esfera de 12 m |

## Cambios a la ruta del patrullero y al centinela
- **Patrullero** (`Guard_Patrulla_A`, marcadores `Patrol_A_*`):
  - Antes: 4 puntos alrededor de la casa: (-9,-8) → (9,-8) → (9,9) → (-9,9).
  - Ahora: 6 puntos: (-9,-8) → (9,-8) → (9,9) → **(-7, 11.2)** delante del portón → **(-5, 15.8)** dentro del rincón, delante del cuarto → (-9, 9), saliendo otra vez por el portón.
  - Cruza el portón dos veces por vuelta. El menú reubica `Patrol_A_0..3`, crea `_4` y `_5` y reasigna el `patrolPoints` del patrullero.
- **Centinela** (`Guard_Centinela`, marcador `Sentinel_PatioTrasero`): de (-4, 0, 13) → **(-5.4, 0, 12.4)**, 1.4 m a la derecha del portón, de espaldas a él y mirando a la puerta trasera (180°).
- La ruta y el centinela viven en `ContadorYardBuilder` (`PatrolRoute`, `SentinelPosition`), y `LevelBuilder_NivelPorky.BuildMarkers` usa esas mismas constantes.

## ¿Se puede llegar al cuarto sin pasar por el portón? (sí/no + cómo lo comprobaste)
**No.**
- **Por geometría (comprobado en el código):** el rincón queda cerrado por:
  - `Valla_Izquierda` (x = -10);
  - `Valla_Fondo` (z = 21): su único hueco está en x -2..2, que queda fuera del rincón;
  - `Valla_Cuarto_Frente` (z = 13.5): su único hueco es el portón;
  - `Valla_Cuarto_Lado` (x = -3.6).

  Las cajas de colisión de las cercas se solapan 0.1 m en cada unión y bajan 0.18 m bajo el suelo, así que no queda rendija abajo aunque el terreno ondule. Las cercas miden 1.6 m y el jugador no salta (`jumpForce = 0`).
- **Por NavMesh (lo hace el menú, no se ejecutó aquí):**
  1. Tapa el portón con un cubo temporal, hornea y comprueba que **no** hay camino desde la puerta trasera hasta el frente del cuarto.
  2. Quita la tapa, vuelve a hornear y comprueba que desde la puerta trasera, el callejón, el pasillo derecho y la calle el camino **cruza la cerca solo por el portón**, que los 6 tramos de la patrulla son transitables y que el centinela queda sobre el NavMesh.

  David debe leer el cuadro del menú: todas las líneas tienen que salir con ✓.

## NavMesh: ¿rebake hecho? (pasos si es manual)
- El menú hornea solo, dos veces (con y sin la tapa), con el horneador integrado `UnityEditor.AI.NavMeshBuilder.BuildNavMesh()`, igual que *Generar Nivel Porky*. Después hay que guardar la escena (Ctrl+S).
- **No se ejecutó en esta sesión** (no hay Editor).
- Si fallara, a mano: *Window → AI → Navigation (Obsolete) → Bake → Bake* con la escena NivelPorky abierta, y luego Ctrl+S. El radio del agente debería ser 0.5 o menos (el menú avisa si es mayor).

## registro_01: ¿dónde estaba antes y qué se quitó?
- **Antes:** la única fuente en el juego era `DialogueDatabase.BuildContable()`, el guardia `guardia_registro` "Contable" del **nivel 2**: su nodo final `fin_ayuda` entregaba `registro_01` con su texto de pista.
- **Se quitó:** la entrega de la pista. El final sigue siendo *Helpful*, así que sigue contando como interrogatorio para el Final C. También se quitó "Llévese la hoja." de su línea, para que no prometa una hoja que ya no da.
- **Se revisó y no había otras fuentes:**
  - Ningún `CluePickup` con `registro_01` en escenas, prefabs ni assets (búsqueda en `.unity`, `.prefab`, `.asset` y `.json`).
  - Colmillo (nivel 1) nunca la dio.
  - `FinaleTrigger`, `GameStateTracker` y `AICompanion` no la nombran.
- **Documentos actualizados** para que la Fase 4 no la vuelva a poner: `Docs/phases/PHASE-4.md` (3 lugares), `Docs/GDD.md` (tabla de pistas) y el comentario de `CluePickup.cs`.
- **Pregunta para el orquestador:** el Contable dice "Faltan 4.300 billetes" y Centavo dice "Tres mil doscientos pesos". Son dos cifras distintas para la misma deuda. No toqué la cifra (es contenido del nivel 2, que todavía no está construido).

## Clases existentes reutilizadas (y por qué)
- `LevelBuilder_NivelPorky`: plano y coordenadas del patio. Su `Generate` ahora llama a `ContadorYardBuilder.BuildInto`, y `BuildMarkers`/props usan sus constantes, para que regenerar el nivel no pierda el cuarto. `ScenePath` pasó de `private` a `public const`.
- `CartoonKit`: `Box`, `Fence` (con `WallOpening` para el portón), `PlankFloor`, `Barrel`, `Crate`, `GetMesh`/`KitMesh`/`AddBox`/`AddLathe`, la paleta `M()` (materiales `Kit_*` del proyecto, sin crear nuevos), `Rng` con semilla fija, `ExcludeFromNavMesh` y `MeshObject`. Es el mismo estilo rubber-hose del resto del nivel.
- `LevelBuilderUtils`: `CreateGroup`, `CreateMarker`, `CreatePointLight`, `MarkStaticRecursive`, `EnsureFolder`.
- `LevelMarker`: gizmos con etiqueta para las anclas. Se agregó `MarkerType.Anchor` al final del enum, y `BuildActors` lo ignora.
- `LightFlicker`: el parpadeo de la lámpara, configurado suave.
- `EnemyAI`: `patrolPoints`/`enemyType` por `SerializedObject`, igual que `BuildActors`.
- `EditorMenuGuard`: bloquea el menú en Play.
- **Nuevo:** `ContadorShed`. No había ningún componente que guarde anclas de escena; lo usarán 3C.2 y 3C.3. Está en `Scripts/Characters/Contador/`. El constructor está en `Scripts/Editor/` y no en `Characters/Contador/` porque esa es la carpeta de herramientas de editor que fija CLAUDE.md.

## Decisiones tomadas
- **Rincón izquierdo:** queda al lado de la puerta trasera, es donde menos accesorios estorbaban (2 contra 5 del lado derecho) y el `Farol_Patio` que ya existía ilumina el frente del cuarto.
- **Qué va en el prefab:** el prefab es solo el cuarto. La cerca, el portón y la luz de luna son objetos de escena generados. `Exit_Path` va dentro del prefab con coordenadas locales calculadas desde la ubicación fija; si alguien mueve el cuarto a mano, el camino se mueve con él y deja de pasar por el portón (hay que volver a correr el menú).
- **Instancia existente:** si ya hay una instancia del prefab, el menú la conserva (y sus overrides futuros de 3C.2) y solo le reajusta la posición. Las copias duplicadas o desempaquetadas se borran.
- **Ruta del patrullero:** entra al rincón en cada vuelta (`Patrol_A_4`), que es la forma más simple de que "cruce el portón".
- **Herramientas:** quedaron fuera del NavMesh y no proyectan sombra.
- **Commits:** los archivos de la 3C.0 (sprites, voces, importadores, `AudioLibrary.asset`, `.gitignore`) siguen **sin commit**, como los dejó esa subfase. Este commit solo lleva archivos de 3C.1.

## Qué no se pudo compilar/probar y qué debe revisar David en el Editor
- **Compilado:** sí. Roslyn de Unity 2022.3.62f3 contra `Library/ScriptAssemblies` y `Managed/UnityEngine`: runtime 0 errores, editor 0 errores. Los únicos warnings nuevos son CS0649 en los `[SerializeField]` de `ContadorShed` (los asigna el menú; el resto del proyecto tiene el mismo aviso).
- **No probado:** el menú, el prefab, la escena, el NavMesh y nada en Play.
- **APIs dudosas:**
  - `NavMesh.CalculatePath`/`SamplePosition` en modo edición justo después de `BuildNavMesh` (debería funcionar). Si el cuadro dice "no hay NavMesh", revisar el horneado a mano.
  - `SaveAsPrefabAsset` sobre un prefab existente conserva el GUID, pero cambia los IDs de los hijos: los overrides en hijos de la instancia se perderían al regenerar (hoy no hay ninguno).
- **Revisar en el Editor:**
  1. El cuadro del menú: todas las líneas en ✓.
  2. Vista del cuarto: tablas torcidas sin parpadeo de caras, el techo cubre bien, las herramientas no flotan demasiado lejos de la pared. El escalón de la puerta puede quedar hundido o flotando hasta 16 cm según el terreno.
  3. Cercas y postes nuevos: que no se vean flotando (están enterrados 18 cm) y que la hoja del portón quede abierta contra el rincón sin tapar el paso.
  4. En Play:
     - el patrullero cruza el portón, entra al rincón y sale;
     - el centinela está al lado del portón mirando a la puerta trasera;
     - no se puede entrar al rincón sin pasar por el portón;
     - la escopeta dispara normal dentro de la esfera de 12 m (no choca con el trigger);
     - debajo del techo del cuarto no llueve.
  5. Luz:
     - la lámpara parpadea suave y su luz sale por la puerta;
     - el acento de luna hace legible el portón (ajustar la intensidad de `Luz_Luna_Patio_Contador`, hoy 8);
     - si alguna luz del patio se apaga al acercarse, es el límite de luces por objeto de URP: subir "Per Object Limit" o quitarle las sombras al acento.
  6. Que *Generar Nivel Porky* siga funcionando (ahora incluye el cuarto) y no muestre el aviso "Patio del contador" en consola.

## Archivos creados/modificados
Creados:
- `PLOMO_Y_PAJAV_1/Assets/_Game/Scripts/Editor/ContadorYardBuilder.cs` (+ `.meta`): menú, prefab, patio, ruta, horneado y comprobaciones.
- `PLOMO_Y_PAJAV_1/Assets/_Game/Scripts/Characters/Contador/ContadorShed.cs` (+ `.meta`, y `.meta` de las carpetas `Characters` y `Characters/Contador`): anclas y gizmos del cuarto.
- `Docs/audits/AUDIT_contador_3C1.md` (este archivo).

Modificados:
- `Scripts/Editor/LevelBuilder_NivelPorky.cs`: `ScenePath` público, ruta y centinela nuevos, fardo y escombro corridos, construye el cuarto y valida al generar.
- `Scripts/Level/LevelMarker.cs`: `MarkerType.Anchor` y su color.
- `Scripts/Dialogue/DialogueDatabase.cs`: el Contable ya no da `registro_01`.
- `Scripts/Clues/CluePickup.cs`: comentario de las pistas planeadas.
- `Docs/phases/PHASE-4.md` y `Docs/GDD.md`: `registro_01` solo sale de Centavo.

## Commits
- `68b8b53`: dia4(david): 3C.1 patio trasero con el cuarto de herramientas de Centavo
- Commit siguiente: esta auditoría.
