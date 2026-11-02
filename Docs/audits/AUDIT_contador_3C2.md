# AUDIT contador 3C.2: Centavo en el mundo (combate, herido, muerte, huida)

Fase: `Docs/phases/Fase-3C-Contador.md`, subfase 3C.2. Leídos: `CLAUDE.md`, la fase (secciones 0 y 1), el guion `Docs/guiones/contador.md` v1.2 y las auditorías `AUDIT_contador_assets.md` y `AUDIT_contador_3C1.md`. Graphify (`graphify update` + `explain`) para `EnemyHealth`, `BillboardSprite`, `CharacterSpriteSet` y `GameStateTracker`.
Rama: `integracion/dia4`.

> **Importante:** Unity no se abrió en esta sesión. El código compila (ver más abajo), pero **nada se probó en Play** y el menú **no se ejecutó**: el prefab `Centavo.prefab` todavía no existe y Centavo todavía no está en la escena. Las respuestas "sí/no" de abajo se refieren a cómo está programado; la prueba real la hace David con la lista del final.

## Antes de programar (0a / 0b)
- **0a: archivos de la 3C.0 en su propio commit.** `8c18846` dia4(david): 3C.0 assets de Centavo (sprites, voces, importadores y croma). Incluye:
  - los 22 sprites de `Art/Characters/contador/` con sus `.meta`;
  - las 21 voces de `Audio/Voices/contador/` y las 8 de Wilbur (`wilbur_int_*`, `wilbur_dec_*`);
  - `CharacterSpriteImporter.cs`, `AudioImporter.cs`, `AudioLibrary.asset` y `.gitignore`;
  - además, porque también son de la 3C.0: `Art/SpriteSets/contador.asset` (lo generó "Reconstruir Sets de Sprites"), `Tools/character_key.py` y su auditoría `Docs/audits/AUDIT_contador_assets.md`.

  No entró nada de `_entrante_*`.
- **0b: lo que generó el menú de la 3C.1.** Revisé `NivelPorky.unity` antes de hacer el commit: estaba guardada a la misma hora que el prefab y el NavMesh (2 nov, 10:49), y tiene la instancia de `ToolShed_Contador` (13 referencias al GUID del prefab), `Patio_Contador`, `Porton_Hoja`, `Valla_Cuarto_*`, `Luz_Luna_Patio_Contador` y `Patrol_A_5`. Commit `64d3cc3` dia4(david): 3C.1 escena generada (cuarto de Centavo, patio, NavMesh): `Prefabs/Level1/` (prefab + `.meta` de la carpeta), las 17 mallas nuevas de `Art/Environment/Generated/` (con sus `.meta`), `NivelPorky.unity` y `Scenes/NivelPorky/NavMesh.asset`.
- **No subí y conviene revisar:**
  - **`.gitignore`:** ignora `PLOMO_Y_PAJAV_1/_entrante_personajes/`, pero la carpeta real se llama **`_entrante_personajes_`** (con guion bajo al final). Por eso sigue apareciendo como "sin seguimiento" en git. No la toqué; hay que cuidar no subirla con un `git add -A`.
  - **Sin commit, no son de esta subfase:** `Art/UI.meta` (carpeta vacía), `Docs/guiones/`, `Docs/phases/Fase-3C-Contador.md` y `Docs/phases/Fase-Intro.md`.

## Sistema de sprites: ¿se extendió el existente? ¿PPU 500 aplicado?
- **Se usa el sistema que ya existía** (`CharacterSpriteSet` + "Reconstruir Sets de Sprites"). No hizo falta cambiar el formato:
  - el set agrupa los archivos `<anim>_<NN>` en una animación por nombre, ordenada por número;
  - cada emoción ya es un par: cuadro 0 = `_01` (boca cerrada) y cuadro 1 = `_02` (boca abierta). Así quedan `herido`, `talk`, `scared`, `nervous`, `plead` y `relief`;
  - `blink`, `death`, `hit`, `peek`, `cover` y `shoot` son de un solo cuadro;
  - `idle` y `limp` son de dos.
- **Extensión en `BillboardSprite`** (los guardias no la usan):
  - `ShowFrame(anim, frame)` muestra un cuadro concreto y lo deja quieto. Lo usan el lip-sync y la cojera.
  - `HasAnim(anim)` y `SetVisible(bool)`. Este último es para D6 en la 3C.3.
  - Por qué no cambia nada en los guardias: el indicador `_manualFrame` solo lo pone `ShowFrame`, así que para ellos `SetAnim` y `UpdateSetAnimation` funcionan exactamente igual que antes.
