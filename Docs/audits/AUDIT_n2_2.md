# AUDIT N2.2 — Don Lobo: la pelea final

**Unity no se abrió: nada se corrió en el Editor ni en Play.** Solo se verificó que compila y que el modo `Lobo` de `character_key.py` funciona con PNG de prueba. Todos los números de tiempo son cuentas sobre el código, no mediciones.

Worktree `.claude/worktrees/n2-2`, rama `worktree-n2-2`, sobre `integracion/dia4` en `f12d251`. No se subió nada.

**Se hizo el diseño completo; no se aplicó el Plan B** (hay cambios de cubierta y refuerzos). Si en la prueba fallan, el Plan B ya sale casi solo: sin NavMesh, o con `coverAnchors` vacío, se queda en su cubierta; sin `reinforcementAnchors`, no hay refuerzos.

## ⚠ Lo que David debe saber primero

1. **No hay ningún dibujo ni voz de Don Lobo.** No existen `_entrante_personajes/Lobo/`, `Art/Characters/lobo/` ni `Audio/Voices/lobo/`. Mientras tanto usa los dibujos viejos de `donlobo` (idle, shoot, surrender, talk), con aviso: no se ve cubrirse, llamar ni caer de rodillas de verdad, y la pelea es muda.
2. **"Bajar el arma" todavía no se puede alcanzar.** La confrontación es N2.3. Al llegar a ≤ 3 m se dispara `OnConfrontationRequested`, pero nadie la atiende (avisa una vez en consola). Hoy, la única salida de la pelea es rematarlo.
3. **Toqué `EnemyAI` (un método), aunque L3 dice "sin código de enemigo nuevo".** `CallToFight()`, 4 líneas: es la única forma limpia de que los refuerzos lleguen "ya persiguiendo" (N2.2 paso 2). Sin él habría que hacerles 0.01 de daño para despertarlos. No cambia a ningún guardia que no lo llame.
4. **`FinaleResolver` se adelantó de N2.3 a aquí**, porque pediste no soltar la llamada al final. Es la función pura de L8 y `GoToFinale`. A N2.3 le queda el menú "Probar resolución" y comparar con `FinaleTrigger`.
5. **El tope por disparo es una regla nueva, no estaba en la fase.** Con una sola vida es imposible cumplir los dos objetivos de balance: 10 balas son 240 de daño, y eso son solo 2 cartuchos a quemarropa (8 × 15 = 120). Ver *Números*.

---

- **Sprites y voces: ¿cuáles faltan? (placeholders usados)**
  - **Dibujos (faltan los 16), en `Art/Characters/lobo/`:** `idle_01`, `idle_02`, `peek_01`, `cover_01`, `shoot_01`, `call_01`, `hit_01`, `rodillas_01`, `rodillas_02`, `talk_01`, `talk_02`, `angry_01`, `angry_02`, `laugh_01`, `laugh_02`, `death_01`.
  - **Voces de la pelea (faltan las 7), en `Audio/Voices/lobo/`:** `lobo_combate_01` … `lobo_combate_05`, `lobo_cae_01`, `lobo_rodillas_01`. Las otras 11 del guion son de N2.3. Sin clip no suena nada y no se rompe nada.
  - **Placeholders, en orden:** set `lobo` → set viejo `donlobo` (con aviso) → cápsula morada. Un cuadro que falte muestra `idle` (`BillboardSprite.ResolveClip`). De rodillas sin `rodillas` usa `surrender`. Muerto sin `death_01`, el dibujo queda aplastado.
  - **Importador:** caso `lobo` a 500 PPU y pivote abajo (como Centavo), versión 15. `lobo` agregado a `CharacterIds`. Las voces no necesitaron cambios: `AudioLibraryBuilder` lee `Audio/Voices` con subcarpetas.
  - **`character_key.py` no tenía modo `Lobo`** (la fase lo daba por hecho). Lo agregué. Probado con 6 PNG sintéticos sobre magenta: lienzo 1024, pares `_02` alineados, base en la fila 1023. Avisa si falta la carpeta. Los originales deben ir en `_entrante_personajes/Lobo/`.

