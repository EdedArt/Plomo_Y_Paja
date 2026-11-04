# AUDIT HUD.3: objetos recogibles (botiquines, cajas de munición, drops) + arte de David

Fase: `Docs/phases/Fase-HUD.md` (v1.1), guion `Docs/guiones/Guion-HUD.md`. Rama: `integracion/dia4`.
**Unity estuvo cerrado toda la sesión: nada de esto se corrió ni se probó en el Editor ni en Play.** Lo único verificado es que compila y que los PNG se ven bien recortados.

## Qué pasó con la sesión anterior

**No encontré rastro de que HUD.3 haya empezado.** No se puede decir que se cortó a la mitad: no había nada a medias.

| Dónde miré | Resultado |
|---|---|
| `git status` | limpio |
| `git log` local y `origin/integracion/dia4` | ambos en `d0ced17`; ningún commit de HUD.3 |
| `git stash list` | solo `stash@{0}` de `integracion/dia3` («cambios viejos antes de probar 3A6»), ajeno a esto; no se tocó |
| `AUDIT_hud_3.md` en todo el repo | no existe (tampoco sin seguimiento ni en otra carpeta) |
| `Scripts/Pickups/`, `Art/Props/` | no existían |
| `git reflog` | lo último fue el commit de las 10:46 (`d0ced17`) |
| `git worktree list` | solo el checkout principal |
| Transcripciones locales de Claude Code | ninguna contiene el pedido de HUD.3. Las últimas: sincronización y merges de git (`39e00803`, hasta las 10:46) y HUD.1.1 en el worktree `intro3` |

**Por qué: no lo puedo saber.** El pedido no quedó registrado en ninguna sesión de esta máquina, así que no sé si se mandó, si se perdió antes de empezar o si corrió en otro lado.

**Estado de cada parte al empezar esta sesión:**
- **A (orden + pull):** sin hacer.
- **B (arte + objetos):** sin hacer.
- **C (auditoría + pull + push):** sin hacer.

No había trabajo sin commitear que revisar (punto 3 del pedido): todo lo de abajo es nuevo.

**Límite:** no tuve el texto original del pedido de HUD.3, solo el resumen («orden + pull, arte + objetos, auditoría + pull + push») y la fase. En «orden» no había nada que hacer: árbol limpio y rama al día.

## Arte del HUD

**Origen:** arte de David en `PLOMO_Y_PAJAV_1/entrante_hud/` (**sin guion bajo**, no `_entrante_hud/`; está en `.gitignore`). No se trajo nada de `origin/dev` ni de `origin/enemies`.

| Destino | Archivos | Cómo |
|---|---|---|
| `Art/UI/HUD/` | `hud_wilbur_01`–`04`, `hud_wilbur_dano`, `hud_cartucho_lleno/vacio`, `hud_bala_llena/vacia` | `character_key.py --center HUD`, 256×256, centrados |
| `Art/Props/pickups/` | `pickup_botiquin_01`, `pickup_cartuchos_01`, `pickup_balas_01` | `character_key.py Objetos`, 512×512, alineados abajo |
| `Art/FX/` | `fx_impacto_01/02`, `fx_agujero_bala` | `character_key.py --center FX --dst Assets/_Game/Art/FX` |

**El arte de Edgar ya no se usa:**
- `hud_wilbur_01` y `hud_wilbur_04` se **sobrescribieron** con los de David; conservan su `.meta` y su GUID.
- `hud_medallon_balas.png` y su `.meta` se **borraron** (`git rm`). `HudBuilder` lo trata como opcional: sin él, la fila de munición empieza en su lugar original. El código del medallón quedó, sin uso.
- El prefab `HUD_Wilbur` todavía apunta al medallón borrado hasta correr el menú; se resuelve solo al rearmarlo (ver «Menús»).

**Herramienta:**
- El modo `python Tools/character_key.py Objetos` que pide la fase no existía: se agregó.
- `--center` ahora encuentra `entrante_hud/` y salta los `pickup_*` que hay copiados en `entrante_hud/HUD/` (son idénticos a los de `Objetos/`, mismo MD5).
- Nuevo `--despill-all`: `fx_impacto_02` salía **rosado**, porque su humo translúcido deja ver el magenta. Se recortó con esa opción y ahora se ve gris malva. Conserva un leve tinte: **conviene mirarlo en juego**.

**Revisión visual:** miré los 15 PNG sobre un damero. Sin bordes magenta. Eso es sobre un fondo de prueba, no dentro de Unity.