- **PPU 500 (D12): sí.**
  - En `CharacterSpriteImporter.cs`, el caso `contador` pasó de 250 a **500** px por metro, con pivote Bottom Center y mipmaps.
  - `GetVersion` subió de 5 a **6** para que Unity reimporte los PNG.
  - También se agregó `contador` a `CharacterIds` y `limp` a 4 cuadros/s en `DefaultFps`. Ojo: el set que ya existe conserva sus fps; la cojera no depende de eso porque `CentavoController` la anima a 4 cuadros/s.
- **Faltan cuadros:** ninguno. Están los 22 del guion §7.
- **Medidas a PPU 500** (las saqué del alpha de los PNG):
  - de pie, `idle_01` mide 1004 px, o sea **2.0 m**;
  - `cover_01` mide 890 px (1.78 m), por eso al cubrirse el dibujo baja `coverSink` = 0.4 m y solo asoma la cabeza sobre el banco de 1 m;
  - los cuadros sentados ocupan 1023 px (2.05 m, todo el lienzo).

## Cómo se conecta el daño de la escopeta con Centavo (clase/método) y confirmación de que los guardias no cambiaron
- **Interfaz nueva:** `IDamageable` (`Scripts/Enemies/IDamageable.cs`), con `bool TakeDamage(float)`.
- **`ShotgunController.ShootPellet`:**
  - primero busca `EnemyHealth` en los padres del collider tocado, igual que antes;
  - **solo si no hay `EnemyHealth`** busca un `IDamageable` (con `TryGetDamageable`, que compara como objeto de Unity);
  - si lo encuentra, llama a `TakeDamage(damagePerPellet)`, y si devuelve true dispara `onEnemyKilled`.
- **`CentavoController`** implementa `IDamageable`. Su collider es la cápsula hija `Hitbox`, en la capa Default, que es la que toca el raycast de la escopeta.
- **Los guardias no cambiaron:**
  - el camino de `EnemyHealth` es el mismo código, en el mismo orden;
  - **no se tocaron** `EnemyHealth`, `EnemyAI`, `EnemyCombatFx` ni `EnemyBarks`;
  - en `BillboardSprite` solo se agregaron métodos nuevos y el indicador apagado.
- **Por qué no se reusó `EnemyHealth`:** destruye el objeto a los 3 s, trabaja con vida/umbral y lo escuchan `EnemyAI`, `EnemyBarks` y `RubberHoseCrosshair`. Centavo necesita "primer golpe = herido, nunca muerto" (D3) y quedarse en el piso muerto.

## Estados implementados y cómo se pasa de uno a otro
`CentavoController` (`Scripts/Characters/Contador/`):

| Estado | Qué hace | Sale a |
|---|---|---|
| **Dormant** | `idle` (01/02) en `Anchor_CentavoStand`. Cada 0.2 s comprueba tres cosas: guardias fuera de combate, jugador dentro de la esfera (`ContadorShed.IsInsideActivation`) y línea de vista. | Alert (las tres cosas a la vez) · Hit (si le disparan) |
| **Alert** | `peek_01` + `contador_ver_01` (este corta lo que esté sonando). 1.2 s. | Combat |
| **Combat** | Nunca se mueve. Ciclo: **Cover** (1.0–1.8 s, `cover_01`, cápsula baja, dibujo hundido 0.4 m) → **Peek** (0.4–0.7 s, `peek_01`) → **Shoot** (`shoot_01` 0.3 s, temblor, "pop" y fogonazo) → Cover. Si en Cover no tiene vista, se queda escondido y vuelve a mirar. Tras 6 tiros: **Reload** 2 s cubierto, con `contador_ver_02` la primera vez. `contador_ver_03` al disparar (30 %) y `contador_cubre_01` al cubrirse (35 %, como mucho una vez cada 10 s). | Hit |
| **Hit** (D3) | Primer perdigón, en cualquiera de los tres estados de arriba. `hit_01` 0.35 s, `contador_cae_01` (corta), chorro de sangre `CartoonFx.BloodBurst` + `impacto_carne` + `sangre_chorro` (lo mismo que los guardias), destello blanco, hit-stop de 40 ms, el revólver se suelta y cae con física. El resto del disparo no cuenta (cápsula apagada, `TakeDamage` devuelve false). | Wounded, en `Anchor_CentavoSeated` |
| **Wounded** | Par `herido` con lip-sync, goteo de sangre cada 0.5–0.9 s (`CartoonFx.BloodDrip`, nuevo). `contador_herido_01` una vez cuando tiene vista; `contador_herido_02` cada 8–10 s si el jugador está entre 3 m y el radio de activación (12 m). Jugador a ≤ 3 m con vista → `OnInterrogationRequested` (una vez por acercamiento; se vuelve a armar si se aleja a más de 3.75 m). Tiene 0.5 s de gracia al sentarse. | Dead (D4) · Interrogation (3C.3) |
| **Interrogation** | Lo pide la 3C.3 con `BeginInterrogation()`: calla y no recibe disparos. `ResolveKilled()` / `ResolvePardoned()` lo terminan. | Dead · Fleeing |
| **Dead** | `death_01` fijo (escala sentado), cápsula apagada. | — |
| **Fleeing** | `limp_01/02` alternando a 4 cuadros/s, recorre `Exit_Path_0..3` a 1.2 m/s pegado al piso (rayo hacia abajo), `contador_huye_01` al empezar. | Gone (al final del camino: `SetActive(false)`) |

