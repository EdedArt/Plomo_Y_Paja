# AUDIT N1.2 — Objetos colocados en el mundo (regla N7)

**Fecha:** 3 nov 2026 · **Rama:** `worktree-n1-1`, encima de N1.1 (**ninguna de las dos está en `integracion/dia4`**, ver el paso 0) · **Sesión A** · **Alcance:** solo N1.2.

> **Nada de esto se probó en Unity.** Se compiló con Roslyn y se revisó la geometría con un script propio. Los prefabs, el asset de objetos, los muebles y el NavMesh **todavía no existen**: los crean los menús.

> **Plantilla:** el pedido decía "plantilla N1.1", pero la fase tiene una propia para N1.2 y la regla 1 de §0 pide la de la subfase. Aquí van las dos: primero la de N1.2 y al final lo que solo pregunta la de N1.1.

## ¿Se hizo HUD.3 completo aquí o solo la distribución?
**Solo la distribución, más lo que la fase agrega.** HUD.3 ya estaba hecho (`b80cf9e`, código) y no se rehízo. Lo nuevo:
- `PickupSpot`: se apoya en la superficie de abajo con un rayo, avisa si flotaba más de 0.05 m y guarda el mueble.
- `PickupBuilder`: distribución nueva, aviso de "piso sin muebles a 0.6 m", y `ValidateSpots`.
- Objetos colocados **sin vaivén** (solo brillo), con dibujo de 0.34 m en lugar de 0.5 m.
- Drops apagados.

**Lo que no estaba en la fase y tuve que construir** (`Nivel1FurnitureBuilder`, grupo `N1_Muebles`). La tabla pide muebles que la casa no tenía:

| La fase pide | Lo que había | Qué hice |
|---|---|---|
| Baño | **La casa no tenía baño** (sala, dormitorio, cocina) | Lo saqué de la punta oeste de la cocina: x -8..-5.2, z 2..6, puerta en z 3.4. Armario abierto, lavabo, bañera y bombilla propia |
| Estante o mostrador de cocina | Nada | `Mostrador_Cocina`, contra la pared del fondo |
| Cajón abierto de la mesita de noche | Una caja, y debajo de la losa caída: no se llega ni se ve | `Mesita_Noche` nueva con el cajón afuera, en la pared este al sur de la losa. La caja vieja sigue de adorno |
| Lata sobre la mesa de la sala | Solo una caja usada de mesita, con las facturas encima | `Mesa_Sala` + `Lata_Sala` (abierta, con la tapa al lado) |

**Consecuencia:** `Barril_Cocina` y `Caja_Cocina` estaban donde ahora está el baño; pasaron al rincón este de la cocina.

**Si no quieres el baño:** es la decisión más grande de esta subfase. La alternativa es poner el botiquín en otro mueble; se cambia en `NivelPorkySeed` y se quita `BuildBathroom`.

## Cómo se recoge (tecla / aviso reutilizado)
Sin cambios desde HUD.3: tecla **E**, a ≤ 2.2 m en horizontal, con línea de vista y el aviso bajo la mira (`RubberHoseCrosshair.SetPrompt`). Textos: «[E] Tomar botiquín / cartuchos / balas».

## Posiciones elegidas (mueble o contenedor de cada una) + mapa ASCII
**Totales: 4 cajas de cartuchos, 3 botiquines, 1 caja de balas**, como pide la tabla. No queda nada justo antes del portón del patio (se quitaron `Botiquin_Porton` y `Balas_Salida`).

| Id | Objeto | Mueble (objeto en la escena) | (x, y, z) |
|---|---|---|---|
| `Cartuchos_Cocina` | cartuchos | Cocina: mostrador (`Mostrador_Cocina`) | (1.50, 0.96, 5.60) |
| `Cartuchos_Dormitorio` | cartuchos | Dormitorio: cajón abierto (`Mesita_Noche`) | (7.26, 0.41, -2.65) |
| `Botiquin_Bano` | botiquín | Baño: repisa del armario abierto (`Armario_Bano_Repisa`) | (-6.60, 1.25, 5.74) |
| `Balas_Sala` | balas | Sala: lata abierta sobre la mesa (`Lata_Sala`, `Mesa_Sala`) | (-1.20, 0.84, -3.50) |
| `Cartuchos_Vecino` | cartuchos | Vecino B: mesa del porche (`Vecino_B_Mesa_Porche`) | (23.45, 1.02, -6.05) |
| `Botiquin_Callejon` | botiquín | Callejón lateral: caja (`Caja_Callejon_Lateral_A`) | (-13.00, 0.80, -1.20) |
| `Botiquin_Banco` | botiquín | Cuarto de Centavo: banco (`Banco_Trabajo`) | (-7.62, 1.22, 18.60) |
| `Cartuchos_Caja_Cuarto` | cartuchos | Cuarto de Centavo: caja (`Caja_Cuarto`) | (-8.45, 1.00, 18.62) |

