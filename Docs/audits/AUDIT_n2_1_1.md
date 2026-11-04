# AUDIT N2.1.1 — Managers, foto de entrada, reinicio al morir y arreglos de Campamento

**Unity no se abrió: nada se corrió en el Editor ni en Play.** Solo se verificó que compila. La tabla de pruebas es lectura de código; cada fila la tiene que confirmar David.

Worktree `.claude/worktrees/n2-1-1`, rama `worktree-n2-1-1`, cortada de `integracion/dia4` en `e33f196` (la punta actual). No se subió nada.

## ⚠ Lo que David debe saber primero

1. **`NivelPorky` no tiene ningún objeto recogible (bug previo, no corregido).**
   - `NivelPorky.unity` tiene el grupo `--- PICKUPS ---`, pero ningún `Pickup_*`.
   - `LevelBuilder_NivelPorky` llama `PickupBuilder.PlaceInOpenScene` (línea 110) **antes** de guardar la escena nueva (línea 114). Con la escena sin nombre, `scene.name` es `""`, así que busca `Data/PickupLayout_.asset` en vez de `PickupLayout_NivelPorky.asset`.
   - Por eso existe ese `PickupLayout_.asset` vacío, que entró en el commit `ae68789` de la sincronización. La distribución de N1.2 nunca se aplica.
   - No lo toqué: `LevelBuilder_NivelPorky` está fuera de esta subfase. **Arreglo sugerido para la Sesión A:** guardar la escena con `ScenePath` justo después de `NewScene` (como hace `LevelBuilder_Campamento.OpenOrCreateScene`), borrar `PickupLayout_.asset` y regenerar.
2. **Nada carga `Campamento` desde `NivelPorky` todavía.** `GameManager.LoadNextLevel` no tiene quien lo llame. La foto se toma sea cual sea el código que cargue la escena, pero el paso real entre niveles falta.
3. **Al integrar N1.3 hace falta una línea.** `TutorialDirector._shown` es estática y sobrevive a reiniciar, como se pide, pero "Jugar" no la borra si no se conecta. No toqué esa rama. En `TutorialDirector.ResetStatics` hay que agregar:
   `GameStateTracker.NewGameStarted -= ClearShown; GameStateTracker.NewGameStarted += ClearShown;`, con `private static void ClearShown() => _shown = 0;`. N1.4 (huellas) debe hacer lo mismo.
4. **Cambia lo que decía N2.1:** morir en un nivel ya **no** conserva lo ganado en ese intento. Las pistas de niveles anteriores sí siguen.

---

## 1. Cómo se crean los managers

- `Core/GameManagersBootstrap.cs` (nuevo): `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` crea un solo objeto `GameManagers` con `GameManager`, `GameStateTracker` y `LevelDeathRestart`. Corre con cualquier escena de arranque, así que abrir `NivelPorky` o `Campamento` directo en el Editor funciona igual.
- `DontDestroyOnLoad` e instancia única: los pone el `Awake` que ya tenían los dos managers.
- **Copia de `Campamento`:** el builder la sigue creando. Como la instancia ya existe, su `Awake` la destruye.
- **Esc en `NivelPorky`:** ya hay `GameManager`, y `PauseMenuSpawner` instancia `Resources/PauseMenu_Canvas` (el prefab existe). Por código, la pausa funciona; falta probarla en Play.
- En `MenuPrincipal` e `Intro` el `GameManager` existe, pero `IsSceneWithoutPause` ya bloquea Esc ahí.

## 2. Foto de entrada al nivel (`GameStateTracker`)

**Qué guarda:**

| Dentro de la foto | Fuera de la foto (sobrevive a reiniciar) |
|---|---|
| `cluesCollected` | `introCinematicPlayed` + estática del director |
| `npcsInterrogated` | `weaponDrawn` + estática del holster |
| `aiContextLog` | Llegada de Campamento (estática) |
| `enemiesKilled` | Tutorial (N1.3, estática) |
| `wolfKilled`, `wolfSpared` | Huellas (N1.4, aún no existe) |
| `contadorState`, `contadorInterrogation`, `contadorLied` | |
| `revolverObtainedInScene` | |