- **Barks:** salen por su propia AudioSource 3D (la que lee el lip-sync). Ninguno empieza mientras otro suena, y entre dos inicios hay al menos `max(4 s, duración del clip)`. Solo `ver_01`, `cae_01` y `huye_01` cortan lo anterior. Sin clip, el bark se cuenta igual pero en silencio.
- **Puntería:** hitscan desde la mano hacia el pecho del jugador, solo con línea libre. **20 %** de acierto (10 % si corre), 5 de daño con `PlayerHealth.TakeDamage`, alcance 30 m.
- **Disparo:** no hay SFX de revólver en la librería, así que se reusa `escopeta_disparo` con **pitch 1.4** y volumen 0.55 (campos `shotSfx` y `shotPitch` en el Inspector).
- **Debug** (apagado por defecto): con `debug` activo, F9 = matar y F10 = perdonar, solo si está herido. Sirven hasta que exista la 3C.3.
- **Nada usa posiciones fijas:** todas salen de `ContadorShed` (`StandAnchor`, `SeatedAnchor`, `ExitPath`, `ActivationRadius`/`IsInsideActivation`).

## ¿Se activa solo con los guardias fuera de combate? (cómo lo probaste)
**Sí (por código, no probado en Play).**
- `yardGuards` es una lista asignable (`EnemyHealth[]`). El menú la llena con `Guard_Patrulla_A` y `Guard_Centinela`, y avisa si no los encuentra.
- Un guardia cuenta como "fuera de combate" si `IsDead`, si `IsSurrendered`, o si ya fue destruido (los muertos se borran a los 3 s).
- Mientras alguno siga en pie, en Dormant se ignora al jugador. Si le disparan igual, cae herido: D3 dice "cualquier golpe".
- Prueba pendiente para David: puntos 3 y 4 de la lista del final.

## ¿El banco bloquea los perdigones cuando está cubierto? (sí/no)
**Sí (por geometría, no probado en Play).**
- Cubierto, la cápsula mide 0.7 m: termina 0.3 m por debajo del borde del banco (1.0 m) y 0.44 m detrás de su cara trasera. El banco es un `BoxCollider` sólido en la capa Default, así que el raycast choca con él primero.
- Con los ojos del jugador a ~1.7 m del piso, las cuentas dicen que el banco lo cubre desde unos **2.5 m o más**. Más cerca, desde la puerta mirando hacia abajo, un perdigón puede pasar por encima, que es lo normal al disparar por encima de una cobertura.
- Al asomarse y al disparar, la cápsula vuelve a medir 1.8 m.
- Lo que se ve: el dibujo `cover_01` baja 0.4 m (`coverSink`), así que solo asoma la cabeza; esa cabeza no recibe golpes.

## Escala sentado (seatedScale) usada
**0.52.** Los cuadros sentados ocupan los 1023 px del lienzo, que a PPU 500 son 2.05 m, así que con 0.52 queda en **≈ 1.06 m de alto**, apoyado en el banco de 1 m. Eso cumple D12 (1.0–1.1 m). La fase sugería ≈ 0.6, pero eso daría 1.23 m (más alto que el banco); elegí 0.52 por D12. Se puede ajustar en el Inspector del prefab (`seatedScale`). La muerte usa la misma escala (`death_01` sale de la maestra sentada: 886 px, ≈ 0.92 m).

