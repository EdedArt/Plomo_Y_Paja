# Auditoría Fase 3A6 — Decisiones del orquestador sobre la 3A5 (rubber hose y audio)

ESTADO: PENDIENTE DE REVISIÓN

## 1. Resumen
- Los guardias reciben `RubberHoseAnimator` (de Edgar) en `SpriteHolder` desde el generador, y el cadáver tiene un solo aplastado de muerte.
- Todo el audio sale de `_SinUso` y va a `Voices/`, `SFX/` y `Music/`. Ya suenan los quejidos y el aviso de poca vida de Wilbur, los pasos madera/barro, la lluvia en bucle (ahogada bajo techo) y los truenos.
- Todo se hizo en `integracion/dia3`. Los dos assemblies compilan con 0 errores, pero nada se probó en Unity. Los cambios locales sin commitear de David no se tocaron.

## 2. Commits
| Hash | Mensaje |
|---|---|
| 0868cc1 | dia3a6(edgar): rubber hose en los guardias y un solo aplastado de muerte |
| f789a86 | dia3a6(cristian): sonidos de wilbur, pasos y lluvia |
| 60fa5c7 | chore: refrescar grafo de scripts tras 3A6 |
| (este commit) | docs: PR actualizado con 3A6 |

## 3. RubberHose
- **Cableado:** `TestPrefabGenerator` añade `RubberHoseAnimator` al `SpriteHolder` del guardia (el objeto del SpriteRenderer y de `BillboardSprite`). `Enemy_Interrogable`, que es variante del guardia, lo hereda. El componente busca solo `EnemyAI.OnStateChanged` y `EnemyHealth.OnDamaged` en los padres. El generador ya **no** añade `RubberHoseMotion`, que hacía lo mismo (reposo, pasos, golpe, temblor) sobre `Visual`: así no hay dos gomas sumadas. El script se conserva para prefabs viejos. El generador fija `anchorFeet = false` y `flipWithMovement = false`. El estirón de alerta de Edgar ya se dispara al entrar en Alert (evento `OnStateChanged`), aunque la pose sea idle.
- **Aplastado quitado:** el de `EnemyCombatFx` al caer (escalaba `Visual` 0.15 s). Queda el de RubberHose, que ahora espera a que el cuerpo aterrice (`PlayDeathSquash()`, que llama `EnemyCombatFx`), porque si no se aplastaría en pleno vuelo. `EnemyCombatFx` conserva el destello, el empujón, la sangre, el hit-stop, el vuelo girando, el humo, las estrellas, los dientes y el desvanecido final. Ese desvanecido encoge `Visual` a 0 a los 2 s: es la desaparición, no un aplastado.
- **Pies:** el origen de `SpriteHolder` coincide con los pies: la raíz del prefab está en los pies, `SpriteHolder` cuelga en (0,0,0) y el importador pone el pivote del sprite en los pies. Por eso la escala sale desde el piso y el ancho va en el eje de pantalla del billboard. `AlignFeetToGround` mueve solo el `baseOffset` del agente en la raíz y mide desde la raíz; RubberHose toca solo la escala y la posición local de `SpriteHolder`, así que no se pisan. El saltito al caminar (6–12 cm, vuelve a 0) es intencional; la sombra blob queda en la raíz.

## 4. Audio
Todo lo que estaba en `_SinUso` se movió con `git mv` (y se borró `_SinUso/`). La librería debe listar **48 voces y 17 efectos**; el builder ahora también lee `Music/`.

| id | Dónde suena | Condición |
|---|---|---|
| `wilbur_dano_01..03` | `PlayerHealth.TakeDamage` (2D) | Al recibir daño y seguir vivo; al azar sin repetir; enfriamiento de 0.6 s (`Mix.hurtVoiceCooldown`) |
| `wilbur_pocavida_01` | `PlayerHealth.CheckLowHealth` (2D) | Una vez al bajar del 30 %; se re-arma por encima del 40 % (mismo cruce que la IA). Ese golpe no suena además el quejido |
| `pasos_madera` (3 variantes) | `PlayerFootsteps` (nuevo, raíz del Player) | Caminando en el piso y bajo techo (estado de `RainEmitter`) |
| `pasos_barro` (3 variantes) | `PlayerFootsteps` | Caminando en el piso al aire libre |
| `lluvia_loop` | `RainEmitter` (AudioSource 2D en bucle) | Siempre que hay lluvia. Afuera a `Mix.rainOutdoor`; bajo techo a `Mix.rainIndoor` con un paso bajo de 900 Hz, con fundido |
| `trueno` | `RainEmitter` (2D) | Cada 25–60 s al azar, solo al aire libre y sin pausa; dos destellos breves de `RenderSettings.sun` si hay sol |
| `puerta_cruje_01` | — (no se cableó) | El nivel no tiene puertas que se abran: la delantera está colgada de una bisagra, estática y sin collider |
| `musica_jazz`, voces de `donlobo`, `porky`, `max`, `narrador` | — | Movidos a su carpeta final; se cablean en las fases 4–5 |