**El estilo cambia:** las caras de David son la cabeza sola sobre transparente, no el medallón redondo de Edgar. Revisar en juego que se lea bien en 150 px.

## Plantilla HUD.3

- **Cómo se recoge (tecla / aviso reutilizado):** tecla **E**, a ≤ 2.2 m en horizontal, con el aviso bajo la mira (`RubberHoseCrosshair.SetPrompt`), como `WeaponPickup` y las pistas. Bloqueado con `PlayerAim.GameplayBlocked()`. Textos del guion: «[E] Tomar botiquín / cartuchos / balas».
  - **Dos agregados a lo que hace `WeaponPickup`:**
    - Pide **línea de vista**: sin esto, los objetos del cuarto de Centavo se tomaban a través de la pared. Guardias y jugador no tapan, así el cadáver no bloquea su drop.
    - Con dos objetos al alcance manda el **más cercano**, y una tecla usa un solo objeto.
- **Botiquín: ¿cura 40 %? ¿no se gasta con la vida llena?** Por código, sí: `Heal(MaxHealth * 0.4)`; con la vida llena muestra «Salud completa» y queda. **Sin probar en Play.**
- **Munición: ¿respeta los topes? ¿la de revólver se guarda sin tener el revólver?** Por código, sí: usa `PlayerAmmo.TryAdd` (topes 30 y 24), que no mira el arma. Llena: «Munición llena» y queda. **Sin probar en Play.**
  - **Decisión:** si solo entra una parte, la caja se gasta y el aviso dice lo que entró (ej. «+2 cartuchos»). La fase no lo define.
- **Posiciones elegidas (nivel 1; nivel 2 si existe):** elegidas leyendo las coordenadas de los generadores, **sin verlas en el Editor**. La altura se ajusta al piso al colocar.

| Id | Objeto | Lugar | (x, z) |
|---|---|---|---|
| `Cartuchos_Casa` | caja de cartuchos | casa, rincón oeste de la cocina | (-7.1, 3.1) |
| `Botiquin_Porton` | botiquín | antes del portón, junto a la valla | (-8.9, 12.3) |
| `Cartuchos_Cuarto` | caja de cartuchos | dentro del cuarto de Centavo, junto a la puerta | (-5.55, 18) |
| `Botiquin_Cuarto` | botiquín | dentro del cuarto de Centavo, junto a la puerta | (-8.45, 18) |
| `Balas_Salida` | caja de balas | camino de salida del patio | (-7.6, 9.4) |

  - **Nivel 2:** no existe la escena `Campamento`. Enganche para la Fase 4: su generador llama `PickupBuilder.BuildPrefabs` + `PlaceInOpenScene`; `PickupLayout_Campamento` se crea vacío y se llena con «Guardar posiciones de objetos». Las cantidades de la fase (3 botiquines, 4 cartuchos, 3 balas; antes de Don Lobo 1 botiquín + 1 de cada munición) quedan **pendientes**.
- **Drops de guardias: ¿solo muertos? ¿nunca Centavo ni Don Lobo?** Por código:
  - 35 %, +4 cartuchos, a los pies y ajustado al piso, máximo uno por guardia (`GuardDropSpawner` escucha el nuevo `EnemyHealth.AnyDied`).
  - Rendirse no suelta nada. **Decisión:** rematar a un rendido sí puede soltar, porque cuenta como muerto.
  - **Centavo:** no usa `EnemyHealth` (es `IDamageable`), así que no pasa por aquí.
  - **Don Lobo:** no existe. La Fase 4 **debe** poner `dropsLoot = false` en su `EnemyHealth`; el valor por defecto es `true`, y si se olvida, soltará cajas.
- **¿Sobreviven a "Generar Nivel Porky"?** Por código, **sí**: `Generate()` rearma los prefabs y coloca desde el asset, que no se regenera. **Sin probar.** Lo movido a mano en la escena se pierde si no se corrió antes «Guardar posiciones de objetos».
- **Menús que David debe correr (en orden):** ver abajo.
- **Ids faltantes y placeholders:**
  - **Sonidos:** `sfx_botiquin`, `sfx_municion_recoger` (sin ellos no suena; el aviso sí sale).
  - **Voces opcionales:** `wilbur_botiquin_01`, `wilbur_municion_01`. `wilbur_vida_baja_01` no se tocó en esta fase.
  - **Arte:** `hud_barra_marco` (opcional, la barra sigue dibujada por código). Los 3 objetos tienen PNG, así que el cubo con etiqueta no debería verse.
