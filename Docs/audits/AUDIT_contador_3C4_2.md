# AUDIT contador 3C.4.2: cambios del nivel 1 pedidos por David

Fase: `Docs/phases/Fase-3C-Contador.md` (secciones 0 y 1). Leídos `AUDIT_contador_3C1.md` y `AUDIT_contador_3C2.md`, `LevelBuilder_NivelPorky`, `ContadorYardBuilder`, `ContadorCentavoBuilder`, `TestPrefabGenerator`, `EnemyAI`, `EnemyHealth`, `ShotgunController`, `PlayerHealth`, `DialogueDatabase`/`DialogueNPC` y la fase 3B.
Rama: `integracion/dia4`.

> **Importante:** Unity no se abrió en esta sesión. El código compila, pero **los menús no se ejecutaron**: los guardias nuevos todavía no están en la escena y el NavMesh no se volvió a hornear. **Nada se probó en Play.**

## 1. Colmillo: qué tenía y qué se quitó → **PARADO, no se quitó nada**
- **Qué tiene Colmillo:**
  - guardia `guardia_porky` (`Enemy_Interrogable`, `canSurrender = true`, `DialogueNPC`), marcador `Guard_guardia_porky` en la cocina, en (5.5, 0, 4.3);
  - se rinde al 30 % de vida y abre su interrogatorio (`DialogueDatabase.BuildColmillo`).
- **Efectos en GameState:**
  - `RegisterInterrogation("Colmillo", …)`: suma 1 a `InterrogationsCompleted` con cualquier final (útil, engaño o negativa);
  - en los dos finales útiles (`fin_ayuda_rastras`, `fin_ayuda_deuda`), `RegisterClueCollected("testimonio_puerta_01", "Testimonio de Colmillo: nadie se llevó a Porky a rastras; salió por su propio pie")`, que **cuenta como pista** para los finales;
  - el contexto de la IA (log de acciones del tracker) y la línea de la IA `InterrogationEnded`.
- **Por qué paré:** busqué `testimonio_puerta_01` en todo el proyecto (código, escenas, prefabs, assets y docs). **Su única fuente es Colmillo.** No hay un `CluePickup` ni otro NPC que la dé. La orden dice que en ese caso se pare esa parte y se reporte sin inventar otra fuente, así que:
  - Colmillo **sigue** en el nivel, en los builders y en `DialogueDatabase`;
  - no se tocó su marcador, su diálogo ni ningún documento sobre él.
- **Para decidir:**
  - **(a)** quitarlo y aceptar que `testimonio_puerta_01` desaparece (los finales siguen alcanzables, ver abajo);
  - **(b)** darle esa pista a otra fuente (por ejemplo, una línea de Centavo o un objeto), cosa que pide un cambio de guion;
  - **(c)** dejarlo.

  Con la respuesta, quitarlo es poco trabajo: el marcador en `BuildMarkers`, el bloque de interrogables en `BuildActors`, `BuildColmillo` y su pista en `DialogueDatabase`, y los documentos.

### NPCs interrogables que quedan en el juego + Final C
| NPC | Nivel | Pista que da | Cuenta como interrogatorio |
|---|---|---|---|
| Colmillo (`guardia_porky`) | 1 (cocina) | `testimonio_puerta_01` (si sale útil) | sí, con cualquier final |
| **Centavo** (el contador) | 1 (cuarto del patio) | `registro_01` (`1A_v` o al perdonarlo) | sí, al llegar a la decisión (D10) |
| Tuerto (`guardia_barracon`) | 2 (barracón) | `testimonio_socios_01` | sí |
| Contable (`guardia_registro`) | 2 (contaduría) | ninguna (D1) | sí |

El nivel 2 (Campamento) **todavía no existe**: es la Fase 4. Tuerto y el Contable solo están en `DialogueDatabase`.

**Final C** pide ≥ 3 pistas, ≥ 2 interrogatorios y el lobo perdonado (`DetermineEnding`, sin cambios):
- **Hoy (con Colmillo):** se alcanza ya en el nivel 1. Pistas: nota + contrato + `registro_01` (+ testimonio). Interrogatorios: Colmillo + Centavo.
- **Si se quita a Colmillo:**
  - en el nivel 1 hay 1 interrogatorio (Centavo); el segundo tiene que venir del nivel 2 (Tuerto o el Contable). **Sigue siendo alcanzable** cuando exista el Campamento, como lo pide la Fase 4;
  - pistas: nivel 1 = 3 (nota, contrato, `registro_01` perdonándolo) + nivel 2 = hasta 3 (foto, carta, testimonio de Tuerto). Alcanza de sobra.
  - **Riesgo hasta la Fase 4:** sin Colmillo y sin el Campamento, el Final C no se puede probar de punta a punta.

