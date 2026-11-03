# AUDIT HUD.2: disparos enemigos desde el arma + guardias con pistola (H18)

Fase: `Docs/phases/Fase-HUD.md` (v1.1), guion `Docs/guiones/Guion-HUD.md`, `Docs/audits/AUDIT_hud_1.md`, `CLAUDE.md` y los contratos de `Fase-3C-Contador.md` §0.4.
Graphify (`graphify-out/graph.json`) para los sistemas de "Touches". El grafo es anterior a la 3C.5, así que lo completé con búsquedas puntuales de quién llama a `PlayerHealth.TakeDamage`, `BillboardSprite`, `ShotgunFeedback` y `CartoonFx`.
Rama: `integracion/dia4`. **Unity estuvo cerrado toda la sesión.** **Solo HUD.2**: HUD.3 no se empezó.

> **Importante:** el código **compila** (ver abajo), pero **los menús no se ejecutaron** y **nada se probó en Play**.

## Tabla de quienes hacen daño (clase, cuerpo a cuerpo o disparo, antes y ahora)
| Quién | Clase | Antes | Ahora (HUD.2) | Cuadros de ataque | SFX |
|---|---|---|---|---|---|
| **Guardias normales** (`Enemy_Guard`) | `EnemyAI.PerformAttack` | **Solo golpe**: a ≤ 2 m, 15 de daño cada 1.5 s, sin chance de fallar; pose `shoot` | **Golpe igual** (2 m, 15, 1.5 s) **+ pistola (H18)**: se planta a 6–14 m (al azar en cada persecución), alcance 18 m, aviso 0.35 s, un tiro cada 1.4–2.2 s, 30 % de acierto (15 % si Wilbur corre), 8 de daño. El tiro sale del cañón (`EnemyGunfire`) | `shoot_01` (512 px, con el fogonazo **dibujado**), `idle_01..04`, `walk_01`. **No hay** cuadro de apuntar ni de disparo sin fogonazo | No había. Ahora `sfx_disparo_guardia` (**falta**) → `escopeta_disparo` con pitch 1.5 |
| **Guardias interrogables** (`Enemy_Interrogable`, Colmillo) | variante del prefab del guardia | Igual que el guardia | Igual que el guardia (heredan `EnemyGunfire`). Rendidos no disparan | mismo set `guardia` | igual |
| **Centavo** | `CentavoController.FireShot` | Rayo desde un punto **fijo** (1.35 m de alto + 0.35 m hacia el jugador). 20 % (10 % corriendo), 5 de daño, 30 m. Fogonazo = luz puntual; "pop" = escopeta a pitch 1.4 | **Mismos números** (20 % / 10 %, 5, 30 m, mismo "pop" a 1.4 y volumen 0.55). El fogonazo, la estela y el rayo de daño salen del **cañón del dibujo `shoot_01`** (`EnemyGunfire`) | `shoot_01` (1024, fogonazo dibujado), `peek_01`, `cover_01` | `escopeta_disparo` a pitch 1.4 (sin cambios) |
| **Don Lobo** | — | **No existe** (Fase 4) | Nada a medias. Queda listo: `Data/MuzzlePoints/Muzzle_donlobo` (respaldo 0.55, 0.55) y `EnemyGunfire` para agregar a su IA | `idle_01`, `shoot_01`, `surrender_01`, `talk_01` | — |
| Otros (trampas, etc.) | — | No hay | — | — | — |

- **Recon:** nadie más llama a `PlayerHealth.TakeDamage`. Lo busqué en todo `Scripts/`: solo `EnemyAI`, `CentavoController` y la depuración del HUD.
- **Del pull no llegaron cambios de código de Edgar** (ver "Git"): la tabla es la del código actual.

## Guardias con pistola: números usados, ¿se siguen rindiendo y abriendo el diálogo? ¿el patio de Centavo sigue funcionando?
- **Números (H18)**, todos serializados en `EnemyAI` → *Pistola (HUD.2, H18)*:

  | Campo | Valor |
  |---|---|
  | `rangedRange` | 18 m |
  | `standDistance` | 6–14 m |
  | `aimTelegraph` | 0.35 s |
  | `shotInterval` | 1.4–2.2 s |
  | `rangedHitChance` | 0.30 |
  | `rangedHitChanceRunning` | 0.15 |
  | `rangedDamage` | 8 |
  | `rangedSpread` | 1.5° |
  | `shootPoseSeconds` | 0.2 s |

  El primer tiro de cada persecución sale a los 0.4–1 s, no al instante.
