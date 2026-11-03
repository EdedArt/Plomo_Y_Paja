# AUDIT HUD.1: HUD de vida y munición + PlayerAmmo

Fase: la orden pide `Docs/phases/PHASE-HUD.md` y `Docs/guiones/hud.md`. En el repo esos archivos se llaman **`Docs/phases/Fase-HUD.md`** y **`Docs/guiones/Guion-HUD.md`** (sin seguimiento en git; son de David y no los toqué ni los subí). Usé esos dos.
También leídos: `CLAUDE.md` y `Fase-3C-Contador.md` §0 (reglas y contratos de §0.4). Graphify (`graphify-out/graph.json`) para PlayerHealth, ShotgunController, IWeapon, RubberHoseCrosshair, CartoonFx, DialogueUI, GameManager, AudioLibrary, LevelBuilder_NivelPorky y CharacterSpriteImporter. El grafo es anterior a la 3C.5, así que lo completé con búsquedas puntuales de quién llama a cada clase.
Rama: `integracion/dia4`. **Solo HUD.1**: HUD.2, HUD.3 y HUD.4 no se tocaron.

> **Importante:** el código **compila**, pero **los menús no se ejecutaron** y **nada se probó en Play**. Durante la sesión Unity estaba abierto en la misma copia de trabajo: alguien corrió los menús de la 3C.5.2 (ver "Archivos compartidos").

## Orden con la 3C.5 (contrato invertido)
La fase previó que HUD.1 cerrara **antes** de la 3C.5. Pasó al revés: la 3C.5 (y la 3C.5.2) ya estaba hecha, con la reserva del revólver dentro del arma y una fila de munición provisional en la mira. Para cumplir H4 y "no crear una segunda reserva ni un segundo indicador":
- **La reserva del revólver también pasó a `PlayerAmmo`** (`AmmoType.Revolver`), no solo la de la escopeta.
  - Los números son los de la 3C.5.2: **5 cargadas + 15 de reserva** al recogerlo; el tope de 24 es el de la tabla de la fase.
  - El "6 + 12" del contrato quedó viejo: la 3C.5.2 lo cambió a 5 + 15 por los 5 huecos del dibujo.
- **Se quitó la fila provisional de la mira** (`RubberHoseCrosshair`). La munición ahora está **solo** en el HUD.
- La mira **conserva** el aviso y el prompt que usa el recogible del revólver (3C.5).
- `IWeapon` ya tenía `LoadedRounds`, `MagazineSize`, `ReserveRounds`, `IsReloading` y `CancelReload` (3C.5). HUD.1 solo agregó lo que faltaba de H5 (ver abajo).

## Menús que David debe correr (en orden)
1. Abrir Unity y dejar que compile. El importador de personajes sube a la versión 8 y agrega la carpeta `Art/UI/HUD/`.
2. **Plomo y Paja → Crear Prefabs de Prueba**: agrega `PlayerAmmo` al `Player.prefab`.
3. **Plomo y Paja → HUD → Crear HUD**:
   - crea `Prefabs/UI/HUD_Wilbur.prefab`;
   - lo pone en la escena abierta si tiene Player, y en **NivelPorky** (la abre si hace falta);
   - el cuadro lista el arte y los sonidos que faltan.
4. **Ctrl+S** y commit de `HUD_Wilbur.prefab` (+ `.meta`), `Player.prefab` y `NivelPorky.unity`.
5. **Cuando llegue el arte** (`_entrante_hud/HUD/*.png`, fondo magenta): `cd PLOMO_Y_PAJAV_1` y `python Tools/character_key.py --center`. Escribe en `Art/UI/HUD/`, 256×256, centrado. Después, volver a correr **Crear HUD**.

*Generar Nivel Porky* también coloca el HUD (`HudBuilder.PlaceInOpenScene`), así que regenerar el nivel no lo pierde.

## Escenas donde quedó el HUD
- **`NivelPorky`** (el menú y *Generar Nivel Porky*).
- **`SampleScene`** solo si estaba abierta al correr el menú. Es la escena de prueba del Player.
- **El nivel 2 (Campamento) no existe todavía** (Fase 4). Cuando exista, su builder tiene que llamar `HudBuilder.PlaceInOpenScene(null)`, igual que NivelPorky.
- Hoy la colocación **no se ejecutó** (sin Editor).

