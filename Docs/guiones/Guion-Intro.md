# Guion — Intro: "El libro", "El taxi" y "La llegada"
**Versión:** 1.1 (2 nov 2026) · **Va antes de:** Nivel 1 (Casa de Porky)

> Copiar este archivo al repo como `Docs/guiones/intro.md`. Claude Code lo lee para la fase INTRO.
> **Cambios en 1.1:** prompts por imagen para ChatGPT (sección 7) y tamaños de la tapa, guardas y papel a **1024×1536** (2:3, lo que genera ChatGPT). Los quads del libro siguen la proporción de la textura.

---

## 1. Cómo se ve todo, de principio a fin
1. **El libro** (escena nueva `Intro`, estilo Cuphead): de noche, una mesa de madera con una lámpara de aceite. Encima, un libro viejo y cerrado con el título **PLOMO Y PAJA**. La tapa se abre sola y aparece la portadilla. Después se pasan 6 páginas, **una por audio del narrador**: la ilustración va a la izquierda y el texto a la derecha, escrito a máquina al ritmo de la voz. Al terminar la última página, la cámara entra en la ilustración de Wilbur con la escopeta → negro → se empieza a oír la lluvia.
2. **El taxi** (Nivel 1): Wilbur va en el asiento de atrás de un taxi de los años 30. Todo está oscuro: solo las luces de la calle que pasan por la ventana, la lluvia y el motor. Se puede mirar un poco alrededor. El taxi frena en la esquina de la cuadra de Porky y hablan.
3. **La bajada:** negro corto, suena la puerta y Wilbur aparece en la acera. El taxi arranca y se va (se ve irse calle abajo).
4. **La casa:** la cámara gira sola hacia la casa destruida de Porky. Wilbur dice su línea y arriba aparece el **primer pensamiento de la IA**. Si se puede, sale el título **PLOMO Y PAJA** sobre la casa, como en una película.
5. **Sin arma:** el jugador camina libre, sin manos ni escopeta.
6. **El primer guardia:** apenas Wilbur ve al primer guardia, saca la escopeta (sonido de bombeo y una línea). Desde ahí el juego es normal.

Durante el taxi y la llegada hay franjas negras de cine arriba y abajo y subtítulos. Todo se puede saltar manteniendo ESPACIO.

---

## 2. El libro: páginas
| Página | Audio | Texto (exacto, va escrito en la página) | Ilustración (página izquierda) |
|---|---|---|---|
| Tapa | — | **PLOMO Y PAJA** | `libro_portada` |
| Portadilla | — | **Plomo y Paja** · *Una historia de los tres cerditos* | `libro_guardas` (a la izquierda) |
| 1 | `narrador_intro_01` | Érase una vez, en la ciudad de Porcópolis, tres hermanos cerditos. | `ilus_intro_01` |
| 2 | `narrador_intro_02` | Wilbur, el mayor, trabajaba de sol a sol. Su casa era de ladrillo, firme como su palabra. | `ilus_intro_02` |
| 3 | `narrador_intro_03` | Max, el del medio, trabajaba… a ratos. Lo que ganaba se le iba en joyas, y su casa de madera brillaba más de lo que debía. | `ilus_intro_03` |
| 4 | `narrador_intro_04` | Y Porky, el menor… Porky tenía un problema: los dados, las cartas y las deudas. | `ilus_intro_04` |
| 5 | `narrador_intro_05` | Su casa de paja nunca resistió mucho. Y una noche de lluvia, ya no había casa. Ni Porky. | `ilus_intro_05` |
| 6 | `narrador_intro_06` | Wilbur tomó su escopeta. Alguien iba a pagar por esto. Lo que no sabía era quién le debía a quién. | `ilus_intro_06` |

**Qué muestra cada ilustración** (los prompts completos están en la sección 7):
1. Porcópolis al atardecer desde una colina. Tres casitas en fila (ladrillo, madera, paja) y los tres cerditos de espaldas mirando la ciudad.
2. Wilbur en overol, sudando, cargando ladrillos. Detrás, su casa de ladrillo, sólida, con el sol en lo alto.
3. Max frente a un espejo, lleno de collares y anillos. Su casa de madera tiene adornos dorados y destellos.
4. Porky en una mesa de juego con dados, naipes y una pila de pagarés, con una sonrisa nerviosa. Alrededor hay sombras de figuras; en una se alcanzan a ver unas orejas de lobo, pero **no se ve a Don Lobo**.
5. Noche de lluvia. La casa de paja deshecha, la puerta tirada en el suelo y nadie a la vista.
6. Wilbur en silueta, descolgando la escopeta de la pared de su casa de ladrillo, iluminado por un relámpago que entra por la ventana.