- **Estados y cómo se pasa de uno a otro:**

  | Estado | Qué hace | Sale a |
  |---|---|---|
  | **Dormant** | `idle` detrás del escritorio en `Anchor_LoboStart`, un poco hundido; ignora al jugador | **Intro**, con `Trigger_Salon` o con el primer tiro |
  | **Intro** | `lobo_combate_01`, se pone de pie (1.6 s o lo que dure la voz) | **Combat** |
  | **Combat** | Cover → Peek → Telegraph → Burst → Cover. Move cada 2 ráfagas. Call una vez | **Hit**, al llegar al umbral |
  | **Hit** | `hit_01` 0.35 s, `lobo_cae_01`, suelta la pistola, los refuerzos se rinden | **Kneeling** |
  | **Kneeling** | Par `rodillas` con lip-sync, sangre, `lobo_rodillas_01` una vez (con vista y lejos). Jugador a ≤ 3 m con vista → `OnConfrontationRequested` | **Dead** si le disparan; **Confrontation** con `BeginConfrontation()` |
  | **Confrontation** | No recibe balas | `ResolveKilled()` → **Dead**; `ResolvePardoned()` → **Pardoned** |
  | **Dead** | `death_01`, `LoboState.Dead`, y a los 2.5 s `FinaleResolver.GoToFinale()` | — |
  | **Pardoned** | Sigue de rodillas, `LoboState.Pardoned`, y a los 2.5 s el final | — |

  - **Move:** va por el NavMesh a 2.5 m/s y solo a una cubierta a ≥ 3.5 m del jugador, cuyo camino recto no pase a < 2 m de él. Si caminando lo tiene a < 2 m, frena y dispara desde ahí.
  - **Si no lo ve:** espera, y a la segunda revisión busca otra cubierta.
  - **Para N2.3:** llamar `BeginConfrontation()` **antes** de `director.Enter`; ese método también pone `Anchor_ConfrontationCam` delante de él, porque cae donde le toque. Implementa `IInterrogationTarget` (`SpeakerName` = "DON LOBO"). Sin voces del mundo con la escena abierta.

- **Números: vida, umbral de rodillas, daño, cadencia, probabilidad; tiempo para tumbarlo con cada arma y munición disponible:** todos serializados en `LoboBossController`.

  | Parámetro | Valor |
  |---|---|
  | Vida | 280 |
  | Umbral de rodillas | 15 % (42 de vida): hay que quitarle **238** |
  | Llamada a refuerzos | 50 % (140 de vida) |
  | **Tope por disparo** | **48** (suma lo que llega en el mismo cuadro: los perdigones de un cartucho) |
  | Espera en cubierta | 2.0–2.8 s; segunda fase ×0.75 (1.5–2.1 s) |
  | Asomarse / aviso | 0.3–0.5 s / 0.4 s (cuadro de apuntar + brillo dorado) |
  | Ráfaga | 3 tiros, 0.15 s entre uno y otro |
  | Puntería | 30 %; 15 % si el jugador corre |
  | Daño | 7 por bala |

  | Arma | Daño por disparo | Disparos para tumbarlo | Objetivo | Tiempo mínimo disparando | Munición disponible (con L5) |
  |---|---|---|---|---|---|
  | Revólver | 24 | **10 aciertos** | 9–11 ✓ | 4.5 s + 2 recargas | 20 de inicio + 2 cajas (6 c/u) = 32 |
  | Escopeta, a quemarropa (≤ 6 m) | 120 → tope 48 | **5 cartuchos** | 4–6 ✓ | 4.8 s + quizá 1 recarga | 30 de inicio + 4 cajas (6 c/u, tope 30) |
  | Escopeta a ~10 m | ~59 con 5 perdigones → tope 48 | 5 | | | |
  | Escopeta a ~15 m | ~31 con 4 perdigones | 8 | | | |

  - **Pelea real (cuenta sobre el código, sin probar):** solo está de pie unos 1.3 s de cada ~3.7 s. Si no lo flanquean, son 4–6 ciclos: **unos 20–30 s**. Flanquearlo acorta eso, y él se mueve para evitarlo.
  - **Daño que recibe Wilbur:** ~6 por ráfaga si está quieto, **~40–50 en total** (de 100), más los dos guardias. Hay 1 botiquín antes del salón.
  - **Ajustes si se siente mal:** `maxHealth` y `maxDamagePerShot`. El tope también limita a quien dispare el revólver, pero 24 nunca lo alcanza.