## PlayerAmmo: ¿la escopeta sigue igual (6 + 18, recarga)? ¿qué se movió?
**Sí, sigue igual.**
- **`PlayerAmmo`** (`Scripts/Player/`, en el Player): reserva + tope por `AmmoType`.
  - Escopeta: **18** al empezar (lo que tenía `ShotgunController.reserveAmmo`), tope **30** (el de su `AddAmmo`).
  - Revólver: **0** al empezar, tope **24**.
  - API: `TryAdd(type, amount, out added)` (false si está lleno), `Take(type, amount)`, `GetReserve`, `GetCap`, `IsFull` y el evento `OnReserveChanged(type, reserve)`.
- **Escopeta:**
  - `ShotgunController` ya no tiene reserva propia: recarga con `PlayerAmmo.Take`, igual que antes (los 6 del cargador, 2.5 s, el clic de cada cartucho) y `AddAmmo` usa `TryAdd`;
  - el campo `reserveAmmo` queda **solo** como respaldo si el Player no tiene `PlayerAmmo`, así sigue funcionando si falta;
  - el UnityEvent `onAmmoChanged` sigue saliendo con (cargados, reserva).
- **Revólver:**
  - `RevolverController` recarga bala por bala sacando de a 1 con `PlayerAmmo.Take` (si se corta, lo metido se queda);
  - `SetAmmo(5, 15)` (al recogerlo o al empezar un nivel con él) **suma** 15 a la reserva, sin pasar del tope de 24. Así la munición de revólver que se junte antes de tener el arma no se pierde (H14, para HUD.3).

## Miembros nuevos de IWeapon y eventos nuevos de PlayerHealth
- **`IWeapon` (H5)**, solo lo que faltaba:
  - `AmmoType AmmoType` (Shotgun / Revolver);
  - `AmmoDisplayStyle DisplayStyle` (Shells / Cylinder);
  - un evento: `OnAmmoChanged`, que avisa cuando cambian las balas cargadas.

  Los enums `AmmoType` y `AmmoDisplayStyle` van en `IWeapon.cs`. La escopeta y el revólver los implementan; `OnAmmoChanged` sale al disparar, al entrar cada bala, al terminar la recarga y con munición nueva.
- **`PlayerHealth`:**
  - ya existían y siguen: `OnHealthChanged(current, max)`, `OnDeath`, `Heal(amount)`;
  - **nuevo `OnDamaged(float amount, Vector3? source)`**: `source` es null si no se conoce;
  - **nuevo `OnHealed(float healed)`**: solo si subió algo (con la vida llena no avisa);
  - **nuevo `TakeDamage(float damage, Vector3 sourcePosition)`**: sobrecarga, y `TakeDamage(float)` sigue igual para todos los que la llaman;
  - **nuevo `IsFullHealth`**: para el botiquín de HUD.3;
  - `Heal` ahora ignora cantidades ≤ 0;
  - al morir activa la razón `Death` y `ResetHealth` la apaga.

## Ocultado del HUD: razones y qué evento maneja cada una (¿se reutilizó un sistema existente?)
**No había un sistema por razones.** INTRO.3 no existe todavía. La 3C.3 usa un solo bool compuesto, `PlayerAim.GameplayBlocked()`, que siguen usando la mira y las manos.
- **Sistema nuevo y reutilizable:** **`UiHideReasons`** (`Scripts/UI/HUD/`).
  - Razones que se componen; el HUD se ve solo si no queda ninguna.
  - `Changed` avisa al HUD.
  - Se limpia al cargar escena.
- **Razones:**

  | Razón | Qué la activa |
  |---|---|
  | **Death** | **Evento**: `PlayerHealth.Die` la activa y `ResetHealth` la apaga |
  | **Pause** | `GameManager.IsGamePaused` (también tapa el game over: el HUD se oculta al morir por Death) |
  | **Dialogue** | `DialogueUI.IsOpen` |
  | **Interrogation** | `InterrogationDirector.IsActive` (escena + epílogo) |
  | **Cinematic** | nadie todavía: es para **INTRO.3** |
  | **Holstered** | nadie todavía: es para **INTRO.3**. Oculta **solo** la fila de munición; la vida queda visible (H6) |

  - **Cómo se leen Pause, Dialogue e Interrogation:** `UiHideReasons.SyncLegacyStates()` los copia una vez por frame. Esos sistemas **no tienen eventos** y son archivos de otras fases (3C y Edgar). Son 3 bool estáticos, sin basura. Si algún día avisan con eventos, llaman a `Set` y se borra su línea.
