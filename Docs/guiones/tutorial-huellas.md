# Guion — Tutorial, "Pensamientos" y escena de las huellas
**Versión:** 1.0 (3 nov 2026) · **Fase:** N1 (`Docs/phases/Fase-N1-Mapa-Tutorial.md`)

> Copiar este archivo al repo como `Docs/guiones/tutorial-huellas.md`. Claude Code lo lee en N1.3 y N1.4.
> Nada de esto bloquea el código: si un archivo no llega, se usa un placeholder y se reemplaza después volviendo a correr el menú.

---

## 1. Cómo se ve
1. **Llegada:** después del pensamiento fijo frente a la casa de Porky, el juego se detiene un momento y aparece una **tarjeta de papel** que explica qué son los pensamientos. Se cierra con un clic.
2. **Primeros pasos:** arriba, un pensamiento corto; abajo, unas etiquetas de papel con las teclas (caminar, correr, examinar). Cada etiqueta se tacha cuando el jugador hace la acción.
3. **Primer guardia:** Wilbur saca la escopeta y aparece otro pensamiento con apuntar, disparar y recargar.
4. **Revólver de Centavo:** al recogerlo, un pensamiento con cómo cambiar de arma.
5. **Huellas:** después de Centavo, detrás del cuarto de herramientas hay huellas de lobo y de cerdo que van juntas hacia una reja con cadena. Al acercarse, una escena corta las sigue hasta el bosque, la IA piensa, Wilbur le dispara a la cadena y la reja se abre. Al final del sendero se pasa al campamento.

---

## 2. Tutorial (textos exactos)

### T1 — Tarjeta "Pensamientos" (pausa el juego)
**Título:** PENSAMIENTOS

**Texto:**
```
Lo que aparece arriba de la pantalla es lo que piensa Wilbur.

Esos pensamientos los escribe una inteligencia artificial en el momento, según lo que has visto y lo que has hecho. Por eso cada partida piensa distinto.

A veces te ayudan. A veces se equivocan. A veces te alejan de la verdad.

Tú decides a quién creerle.
```
**Nota pequeña (abajo, en gris):** Necesita internet. Sin conexión, Wilbur usa pensamientos guardados.
**Para cerrar:** Clic para continuar

### T2 — Moverse (al recuperar el control)
| Dónde | Texto |
|---|---|
| Pensamiento (fijo, arriba) | Primero mira bien la escena. Los muertos no cuentan nada… pero las casas sí. |
| Etiquetas | **WASD** Caminar · **Shift** Correr (hace ruido) · **E** Examinar / tomar |

### T3 — La escopeta (al sacarla)
| Dónde | Texto |
|---|---|
| Pensamiento (fijo) | Clic derecho para apuntar con calma. Clic izquierdo, y que hable la escopeta. De cerca no perdona; de lejos, ni cosquillas. |
| Etiquetas | **Clic derecho** Apuntar · **Clic izquierdo** Disparar · **R** Recargar |

### T4 — El revólver (al recogerlo)
| Dónde | Texto |
|---|---|
| Pensamiento (fijo) | El revólver de Centavo. Una bala, un problema menos… si apuntas bien. |
| Etiquetas | **1** Escopeta · **2** Revólver · **Rueda** Cambiar |

### Etiqueta opcional bajo los primeros 3 pensamientos de la API
pensamiento · IA

*(Si una tecla del proyecto es distinta, se cambia la tecla, no el texto.)*

---

## 3. Escena de las huellas

### Dirección
- Letterbox, sin manos ni HUD. La cámara baja la vista a las huellas: **una de lobo y una de cerdo, lado a lado, al mismo paso, sin marcas de arrastre**.
- La cámara sigue las huellas a la altura de los ojos hasta la reja con cadena. Detrás, el bosque con niebla y, muy lejos, una luz cálida (el campamento).
- Wilbur habla, la IA piensa, Wilbur le dispara a la cadena y la reja se abre.

### Líneas
| id | Quién | Texto |
|---|---|---|
| `wilbur_huellas_01` | Wilbur | Huellas de lobo… y las de Porky. Por aquí se lo llevaron. |
| `wilbur_cadena_01` *(opcional)* | Wilbur | Con permiso. |