---

## 3. El taxi: diálogo
El taxi ya va andando cuando se aclara la pantalla. Durante unos 8 a 10 segundos no habla nadie: solo se oyen la lluvia, el motor y el limpiaparabrisas. El taxi frena y empiezan a hablar.

| # | id | Quién | Texto | Dirección |
|---|---|---|---|---|
| 1 | `taxista_01` | Taxista | Hasta aquí llego, señor. De esta esquina para allá es territorio de Don Lobo. Tenga cuidado. | Gira la cabeza por encima del hombro. Al decir "Don Lobo" baja la voz. |
| 2 | `wilbur_taxi_01` | Wilbur | Tranquilo, amigo. No vengo a buscar problemas… vengo a buscar a mi hermano. | Bajo, cansado, seguro. |
| 3 | `taxista_02` | Taxista | ¿Su hermano? ¿El menor, el de la casa de paja? …Ah. Ese muchacho. | El "Ah" es de alguien que sabe algo y prefiere no decirlo. |
| 4 | `taxista_03` | Taxista | Que Dios lo acompañe, señor. Y cuide la billetera. | Vuelve a mirar al frente. |
| 5 | `wilbur_taxi_02` | Wilbur | Quédese con el cambio. | Suenan las monedas (`sfx_monedas`). |

Se oye la puerta, todo se pone en negro y empieza la llegada.

---

## 4. La llegada
| id | Cuándo | Quién | Texto |
|---|---|---|---|
| `wilbur_casa_01` | la cámara termina de girar hacia la casa | Wilbur | Porky… ¿qué te hicieron? |
| *(texto, sin voz)* | después de la línea de Wilbur | IA (arriba) | Paja mojada, la puerta en el suelo… Esto no fue el viento, Wilbur. Alguien vino a cobrar. |
| `wilbur_arma_01` | ve al primer guardia y saca la escopeta | Wilbur | Muy bien, Lobo. Hablemos en tu idioma. |

El pensamiento de la IA es **fijo** (no se le pide a la API) para que la entrada sea siempre la misma.

---

## 5. Dirección de voz (para Cristian)
| Personaje | Voz |
|---|---|
| **Taxista** | Bulldog viejo. Voz grave y rasposa, de fumador, y habla despacio. Callejero pero respetuoso: le dice "señor" a Wilbur. Le tiene miedo a Don Lobo y lo nombra casi en susurro. |
| **Wilbur** | La misma de siempre (`wilbur_dano`, interrogatorio de Centavo): grave, ronca y cansada, como narrador de radio-drama noir. Nunca grita. |
| **Narrador** | Ya está grabado (`narrador_intro_01..06`). |

**Formato:** WAV, 44 100 Hz, mono, con máximo 0.1 s de silencio al inicio y al final.
**Carpetas:** `Audio/Voices/narrador/`, `Audio/Voices/taxista/`, `Audio/Voices/wilbur/`. El nombre del archivo es exactamente el id.

---

## 6. Audios
**Voces nuevas (7):** `taxista_01`, `taxista_02`, `taxista_03`, `wilbur_taxi_01`, `wilbur_taxi_02`, `wilbur_casa_01`, `wilbur_arma_01`.
**Narrador (6, ya los tiene David):** `narrador_intro_01` … `narrador_intro_06`.

**Efectos de sonido y ambientes** (carpeta de SFX del proyecto; WAV 44.1 kHz; los loops tienen que empalmar sin corte):
| id | Qué es |
|---|---|
| `sfx_libro_abrir` | tapa gruesa de libro viejo que se abre |
| `sfx_pagina_01`, `sfx_pagina_02`, `sfx_pagina_03` | pasar una página de papel grueso (3 variantes) |
| `amb_intro_cuarto` | loop: crujido suave de lámpara de aceite o chimenea y un reloj lejano |
| `musica_intro` | jazz o vaudeville suave años 30, libre de derechos (CC0), 1–2 min, en loop |
| `amb_taxi_interior` | loop: motor viejo andando y lluvia sobre el techo del carro |
| `sfx_taxi_frenar` | frenada suave de carro viejo |
| `sfx_limpiaparabrisas` | loop corto de limpiaparabrisas (opcional) |
| `sfx_monedas` | monedas en la mano |
| `sfx_puerta_carro` | puerta de carro viejo que se abre y se cierra |
| `sfx_taxi_arranca` | carro viejo que arranca y se aleja (3D) |
| `sfx_escopeta_bombeo` | bombeo de escopeta (si ya existe el de la recarga, se usa ese) |