- **INTRO.3 tiene que usar esta misma clase** (`UiHideReasons.Set(Reason.Cinematic / Holstered, on)`), según H6.
- **Cómo se oculta:** con alpha 0, no con SetActive. El HUD sigue corriendo para que el corazón se apague suave.

## Indicador de dirección: ¿qué fuentes de daño ya mandan la posición?
| Fuente | ¿Manda posición? | Cómo |
|---|---|---|
| **Guardias, cuerpo a cuerpo** (`EnemyAI.PerformAttack`) | **Sí** | `TakeDamage(attackDamage, transform.position)` |
| **Centavo** (`CentavoController`, disparo en combate) | **Sí** | `TakeDamage(damage, muzzle)`: un cambio de una línea; su lógica no se tocó |
| Don Lobo | — | no existe todavía |
| Cualquier otra | No | sigue con `TakeDamage(daño)`: solo destello rojo y cara, sin cuña |

- Los tiros de los enemigos **desde el arma** son HUD.2: ahí pasan a `TakeDamage(daño, boca del cañón)`.
- **Cómo funciona la cuña:** en un anillo de 170 px alrededor del centro. Sigue a la fuente si Wilbur gira, se desvanece en 0.8 s y hay hasta 4 a la vez (pool fijo).

## Fuente usada y ¿se ven ñ, ¡, tildes?
- **Special Elite** (`Assets/_Game/Fonts/SpecialElite SDF.asset`, el de la 3C.3/3C.4.1), asignada por el menú. Si no está, se usa la de TMP.
- **Revisé la tabla del asset** (120 caracteres, estático):
  - **sí** están `¡` (161), `Ó` (211, "MUNICIÓN"), `ñ` (241) y `í` (237), así que "¡RECARGA!" y "SIN MUNICIÓN" se ven en Special Elite;
  - **falta `×`** (215, la del "×18").
- **Cómo se resolvió `×`:** la reserva se escribe `<font="LiberationSans SDF">×</font>18`. Esa `×` sale de LiberationSans (TMP la trae en Resources, con su respaldo dinámico) y el número sale en Special Elite.
- **Para tenerla en Special Elite:** agregar `×` a la lista de caracteres de `InterrogationUIBuilder.EnsureFont`. Es un archivo de la 3C y no lo toqué.

## ¿Se ve bien en 16:9, 16:10 y 21:9? ¿choca con las manos?
**Por cálculo, sin probar.**
- **Escalado:** el canvas va con match = alto, así que el grupo ocupa siempre lo mismo respecto del alto: ~560 × 190 px abajo a la izquierda, con margen 40/34 px. En 21:9 queda igual de pegado a la esquina.
- **Manos:** el viewmodel se ajusta al alto y se apoya abajo a la derecha. Por eso el borde izquierdo del arte se corre según el ancho de la pantalla:

  | Pantalla | ¿Choca? |
  |---|---|
  | **16:9** | **No.** El brazo izquierdo de la escopeta empieza en x ≈ 634 y el grupo termina en ~560 |
  | **21:9** | **No.** El brazo empieza en ~1234 |
  | **16:10** | **Un poco.** El brazo izquierdo de la escopeta empieza en x ≈ 442 y se superpone ~120 px con el final de la barra y de la fila. El HUD se dibuja **encima** (Overlay), así que se lee |
  | **Recarga del revólver** | **Sí.** La mano izquierda pasa por la esquina inferior izquierda en todos los formatos (dura ~1–2 s); el HUD queda encima |

- **Mira:** el HUD va en el orden 4, debajo de la mira (5). Están en lados distintos de la pantalla.

## Ids faltantes y placeholders
**No hay arte del HUD:** no existe `_entrante_hud/`. Todo usa placeholder y el menú lo lista.