- **Archivos compartidos tocados (y si estaban limpios en git status):** los tres estaban limpios.
  - `EnemyHealth.cs`: evento `AnyDied`, campo `dropsLoot`.
  - `CharacterSpriteImporter.cs`: versión 13, regla de `Art/Props/pickups` (PPU 500, abajo al centro, mipmaps).
  - `LevelBuilder_NivelPorky.cs`: coloca los objetos antes de hornear; no son estáticos, así que el NavMesh no los ve.
- **Qué no se pudo compilar/probar y qué debe revisar David en el Editor:** compilan los dos ensamblados, pero **nada se ejecutó**. Los menús, los prefabs y el asset **todavía no existen**: los crea el Editor. Revisar en especial:
  1. **Tamaño** de los objetos (0.5 m de lado) y que apoyen en el piso.
  2. **Encuadre del interrogatorio:** los dos objetos del cuarto están cerca de la puerta, y la cámara del interrogatorio está ahí. Si Wilbur no los tomó antes, **pueden salir en cuadro**. Si pasa, moverlos y «Guardar posiciones».
  3. **Tecla E compartida:** un rendido interrogable junto a un drop puede responder a la misma tecla en el mismo cuadro.
  4. **Línea de vista:** un mueble bajo puede bloquear el aviso desde algún ángulo.
  5. **Brillo:** por no tener arte de destello, es la luz cálida subiendo y el dibujo creciendo 8 %. Ver si alcanza.
  6. **Reinicio tras morir (H17):** no se agregó código; depende de que la escena se recargue.
- **Archivos creados/modificados:**
  - **Creados:** `Scripts/Pickups/{Pickup, MedkitPickup, AmmoPickup, PickupLayout, GuardDropSpawner}.cs`, `Scripts/Editor/PickupBuilder.cs`, los PNG de la tabla de arte que no existían, este audit.
  - **Modificados:** los 3 compartidos de arriba, `Tools/character_key.py`, `hud_wilbur_01.png`, `hud_wilbur_04.png`.
  - **Borrados:** `hud_medallon_balas.png` y `.meta`.
- **Commits:** ver «Git».

### Menús, en orden
1. Abrir Unity y esperar la reimportación (importador v13). Se crean los `.meta` que faltan.
2. **Plomo y Paja → HUD → Crear puntos de cañón** (toma `Art/FX` para los efectos de HUD.2).
3. **Plomo y Paja → Generar Nivel Porky** (rearma el HUD con las caras nuevas, crea prefabs y `Data/PickupLayout_NivelPorky.asset`, y coloca los objetos). Si no se quiere regenerar: **HUD → Crear HUD** + **HUD → Colocar objetos** + Ctrl+S.
4. Probar en Play y commitear lo generado: `.meta` nuevos, `Prefabs/Pickups/`, `Prefabs/UI/HUD_Wilbur.prefab`, `Data/PickupLayout_NivelPorky.asset`, `Scenes/NivelPorky.unity`.

## Git

- **Compilación:** Roslyn de Unity 2022.3.62f3 contra las referencias de los `.csproj`, sobre las fuentes reales del disco, después del pull. `Assembly-CSharp`: 102 fuentes, **0 errores**. `Assembly-CSharp-Editor`: 31 fuentes, **0 errores**. Se ocultaron los avisos CS0649, CS0169 y CS0414 (campos serializados y el aviso de `CentavoController` que ya existía).
- **Staging:** por ruta explícita. Comprobado: ningún archivo de `entrante_*`, `Library` ni secretos en estos commits.
- **Commits:**
  - `410d90d`: arte de David y `character_key.py`.
  - `b80cf9e`: objetos recogibles, drops, importador v13.
  - El commit de este audit (`docs: auditoría HUD.3`).
- **Pull:** `git pull --no-rebase origin integracion/dia4` → «Already up to date», sin conflictos.
- **No se trajo nada** de `origin/dev` ni de `origin/enemies`. `origin/dev` tiene commits nuevos (`09d1967`) que siguen afuera.
- **Otro trabajo en la rama:** mientras esta sesión corría apareció el commit local `5181553` (MENU.0, solo 3 documentos: `AUDIT_menu_0.md`, `guiones/menu.md`, `Fase-Menu.md`). No es de esta sesión y no lo modifiqué. Queda **debajo** de los commits de HUD.3, así que **el push lo sube también**. Hay otra sesión o persona trabajando en este mismo checkout.
- **Sin archivos de otros en los commits:** comprobé que `5181553` no se llevó nada de HUD.3 y que los commits de HUD.3 no llevan nada de MENU.

ESTADO: pendiente de revisión