### Pensamiento de la IA (API)
**Contexto que se le manda** (además del resumen normal de pistas, interrogatorios y bajas):
> Wilbur sigue unas huellas en el barro detrás del cuarto de herramientas de la casa de Porky. Hay huellas de lobo y de cerdo, una al lado de la otra, al mismo paso; no hay marcas de arrastre. Van hacia el bosque, donde se ve una luz lejana. Wilbur cree que a Porky se lo llevaron a la fuerza.

**Reglas para la respuesta:** 1–2 oraciones, máximo 25 palabras, español, tono noir, ambigua. Puede sembrar la duda, pero **nunca afirmar** que Porky fue por su cuenta.

**Líneas de respaldo** (sin conexión o si tarda más de 5 s):
- Dos pares de huellas, al mismo paso. Raro… los secuestrados suelen arrastrar los pies.
- El barro no miente, Wilbur. Lo que no dice es quién caminaba adelante.
- Si se lo llevaron a la fuerza, alguien le enseñó a caminar muy tranquilo.
- *(solo si Centavo fue perdonado)* Centavo se fue cojeando para el otro lado. Estas huellas no cojean.
- *(solo si Centavo murió)* Al contador ya no le puedes preguntar. A las huellas, sí.

### Aviso de prueba (solo si la escena `Campamento` no existe todavía)
Continuará en el campamento…

---

## 4. Arte (Edgar, o David con ChatGPT)
**Regla de siempre:** fondo **magenta puro `#FF00FF`**, lienzo **1024×1024**, **sin texto**. Recorte con `python Tools/character_key.py`.

### 4.1 Huellas → `_entrante_props/Huellas/` (quedan en `Art/Props/huellas/`)
| Archivo | Prompt |
|---|---|
| `huella_lobo_01` | `Single wolf paw print pressed into wet mud, seen perfectly from above, 1930s rubber-hose cartoon style, dark brown mud with a little water shine, four toe pads and claw marks, clean ink outlines. Centered. Flat solid pure magenta background (#FF00FF). Square image. No text.` |
| `huella_cerdo_01` | `Single pig hoof print (two-toed cloven hoof) pressed into wet mud, seen perfectly from above, 1930s rubber-hose cartoon style, dark brown mud with a little water shine, clean ink outlines. Centered. Flat solid pure magenta background (#FF00FF). Square image. No text.` |

### 4.2 Fondos lejanos (opcionales; si no llegan, el juego los genera) → `Art/Environment/fondos/`
PNG con transparencia (o magenta para recortar), horizontales, 2048×1024:
| Archivo | Qué es |
|---|---|
| `fondo_ciudad_01` | silueta negra-azulada de Porcópolis de noche: edificios torcidos, tanques de agua, chimeneas, ventanitas amarillas |
| `fondo_colinas_01` | colinas suaves en silueta con algunos árboles redondos |
| `fondo_bosque_01` | línea de bosque denso en silueta, copas redondas de caricatura |

Prompt base: `1930s rubber-hose cartoon background silhouette, night, [lo de la tabla], flat dark blue-black shapes with a few small warm yellow lit windows, no outlines inside the shapes, clean edges. Flat solid pure magenta background (#FF00FF) for the sky. Wide landscape image. No text.`

---

## 5. Audio (Cristian)
WAV, 44 100 Hz, mono (los ambientes pueden ser estéreo), máx. 0.1 s de silencio al inicio y al final; los loops empalman sin corte.
| id | Qué es | Carpeta |
|---|---|---|
| `wilbur_huellas_01` | ver sección 3, misma voz de Wilbur | `Audio/Voices/wilbur/` |
| `wilbur_cadena_01` | opcional, "Con permiso." en voz baja | `Audio/Voices/wilbur/` |
| `sfx_cadena_rota` | cadena y candado que se rompen y caen al barro | SFX |
| `sfx_reja_abrir` | reja de madera vieja que se abre rechinando | SFX |
| `amb_bosque` | loop: bosque de noche, grillos, lluvia suave en hojas, viento | SFX / ambientes (también lo usa el nivel 2) |