| Id | Placeholder |
|---|---|
| `hud_wilbur_01` … `_04` | círculo rosado con la etiqueta **"WILBUR"** |
| `hud_wilbur_dano` | la cara se tiñe de rojo 0.25 s |
| `hud_cartucho_lleno` / `_vacio` | rectángulos redondeados rojo / gris |
| `hud_bala_llena` / `_vacia` | rectángulos redondeados dorado / marrón |
| `hud_barra_marco` | contorno grueso oscuro dibujado por código |
| `sfx_latido` | sin clip: no suena (el latido rojo de los bordes sí se ve) |
| `sfx_hud_aviso` | sin clip: el aviso sale sin sonido |

- **Sonidos de otras subfases:** los demás del guion (zumbido, impactos, botiquín, munición y las líneas de Wilbur) son de HUD.2 y HUD.3; no los usa HUD.1.
- **Herramienta (paso 0):** `Tools/character_key.py --center [carpeta] [--size 256]` es un modo nuevo.
  - Recorte magenta + despill + recorte a lo opaco, en un lienzo **cuadrado y centrado** (no se alinea abajo).
  - La probé con una imagen de prueba: sale centrada y con alpha real.
- **Importación:** `CharacterSpriteImporter` agrega el caso `Art/UI/HUD/` (Sprite, sin compresión, **sin mipmaps**, Bilinear, pivote al centro, versión 8).

## Archivos compartidos tocados (y si estaban limpios en git status)
**Todos estaban limpios** en `git status` antes de editarlos (lo revisé dos veces durante la sesión):
- `IWeapon.cs`, `ShotgunController.cs`, `PlayerHealth.cs`, `EnemyAI.cs` (una línea), `CentavoController.cs` (una línea);
- `CharacterSpriteImporter.cs`, `LevelBuilder_NivelPorky.cs` (una llamada), `TestPrefabGenerator.cs` (agrega `PlayerAmmo`);
- también tocados, sin estar en la lista: `RevolverController.cs` y `RubberHoseCrosshair.cs` (de la 3C.5, limpios).

**Cambios que NO son míos y quedaron sin tocar ni subir** (aparecieron a mitad de la sesión, de una corrida de menús de la 3C.5.2 en el Unity abierto):
- `Art/Viewmodel/VM_Revolver.asset` y `VM_Shotgun.asset`;
- `Prefabs/Player.prefab` y `Prefabs/Level1/Centavo.prefab`;
- `Art/Pickups.meta` y `Art/Pickups/revolver_suelo.png.meta`.

Ojo con el orden: cuando se corra *Crear Prefabs de Prueba* para HUD.1, `Player.prefab` va a cambiar otra vez (`PlayerAmmo`). Conviene subir primero los assets de la 3C.5.2.

**No toqué** `GameManager.cs`, `DialogueUI.cs`, `InterrogationDirector.cs` ni `CLAUDE.md`. Los estados de pausa, diálogo e interrogatorio se leen sin editarlos.

## Qué no se pudo compilar/probar y qué debe revisar David en el Editor
- **Compilado: sí.** Roslyn de Unity 2022.3.62f3 contra los módulos de `Managed/UnityEngine`, `UnityEditor.dll` y `Library/ScriptAssemblies` (TMP incluido): **Assembly-CSharp y Assembly-CSharp-Editor con 0 errores**.
- **No probado:** los menús, el prefab, la colocación y nada en Play.
- **APIs dudosas:**
  - `<font="LiberationSans SDF">` necesita que TMP encuentre ese asset en `Resources/Fonts & Materials/` (la ruta por defecto de TMP Settings).
  - `AudioLibrary.SetActive` desde el menú, para buscar los SFX fuera de Play.