- **Dónde va:** **dentro de Chase**. Antes de moverse, si Wilbur está a ≤ 18 m, se lo ve **desde el cañón** y ya está a la distancia elegida, el guardia frena, gira hacia Wilbur y dispara.
  - Si Wilbur entra a 2 m, sigue el golpe cuerpo a cuerpo de siempre (estado Attack, **sin cambios**).
  - Si deja de verlo o se aleja de la distancia elegida, vuelve a correr y cancela el apuntado.
  - Es el único cambio a la persecución que pide H18 (plantarse a 6–14 m). La regla de "lo perdió" (1.5 × la vista) y la vuelta a patrulla o puesto **no cambiaron**.
- **Nunca dispara si:**
  - está **rendido** o **muerto** (`Update` no corre en esos estados, como antes);
  - el juego está bloqueado (pausa, diálogo, interrogatorio: `PlayerAim.GameplayBlocked()`);
  - Wilbur está muerto.

  El apuntado en curso no termina: se corta.
- **Rendición y diálogo:** `EnemyHealth`, `TriggerSurrender`, `DialogueNPC` y la pose de manos arriba **no se tocaron**. Colmillo se rinde a ≤ 30 % y se lo interroga igual.
- **Patio de Centavo (D2):** Centavo sigue esperando a `Guard_Patrulla_A` y `Guard_Centinela` muertos o rendidos. Eso no cambió. Ahora esos dos guardias también disparan, o sea el patio es más difícil.
- **Ojo con la munición:** con guardias que disparan de lejos, la cuenta de la 3C.5 cambia (Wilbur recibe más daño a distancia). Probarlo en HUD.4. La reserva de la escopeta ya es 24 (3C.6).

## Cuadros de los guardias para apuntar/disparar: ¿existían? ¿cuáles faltan para Edgar?
- **Existía solo `shoot_01`**: el guardia con el arma adelante **y el fogonazo ya dibujado**. Se usaba para el golpe cuerpo a cuerpo.
- **Uso actual:**
  - entre tiros: `idle`;
  - **aviso de 0.35 s**: `shoot_01` (pose con el arma);
  - **disparo**: `shoot_01` 0.2 s + el fogonazo del pool en el cañón.
- **Problema:** como `shoot_01` ya trae un fogonazo dibujado, **durante el aviso ya se ve "disparando"**.
- **Faltan para Edgar** (mismo lienzo de 512 y misma pose):
  - **`guardia_apunta_01`**: arma adelante, **sin fogonazo**;
  - **`guardia_dispara_01`**: el retroceso, con o sin fogonazo.

  Lo mismo para el informante si se quiere distinto: `informante_apunta_01` y `informante_dispara_01`.
- **Cuando lleguen:** se agregan como animaciones nuevas del set y en `EnemyAI.UpdateRanged` se cambian las dos llamadas a `PlayAttack()`.

## Puntos de cañón: ¿qué cuadros quedaron sin punto? (personaje / sprite)
- **Assets:** `MuzzlePointSet` en `Data/MuzzlePoints/Muzzle_<id>.asset`. Los crea **Plomo y Paja → HUD → Crear puntos de cañón**, y también *Crear Prefabs de Prueba* y *Colocar Centavo* al configurar los tiradores.
- **Ventana:** **Plomo y Paja → HUD → Puntos de cañón**.
  - Clic sobre el dibujo = punto del cuadro.
  - Cruz amarilla = punto propio; gris = respaldo.
  - Tiene Deshacer, "Copiar al siguiente cuadro", "Borrar punto" y "Usar como respaldo".
- **Hoy NINGÚN cuadro tiene punto propio**: todos usan el **respaldo**, medido a ojo sobre `shoot_01`.

  | Personaje | Respaldo (normalizado, origen abajo-izquierda) | Cuadro sin punto propio |
  |---|---|---|
  | `guardia` | (0.46, 0.49): arma en (240, 255) de 512 desde arriba | `guardia/shoot_01` |
  | `informante` | (0.46, 0.50): mismo dibujo | `informante/shoot_01` |
  | `contador` | (0.317, 0.60): revólver en (325, 410) de 1024 desde arriba | `contador/shoot_01` |
  | `donlobo` | (0.55, 0.55): estimado, sin IA | `donlobo/shoot_01` |