**Leyenda:** `c` cartuchos · `m` botiquín · `b` balas · `H` casa · `S` cuarto de Centavo · `V` vecino · `K` camión · `P` inicio · `.` caminable. N arriba. Mapa completo con bordes y guardias: `AUDIT_n1_1.md`.

**General** (1 carácter = 2 m)
```
  26                     .....
  24                     .....
  22                     .....
  20                     SSS........
  18                     ScS........
  16                     ...........
  14                     ...........
  12                     ...........
  10                     ...........
   8                     ...........
   6                     .HmHHHcHHH.
   4     VVVVVVVVVVVVVVV..HHHHHHHHH. VVVVVVVV
   2     VVVVVVVVVVVVVVV..HHHHHHHHH. VVVVVVVV
   0     VVVVVVVVVVVVVVV..HHHHHHHHH. VVVVVVVV........
  -2     VVVVVVVVVVVVVVm..HHHHHHHHc. VVVVVVVV........
  -4    .VVVVVVVVVVVVVVV..HHHbHHHHH. VVVVVVVV........
  -6    ..................HHHHHHHHH. .....c..........
  -8    ............................ ................
 -10    ...............P.............................
 -12    ..........................KKKK...............
 -14    .............................................
 -16    .............................................
 -18                .....................
 -20                .....................
```

**Zoom de la casa y el cuarto** (1 carácter = 1 m). `#` pared · `=` mostrador · `n` mesita · `T` bañera · `L` lavabo · `o` barril y caja movidos · `D` portón del bosque. En la fila 19, `c` y `m` caen casi en la misma celda (están a 0.8 m): los separé a mano.
```
  25  --DDDD--
  24  |......|
  23  |......|
  22  |......|
  21  |SSSSS.|-------------
  20  |SSSSS.|............|
  19  |ScmSS.|............|
  18  |S...S.|............|
  17  |SSSSS.|............|
  16  |......|............|
  15  |......|............|
  14  |......|............|
  13  ---.---|............|
  12  |...................|
  11  |...................|
  10  |...................|
   9  |...................|
   8  |...................|
   7  |...................|
   6  |.#m##.####c#######.|
   5  |.#L.#.....===....#.|
   4  |.#...............#.|
   3  |.#T.............o#.|
   2  |.#####.#######.###.|
   1  |.#.........#.....#.|
   0  |.#.........#.....#.|
  -1  |.#.........#.....#.|
  -2  |.#...............#.|
  -3  |.#......b..#....cn.|
  -4  |.#.........#.....#.|
  -5  |.#.........#.....#.|
  -6  |.###.########..###.|
  -7  |...................|
  -8  |...................|
```

## ¿Algún objeto quedó suelto en el piso?
**No, por construcción.** Los 8 tienen mueble y `PlaceInOpenScene` avisa con `⚠` si alguno flota o queda en un piso sin muebles. **Falta confirmarlo:** esos avisos solo salen al correr el menú.

## Botiquín: ¿cura 40 %? ¿no se gasta con la vida llena?
Por código, sí; no cambió en N1.2: `Heal(MaxHealth * 0.4)`, y con la vida llena muestra «Salud completa» y queda. **Sin probar en Play.**

## Munición: ¿respeta los topes? ¿la de revólver se guarda sin tener el revólver?
Por código, sí; no cambió en N1.2: `PlayerAmmo.TryAdd` (topes 30 y 24) no mira qué arma se tiene. Llena: «Munición llena» y queda. **Sin probar en Play.**

## Drops de guardias: ¿apagados por defecto?
**Sí.** `GuardDropSpawner.dropsEnabled` es `false` y serializado; se prende en el Inspector sobre `--- PICKUPS ---/GuardDropSpawner`, y vuelve a funcionar como H16. Sigue en la escena.
**A vigilar:** sin drops, toda la munición del nivel son **24 cartuchos en el mapa (4 × 6)**. No medí si alcanza para todos los guardias: hay que verlo jugando.