- **Cuándo se toma:** en `sceneLoaded`, si la escena es de juego y su nombre es distinto al de la última foto. Es decir, al entrar por progresión. Recargar la misma escena no toma otra. `IsGameplayScene` excluye `MenuPrincipal`, `Intro` y `Final*`. `Start` repite la comprobación por si la primera escena cargó antes de suscribirse; es idempotente.
- **Cuándo se restaura:** en `GameManager.RetryCurrentLevel`, justo antes de recargar. Es la única vía, tanto para la muerte como para Pausa → Reiniciar. Ya no llama `ResetState`. Restaura sin disparar eventos, porque la escena se recarga enseguida.
- **Partida nueva:** `MainMenu.CargarJuego` → `GameManager.StartNewGame` → `GameStateTracker.ResetForNewGame`. Borra progreso, foto, banderas y estáticas, y dispara `NewGameStarted`.
- **Volver al menú** (`GoToMainMenu`): sigue llamando `ResetState`, que ahora también borra la foto.
- **Para Edgar:** `GameStateTracker.IsClueCollected(id)` es estático, de solo lectura y seguro sin tracker. `HasClue(id)` ya existía. `CluePickup` no se tocó.
- **Para N2.2:** un campo nuevo de progreso (como el estado del Lobo) hay que agregarlo a `LevelSnapshot`, `TakeLevelSnapshot`, `RestoreLevelSnapshot` y `ResetState`.
- **No se restaura:** el historial de líneas de `AICompanion` (`recentLines`). Solo evita repeticiones y no afecta los finales.

## 3. Reinicio al morir (`Core/LevelDeathRestart.cs`, nuevo)

- Va en `GameManagers` y se engancha a `PlayerHealth.OnDeath` en cada carga de escena. En escenas sin Player no hace nada.
- **Secuencia:** muerte → `MarkGameOverForRestart` (bloquea la pausa) → 2.5 s reales → fundido a negro de 0.8 s → `RetryCurrentLevel` → fundido desde negro de 0.6 s.
- El fundido es un Canvas propio creado por código (orden 90, sobre el HUD), porque el objeto sobrevive a la recarga. Queda apagado cuando no se usa.
- **Nivel 1:** al recargar, `IntroCinematicDirector.HasPlayed` es verdadero, así que no hay taxi; el holster empieza listo y Wilbur aparece en su `PlayerSpawn` (I6).
- **Quitado de `CampamentoLevelStart`:** `HandleDeath`, `ReloadAfterDeath` y sus campos. El builder no los referenciaba.

## 4. Líneas de respaldo de la llegada

- Las 3 líneas están en `Docs/guiones/nivel2.md` §1 y en `CampamentoLevelStart.fallbackThoughts` (al azar).
- Salen si no hay key, si hay error, si la respuesta viene vacía o si pasan más de 6 s (`thoughtTimeout`). Una respuesta tardía no se muestra.
- Sin `AICompanion` en la escena, la línea sale con subtítulo.

## 5. Objetos de `Campamento`

Cada entrada lleva `container` y la altura de su superficie, calculada de las medidas del builder:

| Id | Altura | Container |
|---|---|---|
| `Cartuchos_Barracon` | 1.05 | Barracón A: repisa alta del estante |
| `Botiquin_Carpa` | 0.08 | Carpa A: cajón abierto de la entrada |
| `Cartuchos_Aserradero` | 0.90 | Aserradero: banco de trabajo junto a la sierra |
| `Cartuchos_Pasillo` | 0.85 | Pasillo: consola |
| `Botiquin_Oficina` | 1.05 | Oficina: repisa alta del estante |
| `Balas_Oficina` | 0.08 | Oficina: cajón abierto |
| `Cartuchos_Gabinete` | 1.05 | Salón: gabinete, muro oeste |
| `Balas_Barra` | 0.45 | Salón: repisa baja detrás de la barra |

- **El asset no existe en el repo:** `PickupLayout_Campamento.asset` lo crea el menú. Si en una copia ya existe sin `container`, `FillMissingContainers` completa esas entradas al regenerar y no toca las que ya tienen mueble. No se editó ningún `.asset` a mano.
- **`ValidateSpots`:** ahora corre dentro de *Generar Campamento*, después de hornear el NavMesh. **No se ha ejecutado.** Parte de (0, 0, -15), un punto fijo pensado para el Nivel 1 que aquí cae en el sendero del bosque.
- **Drops de guardias:** `PlaceInOpenScene` crea el spawner con `dropsEnabled` apagado desde N1.2, así que "HUD → Colocar objetos" ya no los prende. Quité del builder el `SetActive(false)`, que ya sobraba.