- **Refuerzos: ¿aparecen al 50 %? ¿se rinden cuando él cae?**
  - **Por código, sí a las dos; sin probar.** Al cruzar 140 de vida: `call_01`, `lobo_combate_04`, 2 `Enemy_Guard` en `Anchor_Refuerzo_A/B`, y un cuadro después `CallToFight()`. Si está en plena ráfaga, la termina primero.
  - Al caer de rodillas: `TriggerSurrender()` a los vivos. Quedan quietos y se les puede matar.
  - **Solo se rinde su IA:** `EnemyHealth.IsSurrendered` queda en falso y no tienen `DialogueNPC`, así que no se les interroga. No contarán para L8. Tampoco sueltan objetos (drops apagados).
  - Las anclas están apagadas (N2.1); el builder las encuentra con `Transform.Find`, que sí ve objetos apagados.

- **¿El escritorio y las columnas bloquean balas en ambos sentidos?** **Por código, sí; sin probar.**
  - Sus colliders son macizos y lo primero que toca el rayo se lleva el tiro, tanto en `ShotgunController`/`RevolverController` como en `EnemyGunfire`.
  - Cubierto, su cápsula mide 0.84 m (0.7 × 1.2): menos que el escritorio (1.1 m) y el billar (0.9 m). De pie, 2.16 m.
  - **Con flanqueo sí se le da**, a propósito: las anclas lo cubren del lado de la entrada, no de todos lados. Las columnas lo tapan solo desde el sur.

- **Disparo: ¿EnemyGunfire (HUD.2) o el camino de Centavo?** `EnemyGunfire`, el mismo de Centavo y los guardias. Puntos en `Data/MuzzlePoints/Muzzle_lobo.asset` (la fase dice `MuzzlePointSet_Lobo`; seguí el nombre que usa `MuzzlePointsBuilder`), con respaldo (0.55, 0.55) puesto a ojo. Hay que ajustarlo con la ventana de puntos cuando llegue `shoot_01`. Sonido: `sfx_disparo_guardia`.

- **Rematarlo de rodillas: resultado en GameStateTracker:**
  - `LoboState.Dead`, por `RegisterLoboState` y el evento `OnLoboStateChanged`. Se reusan `wolfKilled`/`wolfSpared`, **que ya están en la foto de N2.1.1**: no hubo que agregar campos a `LevelSnapshot`. `Lobo` es una propiedad derivada de ellos.
  - También `RegisterEnemyKilled("Don Lobo")`, el comentario normal de baja de la IA, y a los 2.5 s reales `GoToFinale()`.
  - Los otros perdigones del disparo que lo tumba no lo rematan: sin collider durante Hit y 0.5 s de gracia después.
  - **Mientras no exista la escena `Final` (N2.4), el final solo sale en consola** (`[Final] Final resuelto: …`) y el juego se queda en el salón.
  - El estado del Lobo va en el contexto de la IA (`GetStatusSummary`).
  - **Morir en la pelea:** `LevelDeathRestart` recarga, la foto deja el estado en `None` y él vuelve a Dormant con la vida llena.