## ¿Sobreviven a "Generar Nivel Porky"?
**Sí, por código; sin probar.** `Generar` → `Nivel1MapBuilder.BuildInto` rehace `N1_Muebles`, y `PickupBuilder.PlaceInOpenScene` coloca desde `Data/PickupLayout_NivelPorky.asset`, que no se regenera. Lo movido a mano se pierde si antes no se corrió **Guardar posiciones de objetos** (que ahora también guarda el mueble).
**Asset viejo:** el asset no está en git. Si existe uno de HUD.3 en alguna máquina (versión 0), se reemplaza una vez por esta distribución; lo guardado después ya no se toca.

## Menús que David debe correr (en orden)
0. **Traer los commits a `integracion/dia4`**, desde el checkout principal, porque N1.1 tampoco llegó: `git status` (sin cambios ajenos en los archivos modificados de N1.1 y N1.2) y luego `git cherry-pick 97f46c6..worktree-n1-1`. Si ya trajiste N1.1 por tu cuenta, usa `git cherry-pick 60a3748..worktree-n1-1`.
1. **Abrir Unity** y esperar la importación. Sin `error CS`.
2. **Plomo y Paja → Generar Nivel Porky** (recomendado): corre kit, mapa, muebles y objetos, hornea y valida.
   - Sin regenerar: **Plomo y Paja → Nivel 1 → Ampliar mapa**. Si el mapa ya estaba ampliado, alcanza con **Plomo y Paja → Nivel 1 → Colocar muebles y objetos** (menú nuevo).
3. **Consola:** debe decir `✓ Objetos: los 8 se alcanzan y se ven de pie`, sin ningún `✗` ni `⚠` de `[Pickups]`.
4. **Commit de lo generado** (por ruta explícita): `.meta` nuevos, `Prefabs/Pickups/`, `Data/PickupLayout_NivelPorky.asset`, mallas nuevas de `Art/Environment/Generated/`, `Scenes/NivelPorky.unity` + NavMesh.

## Ids faltantes y placeholders
- **Sonidos:** según HUD.3 faltan `sfx_botiquin` y `sfx_municion_recoger`; sin ellos no suena, pero el aviso sale.
- **Voces opcionales:** `wilbur_botiquin_01`, `wilbur_municion_01`.
- **Arte:** los 3 PNG de `Art/Props/pickups/` están. Los muebles nuevos son de primitivas, sin arte.
- No volví a comprobar la librería de audio en esta subfase. El menú imprime lo que falte.

## Archivos compartidos tocados (y si estaban limpios en git status)
`CharacterSpriteImporter.cs`, `EditorBuildSettings.asset`, `GameStateTracker.cs`, `AICompanion`, `InterrogationData`, `EnemyHealth.cs`: **no se tocaron**.

**Estado en git:** trabajo en un worktree y el entorno no me deja consultar el checkout compartido desde aquí, así que **no pude hacer `git status` allí** en esta subfase; por eso el paso 0 lo hace. El puntero de `integracion/dia4` sigue en `97f46c6`.

**Para Sesión B:** toqué `PickupBuilder`, `Pickup`, `PickupLayout` y `GuardDropSpawner`, que N2 puede usar. Las firmas públicas se mantienen. Una entrada de `PickupLayout` **sin** `container` baja al piso como antes (ahora con aviso si no hay muebles); **con** `container` usa su altura, y esa altura debe ser la de la superficie. Los drops también quedan apagados en `Campamento`. Sesión B no verá nada de esto hasta el paso 0.

## Qué no se pudo compilar/probar y qué debe revisar David en el Editor
**Compile check:** Roslyn de Unity 2022.3.62f3 sobre esta rama: `Assembly-CSharp` 111 fuentes, `Assembly-CSharp-Editor` 37 fuentes, **0 errores, 0 avisos** (sin CS0169, CS0649 ni CS0414).
**Revisión de geometría (script fuera de Unity, coordenadas copiadas a mano):** totales 4/3/1; la mesita queda fuera de la losa; ningún puesto de guardia a menos de 0.6 m de un mueble nuevo. No reemplaza a `ValidateSpots`.