---

## 7. Arte (David con ChatGPT)
**Libro** → carpeta `Art/Intro/` (PNG; no llevan croma ni recorte):
| Archivo | Tamaño | Qué es |
|---|---|---|
| `libro_portada` | 1024×1536 (vertical) | tapa de cuero viejo con adornos dorados y **el centro vacío** (el título lo pone el juego) |
| `libro_guardas` | 1024×1536 (vertical) | guardas (el papel decorado del interior de la tapa) con un patrón de ladrillos, tablas y paja |
| `libro_papel` | 1024×1536 (vertical) | papel envejecido, liso, para el fondo de las páginas |
| `ilus_intro_01` … `ilus_intro_06` | 1024×1024 (cuadrado) | las 6 ilustraciones de la sección 2 |

**Regla:** **ninguna imagen lleva texto** (la IA escribe letras deformes). Todo el texto lo pone el juego.

### 7.1 Cómo usar ChatGPT
1. Abre **un solo chat** para las 6 ilustraciones, así los cerditos salen parecidos de una imagen a otra.
2. **Primer mensaje:** adjunta la imagen de los 3 hermanos y pega el *mensaje de referencia* de abajo, cambiando *[izquierda / centro / derecha]* según cómo estén en tu imagen.
3. Después manda **una ilustración por mensaje**, pegando el prompt completo.
4. Si sale con letras: *"Same image, remove all text, letters and numbers."* Si un cerdito cambia de diseño: *"Keep Wilbur/Max/Porky exactly as in the reference image."*
5. La tapa, las guardas y el papel se piden en un chat aparte, en formato vertical.

**Mensaje de referencia (primer mensaje, con la imagen adjunta):**
```
This reference image shows the three pig brothers from my video game. The pig on the [left / center / right] is WILBUR, the oldest: hard-working and serious. The pig on the [left / center / right] is MAX, the middle one: vain, loves jewelry. The pig on the [left / center / right] is PORKY, the youngest: a nervous gambler. In every image I ask for next, keep their faces, body shapes, colors and clothes exactly consistent with this reference. Never draw any text, letters or numbers in any image. Reply only "OK" for now.
```

### 7.2 Las 6 ilustraciones (cuadradas, 1024×1024)
**ilus_intro_01 — Porcópolis**
```
1930s vintage rubber-hose cartoon storybook illustration, Technicolor colors, ink lines with watercolor, printed on warm aged paper, soft vignette fading into the paper color at the edges, cozy and slightly ominous, no text, no letters, no words. Square image.
Scene: Porcópolis, a 1930s cartoon city of crooked leaning buildings, water towers and smoking chimneys, seen at sunset from a grassy hill. On the hill, three little houses in a row: a solid red-brick house, a wooden house with gold trim, and a flimsy straw hut. In the foreground the three pig brothers (Wilbur, Max and Porky, as in the reference) stand side by side seen from behind, looking down at the city. Warm orange and pink sky, long shadows.
```

**ilus_intro_02 — Wilbur**
```
1930s vintage rubber-hose cartoon storybook illustration, Technicolor colors, ink lines with watercolor, printed on warm aged paper, soft vignette fading into the paper color at the edges, cozy and slightly ominous, no text, no letters, no words. Square image.
Scene: Wilbur (the oldest pig from the reference) in work overalls, sweating but determined, carrying a tall stack of red bricks on his shoulder. Behind him, his sturdy red-brick house with a chimney, a trowel and a bucket of mortar on the ground. Bright midday sun, blue sky. He looks like an honest, proud, hard worker.
```

**ilus_intro_03 — Max**
```
1930s vintage rubber-hose cartoon storybook illustration, Technicolor colors, ink lines with watercolor, printed on warm aged paper, soft vignette fading into the paper color at the edges, cozy and slightly ominous, no text, no letters, no words. Square image.
Scene: Max (the middle pig from the reference) inside his wooden house, admiring himself in an ornate gold mirror, wearing gold chains, shiny rings and a pearl necklace, with a smug happy smile and little sparkle stars around the jewelry. The wooden walls are covered in gilded ornaments that shine too much. In a corner, a hammer and a half-nailed plank lie abandoned and forgotten.
```