- **Pendiente para David:** marcar los 4 con la ventana (2 minutos). El menú lista los que falten.
- **Sin menús:** si los prefabs todavía no tienen `EnemyGunfire`, `EnemyAI` y `CentavoController` lo agregan solos en runtime con esos mismos respaldos, así el disparo sale del arma igual.

## ¿El disparo sale del arma en todos? ¿también con Centavo agachado/asomado?
- **Sí, en los dos tiradores que existen.**
- **Cómo se calcula el punto:** `EnemyGunfire.MuzzlePoint()` toma el cuadro que se ve **en ese momento** (`SpriteRenderer.sprite`):
  - punto normalizado → px del sprite − pivote → unidades del sprite;
  - y de ahí al mundo con el transform del `SpriteRenderer`. Así incluye el giro del billboard, la escala del objeto (Centavo: raíz 1.15 de la 3C.6) y `flipX`.
- **Centavo:** solo dispara en la fase **Shoot**. `StartPhase(Shoot)` pone `shoot_01` y en la línea siguiente llama `FireShot`, así que el fogonazo sale en el mismo frame que el cuadro de disparo.
  - **Agachado (cover) y asomado (peek) no dispara**, como antes; no cambió nada de sus estados ni de los ganchos de la 3C.3/3C.4.
  - Su cañón en `shoot_01` queda a ~1.4 m sobre sus pies, más alto que el banco de 1 m.
- **Guardias:** el punto sale de `shoot_01` en el frame del tiro.

## ¿El rayo de daño sale del cañón? ¿lo bloquean el banco y las paredes?
**Sí.**
- **Recorrido:** `EnemyGunfire.Fire` tira el rayo **desde el cañón**.
  - Si el tiro "acierta" (por probabilidad), apunta al pecho de Wilbur (centro del `CharacterController`) con un temblor de 1.5° (guardias) o 0.5° (Centavo).
  - Si falla, apunta 0.6–1.5 m al costado.
- **Qué recibe el tiro:** lo primero que toca.
  - Pared, banco o piso: polvo + agujero.
  - Otro personaje: sin daño, sin calcomanía.
  - Wilbur: solo pierde vida si el tiro acertaba y él es lo primero → `PlayerHealth.TakeDamage(daño, cañón)`, y la cuña del HUD apunta al cañón.
- **Lo que el rayo ignora:** los colliders del propio tirador, los triggers y la capa Ignore Raycast (el revólver tirado).
- **Para empezar a disparar**, los guardias exigen línea libre **desde el cañón** (`ClearFromMuzzle`). Detrás de una pared no se plantan a tirar: siguen corriendo.
- **Banco de Centavo:** es un `BoxCollider` sólido en la capa Default. Si Wilbur está detrás, el tiro pega en el banco.
- **Los perdigones del jugador** siguen igual. El banco los bloquea cuando Centavo se cubre (3C.2, sin cambios).

## Zumbido de bala cercana: ¿funciona?
**Programado (H12), no probado.**
- **Cuándo suena:** un tiro que no le quitó vida, cuyo segmento cañón → impacto pasa a ≤ 1.5 m de la cámara.
- **Qué suena:** `sfx_bala_zumbido`, en 2D, como mucho uno cada 0.3 s.
- **El clip falta** (`sfx_bala_zumbido_01/_02` no están en la librería): hoy no suena nada y la librería avisa una vez en la consola.

## ShotgunFeedback: ¿la luz y la onomatopeya siguen la boca del cañón?
**Sí (programado).**
- **De dónde sale el punto:** `FirstPersonViewmodel.TryGetMuzzleScreenPoint`, la nueva, da el punto en pantalla del ancla del cañón del perfil (`hipMuzzle` desde la cadera, `aimMuzzle` apuntando), las mismas anclas del fogonazo 2D.
- **En cada disparo, `ShotgunFeedback`:**
  - pone la luz del fogonazo en ese punto, a 0.8 m de la cámara;
  - usa ese punto (un poco arriba y a la izquierda) para la onomatopeya.
