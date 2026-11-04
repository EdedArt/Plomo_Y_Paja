# AUDIT N1.1 — Kit de bordes + Nivel 1 más grande

**Fecha:** 3 nov 2026 · **Rama:** `worktree-n1-1` (**todavía no está en `integracion/dia4`**, ver el paso 0) · **Sesión A** · **Alcance:** solo N1.1 (no se tocó N1.2, N1.3 ni N1.4).

> **Nada de esto se probó en Unity.** No hay Editor en este entorno: se compiló con Roslyn y se revisó el plano con una simulación propia (ver "Comprobaciones"). Los prefabs, las texturas, la escena y el NavMesh **todavía no existen**: los crean los menús de abajo cuando David los corra.

## Menús que David debe correr (en orden)
0. **Traer los commits a `integracion/dia4`**, desde el checkout principal: `git status` (que no haya cambios ajenos sin commit en los 4 archivos modificados de abajo) y luego `git cherry-pick 97f46c6..worktree-n1-1`. Sin este paso, Unity no ve nada de N1.1.
1. **Abrir Unity** y esperar la importación. Consola sin `error CS`. Aparecen los `.meta` de los 7 scripts nuevos y de `Level/Boundary/`.
2. **Plomo y Paja → Generar Nivel Porky** (recomendado). Ya corre el kit y el mapa, hornea el NavMesh y valida.
   - Otra opción, si no quieres regenerar: **Plomo y Paja → Nivel 1 → Crear kit de bordes** y luego **Plomo y Paja → Nivel 1 → Ampliar mapa**. Este quita lo viejo de la escena abierta, rehace terreno, huellas y cercas del rincón, hornea, valida y guarda.
3. **Leer la consola:** `[LevelBuilder] Mapa N1.1` (o `[Nivel 1] Ampliar mapa`) debe decir `✓ Mapa: 10 zonas alcanzables y 9 puntos del otro lado de los bordes sin camino`, sin ningún `✗`. También `[LevelBuilder] Patio del contador` no debe aparecer como aviso.
4. **Commit de lo generado** (por ruta explícita): `.meta` nuevos, `Prefabs/Level/Boundary/`, `Art/Environment/fondos/`, `Art/Environment/Generated/` (mallas nuevas), `Materials/Greybox/` (`Fondo_*`), `Scenes/NivelPorky.unity` + su `NavMesh.asset`.

## Bordes invisibles o en blanco que había antes y cómo se cerró cada uno
No había colliders sin nada visible (las cajas de `CartoonKit.Fence` coinciden con la cerca dibujada). El problema eran **paredes en blanco** y **fachadas continuas sin nada detrás**:

| Antes | Problema | Ahora |
|---|---|---|
| `Calle_Muro_Izq` (x -20.1, ladrillo liso, 3 m) | Fin del mundo en blanco, oeste | Quitado. La calle sigue a x -44 y termina en la **barricada de Don Lobo**; detrás se ve seguir la calle con faroles |
| `Calle_Muro_Der` (x 20.1, ladrillo liso, 3 m) | Igual, este | Quitado. La calle sigue a x 44 y termina en un **auto chocado + escombros + farol roto**; detrás sigue la calle |
| `Callejon_Muro_Izq/Der` (ladrillo liso, 4 m) | Pasillo sin nada que ver | **Callejón del fondo eliminado** (x -2..2, z 21..33) |
| `Callejon_Muro_Fondo` (revoque liso) | Pared en blanco en la salida | Eliminado. La salida pasa al final del sendero del bosque |
| `Edificios_Enfrente` (x -20..20, sin huecos, plana) | Fachada corrida sin profundidad | Partida en 3 tramos con **2 huecos cerrados con reja de hierro** que dejan ver la calle de atrás; cada puerta gana escalón y alero |
| `Calle_Borde_Izq/Der` (cerca de 1.6 m hasta x ±20) | Tapaba, sin nada detrás | Patios de vecinos con cerca baja de 1.1 m y entradas, el callejón lateral y el lote |
| `Valla_Fondo` (z 21 con hueco al callejón) | Daba al callejón eliminado | Sin hueco, solo de x -2.9 a 10; detrás, el borde del bosque. Las vallas laterales de 1.6 m siguen (ojos a 1.7 m: se ve por encima) |

