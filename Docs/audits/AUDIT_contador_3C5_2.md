# AUDIT contador 3C.5.2: arreglos de la prueba del revólver (recogible, posición y mira, recarga bala por bala, tamaño de manos)

Fase: `Docs/phases/Fase-3C-Contador.md` (secciones 0 y 1). Leídos `AUDIT_contador_3C5.md`, `AUDIT_contador_3C5_1.md`, `docs/phases/PHASE-VM2.md`, el contrato `Art/Viewmodel/README_ART.md`, y de paso `Docs/phases/Fase-HUD.md` (la fila de munición de abajo es provisional hasta HUD.1).
Rama: `integracion/dia4`. **No se empezó la 3C.6.**

> **Importante:** Unity no se abrió en esta sesión. El código **compila**, pero **los menús no se ejecutaron** y **nada se probó en Play**. Todos los números de abajo salen de medir los PNG con un script de Python que usa **el mismo algoritmo** que el builder en C#.

## Antes de empezar
- **Assets que dejó la corrida de menús de la 3C.5.1, en su propio commit** (`74d6974`):
  - los 8 `.meta` de los PNG del revólver, ya como 2D;
  - `VM_Revolver.asset`, ahora **con cuadros**: confirma el arreglo del Bug 1 de la 3C.5.1;
  - `Interrogation_Contador.asset`, `Player.prefab`, `Centavo.prefab` e `InterrogationCanvas.prefab`.
- **No toqué** `Docs/guiones/Guion-HUD.md` ni `Docs/phases/Fase-HUD.md` (sin seguimiento, no son de esta subfase).

## 1. Aspecto del revólver en el piso
### Arte
- **Recorte:** `python Tools/character_key.py --single _entrante_personajes/Pickups/revolver_suelo.png --out Assets/_Game/Art/Pickups/revolver_suelo.png --crop 4`.
  - Entrada: 1254×1254 RGB con fondo magenta.
  - Salida: **1137×676 RGBA**, 45.3 % transparente.
  - Lo revisé sobre fondo blanco: borde limpio, sin magenta; el hueco del guardamonte queda transparente.
- **Importación:** se extendió `CharacterSpriteImporter` (regla 4: no hay un importador paralelo) con la carpeta **`Art/Pickups/`**:
  - Sprite Single, forma 2D, alpha real (FromInput, Alpha Is Transparency), sin compresión;
  - mipmaps + Trilinear, pivote al centro, 1000 px/m;
  - la versión sube de 6 a 7.
- **Nota:** la carpeta de entrada real es `_entrante_personajes/` (sin el guion bajo final). Está ignorada por git.

### En el mundo (`Revolver_Suelto`, lo arma `ContadorCentavoBuilder.BuildDroppedRevolver`)
- **Raíz:**
  - lleva la física: el `Rigidbody` lo agrega `DropRevolver` (3C.5.1), ahora con la **rotación congelada**, así el collider cae plano;
  - un **`BoxCollider` invisible** de 0.32 × 0.06 × 0.12 m;
  - `WeaponPickup` y el rescate del piso de la 3C.5.1, sin cambios.
- **Hijo `Visual` con el PNG:** `SpriteRenderer` + **`BillboardSprite`** (el de los personajes: gira solo en Y hacia la cámara, look plano sin iluminación). `WeaponPickup`:
  - lo deja de **0.35 m de largo** (`visualLength`), o sea ~0.21 m de alto;
  - lo apoya **2 cm sobre el piso** (`restHeight`, medido desde el borde de abajo del collider);
  - lo mece **±2 cm** (`bobAmplitude`, 0.8 Hz) cuando ya se puede recoger;
  - el brillo de la 3C.5.1 sigue.