- Sin manos visibles, quedan donde estaban (`muzzleLocalPosition`, `popupViewport`).
- **Revólver:** no usa `ShotgunFeedback` (desde la 3C.5), así que no tiene luz ni onomatopeya; no se agregó.
- **Impactos del jugador:** perdigones y balas del revólver que pegan en el mundo usan **el mismo pool** (`CombatFx.Impact`: polvo + agujero), salvo que se asigne un `bulletHolePrefab`.

## Tamaños de pool y ¿hay GC por disparo?
`CombatFx`: un objeto por escena, creado la primera vez que se usa. **Nada de `Instantiate` por disparo.**

| Pool | Tamaño | Dura |
|---|---|---|
| Fogonazos (billboard) | **8** | 0.05 s |
| Luces | **4** | 0.06 s |
| Estelas (`LineRenderer`) | **8** | 0.06 s |
| Polvo de impacto | **16** | 0.35 s, sube y se desvanece |
| Agujeros de bala | **32** | 10 s, el último 1.5 s se desvanecen; el más viejo se reusa |
| Fuentes de sonido 3D | **6** | para disparos con pitch propio |

**Basura por disparo:** ninguna a propósito.
- Los rayos usan búferes fijos (`RaycastNonAlloc`, 12).
- No se crean strings ni listas.
- `Camera.main` está cacheado en `CombatFx`; en el zumbido se consulta una vez por tiro fallado (en 2022.3 no genera basura).
- `CanShoot` tira un rayo por frame por cada guardia en persecución.

## Menús que David debe correr
1. Abrir Unity y dejar que compile. El importador de personajes sube a la versión 9 y agrega la carpeta `Art/FX/`.
2. **Plomo y Paja → HUD → Crear puntos de cañón**: crea los 4 `Muzzle_*.asset`.
3. **Plomo y Paja → HUD → Puntos de cañón**: marcar el cañón en `shoot_01` de guardia, informante y contador.
4. **Plomo y Paja → Crear Prefabs de Prueba**: rehace `Enemy_Guard` y `Enemy_Interrogable` con `EnemyGunfire` (también rehace `AICompanion` y completa el Player).
5. **Plomo y Paja → Contador → Colocar Centavo**: rehace `Centavo.prefab` con `EnemyGunfire`.
6. **Ctrl+S** y commit de los `Muzzle_*.asset`, los prefabs y `NivelPorky.unity` si cambió.
7. **Cuando llegue el arte de efectos** (`_entrante_hud/FX/`): `python Tools/character_key.py --center FX --dst Assets/_Game/Art/FX` y volver a correr 4 y 5.

## Archivos compartidos tocados (y si estaban limpios en git status)
**Todos estaban limpios** antes de editarlos (con Unity cerrado nadie más escribía):
- `EnemyAI.cs`: pistola dentro de Chase.
- `CentavoController.cs`: solo `FireShot` y el `EnemyGunfire` en `Awake`.
- `BillboardSprite.cs`: un getter, `MainRenderer`.
- `ShotgunController.cs` y `RevolverController.cs`: el impacto en el mundo va al pool.
- `CharacterSpriteImporter.cs`: carpeta `Art/FX` (versión 9).
- `TestPrefabGenerator.cs` y `ContadorCentavoBuilder.cs`: `EnemyGunfire` en los prefabs.

**No toqué:** `EnemyHealth.cs`, `PlayerHealth.cs`, `PlayerAmmo.cs`, `UiHideReasons`, el HUD ni `CLAUDE.md`.

