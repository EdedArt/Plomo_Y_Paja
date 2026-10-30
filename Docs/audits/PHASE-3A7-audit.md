# Auditoría Fase 3A7 — Arreglos del playtest (voces, warnings de mallas, nevera)

ESTADO: PENDIENTE DE REVISIÓN

## 1. Resumen
- Las voces de los enemigos pasan por un único canal con prioridades y probabilidades: ya no se solapan, la muerte siempre suena (sonido no verbal) y la frase hablada sale solo de vez en cuando. Todo se ajusta en `AudioLibrary.asset` → *Mix*.
- Los warnings "has more materials than submeshes" se arreglan en el origen para todas las piezas del kit.
- La nevera sí se construía; el "bloque morado" era su sombra violeta por defecto (hipótesis, sin verlo en pantalla). No se probó nada en Unity. No se commitearon los assets generados de David.

## 2. Commits
| Hash | Mensaje |
|---|---|
| 5939953 | dia3a7(cristian): voces de guardias sin solaparse y muerte con sonido |
| 2b709fd | dia3a7(cristian): materiales igual a submallas en el kit |
| 1ff4b2d | dia3a7(cristian): nevera de la cocina |
| (este commit) | docs: auditoría 3A7 |

## 3. Reglas de voz
Un solo `AudioSource` para todas las voces de enemigos (guardias e informantes). Prioridad: patrulla < ver < daño < rendición < muerte. Una voz nueva solo interrumpe si tiene **mayor** prioridad; la muerte siempre corta. Entre frases hay 4 s de enfriamiento global, salvo que la nueva tenga mayor prioridad que la anterior (así una rendición no queda muda por un quejido de hace 1 s).

| Evento | ¿Siempre? | Probabilidad | Enfriamiento | Clip |
|---|---|---|---|---|
| Muerte, sonido no verbal | Sí | 100 % | — | `caida_silbato` al empezar + `golpe_cartoon` / `humo_puf` / `estrellas` al caer (ya los pone `EnemyCombatFx`) |
| Muerte, frase | No | 25 % | 20 s entre frases de muerte (global) | `guardia_morir_01..03` / `informante_morir_01` |
| Rendición | Sí (configurable) | 100 % | 4 s global, salvo mayor prioridad | `guardia_rendir_*` / `informante_rendir_*` |
| Daño | No | 40 % | 1.5 s por enemigo (se gasta en el intento: 8 perdigones = 1 sola tirada) | `guardia_dano_*` / `informante_dano_01` |
| Ver (alerta) | No | 50 %, una sola tirada por episodio de alerta | 4 s global; se re-arma al volver a patrullar | `guardia_ver_*` / `informante_ver_*` |
| Patrulla | No | 30 % | Solo si nadie habló en los últimos 15 s | `guardia_patrulla_*` |
| Efecto idéntico (mismo id) | — | — | 0.08 s | Impacto, sangre, etc.: un disparo de 8 perdigones suena una vez |

Además: al morir se corta cualquier voz de menor prioridad y se oculta el globo de daño. El golpe mortal nunca lanza el quejido de daño. El globo de texto aparece solo si la frase se aceptó. `AICompanion` (texto) no se tocó. Parámetros en `AudioLibrary` → *Mix*: `enemyPhraseCooldown`, `sfxDedupeSeconds`, `deathPhraseChance`, `deathPhraseCooldown`, `surrenderPhraseChance`, `hurtPhraseChance`, `hurtPhraseCooldown`, `spottedPhraseChance`, `patrolPhraseChance`, `patrolQuietSeconds`.

## 4. Sonido de muerte no verbal
Se inspeccionaron las tres `guardia_morir_*`: duran 2.22 s, 2.67 s y 1.98 s, y son frases ("¡Aaaay, mamá!", etc.), no gruñidos. Ninguna baja de 0.8 s, así que no sirven como sonido corto. Se usan los efectos de dibujos animados que ya sonaban en la muerte: `caida_silbato` (2.48 s, el silbato de caída al salir volando) y `golpe_cartoon` (1.0 s, el golpe al aterrizar), más `humo_puf` (0.84 s). Suenan siempre desde `EnemyCombatFx`, sin pasar por la regla de la frase. No hizo falta cambiar ese código ni recortar audios.