- **Sin el PNG (respaldo, nunca el cubo):** un revólver de piezas de `CartoonKit`, acostado de costado, ~0.32 m.
  - Piezas: armazón, tambor, cañón, mira, martillo, guardamonte y gatillo (`Kit_Metal`), culata (`Kit_MaderaOscura`) y remache (`Kit_Laton`).
  - Las mallas van a `Art/Environment/Generated/Pickup_Revolver_*.asset`.
  - `WeaponPickup` le pone el look plano con contorno (`CharacterLook`).
- **Elección automática:** el menú usa el dibujo si existe `Art/Pickups/revolver_suelo.png`; si no, el modelo, y lo avisa con ⚠ en el cuadro.
- **Menús que lo mantienen:** "Colocar Centavo", "Construir patio y cuarto" y "Generar Nivel Porky" usan el mismo builder.

## 2. Posición del revólver y mira
### Causa real
**`VM_Revolver.aimSightPoint` nunca se había medido.**
- El perfil se creó en la 3C.5 cuando `aimidle` todavía no tenía cuadros (Bug 1 de la 3C.5.1).
- `ApplySight` igual lo marcó como medido (`sightVersion = 1`), así que se quedó con el valor por defecto **(960.5, 575)**, que es el de la escopeta.
- **Medido del PNG:** la punta de la mira está en **(964.3, 723)**, **148 px más arriba**.
- **Efecto al apuntar:** se centraba un punto del cuerpo del arma, no la mira. El revólver quedaba en el medio de la pantalla tapando el centro, y la fila de munición (que seguía visible al apuntar, bajo la mira) quedaba encima del arma. Es lo que muestra la captura de David.

### Cadera
- **El arma en la cadera, en px de canvas a 1080p, con el ajuste de manos real** (`FitHands`: el arte se ajusta al alto × 1.08 y sobra a la derecha y abajo):
  - el borde izquierdo del arma en el arte es **x = 1053** (lo mide el builder, el mínimo entre `idle` y `fire`);
  - en **16:9** queda a **+187 px** del centro y en **16:10** a **+91 px**: en esos formatos no tapaba la mira;
  - en **4:3** (−53 px) y **5:4** (−98 px) sí la tapaba.
- **Arreglo:**
  - nuevos `ViewmodelProfile.hipClearLeftArtX` (medido: 1053) y `hipCenterClearance` (70 px);
  - `FirstPersonViewmodel.FitHands` corre las manos a la derecha **lo justo** para que el borde del arma quede al menos 70 px a la derecha del centro: 0 px en 16:9 y 16:10, 123 px en 4:3, 168 px en 5:4;
  - la escopeta tiene `hipClearLeftArtX = 0`, así que **no cambia**.

### Al apuntar
- `SightVersion` sube de 1 a 2. Se vuelve a medir con el mismo algoritmo de la VM 1.4: el cúmulo opaco más alto en la franja central.
  - **Escopeta:** (960.5, 575), igual que antes, no cambia.
  - **Revólver:** **(964.3, 723)**, como decía `PHASE-VM2.md`.
- **Arreglo de fondo:** `ApplySight` ya **no marca "medido" si no hay dibujo** de apuntar; mide cuando llegue el PNG.
- El centrado exacto en cualquier formato es el de la VM 1.4 (`ViewmodelMotion.SightCenterOffset`). Usa el tamaño y la posición **reales** de `HandsImage`, así que sigue exacto con `handsScale` y con el corrimiento de la cadera.

### Fila de munición
- Pasó de debajo de la mira a la **esquina inferior izquierda**: a 56, 52 px del borde.
- Puntos 1.5 veces el punto de la mira, separados 18 px, y "+N" a 22 px.
- Ahí no hay arma en ninguno de los dos casos (cadera ni apuntar). En la recarga del revólver la manga izquierda pasa por esa zona; el HUD Overlay se dibuja encima y los puntos tienen contorno, así que se leen igual.
- Campos: `ammoCorner`, `ammoSpacing`, `ammoDotScale`.
- Es **provisional**: HUD.1 (`Fase-HUD.md`) la reemplaza con su HUD de munición.

