# Guion — Escena de interrogatorio: "Centavo", el contador de Don Lobo
**Nivel:** 1 (Casa de Porky, cuarto de herramientas del patio) · **Pista que entrega:** `registro_01` (Registro de deudas) · **Versión:** 1.2 (1 nov 2026)

> Copiar este archivo al repo como `Docs/guiones/contador.md`. Claude Code lo lee para la fase 3C.
> **Cambios en 1.2:** sprites con fondo magenta, pares de boca `_01` cerrada / `_02` abierta para el lip-sync, nuevos `herido_02`, `blink_01`, `idle_02`, `limp_02` y la carpeta de los originales (sección 7).

---

## 1. El personaje
**Centavo** es un lobo flaco, gris azulado, con el pelo revuelto y la cola peluda. Lleva gafas redondas de alambre, una gorra verde de ala corta con banda verde clara, un lápiz amarillo detrás de la oreja derecha, camisa crema con corbata negra, ligas negras en las mangas, guantes blancos de cuatro dedos, pantalón café y zapatos gastados con parches. Carga un libro de cuentas café y un revólver diminuto que le tiembla en la mano. No es un matón: es el que lleva los números de Don Lobo. Lo mandaron a la casa de Porky a ver si quedaba algo de valor para cobrarse la deuda.

---

## 2. Cómo pasa en el juego
1. **El lugar:** un **cuarto de herramientas** nuevo en el patio trasero de la casa de Porky, una caseta de tablas torcidas con banco de trabajo, herramientas colgadas, barriles y una lámpara de aceite.
2. **Para llegar hay que pasar por los guardias.** El portón del patio está en el camino de los guardias (patrullero y centinela), así que hay que eliminarlos o neutralizarlos. Centavo no se activa hasta que los guardias están fuera de combate.
3. **El tiroteo:** cuando Wilbur se acerca (~12 m), Centavo grita desde la puerta del cuarto. **No se mueve de su sitio:** se agacha detrás del banco de trabajo (`cover`), se asoma (`peek`) y dispara (`shoot`) con mala puntería, una y otra vez.
4. **Le das:** al primer impacto suelta el revólver y **cae herido**, sentado contra el banco, sangrando (sangre de caricatura) y agarrándose el costado. Desde ahí **suplica** mientras Wilbur se acerca.
5. **Te acercas** (≤ 3 m): la cámara baja y se acerca a él, como si Wilbur se agachara, y **empieza la escena de interrogatorio**: mundo en pausa y desenfocado, Centavo grande en pantalla, herido en el piso, y el papel del diálogo abajo.
6. **Al final** suplica, la IA habla arriba y Wilbur decide: **"Jalar el gatillo"** o **"Dejarlo ir"**.

---

## 3. Dirección de voz (para Cristian)
| Personaje | Voz |
|---|---|
| **Centavo** | Aguda, nasal y rápida. Tartamudea cuando tiene miedo ("s-sí, señor"). Cuando habla de números se calma de golpe y se vuelve preciso, casi orgulloso. Oficinista de los años 30, cómico pero no payaso. **Herido:** voz quebrada, con quejidos entre palabras ("ay…"), pero se le entiende todo. Si te gusta la voz del `informante`, puede ser la misma. |
| **Wilbur** | Grave, ronca y cansada. Habla lento, como narrador de radio-drama noir. Nunca grita: amenaza en voz baja. Debe sonar como el mismo Wilbur de `wilbur_dano`. |

**Formato:** WAV, 44 100 Hz, mono, sin silencio largo al inicio ni al final (máx. 0.1 s).
**Carpetas:** `Audio/Voices/contador/` y `Audio/Voices/wilbur/` (dentro de la misma carpeta de Assets que contiene `Art/`). El nombre del archivo es exactamente el id (ej.: `contador_int_00.wav`).

---