## 5. Warnings "more materials than submeshes"
**Causa:** `CartoonKit.WallDamage` pasa siempre la misma lista de 5 materiales (paja, papel, madera, hueco, clavos), pero la variante sin tablas solo usa 2 submallas. La asignación de materiales estaba en `CartoonKit.MeshObject`, que alimenta toda pieza del kit (23 llamadas).
**Arreglo:** `MeshObject` pasa los materiales por `FitMaterials(mesh, mats)`: deja exactamente un material por submalla (recorta los sobrantes; si faltaran, repite el último). Afecta a todas las piezas de golpe: `DanoPared_*` y cualquier otra. Es el único sitio que asigna materiales múltiples (el otro es la carga de modelos externos, que usa sus propios materiales). Hay que regenerar el nivel para que se apliquen.

## 6. Bloque morado
La nevera sí se construía bien: en la escena generada está `Nevera` en (7.4, 5.25) con la malla `NeveraAbierta` (5 submallas) y sus 5 materiales del kit con el shader correcto. Tampoco hay materiales rosados ni sin shader. El único material morado del nivel es `Kit_Sofa`, y el sofá está en la sala, no en la cocina.
**Hipótesis:** el shader `CartoonEnvironment` tiñe toda superficie en sombra con un violeta por defecto (0.42, 0.33, 0.62). La 3A3 corrigió eso a azul frío solo en empapelado, piso y cielorraso. La nevera crema, en la sombra del interior, se veía como un bloque lavanda.
**Hecho:** `Nevera`, `NeveraAbollada` y `Cromo` (usado solo en la nevera) pasan a sombra azul fría. No pude confirmarlo visualmente: si el bloque morado sigue, mandar captura. Mesa y sillas (`MaderaClara`/`Madera`) se dejaron como están porque se usan también afuera.

## 7. Menús a ejecutar (fuera de Play)
1. Plomo y Paja → Generar Nivel Porky (aplica `FitMaterials` y las sombras de la nevera; regenera las mallas del kit).
2. Plomo y Paja → Reconstruir Librería de Audio (solo si el asset no tiene el bloque *Mix* nuevo; Unity lo rellena con los valores por defecto al abrir el proyecto).
3. Commitear los assets generados (`chore: assets generados 3A2-3A7`).

## 8. Pruebas (las llena David)
| # | Prueba | ¿OK? | Notas |
|---|---|---|---|
| 1 | Consola sin warnings "more materials than submeshes" | | |
| 2 | Al matar un guardia nunca se oyen 2 voces a la vez | | |
| 3 | Siempre suena un sonido de muerte; frase solo de vez en cuando | | |
| 4 | Guardias hablan poco y sin cansar (ver, patrulla, daño) | | |
| 5 | Nevera de los años 30 en la cocina | | |
| 6 | Pistas: nota (cómoda) y contrato (bajo el sofá) se ven y se recogen | | |

**Errores de consola:**

**Captura(s):**

## 9. Riesgos
- **Bloque morado sin confirmar:** la causa es una hipótesis. Si no era la nevera, hay que ver una captura de dónde está.
- **Muerte con efectos largos:** `caida_silbato` y `estrellas` duran 2.5 s y pueden sonar a la vez entre sí y con la frase (25 %). Si se oye cargado, bajar `deathPhraseChance` o recortar `estrellas`.
- **Un solo canal:** si dos guardias hablan en peleas masivas, el segundo se omite (y su globo no sale). Es lo pedido, pero puede parecer que un guardia "no reacciona".
- **Voz global estática:** la regla vive en variables estáticas de `AudioLibrary`; se reinician con *Reload Domain* al entrar en Play. Si David desactiva Enter Play Mode Options, no hay problema; si las activa sin recargar el dominio, quedan restos entre partidas (solo temporizadores).