## 3. Recarga bala por bala
- **Capacidad:** **5 + 15**. Daño **24**, sin cambios.
  - `RevolverController`: `magazineSize` 5, `loadedRounds` 5, `reserveRounds` 15.
  - `WeaponSwitcher`: al recogerlo da **5 + 15**.
  - Los prefabs que ya tienen los valores de la 3C.5 (6 / 6 / 12) los migra *Crear Prefabs de Prueba* (`MigrateInt`: solo si siguen en el valor viejo).
- **Secuencia** (`RevolverController`, nueva interfaz `IStagedReloadWeapon`):
  1. **abrir** 0.35 s (`openSeconds`, `OnReloadOpened`);
  2. **una bala por cada una que falta, nunca más que la reserva**, 0.30 s cada una (`insertSeconds`, `OnRoundInserting` y luego `OnRoundLoaded(n)`; cada bala **cuenta al entrar**);
  3. **cerrar** 0.35 s (`closeSeconds`, `OnReloadClosing`).

  | Disparos | Balas que mete | Tiempo |
  |---|---|---|
  | 1 | 1 | 0.35 + 0.30 + 0.35 = **1.0 s** |
  | 2 | 2 | **1.3 s** |
  | 5 | 5 | **2.2 s** |
- **Disparar durante la recarga:** termina la bala en curso, cierra y dispara solo (VM2).
- **Cambiar de arma:** corta la recarga y **las balas ya metidas se quedan** (cuentan una por una).
- **R** recarga si falta alguna bala y hay reserva. Con el tambor vacío, recarga sola como antes.
- **Cuadros** (`ViewmodelAnimator`, modo "por etapas"):
  - `reload_01` (abrir) queda quieto;
  - cada bala muestra `reload_02` (meter) el 60 % de su tiempo y vuelve a abrir;
  - `reload_03` (cerrar) al final, con un golpecito de escala al cambiar.
  - Se quitó la secuencia de 7 cuadros de la 3C.5 (`reload.fps` vuelve a 0).
- **Balas visibles** (`ViewmodelChamberOverlay`, el `ChamberOverlay` de VM2):
  - es hijo de `HandsImage`, así que sigue al dibujo en balanceo, culatazo, escala y formato;
  - una `Image` por hueco con `fx_round_rim_01`, ubicada con **anclas en coordenadas del arte**: no hay que acomodar nada a mano;
  - se ve solo en los cuadros abrir y meter. Huecos llenos = balas cargadas; la que entra hace un golpecito 1.15 → 1.
- **Huecos medidos automáticamente** (`ViewmodelProfileBuilder.MeasureChambers`):
  - **Algoritmo:** píxeles opacos muy oscuros → erosión con un cuadrado de 15 px (borra los contornos) → componentes redondas de tamaño parecido → hasta 5, en el sentido de las agujas del reloj. Empieza por el que sigue al de arriba; el de arriba, donde el dibujo "meter" pone la bala, se llena el último.
  - **`reload_01` (abrir)**, px del arte con origen abajo-izquierda, en orden de llenado: (1081.2, 517.5) · (1066.8, 448.7) · (996.5, 444.6) · (968.8, 509.5) · (1021.1, 554.5). **Diámetro 61 px.**
  - **`reload_02` (meter):** (1082.5, 517.1) · (1068.0, 448.0) · (998.0, 444.0) · (971.0, 505.8) · (1027.9, 554.5). El de arriba está medio tapado por la bala.
  - La bala se dibuja al 92 % del hueco (`chamberRimScale`).
  - Si no salen 5 huecos, la consola dice ✗.
- **Fila de munición:** 5 puntos + "+N", sale sola de `MagazineSize` / `LoadedRounds` / `ReserveRounds`.
- **Pendiente de VM2:** los casquillos que caen (`fx_casing_revolver_01`).

## 4. Tamaño de las manos
**Medido**: guante = el componente opaco casi blanco más grande. En el resumen, px del arte (alto × ancho, área); en el detalle, cajas de los guantes en px del arte.