## ¿Hay salto? stepOffset y altura usada en los bordes
- **No hay salto:** `jumpForce = 0` en `PlayerController` (y no se tocó). `stepOffset = 0.3`, alto 2, radio 0.5 (`Player.prefab`), ojos a 1.7 m.
- **N3 pide ≥ 1.0 m** (máximo entre 1.0 y 0.3 + 0.4). El kit avisa con `✗` si alguna pieza queda más baja.
- Alturas de diseño: picket 1.12 · hierro 1.6 · seto ~1.05 · barricada ~1.28 · autos ~2 · escombros ~1.13 · cajas 1.6 · baranda 1.1 · árboles: la caja llega a 3.5 m · portón 2.2. Las alturas exactas salen de los bounds de las mallas y las imprime el menú.
- **Porches:** piso de 0.24 m, se suben caminando. Cada pieza es una caja, baja hasta -0.3 m (sin rendija) y las balas chocan con ella (capa Default).

## Kit de bordes: piezas creadas y cómo se mezcla
Menú **Plomo y Paja → Nivel 1 → Crear kit de bordes** → `Prefabs/Level/Boundary/`: `Fence_Picket`, `Fence_PicketBroken`, `Fence_Iron`, `Hedge_Low`, `Barricade_Lobo`, `Car_Parked_30s`, `Car_Crashed_30s`, `Rubble_Pile`, `Crate_Stack`, `Ditch_Railing`, `TreeLine_Segment`, `Lamp_Post`, `Gate_Bosque`, `Backdrop_Card`.

- **`BorderSegment`:** en la raíz de cada pieza; una caja que abraza la forma; estático y área **Not Walkable**. Excepción: `Backdrop_Card` no lo lleva (N4: sin collider).
- **`BorderPath` + `BorderPathFiller`:** el borde es una polilínea con semilla. Elige piezas al azar y no repite la misma más de 8 m (N5). Las estira un poco para cubrir el tramo exacto y las solapa 0.1 m.
- **Autos:** reusan la carrocería del taxi; extraje `IntroTaxiBuilder.AddCarBody` (el taxi conserva el mismo orden de números al azar).
- **`Gate_Bosque` queda listo para N1.4:** `Hoja_Izq` y `Hoja_Der` con pivote en la bisagra, `Cadena`, `Candado`; se abre apagando la caja de la raíz (los postes tienen la suya).
- **Una sola excepción a N5, y es corta:** las puntas usan orden fijo. La barricada oeste son 2 piezas iguales (7.2 m en total, dentro de los 8 m).

**Clases existentes reutilizadas:** `CartoonKit` (mallas, materiales, `Fence`, `Facades`, `StreetLamp`, `WallLamp`, `WindowFrame`, `DoorLeaf`, `Cobblestones`, `DirtPath`), `LevelBuilderUtils` (grupos, estáticos, marcadores), `IntroTaxiBuilder` (carrocería), shader `PlomoYPaja/BackdropCard` y `NightSkyBuilder.Horizon` (tarjetas), `LightFlicker` (farol roto), `LevelMarker` (anclas), `ContadorYardBuilder.ValidatePaths` y `ValidateExtraGuards` (comprobaciones). **Ubicación:** `Scripts/Level/Boundary/` y `Scripts/Editor/`, como pide la fase.

## Mapa ASCII antes / después
N arriba, 1 carácter = 2 m, aproximado.

**Leyenda:** `P` acera/inicio · `T` parada del taxi · `K` camión · `H` casa de Porky (`d` puerta, `b` boquete) · `S` cuarto de Centavo · `G` guardia/Centavo · `o` objeto (distribución actual de HUD.3; **N1.2 la rehace**) · `.` caminable · `E` salida.
**Bordes:** `#` pared en blanco · `F` fachada · `-` `|` cerca de 1.6 m · `~` mezcla baja (picket, roto, seto) · `_` entrada · `m` mezcla (hierro, seto, auto, cajas, escombros, baranda) · `i` reja de hierro · `B` barricada · `X` auto chocado + escombros · `t` árboles · `D` `Gate_Bosque` · `V` casa de vecino · `p` porche con mesa · `c` cartel vacío · `:` sendero.