## Qué no se pudo compilar/probar y qué debe revisar David en el Editor
- **Compilado: sí.** Roslyn de Unity 2022.3.62f3 (`DotNetSdkRoslyn/csc.dll`) contra los módulos de `Managed/UnityEngine`, `UnityEditor.dll` y `Library/ScriptAssemblies`: **Assembly-CSharp y Assembly-CSharp-Editor con 0 errores**. Compilé al final de la parte A (después del pull) y después de HUD.2.
- **No probado:** los menús, la ventana y nada en Play.
- **Arte y sonido que faltan** (placeholders):
  - **Arte:** `fx_impacto_01` y `fx_impacto_02` → se usa `Art/Characters/fx/humo_01` de Edgar; `fx_agujero_bala` → disco oscuro dibujado por código.
  - **Cuadros:** `guardia_apunta_01`, `guardia_dispara_01` (y del informante si se quiere).
  - **Sonido** (no suena; la librería avisa una vez): `sfx_disparo_guardia` → se usa `escopeta_disparo` con pitch 1.5. Además faltan `sfx_bala_zumbido_01/_02`, `sfx_impacto_01..03` y `sfx_rebote` (este no se usa todavía).
  - El fogonazo enemigo usa el `fx_fogonazo_01` del jugador (H11).
- **APIs dudosas:**
  - `Shader.Find("Sprites/Default")` para las estelas en un build (está en los shaders siempre incluidos; si no, cae a URP Unlit).
  - La conversión del punto del cañón con `flipX`, pivote y escala.
  - `Physics.RaycastNonAlloc` con muchos colliders: si hay más de 12 en el rayo, puede no encontrar el primero.
- **Revisar en Play:**
  1. **Un guardia de la calle:**
     - al verte, corre;
     - a 6–14 m se planta, levanta el arma (0.35 s) y dispara;
     - el fogonazo y la estela salen **del arma del dibujo**;
     - en la pared queda polvo y un agujero que se borra a los 10 s;
     - si te pega, la cuña del HUD apunta a él.
  2. **Correr:** pegan menos. Esconderse detrás de una pared: no disparan, se acercan.
  3. **Acercarse a 2 m:** golpe cuerpo a cuerpo como antes.
  4. **Colmillo:** se rinde a ≤ 30 % y no dispara más; el diálogo se abre igual.
  5. **Patio:** los dos guardias del portón disparan; al caer los dos, Centavo despierta (D2).
  6. **Centavo:** el fogonazo sale del revólver del dibujo al asomarse; con Wilbur detrás del banco, el tiro pega en el banco. Recibe el primer golpe y la escena funciona igual.
  7. **Escopeta:** la luz y el "¡BANG!" salen de la boca del cañón, desde la cadera y apuntando.
  8. **Gizmo:** con `debug` en `EnemyGunfire` se ven la cruz del cañón y el último rayo (Scene view).
  9. **Profiler:** sin GC al disparar.

## Archivos creados/modificados
Creados (con `.meta`):
- `Scripts/Combat/` (carpeta).
- `Scripts/Combat/MuzzlePointSet.cs`: puntos del cañón por cuadro (H10).
- `Scripts/Combat/EnemyGunfire.cs`: disparo enemigo desde el arma (H9, H12).
- `Scripts/Combat/CombatFx.cs`: efectos de disparo en pool (H11).
- `Scripts/Editor/MuzzlePointWindow.cs`: ventana **Puntos de cañón** + menú **Crear puntos de cañón** (`MuzzlePointsBuilder`).
- `Docs/audits/AUDIT_hud_2.md` (este archivo).

Modificados:
- `Scripts/Enemies/EnemyAI.cs`: pistola (H18) dentro de Chase.
- `Scripts/Characters/Contador/CentavoController.cs`: `FireShot` con `EnemyGunfire` (mismos números). Los campos `sfxSource`, `muzzleFlash` y `flashSeconds` quedan en el prefab pero ya no se usan para el tiro.
- `Scripts/Enemies/BillboardSprite.cs`: `MainRenderer`.
- `Scripts/Player/FirstPersonViewmodel.cs`: `TryGetMuzzleScreenPoint`.
- `Scripts/Player/ShotgunFeedback.cs`: luz y onomatopeya en la boca del cañón.
- `Scripts/Player/ShotgunController.cs` y `Scripts/Player/RevolverController.cs`: impactos al pool.
- `Scripts/Editor/CharacterSpriteImporter.cs`: `Art/FX` (versión 9).
- `Scripts/Editor/TestPrefabGenerator.cs`: `EnemyGunfire` en `Enemy_Guard` (y en la variante interrogable).
- `Scripts/Editor/ContadorCentavoBuilder.cs`: `EnemyGunfire` en Centavo.