| Dibujo | Guante | Alto × ancho | Área |
|---|---|---|---|
| Escopeta `idle` | derecho | **284** × 430 | 75 448 px² |
| Revólver `idle` | derecho | **288** × 338 | 45 856 px² |
| Escopeta `aimidle` | — | 250 × 520 | 65 320 px² (cortado abajo) |
| Revólver `aimidle` | — | 238 × 344 | 51 848 px² |
| Revólver `reload_01` | izquierdo | 392 × 418 | 75 368 px² |
| Revólver `reload_02` | izquierdo | **428 × 422** | **101 444 px²** |
| Escopeta `idle` | izquierdo (adelante) | 202 × 182 | 14 748 px² |

- **Detalle (cajas, px del arte):**
  - escopeta `idle`: guante derecho x 1338–1768, y 672–956;
  - revólver `idle`: guante derecho x 1264–1602, y 704–992;
  - escopeta `idle`, izquierdo: x 942–1124, y 582–784.
- **Resultado:** la mano que sostiene el arma tiene **el mismo alto** en los dos (284 vs 288 px). Por área, la del revólver es más chica (un puño contra una mano abierta).
  - `handsScale` medido = 284 / 288 = **0.99**. El builder lo guarda **una sola vez** (`handsScaleVersion`); después manda lo que ajuste David.
- **Lo que sí es más grande:** la **mano izquierda de la recarga**. En `reload_02` mide 428 px de alto, **2.1 veces** la mano de adelante de la escopeta (202 px), porque está cerca de la cámara.
  - Probablemente eso, más el arma centrada al apuntar (punto 2), es lo que se veía "grande".
- **Cómo se aplica `handsScale`** (`VM_Revolver` → *Tamaño y lugar en la cadera*, rango 0.5–1.5):
  - escala todo el dibujo del revólver desde la esquina inferior derecha;
  - la mira centrada al apuntar sigue exacta;
  - **se puede tocar en Play**: `FitHands` lo lee cada frame.
- **Sugerencia:** si en Play sigue viéndose grande, probar entre **0.85 y 0.9**. No lo cambié porque la medida dice 0.99.

## Menús que David debe correr
1. Abrir Unity y dejar que compile.
   - La versión del importador de personajes sube a 7 e importa `Art/Pickups/revolver_suelo.png` como Sprite.
   - La migración del viewmodel vuelve a medir la mira (`SightVersion` 2).
2. **Plomo y Paja → Crear Prefabs de Prueba.**
   - Rehace `VM_Revolver` con las medidas. En la consola debe salir `[Viewmodel] VM_Revolver medido: borde izq. … 1053 px; huecos abrir 5 / meter 5, diámetro 61 px; guante escopeta 284 px / revólver 288 px → handsScale 0.99`, **sin ✗**.
   - Migra el Player a 5 + 15.
3. **Plomo y Paja → Contador → Colocar Centavo.** El cuadro debe decir `✓ Revólver en el piso: dibujo …revolver_suelo.png (billboard)`.
4. **Ctrl+S** y commit de:
   - `Art/Pickups.meta` y `Art/Pickups/revolver_suelo.png.meta` (los crea Unity);
   - `VM_Revolver.asset`, `Player.prefab`, `Centavo.prefab` y `NivelPorky.unity` si cambió.