## 4. Antes de la escena (tiroteo)
| id | Cuándo | Texto | Emoción / sprite |
|---|---|---|---|
| `contador_ver_01` | Wilbur se acerca | ¡A-alto ahí! ¡Estoy armado! …Creo que está cargado. | asustado / `peek` |
| `contador_ver_02` | disparando | ¡No se acerque! ¡Le advierto que sé contar hasta seis! | asustado / `shoot` |
| `contador_ver_03` | disparando | ¡Fuera de aquí! Este cuarto es propiedad del señor Lobo. Bueno… embargado. | nervioso / `peek` |
| `contador_cubre_01` | se cubre | ¡Ay, mamá, ay, mamá, ay, mamá! | pánico / `cover` |
| `contador_cae_01` | recibe el disparo | ¡AY! ¡Me dio! ¡Me rindo, me rindo, no dispare más! | dolor / `hit` → `herido` |
| `contador_herido_01` | tirado, Wilbur aún lejos | Ya no puedo ni levantar el revólver… ¡Por favor, déjeme hablar! | suplicando / `herido` |
| `contador_herido_02` | tirado, se repite si Wilbur tarda | ¡No me remate! ¡Yo sé cosas, señor! ¡Sé números! | suplicando / `herido` |

---

## 5. La escena (Centavo herido en el piso)

**Confianza:** empieza en 0. Calmar suma 1, intimidar resta 1 y preguntar directo no cambia nada.
- Confianza **≥ 0**: en la ronda 1 dice la verdad (resultado *Helpful*).
- Confianza **< 0**: miente (resultado *Misleading*), salvo que Wilbur elija "Mírame a los ojos", que lo quiebra.

### Ronda 0 — Apertura
| id | Quién | Texto | Sprite |
|---|---|---|---|
| `contador_int_00` | Centavo | ¡No me remate, señor! ¡Yo no soy de los que pegan! Soy el contador. Números, solo números. | `scared` |

**Opciones de Wilbur:**
| # | id | Texto | Efecto |
|---|---|---|---|
| 1 | `wilbur_int_0A` | Habla rápido o la próxima no va al costado. | confianza −1 |
| 2 | `wilbur_int_0B` | Tranquilo. Si cooperas, sales de aquí con vida. | confianza +1 |
| 3 | `wilbur_int_0C` | ¿Qué hace el contador de Don Lobo escondido en el patio de mi hermano? | confianza 0 |

**Respuestas:**
| id | Tras | Texto | Sprite |
|---|---|---|---|
| `contador_int_0A` | 1 | ¡S-sí, señor! ¡Lo secuestraron! ¡Se lo llevaron a la fuerza, pataleando y todo! *(miente)* | `scared` |
| `contador_int_0B` | 2 | G-gracias… ay… El jefe me mandó a revisar la casa. A ver si quedaba algo de valor para cobrarse. | `nervous` |
| `contador_int_0C` | 3 | Vine por las cuentas, señor. Su hermano tiene una cuenta abierta con el jefe. Muy, muy abierta. | `talk` |

### Ronda 1 — Lo importante
**Opciones de Wilbur:**
| # | id | Texto |
|---|---|---|
| 1 | `wilbur_int_1A` | ¿Cuánto le debe Porky? |
| 2 | `wilbur_int_1B` | ¿Dónde tienen a mi hermano? |
| 3 | `wilbur_int_1C` | Mírame a los ojos. Si me mientes, lo voy a saber. |

**Respuestas (según la confianza):**
| id | Opción | Condición | Texto | Sprite | Efecto |
|---|---|---|---|---|---|
| `contador_int_1A_v` | 1 | confianza ≥ 0 | Tres mil doscientos pesos… y los intereses de la semana. Pero no es un préstamo, señor. Él los sacó de la caja fuerte del jefe. Aquí está anotado, con fecha y todo. | `talk` (orgulloso) | entrega `registro_01` · Helpful |
| `contador_int_1A_m` | 1 | confianza < 0 | ¿Deber? ¡Nada, nada! Su hermano es un santo. El jefe es el malo, se lo juro. | `nervous` | Misleading |
| `contador_int_1B_v` | 2 | confianza ≥ 0 | En la guarida del jefe, al otro lado del campamento. Nadie le ha tocado un pelo… El señor Lobo solo quiere lo suyo. | `talk` | Helpful |
| `contador_int_1B_m` | 2 | confianza < 0 | Lo… lo tienen colgado de los pies en el sótano. Sí. Muy feo. Vaya rápido y no pregunte más. | `nervous` | Misleading |
| `contador_int_1C` | 3 | siempre | ¡Está bien, está bien! Nadie lo secuestró. Él solito tocó la puerta del jefe… y el jefe todavía está esperando su plata. | `scared` | Helpful (sin la cifra) |