- **Basura por frame:** ninguna a propósito. Sin LINQ y sin Find en Update (el Player se busca una vez, y cada 1 s solo si todavía no apareció). Los textos se rehacen solo cuando cambia el valor y los pools son fijos (4 cuñas, 8 íconos, 3 avisos).
- **Pendiente para HUD.4:** el `NoirPostProcess` existente también pone una viñeta roja al recibir daño (Volume). Con el destello del HUD puede quedar doble; ver si se deja uno.
- **Revisar en Play (con `debug` activo en `HUD_Wilbur`):**
  1. Abajo a la izquierda: cara (placeholder "WILBUR"), barra llena, 6 cartuchos y "×18".
  2. **F5**:
     - daño desde un lado al azar → la cara hace mueca, la barra tiembla, el fantasma baja 0.4 s después, destello rojo;
     - aparece una **cuña** que apunta hacia ese lado y se mueve si giras;
     - con ≤ 25 % de vida, los bordes laten (y el corazón, si hay `sfx_latido`).
  3. **F6:** cura → la barra sube animada con brillo cálido; el latido se va.
  4. **Disparar la escopeta:** el cartucho gastado hace un golpecito; con 2 o menos se ponen rojos.
     - Vacía con reserva: "¡RECARGA!" titilando.
     - Recargar: se llenan en cascada.
     - **F7** (vacía la reserva) + gastar todo → "SIN MUNICIÓN".
  5. **Revólver** (después de recogerlo): la fila cambia a 5 balas deslizándose. Cada bala que entra en la recarga hace golpecito. "×15" al recogerlo.
  6. **Ocultado:**
     - pausa (Esc), diálogo de Colmillo, interrogatorio de Centavo (y su epílogo) → el HUD desaparece y vuelve al salir;
     - al morir, desaparece.
  7. **Mira:** ya no aparece la fila de puntos abajo a la izquierda (3C.5.2); solo queda la del HUD.
  8. **Pantallas:** probar 16:9, 16:10 y 21:9 (Game view). Ver el roce con el brazo de la escopeta en 16:10.
  9. **Reinicio:** al morir y reintentar se recarga la escena, así que vida, munición y HUD vuelven al estado inicial y las razones de ocultado se limpian.

## Archivos creados/modificados
Creados (con `.meta`):
- `Scripts/Player/PlayerAmmo.cs`: reserva por tipo (H4).
- `Scripts/UI/HUD/UiHideReasons.cs`: razones de ocultado reutilizables (H6).
- `Scripts/UI/HUD/HudWilbur.cs`: controlador del HUD (+ `HudUi`, piezas de UI).
- `Scripts/UI/HUD/HudHealthPanel.cs`: cara y barra.
- `Scripts/UI/HUD/HudAmmoRow.cs`: fila de munición y avisos de estado.
- `Scripts/UI/HUD/HudFeedback.cs`: bordes, cuñas de dirección y latido.
- `Scripts/UI/HUD/HudToast.cs`: avisos cortos (para HUD.3 y 3C: `HudWilbur.ShowToast`).
- `Scripts/UI/HUD/HudSprites.cs`: dibujos por código y placeholders.
- `Scripts/Editor/HudBuilder.cs`: menú **Plomo y Paja → HUD → Crear HUD**.
- `Docs/audits/AUDIT_hud_1.md` (este archivo).

Modificados:
- `Scripts/Player/IWeapon.cs`: `AmmoType`, `DisplayStyle`, `OnAmmoChanged` y los enums.
- `Scripts/Player/ShotgunController.cs`: reserva en `PlayerAmmo` (respaldo propio), miembros de H5.
- `Scripts/Player/RevolverController.cs`: reserva en `PlayerAmmo`, miembros de H5, `SetAmmo` suma a la reserva.
- `Scripts/Player/PlayerHealth.cs`: `OnDamaged`, `OnHealed`, `TakeDamage(daño, fuente)`, `IsFullHealth`, razón `Death`.
- `Scripts/UI/RubberHoseCrosshair.cs`: se quitó la fila de munición provisional (quedan aviso y prompt).
- `Scripts/Enemies/EnemyAI.cs`: el golpe manda la posición del guardia.
- `Scripts/Characters/Contador/CentavoController.cs`: el tiro manda la boca del cañón.
- `Scripts/Editor/CharacterSpriteImporter.cs`: caso `Art/UI/HUD/` (versión 8).
- `Scripts/Editor/LevelBuilder_NivelPorky.cs`: *Generar Nivel Porky* coloca el HUD.
- `Scripts/Editor/TestPrefabGenerator.cs`: agrega `PlayerAmmo` al Player.
- `Tools/character_key.py`: modo `--center` (íconos centrados) y `--size`.

## Commits
- `51f4e42`: dia4(david): HUD.1 HUD de vida y munición, PlayerAmmo, razones de ocultado y eventos de daño con posición
- Commit siguiente: esta auditoría.