## 2. Guardias nuevos (tipo, posición, ruta)
**Cuatro guardias normales** con el prefab que ya existía, `Enemy_Guard`: `canSurrender = false` y sin `DialogueNPC`, así que **pelean hasta morir**. Sus tipos son los de siempre (`Patrullero` / `Centinela`).

| Guardia | Tipo | Dónde | Ruta / mira |
|---|---|---|---|
| `Guard_Patrulla_B` | Patrullero | Calle, a la derecha del spawn | (14, −12.5) → (6.5, −12.5) → (6.5, −16.5) → (14, −16.5) |
| `Guard_Centinela_Sala` | Centinela | Sala, junto a la puerta a la cocina, en (−0.6, 0.4) | mira a la puerta delantera (−145°) |
| `Guard_Patrulla_C` | Patrullero | Dormitorio, entre el boquete de la fachada y la puerta a la sala | (6.2, −4.3) → (6.2, −1.6) → (3.6, −1.6) → (3.6, −4.3) (lejos del colchón y de la cajonera) |
| `Guard_Centinela_Cocina` | Centinela | Cocina, cerca de la puerta trasera, en (−1, 5.1) | mira a la puerta de la sala (−147°) |

- **De a 1–2 por vez,** en este orden:
  1. calle (B, y a veces el patrullero A cuando pasa por el frente);
  2. sala (centinela);
  3. dormitorio (C, si el jugador entra por el boquete o la puerta);
  4. cocina (centinela + Colmillo, que se rinde);
  5. patio (A + centinela del portón);
  6. Centavo.

  Las paredes cortan la vista (los guardias miran solo con la capa Default).
- **Ninguna ronda toca la del patrullero del patio.** La ruta A es el anillo exterior de la casa: x = ±9, z = −8 al frente, más el rincón del contador.
  - B va por la calle, a ≥ 4.5 m del tramo z = −8.
  - C va dentro del dormitorio, con la pared de por medio.
  - Los centinelas están dentro de la casa.
- **D2 sin cambios:** Centavo sigue esperando solo a `Guard_Patrulla_A` y `Guard_Centinela` (`ContadorCentavoBuilder.YardGuardNames`).
- **Cómo se colocan:** `LevelBuilder_NivelPorky.ExtraGuards` (datos) + `PlaceExtraGuards()`. Es idempotente:
  - crea o reubica los marcadores `Patrol_B_*`, `Patrol_C_*`, `Sentinel_Sala` y `Sentinel_Cocina`;
  - crea o reubica a los guardias bajo `--- ACTORS ---`;
  - asigna `enemyType` y `patrolPoints`, y borra marcadores que sobren.

  Lo llaman **Generar Nivel Porky** (en `BuildActors`) y **Contador → Construir patio y cuarto**.
- **Arreglo necesario en `ContadorYardBuilder.ApplyGuardRoute`:** tomaba "el primer marcador de centinela" y movía al portón del patio a **cualquier** guardia centinela sin diálogo. Con los centinelas nuevos, los habría llevado a todos al patio. Ahora busca **por nombre** (`Sentinel_PatioTrasero` y `Guard_Centinela`). En `BuildActors` el centinela del patio también se busca por nombre.
- **NavMesh:**
  - El horneado lo hacen los dos menús (con el builder heredado `NavMeshBuilder.BuildNavMesh()`, como en la 3C.1). Los guardias no son estáticos: no cambian el NavMesh.
  - **Comprobación nueva** (`ValidateExtraGuards`, la corren `ValidatePaths` y por lo tanto los dos menús): cada punto de cada guardia nuevo a ≤ 0.6 m del NavMesh y cada tramo de las rondas B y C con camino completo. Cada guardia saca una línea ✓/✗ en el cuadro del menú y en la consola.
  - **No se ejecutó:** hay que leer ese cuadro.

## 3. Munición: la cuenta
- **Escopeta** (`ShotgunController`): 8 perdigones × 15 de daño = **120 por disparo** si entran todos. **No hay caída por distancia** en el código.
- **Dispersión** 5°: cada eje se desvía hasta ±0.05 × distancia (uniforme). Al apuntar, × 0.6.
- **Guardia:** 60 de vida (`EnemyHealth` por defecto) → hacen falta **4 perdigones** (60 / 15).
- **Hitbox:** cápsula de 0.8 m de ancho y 2 m de alto. Disparando al centro, la fracción de perdigones que entra en horizontal es min(1, 0.4 / (0.05 · d)):

| Distancia | Perdigones que entran | Daño | ¿Muere de un tiro? |
|---|---|---|---|
| ≤ 8 m | 8 (100 %) | 120 | sí |
| 10 m | ≈ 6.4 (80 %) | ≈ 96 | sí |
| 12 m | ≈ 5.3 (67 %) | ≈ 80 | sí |
| 15 m | ≈ 4.3 (53 %) | ≈ 64 | justo |
| 20 m | ≈ 3.2 (40 %) | ≈ 48 | no (dos tiros) |

  Los guardias pegan cuerpo a cuerpo y corren a 5 m/s, así que casi todos los disparos son a menos de 8 m.
