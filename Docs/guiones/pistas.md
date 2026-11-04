# Guion — Las pistas (textos, cómo se leen y arte)
**Versión:** 1.0 (3 nov 2026) · **Fase:** PISTAS (`Docs/phases/Fase-Pistas.md`) · **Responsable:** Edgar

> Copiar este archivo al repo como `Docs/guiones/pistas.md`. Claude Code lo lee en la fase PISTAS.
> Nada de esto bloquea el código: si un dibujo no llega, la pista usa el placeholder de hoy (una hoja dibujada).

---

## 1. Cómo se ve
1. Wilbur se acerca a una pista (≤ 2.2 m, viéndola) → bajo la mira aparece **[E] Examinar**.
2. Al pulsar E, el juego se detiene y aparece una **hoja de papel** grande en el centro (mismo papel del interrogatorio, letra Special Elite): título arriba, a veces un dibujo (la foto) y el texto. Abajo, en gris: *E / clic para cerrar*.
3. Al cerrar: aviso del HUD **"Pista: <título>"** y, como siempre, el pensamiento de la IA sobre la pista.
4. Las pistas que se ganan hablando (Centavo, Colmillo) no abren hoja: solo el aviso **"Pista: <título>"**.

---

## 2. Las pistas (textos exactos)
Si la pista ya tiene un texto en el proyecto (datos de `PHASE-3B.md` / `DialogueDatabase`), **se reemplaza por este**, salvo `testimonio_puerta_01`, que se queda como está.

| id | Nivel / dónde | Cómo se obtiene | Título |
|---|---|---|---|
| `nota_porky_01` | 1 · casa de Porky, cajón | se examina | Nota de Porky |
| `contrato_01` | 1 · casa de Porky, debajo del sofá | se examina | Pagaré |
| `registro_01` | 1 · Centavo | interrogatorio o al perdonarlo | Hoja del libro de cuentas |
| `testimonio_puerta_01` | 1 · Colmillo | interrogatorio | (el que ya tiene) |
| `foto_reunion_01` | 2 · barracón, sobre la mesa | se examina | Foto de la reunión |
| `carta_lobo_01` | 2 · oficina de Don Lobo, escritorio | se examina | Carta de Don Lobo |

### `nota_porky_01` — Nota de Porky
```
L.:
Esta noche no puedo. Dile a tu gente que no venga a la casa, que yo voy solo.
Llevo lo que pueda.
Y no le digas nada a Wilbur.
—P.
```

### `contrato_01` — Pagaré
```
Yo, Porky, reconozco deberle al señor Lobo la suma de QUINIENTOS PESOS por préstamo de juego, pagaderos el primero del mes.
Si no pago, respondo con mi casa y con mi trabajo.

Firma: Porky  (la firma está toda temblorosa)
```

### `registro_01` — Hoja del libro de cuentas *(no abre hoja; solo aviso)*
Texto para el diario de pistas, si existe uno:
```
Caja fuerte, oficina del club. Faltante: $3.200.
Último en entrar: P. (el menor). Testigo: el portero.
Intereses de la semana: $40.
Nota del jefe: que nadie lo toque. Que pague trabajando.
```

### `foto_reunion_01` — Foto de la reunión *(con imagen)*
Imagen: `pista_foto_reunion_grande` (si no existe, sin imagen).
```
Club del Lobo, hace dos semanas. Porky sentado en la mesa del jefe, sonriendo, con un fajo de billetes en la mano.

Al reverso, a lápiz: «El socio nuevo. Que no se nos olvide la cara.»
```

### `carta_lobo_01` — Carta de Don Lobo
```
A quien venga por el cerdito:

No lo tengo encerrado. Vino solo, con la cola entre las patas, porque los que le prestaron después de mí no son tan caballeros como yo.
Me robó tres mil doscientos pesos y el orgullo. La plata la puede pagar trabajando. El orgullo… ya veremos.

Le escribí a su hermano Max. Max sabe.
—L.
```

---

## 3. Arte (Edgar)
**Regla de siempre:** fondo **magenta puro `#FF00FF`**, lienzo **1024×1024**, **sin texto ni letras legibles** (los papeles llevan garabatos), la parte más baja tocando el borde inferior. Recorte con `python Tools/character_key.py` (la fase dice la carpeta exacta). Quedan en `Art/Props/pistas/`.

| Archivo | Qué es (en el mundo, pequeño) |
|---|---|
| `pista_nota_01` | papel doblado, arrugado, con garabatos de lápiz, apoyado de lado |
| `pista_contrato_01` | pagaré enrollado con una cinta roja, un poco manchado |
| `pista_foto_01` | foto vieja de bordes blancos dentados, boca arriba, con la esquina doblada |
| `pista_carta_01` | sobre crema con sello de lacre rojo con una cabeza de lobo, la carta medio afuera |

Prompt base:
```
Game item sprite, 1930s rubber-hose cartoon style, Technicolor colors, clean ink outlines. [lo de la tabla], seen at three-quarter view from slightly above, resting on a table. Any writing is just illegible squiggles. Flat solid pure magenta background (#FF00FF). Square image. No text, no letters, no numbers.
```

**Imagen grande de la foto (para la hoja; sin magenta):** `pista_foto_reunion_grande`, 1024×1024, va en `Art/Props/pistas/`:
```
Old 1930s black-and-white photograph with a white deckled border, slightly yellowed and scratched, rubber-hose cartoon style: in a smoky jazz club, a young nervous-happy pig (Porky, from the reference) sits at a fancy table holding a stack of banknotes, next to a big grey wolf gangster in a fedora and pinstripe suit with his arm around him. Both smiling at the camera. No text, no letters, no numbers.
```
*(Adjuntar la imagen de referencia de los hermanos, como en las ilustraciones de la intro.)*