**Revisar:**
1. **Interrogatorio:** `Botiquin_Banco` y `Cartuchos_Caja_Cuarto` quedan detrás de Centavo sentado, y **probablemente salgan en cuadro** si Wilbur no los tomó. Ya era un riesgo en HUD.3; ahora están más altos. Si molesta, mueve y **Guardar posiciones**.
2. **"Premio después de la pelea" no se impone:** esos dos se pueden tomar durante el combate si Wilbur llega al banco. La fase no pide bloquearlos y no lo hice.
3. **Tamaño:** 0.34 m es a ojo. ¿Se ve bien en el cajón, la repisa y la lata?
4. **Tecla E compartida:** `Cartuchos_Cocina` queda a 4.2 m de Colmillo. Es justo en el límite, si hablar con él llega a 2 m; ver si los dos avisos chocan. Del resto, ninguno queda cerca de una pista o de otro personaje con diálogo.
5. **Baño:** ¿las paredes se ven bien? ¿La puerta encaja? ¿Se lee como baño? ¿La bombilla alcanza?
6. **Armario y cajón:** el aviso debe verse de pie, desde adelante. La puerta del armario está sin ver.
7. **Luces:** en la casa hay más luces cerca de `Casa_Piso` (la bombilla del baño y hasta 4 brillos). Por eso los brillos solo se prenden a 7 m. Si algo parpadea, es el límite de 8 de URP.

## Archivos creados/modificados
**Creados:**
- `Scripts/Pickups/PickupSpot.cs`
- `Scripts/Editor/Nivel1FurnitureBuilder.cs`
- `Docs/audits/AUDIT_n1_2.md`

**Modificados:**
- `Scripts/Editor/PickupBuilder.cs` — distribución N7, avisos, look de colocado, `ValidateSpots`, versión del asset
- `Scripts/Pickups/Pickup.cs` — `TryFindSurface`; `IsActor` público
- `Scripts/Pickups/PickupLayout.cs` — `container`, `version`
- `Scripts/Pickups/GuardDropSpawner.cs` — `dropsEnabled`, apagado
- `Scripts/Editor/Nivel1MapBuilder.cs` — constantes de las superficies; llama a los muebles y a los objetos; `Reachable` público
- `Scripts/Editor/LevelBuilder_NivelPorky.cs` — barril y caja movidos; `ValidateSpots`; `WallHeight` y `FloorTop` públicos
- `Scripts/Editor/ContadorYardBuilder.cs` — `WorkbenchSpot`, `CrateSpot`

## Commits
En la rama `worktree-n1-1` (el cherry-pick les dará otro hash en `integracion/dia4`):
- `b9b5315` — dia4(david): N1.2 objetos en muebles (N7), bano y muebles nuevos, drops apagados
- Esta auditoría: `dia4(david): N1.2 auditoria` (su propio hash no se puede escribir aquí)

---

## Lo que solo pregunta la plantilla N1.1
- **Bordes invisibles o en blanco:** N1.2 no cambió ningún borde. Las paredes del baño son interiores.
- **¿Hay salto? stepOffset:** sin cambios: `jumpForce = 0`, `stepOffset = 0.3`.
- **Kit de bordes:** sin piezas nuevas. `Rubble`, `Crates`, etc. se quedan como en N1.1.
- **Área jugable:** casi igual a N1.1 (≈ 1816 m² brutos); el baño la divide por dentro, sin sumar.
- **Vista:** sin cambios. **Las casas de Wilbur y Max siguen sin confirmarse en pantalla.**
- **¿Siguen bien la ruta del taxi, las patrullas y el NavMesh?** Por código sí; **en Unity no está comprobado**. Ninguna ruta cambió. Los muebles nuevos cortan el NavMesh, y por eso `Colocar muebles y objetos` vuelve a hornear. Cerca de `Patrol_C` queda el cajón (a más de 0.6 m). Los menús corren `Validate`, `ValidatePaths` y `ValidateExtraGuards` (este último solo en `Generar` y `Ampliar mapa`).
- **Cosas que bloquean al jugador sin verse:** **ninguna por construcción; falta confirmarlo jugando.** Cada caja nueva está sobre un mueble visible. El armario solo choca en la repisa, a 1.25 m, y el lavabo está debajo.
- **Batches:** no pude medirlos. **Mirar Stats** dentro de la casa. Los muebles nuevos son pocos y estáticos, y hay hasta 8 luces de brillo más en el nivel.