## Prueba del lip-sync en el mundo (herido_01/02)
**No probado** (sin Editor). Cómo funciona:
- `LipSyncDriver` (`Scripts/Interrogation/`) lee la voz con `GetOutputData` en un búfer de 256 reservado una sola vez (sin basura) y calcula el RMS.
- El umbral es `max(0.01, 0.35 × pico del clip en curso)`. Para cerrar, el RMS tiene que bajar a 0.7 × el umbral (histéresis). Cada cambio dura al menos 0.06 s. Todo va en tiempo real (`unscaledDeltaTime`).
- En silencio la boca queda cerrada (`_01`).
- `CentavoController` lee `IsOpen` y le pide `herido` cuadro 0 o 1 al `BillboardSprite`.
- Adaptadores para usar en otros lugares: `LipSyncSpriteAdapter` (SpriteRenderer) y `LipSyncImageAdapter` (Image de UI, para el retrato de la 3C.3).
- **API dudosa:** no sé si `AudioSource.GetOutputData` devuelve la señal antes o después de la atenuación 3D. Como el umbral es relativo al pico del clip, no debería importar, salvo por el mínimo de 0.01 cuando el jugador está lejos. Si a 10–12 m la boca no se abre, bajar `minThreshold`.

## D4 (dispararle herido): resultado en GameStateTracker
- Un perdigón estando herido (y pasados los 0.5 s de gracia) llama a `Die(killHook: true)`. Resultado:
  - `RegisterContadorState(Dead)`: `contador = Dead`;
  - `RegisterEnemyKilled("Centavo")`;
  - el gancho normal de la IA, `AICompanion.Speak(EnemyKilled)` (AICompanion decide si habla, 50 %);
  - `death_01`, sangre, y la cápsula se apaga.
- **No** cambia `ContadorInterrogation` (queda `None`), **no** da `registro_01` y **no** cuenta interrogatorio.
- `ResolveKilled()` (el final de la escena, 3C.4) hace lo mismo pero **sin** la línea de la IA (D11).

## Huida: ¿recorre el camino y desaparece? (sí/no)
**Sí (por código, no probado en Play).**
- `ResolvePardoned()` registra `contador = Pardoned` y empieza la huida:
  - recorre `Exit_Path_0..3` (delante de la puerta, el portón, el patio y el pasillo izquierdo detrás de la casa) a 1.2 m/s;
  - va pegado al piso con un rayo hacia abajo (baja del escalón del cuarto, sigue el terreno);
  - alterna `limp_01/02` a 4 cuadros/s y dice `contador_huye_01`;
  - en el último punto se apaga (estado Gone).
- No usa NavMesh: el camino es recto por el portón.
- Mientras huye no recibe disparos (cápsula apagada), así el "perdonado" no se puede deshacer.

## Campos nuevos de GameStateTracker
- **Enums nuevos** (al final de `GameStateTracker.cs`): `ContadorState { None, Pardoned, Dead }` y `ContadorInterrogation { None, Helpful, Misleading }`.
- **Campos serializados** (se ven en el Inspector): `contadorState`, `contadorInterrogation` y `contadorLied` (bool).
- **Getters:** `Contador`, `ContadorInterrogationResult` y `ContadorLied`.
- **Métodos:** `RegisterContadorState(state)` y `RegisterContadorInterrogation(result, lied)`. La 3C.4 usará el segundo; ninguno cuenta interrogatorios (D10 queda para la 3C.4).
- **Eventos:** `OnContadorStateChanged` y `OnContadorInterrogationChanged`.
- `ResetState()` también los reinicia.
- **Contexto para la IA:**
  - `GetStatusSummary()` agrega una frase sobre Centavo (muerto/perdonado, verdad/datos falsos, si mintió) cuando ya pasó algo con él.
  - `AICompanion.ContextProvider` existía pero **nadie lo asignaba**. Ahora `GameStateTracker.Awake` le asigna `GetStatusSummary`, y `OnDestroy` lo limpia.
  - `AICompanion.cs` no se tocó. Efecto colateral buscado: desde ahora **todas** las líneas de la IA reciben el resumen de pistas, interrogatorios y bajas.

## Línea del Contable (cifra de la deuda)
- **¿Tenía clip?** No.
  - La librería no tiene voces de `guardia_registro` ni del Contable (solo `guardia_*`, `informante_*`, `donlobo`, `porky`, `max`, `narrador`, `contador` y `wilbur`).
  - Además, `DialogueNPC` y `DialogueUI` no reproducen voces en los nodos del árbol.