- **Mapa ASCII del salón (coberturas, anclas, refuerzos, objetos):** norte arriba, 1 columna = 0.5 m, 1 fila = 1 m.

  ```
   74 HHHHHHHHHHHHHHHHHHHXHHHHHHHHHHHHHHHHHHH
   73  H                                   H
   72  H              0  L                 H
   71  H       2     EEEEEEEEE     1       H
   70  P A     |     EEEEEEEEE     |     B P
   69  H                                   H
   68  H                 C                 H
   67  H                             BB    H
   66  H  M3M                        BB   SH
   65  H  MMM                        BB   SH
   64  H  MMM  |                   | BB   SH
   63  H  MMM                        BB   SH
   62  Hc            ttttttttt       BB   bH
   61  HG            ttttttttt  YYYY BB    H
   60 HHHHHHHHHHHHHHHHHH///HHHHHHHHHHHHHHHHHH
       |     |     |     |     |     |     | 
      x = -9, -6, -3, 0, 3, 6, 9
  ```

  **Leyenda.** `L` `Anchor_LoboStart` · `0 1 2 3` `Anchor_LoboCover_*` (0 = escritorio) · `A B` `Anchor_Refuerzo_A/B` · `P` puertas laterales · `C` `Anchor_ConfrontationCam` · `t` `Trigger_Salon` · `X` `Door_CuartoAtras` · `/` entrada · `E` escritorio · `|` columna · `B` barra · `S` estante detrás de la barra · `M` billar · `Y` sofá · `G` gabinete · `c` cartuchos · `b` balas.

  **Mapa de `Campamento`:** sin cambios de geometría desde N2.1 (norte arriba, 1 columna = 1 m, 1 fila = 2 m). Don Lobo está en `L`. La leyenda completa está en `AUDIT_n2_1.md`.

  ```
    80 
                          HHHHHHH                 ~~~~~~
                          H     H                 ~~~~~~
                    HHHHHHHHHXHHHHHHHHH           ~~~~~~
                    H   2   0L    1   H           ~~~~~~
    70              HR  |   EEE   |  RH           ~~~~~~
                    H        C        H           ~~~~~~
                    H 3            B  H           ~~~~~~
                    H M |         |B  H           ~~~~~~
                    c        t     B  b           ~~~~~~
    60            HHHHHHHHHHH/HHbYHHHHHHH         ~~~~~~
                  H        H   H    A   H         ~~~~~~
                  H        Hc  /    e   +         ~~~~~~
       vvv       THHHHHHHHHHH/HHHHHHHHHHHT        ~~~~~~
       vvv       T              D        T        ~~~~~~
    50 vvv      T                         T       ~~~~~~
       vvv      !                         =       ~~~~~~
       vvv      !         S               ==      ~~~~~~
       vvv     !    o           o    Z     =      ~~~~~~
       vvv     !                     Z     =      ~~~~~~
    40 vvv     !         o             c   ==     ~~~~~~
       vvv    !!                            =     ~~~~~~
       vvv   !!      o               o       =    ~~~~~~
       vvv   !          o        o           ==   ~~~~~~
       vvv  !!                                =   ~~~~~~
    30 vvv !!                                  =  ~~~~~~
       vvv !           ............. HHHHHHHHH =  ~~~~~~
       vvv ! HHHHHHHcH .           . H       H =  ~~~~~~
       vvv ! H   a   H .           . H       H =  ~~~~~~
       vvv ! H       H . K         . H       H =  ~~~~~~
    20 vvv ! HHHH/HHHH .         G . HHHHHHHHH =  ~~~~~~
       vvv !           .           .    ^      =  ~~~~~~
       vvv !     ^     .     F     .           =  ~~~~~~
       vvv !           .           .           =  ~~~~~~
       vvv ! HHHHHHHHH .           .       ^   =  ~~~~~~
    10 vvv ! H       H .           .       +   =  ~~~~~~
       vvv ! HHHHHHHHH P............           =  ~~~~~~
       vvv !                                   =  ~~~~~~
       vvv #########   ...........     #########  ~~~~~~
                   #   .         .     #          ~~~~~~
     0             #   .         .  U  #          ~~~~~~
                   #   . S       .     #          ~~~~~~
                   #   .         .  U  #          ~~~~~~
                   #   .         .     #          ~~~~~~
                   #   ..........P     #          ~~~~~~
   -10             #####T###///###T#####          ~~~~~~
                        T         T               ~~~~~~
                        T         T               ~~~~~~
                        T         TT              ~~~~~~
                        T         T               ~~~~~~
   -20                  T         T               ~~~~~~
                        T         T               ~~~~~~
                        T         T               ~~~~~~
                       TT         T               ~~~~~~
                       T          T               ~~~~~~
   -30                 TT         T               ~~~~~~
                        T         T               ~~~~~~
                        T         TT              ~~~~~~
                        T          T              ~~~~~~
                        T          T              ~~~~~~
   -40                  T          T              ~~~~~~
                        T          T              ~~~~~~
                        T         TT              ~~~~~~
                        T         T               ~~~~~~
                        T    @    T               ~~~~~~
   -50                 TTTTTTTTTTTT               ~~~~~~
       
         |         |         |         |         |      
       -20       -10       0         10        20
  ```

- **Menús que David debe correr:**
  1. Integrar la rama.
  2. **Si `Campamento` ya está generado: Plomo y Paja → Nivel 2 → Colocar Don Lobo**, y Ctrl+S. **Si no: Generar Campamento**, que ya lo incluye.
  3. Leer la consola `[Lobo]` / `[Campamento]`: lista lo que falta.
  4. **Cuando llegue el arte:** poner los PNG en `_entrante_personajes/Lobo/` → desde `PLOMO_Y_PAJAV_1/`: `python Tools/character_key.py Lobo` → **Reconstruir Sets de Sprites** → **Colocar Don Lobo**. Voces en `Audio/Voices/lobo/` → **Reconstruir Librería de Audio**.