**Antes** (≈ 1068 m²: calle 400 + patios y casa 620 + callejón 48)
```
  36 
  34            ###
  32            #E#
  30            #.#
  28            #.#
  26            #.#
  24            #.#
  22            #.#
  20        -GSS#.#----
  18        |ooS......|
  16        |..|......|
  14        ---|......|
  12        |oG.......|
  10        |o........|
   8        |........G|
   6        |HHdHGHHHH|
   4        |oHHHHHHGH|
   2        |HHHHHHHHH|
   0        |HHHHGHHHH|
  -2        |HHHHHHHHH|
  -4        |HHHHHHHGH|
  -6        |HHdHHHbHH|
  -8        |G........|
 -10   ---P-|.........|-----
 -12   #..T..........KKK..G#
 -14   #...................#
 -16   #...................#
 -18   #...................#
 -20   #FFFFFFFFFFFFFFFFFFF#
 -22   FFFFFFFFFFFFFFFFFFFFF
       |         |         |
       -20       0         20
```

**Después** (≈ 1816 m², **+70 %**, medido igual; sin contar el sendero, que está cerrado)
```
  58                     tttt
  56                     t Et
  54                     t :t
  52                     t :t
  50                     t :t
  48                     tt:t
  46                      t:t
  44                      t:t
  42                      t: t
  40                      t:tt
  38                      t:t
  36                     tt:t
  34                     t::t
  32                     t: t
  30                     t::t
  28                     t :t
  26                     tDDD-
  24                     |...|
  22                     |...|
  20                     SSG.|------
  18                     Soo.|.....|
  16                     |...|.....|
  14                     ----|.....|
  12                     |oG.......|
  10                     |o........|
   8                     |.........G
   6                     |HHdGHHHHH|
   4     VVVVVVVVVVVVVVii|oHHHHHHGH| VVVVVVVV
   2     VVVVVVVVVVVVVV~.|HHHHHHHHH| VVVVVVVV
   0     VVVVVVVVVVVVVV~.|HHHHGHHHH| VVVVVVVVmmmmmmmm
  -2     VVVVVVVVVVVVVV~.|HHHHHHHHH| VVVVVVVV~ccccc.m
  -4    ~VVVVVV~VVVVVVV~.|HHHHHHHGH| ~VVVVVVV~......m
  -6    ~......~.......~.|HdHHHHbHH| ~...p...~......m
  -8    ~......~.......~.G.........| ~.......~......m
 -10    ~~~_~~~~~~~_~~~P.|.........|~~~~~_~~~~~___~~m
 -12    B..............T..........KKKK.G............X
 -14    B...........................................X
 -16    Bmmmmmmmmmmmm...................mmmmmmmmmmmmX
 -18                m...................m
 -20                mFFFFFiiiFFFFiiiFFFFm
 -22                FFFFFFFFFFFFFF FFFFFF
          |         |         |         |         |
          -40       -20       0         20        40
```

**+70 % queda por encima del 40–60 % de N6.** El pedido se contradice: +30 m de calle a cada lado (10 m de ancho) ya son +56 % por sí solos, antes de vecinos, callejón y lote. Para acercarlo dejé la calle en **+24 m por lado** (x ±44) y corrí el borde sur a z = -17 en las extensiones. Si se quiere menos, lo más fácil es achicar el lote (`Suelo_Lote` + sus caminos).

## Vista: capas, distancias, ¿fondos generados o arte de Edgar? ¿se ven las casas de Wilbur y Max?
- **Cerca (10–80 m):** `Vista_Casas` (hileras de casas, ventanas emisivas, 2 tanques de agua, faroles solo emisivos, calles que siguen) y `Vista_Bosque` (árboles). Son **dos mallas únicas**: sin collider, sin luz real, sin sombras, solo Batching Static, fuera del NavMesh.
- **Lejos:** tarjetas en `--- VISTA ---/Fondos`, a 86–106 m del centro, antes del anillo que ya tenía `NightSkyBuilder` (92/122/150 m), que se mantiene. La cúpula mide 190 m, así que no tiene sentido llegar a 300 m.
- **Fondos: generados.** Edgar todavía no entregó nada: **faltan** `fondo_ciudad_01`, `fondo_colinas_01`, `fondo_bosque_01`. Agregué dos ids que el guion no nombra: `fondo_casas_01` (colina del libro) y `fondo_luz_campamento` (la luz lejana de N1.4). Se generan como `gen_<id>.png`; si llega `<id>.png`, gana al volver a correr el menú.
- **Casas de Wilbur y Max: deberían verse, sin confirmar.** `Tarjeta_Casas_Wilbur_Max` en (105, -1, 22): ladrillo rojo y madera con dorado, ventanas encendidas. Pensada para verse mirando al este por la calle y desde el patio trasero. **David debe confirmarlo en pantalla.**
- **Luz:** 14 luces nuevas y 2 menos (las del callejón viejo), todas las nuevas **sin sombras** (aumentan el costo por píxel, no las sombras). Luna de 0.5 a 0.75. Niebla de 22–170 m a 30–190 m. El Volume global no se tocó. La calle está partida en 3 mallas por el límite de 8 luces por objeto de URP.