**ilus_intro_04 — Porky y las deudas**
```
1930s vintage rubber-hose cartoon storybook illustration, Technicolor colors, ink lines with watercolor, printed on warm aged paper, soft vignette fading into the paper color at the edges, cozy and slightly ominous, no text, no letters, no words. Square image.
Scene: Porky (the youngest pig from the reference) sitting at a smoky gambling table under a single hanging lamp, with a sweaty, nervous grin. Dice tumbling in the air, scattered playing cards, and a tall messy pile of blank paper IOU slips in front of him (no writing on them). Around the table, dark shadowy figures in fedora hats, faces hidden. On the wall behind, one big shadow clearly has pointed wolf ears, but no wolf is visible. Dark green and amber lighting, cigarette smoke.
```

**ilus_intro_05 — La casa de paja destruida**
```
1930s vintage rubber-hose cartoon storybook illustration, Technicolor colors, ink lines with watercolor, printed on warm aged paper, soft vignette fading into the paper color at the edges, cozy and slightly ominous, no text, no letters, no words. Square image.
Scene: a rainy night. The little straw hut has been torn apart: straw scattered in muddy puddles, the wooden door lying flat on the ground, a broken lantern on its side, the doorframe half standing. Nobody is there, no characters at all. Cold blue tones with the warm glow of a single distant street lamp. Lonely and mysterious.
```

**ilus_intro_06 — Wilbur toma la escopeta**
```
1930s vintage rubber-hose cartoon storybook illustration, Technicolor colors, ink lines with watercolor, printed on warm aged paper, soft vignette fading into the paper color at the edges, cozy and slightly ominous, no text, no letters, no words. Square image.
Scene: inside Wilbur's brick house at night. A lightning flash through the rainy window throws hard white light and long black shadows. Wilbur (the oldest pig from the reference), seen mostly as a dark silhouette with a serious, determined face, is taking a pump-action shotgun down from two hooks on the wall above the fireplace. Film-noir mood, rain streaks on the window glass.
```

### 7.3 Tapa, guardas y papel (verticales, 1024×1536, en otro chat)
**libro_portada**
```
Front cover of an old 1930s storybook, seen perfectly flat and straight-on, filling the whole image edge to edge. Worn deep red leather with gold embossed art-deco ornaments, decorated gold corners and a gold border. A small gold emblem at the bottom center: a pig silhouette next to a shotgun shell. The large center area is EMPTY, plain leather, reserved for a title. Light scuffs and aging. Vertical portrait image. No text, no letters, no words.
```

**libro_guardas**
```
Decorative endpaper of an old 1930s storybook, flat and straight-on, filling the whole image edge to edge: a repeating pattern of tiny red bricks, wooden planks and bundles of straw, in muted cream, ochre and brick-red colors, on aged paper. Vertical portrait image. No text, no letters, no words.
```

**libro_papel**
```
Plain aged storybook paper texture, flat and evenly lit, filling the whole image edge to edge: warm cream color, subtle paper fibers, a few light foxing spots and slightly darker edges. Nothing drawn on it. Vertical portrait image. No text, no letters, no words.
```

### 7.4 Taxista (sprites con croma magenta)
Originales en `_entrante_personajes/Taxista/` sobre **magenta puro `#FF00FF`**, lienzo 1024×1024, mismo flujo que Centavo (`Tools/character_key.py`). Quedan en `Art/Characters/taxista/`. Se piden en un chat aparte: primero `conduce_01`, luego `habla_01` en el mismo chat, y `habla_02` editando `habla_01`.

**conduce_01**
```
Game character sprite, 1930s rubber-hose cartoon style, Technicolor colors, clean ink outlines. An old brown bulldog taxi driver with a plaid flat cap, a worn brown jacket, a toothpick in his mouth and white four-finger cartoon gloves. Seen from behind at three-quarter view, as if from the back seat of a car: upper body only, both arms forward, hands in a driving pose. Draw ONLY the character, no car parts, no steering wheel, no seat. Flat solid pure magenta background (#FF00FF), no shadows or gradients on the background. Square image. No text.
```

**habla_01**
```
Same bulldog taxi driver, same style, same clothes, same magenta background (#FF00FF). Now he turns his head over his shoulder to look back at the viewer, worried expression, mouth closed, hands still in a driving pose. Upper body only, only the character, no car parts. Square image. No text.
```

**habla_02** (editando `habla_01`)
```
Same image. Change only the mouth: open, as if he is talking. Keep everything else exactly identical.
```

---

## 8. Plan B (si no alcanza el tiempo)
- **Sin ilustraciones:** las páginas muestran solo el texto con un adorno simple. La escena funciona igual.
- **Sin el taxi armado:** pantalla negra con lluvia, motor y subtítulos. Ya de por sí es "todo oscuro".
- **Sin sprites del taxista:** solo se oye su voz y no se le ve la cara.
- **Sin `wilbur_arma_01`:** la escopeta sale solo con el sonido del bombeo.