- **Cambio** (`DialogueDatabase.BuildContable`, nodo `fin_ayuda`):
  - Antes: "Faltan 4.300 billetes, justo la semana en que Porky desapareció. Una sola inicial al margen: P."
  - Ahora: "**Faltan 3.200 pesos, más los intereses de la semana en que Porky desapareció. Una sola inicial al margen: P.**"
  - Mantiene el tono de hombre de números y coincide con `contador_int_1A_v` ("Tres mil doscientos pesos… y los intereses de la semana").
  - Sigue siendo `Helpful` y sin pista (D1). Ningún otro archivo tenía la cifra 4.300.

## Colocación en la escena
- **Menú nuevo: Plomo y Paja → Contador → Colocar Centavo** (`ContadorCentavoBuilder`).
  - Crea o actualiza `Assets/_Game/Prefabs/Level1/Centavo.prefab`.
  - Lo coloca bajo `--- ACTORS ---` en la posición y rotación de `Anchor_CentavoStand`, leídas de `ContadorShed`.
  - Le asigna el cuarto y los dos guardias del patio.
  - **Idempotente:** conserva la instancia que ya exista y borra duplicados y copias desempaquetadas.
- `ContadorYardBuilder.BuildInto` también lo llama al final, así que "Construir patio y cuarto" y **"Generar Nivel Porky" lo mantienen**. El orden sirve porque en `Generate` los guardias ya existen cuando se llama a `BuildInto`.
- **NavMesh:** Centavo no es estático, así que no entra al horneado y **no hace falta re-hornear**.
- **Mixer:** el proyecto no tiene AudioMixer, y la voz de los guardias (`AudioLibrary_EnemyVoice`) sale sin grupo. Por eso Centavo tampoco tiene grupo. Sus fuentes: `spatialBlend` 1, lineal, mínimo 2 m, máximo 25 m. La voz además tiene `ignoreListenerPause` (D5).

## Clases existentes reutilizadas (y por qué)
- **`BillboardSprite`:** el billboard de los guardias (gira hacia la cámara, look sin iluminación, BlobShadow, set de sprites, `PlayDeath`). Se extendió con `ShowFrame`/`SetVisible`.
- **`CharacterSpriteSet` + `CharacterSpriteSetBuilder`:** el set `contador` (ya tenía los pares).
- **`CharacterSpriteImporter`:** PPU 500 en su caso `contador`.
- **`CartoonFx`:** chorro de sangre (`BloodBurst`) y hit-stop, iguales a los de los guardias. Se agregó `BloodDrip` para el goteo.
- **`CharacterLook`:** destello blanco del sprite (`SetFlash`).
- **`AudioLibrary`:** clips de voz y SFX. Se agregaron `GetVoice`, `HasVoice` y `VoiceVolume` porque Centavo necesita su propia fuente para el lip-sync (`TryPlayEnemyVoice` usa una sola fuente compartida).
- **`PlayerHealth.TakeDamage`, `PlayerController.IsRunning/IsMoving`:** daño y puntería.
- **`GameStateTracker`, `AICompanion.Speak(EnemyKilled)`:** registro y gancho de la IA.
- **`ContadorShed` (3C.1):** todas las anclas.
- **`LevelBuilderUtils.EnsureFolder`, `CartoonKit.M`, `EditorMenuGuard`:** igual que los demás builders.
- **Nuevas:**
  - `IDamageable` (no había interfaz de daño);
  - `LipSyncDriver` + 2 adaptadores en `Scripts/Interrogation/` (reusables);
  - `CentavoController` en `Scripts/Characters/Contador/`;
  - `ContadorCentavoBuilder` en `Scripts/Editor/` (la carpeta de herramientas que fija CLAUDE.md).

## Qué no se pudo compilar/probar y qué debe revisar David en el Editor
- **Compilado: sí.** Roslyn de Unity 2022.3.62f3 contra `Library/ScriptAssemblies` y `Managed/UnityEngine`: runtime 0 errores, editor 0 errores. Los únicos warnings nuevos son CS0649 en los `[SerializeField]` de `CentavoController` (los asigna el menú).
- **No probado:** nada en Play, ni el menú, ni la reimportación a PPU 500.
- **APIs dudosas:**
  - `GetOutputData` antes o después de la atenuación 3D (ver lip-sync).
  - `Collider.enabled = false` a mitad del bucle de perdigones: aunque la física lo siguiera viendo, `TakeDamage` en Hit devuelve false.
  - `PrefabUtility.InstantiatePrefab(prefab, scene)`.