### Ronda 2 — Súplica (siempre)
| id | Texto | Sprite |
|---|---|---|
| `contador_int_2` | Ya le dije todo lo que sé… ay… Tengo esposa, tres cachorros… y una calculadora que todavía estoy pagando a plazos. | `plead` |

### Pensamiento de la IA (texto arriba, sin voz)
Aparece después de la súplica y antes de la decisión. Lo genera la API con el contexto de la conversación (qué dijo, si mintió, la confianza, que está herido). Si no hay conexión, se usa una de estas líneas de respaldo:
- "Un contador vivo lleva la cuenta de quién lo dejó ir. Uno muerto no le cuenta nada a nadie."
- "Tiembla como hoja. ¿Miente por miedo… o el miedo es lo único que no está fingiendo?"
- "Porky nunca supo sumar. ¿Y si este es el único que tiene las cuentas claras?"
- "Ya está en el suelo, Wilbur. Lo difícil no es disparar… es saber a quién le estás apretando el gatillo."
- *(solo si mintió)* "Dijo que lo colgaron de los pies. Guarda bien esas palabras… y compáralas después."

### Decisión
Dos opciones grandes: **"Jalar el gatillo"** / **"Dejarlo ir"**.

| id | Rama | Quién | Texto | Sprite |
|---|---|---|---|---|
| `wilbur_dec_matar` | matar | Wilbur | Las cuentas se saldan, Centavo. | — |
| `contador_muerte_01` | matar | Centavo | ¡Espere, todavía no he cuadrado el…! *(se corta con el disparo)* | `scared` → `death` |
| `wilbur_dec_perdonar` | perdonar | Wilbur | Lárgate. Y no vuelvas a esta casa. | — |
| `contador_perdon_01` | perdonar | Centavo | ¡Gracias, señor! ¡Que Dios se lo pague… con intereses bajitos! | `relief` |
| `contador_perdon_02` | perdonar, si aún no dio `registro_01` | Centavo | Tome… la hoja del libro. Yo no la vi, usted no me la dio. | `talk` → entrega `registro_01` |

**Al perdonarlo:** se levanta como puede y se va cojeando por el portón del patio (`limp`, alternando `limp_01` y `limp_02`), mientras dice `contador_huye_01`.
**Al matarlo:** queda en el piso con su dibujo de muerte.

**Consecuencias:**
- **Perdonar:** siempre da `registro_01` (por la respuesta 1A o por `perdon_02`).
- **Matar:** solo tienes `registro_01` si te lo dio antes.
- `GameStateTracker` registra `contador = Perdonado / Muerto`, el resultado del interrogatorio (Helpful / Misleading) y si mintió. La IA y Don Lobo lo pueden mencionar más adelante.

### Pensamiento después de la decisión (texto arriba, sin voz)
*(Agregado en la fase 3C.4.1.)* Al cerrarse la escena, el mundo vuelve a correr, Wilbur no se puede mover y aparece un último pensamiento arriba. Lo genera la API con lo que dijo Centavo, si mintió, si entregó el registro y la decisión. Si no hay conexión (o tarda más de 4 s), se usa una de estas líneas:

**Si lo dejó ir** (la cámara lo sigue cojeando hacia el portón y después mira al cielo):
- "Se va cojeando con tus secretos en el bolsillo. Ojalá la gratitud le dure más que el miedo."
- "Dejaste ir al único que lleva las cuentas. Algún día alguien te va a cobrar este favor."
- "Un lobo menos en el suelo. ¿Fue piedad, Wilbur… o solo cansancio?"

**Si lo mató** (la cámara se queda en el cuerpo y sube despacio):
- "Las cuentas se saldaron. Lo que nadie te dijo es quién paga los intereses."
- "Ya no tiembla. Tú sí."
- "Un contador menos. Los números de Porky siguen sin cuadrar."
- *(solo si mintió)* "Murió mintiendo… o eso quieres creer, para que pese menos."
- *(solo si dijo la verdad y no mintió)* "Te dijo la verdad y le pagaste con plomo."