## Qué no se pudo compilar/probar y qué debe revisar David en el Editor
- **Compilado: sí.** Roslyn de Unity 2022.3.62f3 contra los módulos de `Managed/UnityEngine`, `UnityEditor.dll` y `Library/ScriptAssemblies`: **Assembly-CSharp y Assembly-CSharp-Editor con 0 errores**.
- **No probado:** los menús, la importación del PNG y nada en Play.
- **APIs dudosas:** ninguna nueva. El billboard reusa `BillboardSprite` sin agente (solo gira y muestra `idleSprites`).
- **Revisar en Play:**
  1. **Revólver en el piso:**
     - es el dibujo, de ~35 cm, mirando siempre a la cámara;
     - queda apoyado (no flota ni se hunde) y se mece un poco con brillo;
     - si se hunde o flota, ajustar `restHeight` en el `WeaponPickup` del prefab de Centavo.
  2. **Cadera:** en 16:9 la mira queda libre a la izquierda del revólver. Probar también una ventana 4:3: las manos se corren a la derecha y la mira sigue libre.
  3. **Apuntar:** la mira delantera del revólver queda en el centro exacto. Con `debugSightLog` en el `FirstPersonViewmodel` debe dar < 2 px.
  4. **Fila de munición:** abajo a la izquierda, 5 puntos + "+15". No tapa ni la tapa el arma.
  5. **Recarga:**
     - disparar 1 → R: abrir, 1 bala (se ve aparecer en el tambor), cerrar, ~1.0 s;
     - disparar 2: entran 2;
     - vaciar: entran 5 y los huecos se llenan en orden.
  6. **Disparar a mitad de la recarga:** termina la bala, cierra y dispara.
  7. **Cambiar de arma a mitad de la recarga:** al volver, las balas que ya habían entrado siguen.
  8. **Tamaño de las manos:** comparar con la escopeta y ajustar `VM_Revolver.handsScale` en Play si hace falta (se ve el cambio al instante).

## Archivos creados/modificados
Creados:
- `Scripts/Player/ViewmodelChamberOverlay.cs` (+ `.meta`): balas visibles en el tambor.
- `Art/Pickups/revolver_suelo.png`: recortado del PNG de David. El `.meta` lo crea Unity.
- `Docs/audits/AUDIT_contador_3C5_2.md` (este archivo).

Modificados:
- `Scripts/Player/IWeapon.cs`: interfaz `IStagedReloadWeapon`.
- `Scripts/Player/RevolverController.cs`: 5 + 15, recarga por etapas, disparar en la recarga, cancelar sin perder balas.
- `Scripts/Player/WeaponSwitcher.cs`: 5 + 15 al recogerlo.
- `Scripts/Player/ViewmodelProfile.cs`: `handsScale`, `hipClearLeftArtX`, `hipCenterClearance`, huecos del tambor, dibujo y escala de la bala.
- `Scripts/Player/ViewmodelAnimator.cs`: modo de recarga por etapas, `ReloadStage`.
- `Scripts/Player/FirstPersonViewmodel.cs`: escala y corrimiento en `FitHands`, eventos por etapas, overlay del tambor.
- `Scripts/Player/WeaponPickup.cs`: dibujo billboard de 0.35 m apoyado en el piso con vaivén; el modelo de respaldo se mece en su lugar.
- `Scripts/UI/RubberHoseCrosshair.cs`: fila de munición abajo a la izquierda.
- `Scripts/Characters/Contador/CentavoController.cs`: el revólver cae con la rotación congelada y un giro al azar en Y.
- `Scripts/Editor/ViewmodelProfileBuilder.cs`: `SightVersion` 2 (sin marcar sin dibujo); mediciones del revólver (borde del arma, huecos, guante, bala); se quitó la secuencia de 7 cuadros.
- `Scripts/Editor/CharacterSpriteImporter.cs`: carpeta `Art/Pickups/` (versión 7).
- `Scripts/Editor/ContadorCentavoBuilder.cs`: `BuildDroppedRevolver` (dibujo o modelo de respaldo) y `BuildRevolverModel`.
- `Scripts/Editor/TestPrefabGenerator.cs`: migración 6/6/12 → 5/5/15 (`MigrateInt`).

## Commits
- `74d6974`: dia4(david): 3C.5.1 assets generados (PNG del revólver como 2D, VM_Revolver con cuadros, datos del remate, prefabs)
- `d1223f7`: dia4(david): 3C.5.2 revólver: dibujo en el piso, mira medida, manos que no tapan la mira, recarga bala por bala con balas visibles
- `0bcc11b`: esta auditoría (y este ajuste de hashes en el commit siguiente).