## ¿Siguen bien la ruta del taxi, las patrullas y el NavMesh?
**Por código sí; en Unity no está comprobado.**
- **Taxi:** no toqué `Taxi_Path`, `Taxi_ExitPath` ni las anclas. **Ojo:** la salida recta al oeste (hasta x -60) **atraviesa la barricada de x -44**. La cámara mira al otro lado (INTRO.2.1 mide ≥ 31° fuera de cuadro) y el taxi no choca (kinemático), pero conviene mirarlo. Esa punta es oeste a propósito: el taxista dice "de esta esquina para allá es territorio de Don Lobo".
- **Primer plano al bajar:** Wilbur en (-14, -10.9) mirando a yaw 20° ahora ve la **boca del callejón lateral** en lugar de la cerca alta. Es un cambio de encuadre: revisar si gusta.
- **Guardias:** ninguna ruta cambió y no hay enemigos nuevos. Cambios en el rincón de 3C.1: la cerca lateral pasó de x -3.6 a **x -2.8** y llega a z 25.5; `Fardo_Patio` pasó a (-3.5, 15), porque tapaba el paso al lado del cuarto. La ruta A y el centinela no se movieron.
- **NavMesh:** el sendero del bosque queda fuera a propósito (portón cerrado). Los menús corren `Nivel1MapBuilder.Validate`, `ContadorYardBuilder.ValidatePaths` y `ValidateExtraGuards`. Actualicé el origen "callejón" de `ValidatePaths` al callejón lateral.

## Cosas que bloquean al jugador sin verse
**Ninguna por construcción; falta confirmarlo jugando.** Todo collider nuevo está en una pieza visible. La red de seguridad (`N1_RedSeguridad`, a ≥ 12 m de los bordes, + piso a -8 m) son solo triggers: no bloquean, y si el jugador llega vuelve al último punto seguro con un aviso en consola. Los árboles chocan en una franja de 1.4 m en los troncos, no en el follaje. El marco del cartel no tiene collider, solo los postes.

## Qué debe mirar David en el Editor
- **Batches en Stats, antes y después** (no pude medirlo). Mirar desde la calle hacia el este y hacia el oeste, con la vista completa en cuadro. Antes de regenerar, anotar el valor con la escena actual.
- **Legibilidad:** ¿se lee bien de noche? Si falta o sobra luz, ajustar `ApplyReadableLighting` y las intensidades de `Lamp(...)`.
- **Luces por objeto:** el terreno `Patios` tiene muchas luces cerca (N1.1 agrega `Farol_Porton_Bosque` y `Farol_Callejon_Lateral`). Si alguna parpadea al moverse, es el límite de 8 de URP.
- **Piezas estiradas:** algunos tramos cortos estiran bastante la pieza (`Cierre_B_Lote`, 1.1 m con una reja de 3 m). Revisar si se ve mal.
- **Hojas del portón y gabletes:** el lado de las caras (`Card`) y la dirección de las bisagras no están vistos en pantalla.
- **Recorrido:** caminar por todos los bordes buscando rendijas, y ver que las balas choquen con ellos.
- **Primera corrida:** escena del taxi desde `Intro.unity`, el camión y el guardia B, y el combate de Centavo (el rincón es más grande).

## Comprobaciones
- **Compile check:** Roslyn de Unity 2022.3.62f3 sobre las fuentes de esta rama, contra las referencias de los `.csproj`: `Assembly-CSharp` 110 fuentes, `Assembly-CSharp-Editor` 36 fuentes, **0 errores, 0 avisos** (sin CS0169, CS0649 ni CS0414, ocultos como en auditorías anteriores).
- **Simulación del plano (fuera de Unity, sin commit):** flood fill con el radio del jugador sobre coordenadas copiadas a mano de los builders. 12 zonas alcanzables y 11 puntos del otro lado de los bordes **sin paso**. No cubre rendijas por la forma real de las mallas ni el NavMesh real: para eso está la validación del menú.
- **No probado:** nada en Play ni en Editor. Tampoco corrí los menús.