## Tabla de pruebas (por lectura de código, **sin ejecutar**)

| Prueba | Resultado esperado | ¿Probado en Play? |
|---|---|---|
| Morir en N1 después de matar a Centavo | Fundido, recarga en la acera sin taxi y armado; Centavo vivo otra vez, `contadorState` en `None`, muertes y revólver como al entrar | No |
| Morir en N2 después de tomar una pista | Vuelve a `Campamento` sin la línea de llegada; esa pista ya no está y se puede tomar otra vez; las de N1 y el revólver siguen | No |
| Pausa → Reiniciar (N1 o N2) | Sin pausa al recargar y con la misma foto; sin fundido de muerte | No |
| Partida nueva desde el menú | Pistas y banderas en cero; salen taxi, escopeta enfundada y llegada otra vez | No (el tutorial necesita la línea del punto 3) |
| Abrir `NivelPorky` o `Campamento` directo | Managers creados, Esc pausa, foto vacía al entrar | No |
| API caída o lenta en la llegada | Una de las 3 líneas de respaldo | No |

## Archivos tocados

**Creados:** `Scripts/Core/GameManagersBootstrap.cs`, `Scripts/Core/LevelDeathRestart.cs`, este audit. Los `.meta` de los dos scripts los crea Unity al abrir; hay que subirlos.

**Modificados** (todos estaban limpios en `git status` al empezar):
- `Scripts/Core/GameStateTracker.cs` — foto, `ResetForNewGame`, `IsClueCollected`
- `Scripts/Core/GameManager.cs` — `RetryCurrentLevel`, `MarkGameOverForRestart`, `StartNewGame`
- `Scripts/UI/MainMenu.cs` — una línea en `CargarJuego`
- `Scripts/Intro/IntroCinematicDirector.cs` — `ResetSession`, al final de la clase para no chocar con N1.3, que toca este archivo
- `Scripts/Player/PlayerWeaponHolster.cs` — `ResetSession`
- `Scripts/Level/Campamento/CampamentoLevelStart.cs` — sin reinicio, con respaldo
- `Scripts/Editor/LevelBuilder_Campamento.cs` — semilla con `container`, `FillMissingContainers`, `ValidateSpots`
- `Docs/guiones/nivel2.md` — §1 Llegada

**No tocados:** `LevelBuilder_NivelPorky`, `ContadorYardBuilder`, `NivelPorky.unity`, `CluePickup`, datos de pistas, `PickupBuilder`.

## Compilación

Roslyn de Unity 2022.3.62f3, con las referencias de los `.csproj` de la copia principal y las fuentes de este worktree tomadas del disco (los `.csproj` están atrasados). `Assembly-CSharp`: 115 fuentes, **0 errores**. `Assembly-CSharp-Editor`: 41 fuentes, **0 errores**. Avisos ocultos, como en audits anteriores.

## Qué debe revisar David en el Editor

1. Integrar la rama y abrir Unity (crea los `.meta`).
2. **Plomo y Paja → Nivel 2 → Generar Campamento.** Sin esto `Campamento` no está en Build Settings y recargarla por nombre falla. Leer en la consola `✓ Objetos` de `ValidateSpots`, y que no haya `⚠` de objetos flotando o en el piso.
3. Correr las 6 filas de la tabla y mirar los logs `[GameState] Foto de entrada…` y `Foto … restaurada`.
4. Esc en `NivelPorky`, también durante un diálogo con guardias: `GameManager.Update` no bloquea la pausa con `DialogueUI` abierto; viene de antes y no lo cambié.
5. Cada objeto apoyado en su mueble, y que "HUD → Colocar objetos" no cambie nada de los drops.
6. Decidir los puntos 1 a 3 del inicio.

## Commits

En `worktree-n2-1-1`, sin integrar y sin subir. Ver `git log e33f196..worktree-n2-1-1`:
- `dia4(david): N2.1.1 managers en toda escena, foto de entrada al nivel, reinicio al morir y respaldo de la llegada`
- `dia4(david): N2.1.1 objetos de Campamento con mueble y altura, ValidateSpots y lineas de respaldo en el guion`
- `dia4(david): N2.1.1 auditoria`