### Pensamiento si lo rematas sin escucharlo
*(Agregado en la fase 3C.5.1.)* Si Wilbur le dispara a Centavo herido **antes** de la escena (D4), no hay interrogatorio. En vez del comentario de la IA que sale a veces al matar a un guardia, aparece **siempre** un pensamiento en los subtítulos normales del juego (sin bloquear al jugador ni mover la cámara). Lo genera la API ("remataste a Centavo, herido y pidiendo que lo escucharas, sin escucharlo; era el contador de Don Lobo y sabía de la deuda de Porky"). Si no hay conexión (o tarda más de 4 s), se usa una de estas líneas:
- "Ni siquiera lo dejaste hablar. Lo que sabía se quedó en el suelo con él."
- "Pedía que lo escucharas. Ahora ya no va a decir nada… ni a favor ni en contra de Porky."
- "Un tiro rápido ahorra preguntas. También respuestas."

---

## 6. Resumen de audios para Cristian (29 archivos)
**Centavo (21):** contador_ver_01, contador_ver_02, contador_ver_03, contador_cubre_01, contador_cae_01, contador_herido_01, contador_herido_02, contador_int_00, contador_int_0A, contador_int_0B, contador_int_0C, contador_int_1A_v, contador_int_1A_m, contador_int_1B_v, contador_int_1B_m, contador_int_1C, contador_int_2, contador_muerte_01, contador_perdon_01, contador_perdon_02, contador_huye_01 ("¡Gracias, gracias, gracias… ay, mi costado!" mientras se va cojeando)

**Wilbur (8):** wilbur_int_0A, wilbur_int_0B, wilbur_int_0C, wilbur_int_1A, wilbur_int_1B, wilbur_int_1C, wilbur_dec_matar, wilbur_dec_perdonar

---

## 7. Sprites — `Art/Characters/contador/` (22 archivos)
**Flujo:** los originales se generan con IA sobre fondo **magenta puro `#FF00FF`** (la gorra es verde, por eso no se usa croma verde) y se dejan en `_entrante_personajes/Contador/` (junto a `_entrante_viewmodel/`, no se sube a git). La herramienta de recorte los deja en `Art/Characters/contador/`: lienzo **1024×1024**, transparencia real, la parte más baja del dibujo tocando el borde inferior.

**Maestras:** de pie = la imagen principal de Centavo. Sentado = `herido_01`. Cada cuadro sale de su maestra; la única excepción son los `_02` de boca/parpadeo, que salen de su `_01` cambiando solo la boca.

**De pie, en el tiroteo** (sin lip-sync, se ve lejos):
| Archivo | Uso |
|---|---|
| idle_01, idle_02 | respirando, nervioso (se alternan) |
| peek_01 | asomado detrás del banco de trabajo |
| cover_01 | agachado detrás del banco, tapándose la cabeza |
| shoot_01 | disparando, con el revólver temblando |
| hit_01 | le dan: salta hacia atrás y suelta el revólver |
| limp_01, limp_02 | se va cojeando, agarrándose el costado; ciclo de caminar de 2 cuadros (pierna arriba / pierna apoyada) |

**Herido y sentado en el piso** (lo que se ve grande en la escena). Cada emoción tiene un **par de boca**: `_01` = boca cerrada, `_02` = boca abierta. Mientras suena la voz, el código alterna `_01`/`_02` según el volumen; en silencio queda `_01`.
| Sprite del guion | Archivos | Uso |
|---|---|---|
| `herido` | herido_01, herido_02 | sentado, sangrando, agarrándose el costado; súplicas mientras Wilbur se acerca |
| `talk` | talk_01, talk_02 | hablando tranquilo, casi orgulloso (números) |
| `scared` | scared_01, scared_02 | aterrado, orejas abajo, manos arriba |
| `nervous` | nervous_01, nervous_02 | sudando, acomodándose las gafas rotas |
| `plead` | plead_01, plead_02 | manos juntas, suplicando |
| `relief` | relief_01, relief_02 | alivio, sonrisa temblorosa |
| — | blink_01 | parpadeo (sale de `talk_01`); se muestra de vez en cuando en silencio |
| `death` | death_01 | muerto, desplomado contra el banco, ojos en X |