## Decisiones que conviene revisar
1. **Se quitó el marcador `LevelExit`** (no lo lee nadie; `MarkerType.LevelExit` sigue en el enum). Queda `Anchor_LevelExit` en (-6.2, 0, 55) y `Anchor_PortonBosque`. N10 (la carga real) es de N1.4.
2. **Portón detrás del cuarto:** para que N9 funcione, el rincón creció hasta z 25.5 y se pasa por el lado este del cuarto (2.2 m de ancho).
3. **Huellas de lobo y camino trasero:** antes llevaban al callejón eliminado; ahora llevan al portón de Centavo. Las huellas de lobo + cerdo hasta el bosque son de N1.4.
4. **Trabajé en un worktree** (`worktree-n1-1`, en `.claude/worktrees/n1-1`), porque el entorno lo exige con sesiones en paralelo. **Los commits no llegaron a `integracion/dia4`:** el entorno no deja operar sobre el checkout compartido desde el worktree. Falta el paso 0. No se hizo push.
5. **El guion está en** `Docs/guiones/Guion-Tutorial-Huellas.md`, no en `tutorial-huellas.md` como dice la fase. N1.1 no lo necesita, pero N1.3 y N1.4 sí.
6. `Docs/phases/Fase-N1-Mapa-Tutorial.md`, `Fase-N2-Campamento.md` y los guiones nuevos siguen sin commit en el checkout principal; no son de esta subfase y no los toqué.

## Archivos compartidos tocados (y si estaban limpios en git status)
`CharacterSpriteImporter.cs`: **no se tocó** (los fondos ponen sus propios ajustes de importación). `EditorBuildSettings.asset`, `GameStateTracker.cs`, `InterrogationData`, `AICompanion`, `AudioImporter`: no se tocaron.

No figuran en la tabla como compartidos, pero Sesión B los puede leer:
- `CartoonKit.Props.cs` (`Facades`): escalón y alero en cada puerta. **Si Sesión B usa `Facades` en el Campamento, sus edificios también los tendrán.** No cambia colisiones (sin collider).
- `IntroTaxiBuilder.cs`: `AddCarBody` público, sin cambios en el comportamiento del taxi.

`LevelBuilder_NivelPorky.cs` y `ContadorYardBuilder.cs` son de Sesión A.

**Estado en git:** los 4 archivos modificados estaban limpios en el checkout principal al empezar (`97f46c6`, antes de crear el worktree). **No pude volver a mirarlo al final**, porque el entorno no lo permite desde el worktree: por eso el paso 0 empieza con `git status`. Si Sesión B cambió `CartoonKit.Props.cs` o `IntroTaxiBuilder.cs` entretanto, el cherry-pick puede dar conflicto ahí.

## Archivos creados/modificados
**Creados:**
- `Scripts/Level/Boundary/BorderSegment.cs`, `BorderPath.cs`, `SafetyCatchVolume.cs`
- `Scripts/Editor/BoundaryKitBuilder.cs`, `BorderPathFiller.cs`, `BackdropTextureGenerator.cs`, `Nivel1MapBuilder.cs`
- `Docs/audits/AUDIT_n1_1.md`

**Modificados:**
- `Scripts/Editor/LevelBuilder_NivelPorky.cs` — llama a `Nivel1MapBuilder`, quita muros y callejón viejos, `RefreshYardGround`, `BakeNavMesh` público
- `Scripts/Editor/ContadorYardBuilder.cs` — rincón hasta z 25.5, cerca en x -2.8, fardo, `RefreshYardForMap`, validación
- `Scripts/Editor/IntroTaxiBuilder.cs` — extrae `AddCarBody`
- `Scripts/Editor/CartoonKit.Props.cs` — escalón y alero en `Facades`

## Commits
En la rama `worktree-n1-1` (el cherry-pick les dará otro hash en `integracion/dia4`):
- `071aae2` — dia4(david): N1.1 kit de bordes, mapa ampliado, vista y red de seguridad
- Esta auditoría: `dia4(david): N1.1 auditoria` (su propio hash no se puede escribir aquí)