- **Menús que David debe correr (en orden):**
  1. Abrir Unity y dejar que recompile y **reimporte los sprites de contador** (la versión del importador subió a 6). En el Inspector de `idle_01.png`, comprobar *Pixels Per Unit = 500*.
  2. **Plomo y Paja → Reconstruir Sets de Sprites**: no debería cambiar nada, pero confirma el set `contador`.
  3. **Plomo y Paja → Contador → Colocar Centavo** (abre NivelPorky si hace falta). Todas las líneas del cuadro deben salir ✓.
  4. **Ctrl+S** y commit de `Prefabs/Level1/Centavo.prefab` (+ `.meta`) y `NivelPorky.unity`.
- **Revisar en Play:**
  1. Con los dos guardias vivos, Centavo respira (idle) detrás del banco y no reacciona aunque Wilbur entre al patio.
  2. Matar o rendir a los dos guardias y acercarse a menos de 12 m del cuarto con vista: suena "¡A-alto ahí!…" y se asoma.
  3. Combate: se cubre, se asoma y dispara con fogonazo y "pop"; casi nunca acierta (5 de daño); recarga tras 6 tiros con "sé contar hasta seis"; los barks no se pisan.
  4. Disparar mientras está cubierto, desde más de 3 m: los perdigones pegan en el banco y no lo hieren. Disparar cuando se asoma: cae.
  5. Al caer: sangre, suelta el revólver, queda sentado delante del banco. **Ajustar `seatedScale`** si no se ve apoyado en el banco, y `coverSink` si al cubrirse se ve demasiado o se esconde entero.
  6. Herido: la boca se mueve con "Ya no puedo ni levantar el revólver…" y repite "¡No me remate!" cada 8–10 s; gotea sangre.
  7. Acercarse a ≤ 3 m: el evento sale (no se ve nada, la 3C.3 lo usa). Con `debug` activo en el prefab: F9 lo mata (queda `death_01`) y F10 lo hace irse cojeando por el portón hasta desaparecer.
  8. D4: herido, dispararle otra vez → `death_01`; en el `GameStateTracker` (Inspector) `contadorState = Dead`, `contadorInterrogation = None`, sin `registro_01`.
  9. "Generar Nivel Porky" sigue creando a Centavo y la consola no muestra el aviso "[Contador] Centavo".

## Archivos creados/modificados
Creados (todos con `.meta`):
- `Scripts/Characters/Contador/CentavoController.cs`: máquina de estados de Centavo.
- `Scripts/Interrogation/LipSyncDriver.cs`, `LipSyncSpriteAdapter.cs`, `LipSyncImageAdapter.cs` (+ `.meta` de la carpeta).
- `Scripts/Enemies/IDamageable.cs`.
- `Scripts/Editor/ContadorCentavoBuilder.cs`: menú "Colocar Centavo" y prefab.
- `Docs/audits/AUDIT_contador_3C2.md` (este archivo).

Modificados:
- `Scripts/Player/ShotgunController.cs`: `IDamageable` si no hay `EnemyHealth`.
- `Scripts/Enemies/BillboardSprite.cs`: `ShowFrame`, `HasAnim` y `SetVisible`.
- `Scripts/Visual/CartoonFx.cs`: `BloodDrip`.
- `Scripts/Audio/AudioLibrary.cs`: `GetVoice`, `HasVoice` y `VoiceVolume`.
- `Scripts/Core/GameStateTracker.cs`: estado de Centavo, eventos y contexto de la IA.
- `Scripts/Editor/CharacterSpriteImporter.cs`: contador a PPU 500, versión 6, id y fps de `limp`.
- `Scripts/Editor/ContadorYardBuilder.cs`: `BuildInto` coloca a Centavo.
- `Scripts/Dialogue/DialogueDatabase.cs`: cifra del Contable.

## Commits
- `8c18846`: dia4(david): 3C.0 assets de Centavo (sprites, voces, importadores y croma) (paso 0a)
- `64d3cc3`: dia4(david): 3C.1 escena generada (cuarto de Centavo, patio, NavMesh) (paso 0b)
- `580d8de`: dia4(david): 3C.2 Centavo en el mundo (combate, herido, muerte, huida)
- Commit siguiente: esta auditoría.