Pasos: cada archivo de Cristian trae 3–7 pasos seguidos (2.5–3 s), no un paso suelto. Por eso se reproducen como tiras encadenadas mientras Wilbur camina, con un fundido de 0.12 s al detenerse. El ritmo sigue a la velocidad: el pitch va de 0.85 a 1.3, así que al correr suenan más seguidos.
**Volúmenes en un solo sitio:** `AudioLibrary.asset` (`voiceVolume`, `sfxVolume` y el bloque *Mix*: `footsteps`, `rainOutdoor`, `rainIndoor`, `rainIndoorCutoff`, `thunder`, intervalos de trueno y `hurtVoiceCooldown`). API mínima nueva: `PlayVoice2D`, `PlaySfx2D(nombre, escala)`, `GetSfx`, `Mix` y `SfxVolume`.

## 5. Menús a ejecutar en orden (fuera de Play)
1. Abrir el proyecto en `integracion/dia3` y esperar la importación y la compilación.
2. Plomo y Paja → Configurar Renderer (SSAO)
3. Plomo y Paja → Crear Prefabs de Prueba (guardias con `RubberHoseAnimator`, Player con `PlayerFootsteps`)
4. Plomo y Paja → Reconstruir Librería de Audio (debe decir "48 voces y 17 efectos")
5. Plomo y Paja → Generar Nivel Porky
6. Commitear los assets generados en `chore: assets generados 3A2-3A6`.

## 6. Pruebas en Unity (las llena David)
| # | Prueba | ¿OK? | Notas |
|---|---|---|---|
| 1 | Consola limpia, ningún "Sin audio para…" | | |
| 2 | Pies en el suelo, sombra bajo los zapatos | | |
| 3 | Casa pobre/saqueada adentro y afuera, nevera, goteras, farol roto | | |
| 4 | Pistas recogibles; jugador y guardias recorren todo sin atascarse | | |
| 5 | Guardia: patrulla, persigue, se rinde, muere (comportamiento de Edgar) | | |
| 6 | Rubber hose: rebote al caminar, aplastado al golpe, temblor al rendirse, aplastado al morir (uno solo) | | |
| 7 | Voces del guardia (ver, daño, rendirse, morir, patrulla) y SFX de escopeta | | |
| 8 | Wilbur se queja al recibir daño y avisa con poca vida | | |
| 9 | Pasos madera adentro / barro afuera | | |
| 10 | Lluvia con sonido afuera, apagada adentro, truenos de vez en cuando | | Adentro la lluvia queda baja y ahogada, no muda (decisión de la orden: "muffled") |

**Errores de consola:**

**Captura(s):**

## 7. Riesgos / preguntas
- **El placeholder ya no se mueve:** al quitar `RubberHoseMotion`, la cápsula gris (solo visible si el set del guardia no tiene dibujos) ya no respira ni rebota; solo el sprite tiene goma. ¿Basta así?
- **Pasos en tiras:** como los archivos traen varios pasos, el ritmo no queda sincronizado con la velocidad real (solo se acelera con el pitch). Si se quiere paso a paso exacto, Cristian tendría que entregar pasos sueltos (`pasos_madera_01..` de ~0.3 s).
- **Destello del trueno:** depende de `RenderSettings.sun`. Si la escena de noche no tiene sol asignado, el trueno suena sin destello.
- **Pausa:** el bucle de lluvia sigue sonando con el juego en pausa (los truenos y los pasos sí se detienen). Si molesta, `GameManager` puede usar `AudioListener.pause`.
- **Metas de audio:** los `.meta` se movieron con sus archivos y conservan el GUID. Unity completará los ajustes de importación en la primera importación; hay que commitearlos con los assets generados.