- **Jugador promedio:** supuse que 2 de cada 3 disparos van bien apuntados, o sea **1.5 tiros por guardia**. La versión pesimista: **2 tiros por guardia**.

| Blanco en el nivel 1 | Promedio | Pesimista |
|---|---|---|
| 6 guardias que pelean hasta morir (A, centinela del patio, B, C, sala, cocina) | 6 × 1.5 = 9 | 6 × 2 = 12 |
| Colmillo (un tiro lo rinde: 60 → 15 ≤ 18 con 3 perdigones; los demás no cuentan por la ventana de rendición) | 1.5 | 2 |
| Centavo (solo se le puede dar asomado: 0.4–0.7 s; el banco tapa cuando se cubre) | 3 | 4 |
| Rematar a Centavo herido (D4, opcional) | — | 1 |
| **Total** | **≈ 13.5** | **≈ 19** |

- **Disponible:** 6 en el cargador + 18 de reserva = **24 cartuchos**. Recargar no desperdicia cartuchos.
- **Margen:** ≈ 10 en el promedio (44 %) y 5 en el pesimista (21 %). **Alcanza sin recogibles.**
- **Además:** el jugador tiene 100 de vida y un guardia quita 15 cada 1.5 s (7 golpes); de a 1–2 guardias por vez, morir por falta de balas no debería pasar.
- **Recogibles de munición:** **no existe ningún sistema.** Solo está el método `ShotgunController.AddAmmo()` y nadie lo llama. No se construyó uno nuevo porque no hace falta. Si la prueba muestra que falta, habría que hacer un recogible pequeño (lo dejo anotado, no hecho).
- **Nota:** cada escena trae su propio Player, así que en el nivel 2 se empieza otra vez con 6 + 18.

## 4. GDD
`Docs/GDD.md`, "Nivel 1: Casa de Porky": contenido actualizado.
- **Pistas:** nota, contrato, `registro_01` de Centavo y el testimonio de Colmillo.
- **Enemigos:** 6 que pelean hasta morir: calle, sala, dormitorio, cocina, y en el patio el patrullero y el centinela.
- **Interrogables:** Colmillo y Centavo.
- **Munición:** sin recogibles.

## Menús que David debe correr
1. Abrir Unity y dejar que compile.
2. Con NivelPorky abierta, **Plomo y Paja → Contador → Construir patio y cuarto**:
   - pone los 4 guardias nuevos;
   - vuelve a acomodar la ruta y el centinela del patio;
   - hornea el NavMesh dos veces;
   - muestra el cuadro.

   Todo debe salir ✓, incluidas las 4 líneas de los guardias nuevos.

   Alternativa: **Plomo y Paja → Generar Nivel Porky** (rehace toda la escena; también los incluye y los valida en la consola).
3. **Ctrl+S** y commit de `NivelPorky.unity` y `Scenes/NivelPorky/NavMesh.asset`.

## Qué no se pudo compilar/probar y qué debe revisar David en el Editor
- **Compilado: sí.** Roslyn de Unity 2022.3.62f3: runtime y editor con **0 errores**.
- **No probado:** el menú, el horneado, la validación y nada en Play.
- **Revisar en Play:**
  1. Que se pelee de a 1–2 guardias: calle → sala → (dormitorio) → cocina → patio. Si dos zonas se juntan, mover los puntos en `LevelBuilder_NivelPorky.ExtraGuards` y volver a correr el menú.
  2. Que el patrullero B no vea al jugador apenas aparece. Si lo hace, correr su ronda más lejos en x.
  3. Que el centinela del patio y el patrullero A sigan en su lugar, y Centavo siga esperándolos solo a ellos.
  4. Munición: terminar el nivel y anotar cuántos cartuchos sobran.

## Archivos creados/modificados
Creados:
- `Docs/audits/AUDIT_contador_3C4_2.md` (este archivo).

Modificados:
- `Scripts/Editor/LevelBuilder_NivelPorky.cs`: `ExtraGuards`, `PlaceExtraGuards`, `ValidateExtraGuards`; el centinela del patio se busca por nombre.
- `Scripts/Editor/ContadorYardBuilder.cs`: centinela del patio por nombre, coloca los guardias extra y los valida junto con el patio.
- `Docs/GDD.md`: contenido del nivel 1.

Sin cambios (porque se paró la parte 1): `DialogueDatabase.cs`, `DialogueNPC.cs`, los marcadores y el bloque de interrogables, y los documentos de Colmillo.

## Commits
- `d0741ef`: dia4(david): 3C.4.2 cuatro guardias normales en la calle y la casa antes del cuarto de Centavo
- Commit siguiente: esta auditoría.