## Commits
- `89ad934`: dia4(david): HUD.2 disparos enemigos desde el arma, guardias con pistola, puntos de cañón y efectos en pool
- Commit siguiente: esta auditoría.

## Git
### Parte A: commits de orden
- **a) Assets de la 3C.5.2:** **nada que subir.** La sesión de la 3C.6 ya los había subido en `03dbabf` (`VM_Revolver`, `VM_Shotgun`, prefabs de Player y Centavo, `.meta` de `Art/Pickups`).
- **b) Salida de los menús de HUD.1:** `451ede5`. Revisé el diff de cada archivo antes de subirlo:
  - `Prefabs/UI/HUD_Wilbur.prefab` + `.meta`: Canvas en el orden 4 con `HudWilbur`, la fuente Special Elite y el `AudioSource` del latido;
  - `Prefabs/Player.prefab`: agrega `PlayerAmmo` (24 / tope 30, 0 / tope 24), saca los campos de la fila de munición que se quitó de la mira y deja `reserveAmmo` en 24 (3C.6);
  - `Assets/Scenes/NivelPorky.unity`: solo la instancia de `HUD_Wilbur` (98 líneas, todas del HUD).
  - `Art/UI/HUD/` no existe (no llegó el arte).
- **c) Documentos:** `9dafa84`: `Docs/phases/Fase-HUD.md` (v1.1) y `Docs/guiones/Guion-HUD.md`.
- **d) Quedó sin subir, a propósito:** `PLOMO_Y_PAJAV_1/Assets/_Game/Prefabs/AICompanion.prefab` (`logRequests: 1 → 0`).
  - Lo regenera *Crear Prefabs de Prueba* con el valor por defecto que la 3C.6 apagó en `AICompanion.cs`.
  - Es seguro subirlo, pero no estaba en las categorías a, b o c de la orden.
- **Secretos:** en cada commit revisé que no hubiera claves (`sk-…`) en lo agregado. Ninguna. `openai_key.txt` está en `.gitignore` y no está en git.

### Pull
- **Antes del fetch:** `origin/integracion/dia4` = `e5f0ed2`. La rama local estaba **16 commits adelante y 0 atrás**.
- **`git pull --no-rebase origin integracion/dia4`:** **"Already up to date"**. No entró nada y no hubo conflictos.
- **Compilado después del pull:** 0 errores en los dos ensamblados.
- Graphify no se volvió a correr: nadie tocó `EnemyAI`, `EnemyHealth` ni los sprites de los guardias.

### Otras ramas con trabajo nuevo (NO se mezclaron)
- **`origin/dev`** pasó de `6cc57c3` a `5d9c98e`. Todo de **Edgar**, del 2 nov:

  | Commit | Qué trae |
  |---|---|
  | `bf940cc` "sprites hud e intro" | `Art/Characters/hud/medallon_balas.png`, `wilbur_retrato_normal.png`, `wilbur_retrato_herido.png`, y `Art/Characters/intro/intro_01..06.png` (+ `.meta`) |
  | `ca46276` "sprites del taxista" | `Art/Characters/taxista/conduce_01.png`, `habla_01.png`, `habla_02.png` (+ `.meta`) |
  | `5d9c98e` | merge de `integracion/dia4` (en `e5f0ed2`, la versión vieja) en `dev` |

  - **Solo arte, sin código.**
  - **Para decidir:** los retratos del HUD de Edgar **no usan los nombres del guion** (`hud_wilbur_01..04`, `hud_wilbur_dano`, `hud_bala_*`), y están en `Art/Characters/hud/` (con el importador de personajes, pivote en los pies) en vez de `Art/UI/HUD/`. Si se quieren usar, hay que traerlos (merge de `dev` o copiar los PNG) y renombrarlos o recortarlos con `character_key.py --center`. No lo hice.
- **`origin/enemies` (`b0fb4d3`):** no cambió en este fetch. Ya tenía 3 commits que no están en nuestra rama:
  - `48051a3`: medallones del HUD;
  - `909a068`: ilustraciones de la intro;
  - `b0fb4d3`: sprites de Centavo, que ya están en nuestra rama por la 3C.0.
- **`origin/player`** no tiene nada nuevo. **`origin/levels`** no existe en el remoto.

### Pull final y push
*(Se completa abajo después del segundo pull.)*