- **Archivos compartidos tocados (y si estaban limpios en git status):** todos limpios, y ninguno cambió en `integracion/dia4` mientras trabajaba (verifiqué con `git diff bde1fb3 integracion/dia4`). Están en su propio commit.
  - `Core/GameStateTracker.cs` (dueña: B) — `LoboState`, `Lobo`, `OnLoboStateChanged`, `RegisterLoboState`, resumen para la IA.
  - `Editor/CharacterSpriteImporter.cs` (compartido) — caso `lobo`, versión 15, `CharacterIds`. **Si N1.4 agrega `huellas`, puede chocar en la línea de la versión:** quedarse con el número más alto y subirlo uno.
  - `Enemies/EnemyAI.cs` — `CallToFight()`.
  - `Editor/MuzzlePointWindow.cs` — `lobo` en `Shooters`.
  - `Tools/character_key.py` — modo `Lobo`. El camino de Contador no cambia salvo el aviso si no existe la carpeta.
  - `Editor/LevelBuilder_Campamento.cs` (mío) — una línea.
  - No tocados: `LevelBuilder_NivelPorky`, `ContadorYardBuilder`, `NivelPorky`, `CentavoController`, interrogatorio, `AICompanion`.

- **Qué no se pudo compilar/probar y qué debe revisar David en el Editor:**
  - **Compilación:** Roslyn de Unity 2022.3.62f3, referencias de los `.csproj` de la copia principal, fuentes de este worktree tras el rebase. `Assembly-CSharp`: 122 fuentes, **0 errores**. `Assembly-CSharp-Editor`: 44 fuentes, **0 errores**. Avisos CS0649, CS0169, CS0414, CS0618 y CS0612 ocultos.
  - **Nada se ejecutó.** En Play, en este orden:
    1. Entrar al salón: habla, se para y pasa a pelear. Tirarle dormido también lo despierta.
    2. Aviso visible (0.4 s y brillo) antes de cada ráfaga. Los gritos no se pisan.
    3. **Cambios de cubierta: que camine de verdad y no atraviese muebles ni al jugador.** Es lo más riesgoso que no pude ver. Si falla, ver el Plan B arriba.
    4. Cubierto: que el escritorio se lleve las balas. Revisar `coverSink` con el dibujo `cover_01` real.
    5. Al 50 %: los dos refuerzos aparecen, persiguen y no se traban en las puertas.
    6. Tumbarlo: 10 balas o 5 cartuchos de cerca. Los refuerzos se rinden y la pistola cae.
    7. De rodillas: tamaño (`kneelScale` 0.6) y que un mismo cartucho no lo remate.
    8. A ≤ 3 m sale el aviso `[Lobo] Pidió la confrontación…`. Rematarlo: `[GameState]` con "MUERTO" y `[Final] Final resuelto`.
    9. Morir en la pelea: recarga con todo en su sitio.
  - Los `.meta` de los archivos nuevos (incluidas las carpetas `Characters/Lobo` y `Finale`) los crea Unity; hay que subirlos.

- **Archivos creados/modificados:**
  - Creados: `Scripts/Characters/Lobo/LoboBossController.cs`, `Scripts/Editor/LoboBossBuilder.cs`, `Scripts/Finale/FinaleResolver.cs`, este audit.
  - Modificados: los de *Archivos compartidos*.
  - **Reutilizados:** `CentavoController` (como modelo: estados, golpe, voz, poses), `EnemyGunfire`, `CombatFx`, `CartoonFx`, `BillboardSprite`, `LipSyncDriver`, `CharacterLook`, `IDamageable`, `IInterrogationTarget`, `MuzzlePointsBuilder`, `CharacterSpriteSetBuilder`, `AudioLibrary`, `Enemy_Guard`.
  - **Lo crearán los menús:** `Prefabs/Level2/DonLobo.prefab`, `Data/MuzzlePoints/Muzzle_lobo.asset`, el set de sprites `lobo` y las carpetas `Art/Characters/lobo` y `Audio/Voices/lobo`.

- **Commits:** en `worktree-n2-2`, sin integrar y sin subir:
  - `eb8c0c8` — dia4(david): N2.2 LoboState en el tracker, caso lobo en importador y puntos de cañon, CallToFight en EnemyAI y modo Lobo en character_key
  - `1ce834c` — dia4(david): N2.2 Don Lobo: LoboBossController, FinaleResolver minimo y menu Colocar Don Lobo
  - El de este audit: ver `git log worktree-n2-2`.
  - Para integrar, desde la copia principal: `git merge --ff-only worktree-n2-2`. Si `integracion/dia4` ya avanzó: `git cherry-pick integracion/dia4..worktree-n2-2`.
