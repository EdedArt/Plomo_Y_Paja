# Guion y assets — Pantalla de inicio
**Versión:** 1.2 (3 nov 2026) · **Fase:** MENU (`Docs/phases/Fase-Menu.md`)

> Copiar este archivo al repo como `Docs/guiones/menu.md`. Claude Code lo lee en MENU.1.
> **Cambios en 1.2 (tras MENU.0):** se usa **la misma imagen que ya existe** (los tres cerditos en pose de pelea al atardecer, frente a la casa de ladrillo). Al pulsar una tecla **cae un rayo** y Wilbur, en el mismo lugar, **apunta la escopeta a la gran sombra de Don Lobo**. Ya no hay abrazo, ni noche, ni sprites con croma. La música es **`musica_intro`** (no el jazz).
> Nada de esto bloquea el código: si un archivo no llega, se usa lo que hay hoy y se reemplaza después volviendo a correr el menú.

---

## 1. Cómo se ve
1. **Espera (antes de pulsar):** la imagen actual de los tres cerditos al atardecer, animada como hoy (dos cuadros con fundido y respiración), con grano y ruido de película vieja **suaves**: se sienten, pero no se notan. Abajo dice **"Pulsa cualquier tecla"**, que late despacio. Suena `musica_intro`.
2. **Al pulsar una tecla, cae el rayo:** destello blanco doble, trueno y la imagen cambia a **la versión de tormenta**: el cielo se oscurece, detrás de la casa se levanta **la sombra gigante de Don Lobo** (sombrero, orejas puntiagudas y ojos que brillan) y **Wilbur, en la misma posición**, baja la escopeta del hombro y **le apunta**. Max y Porky siguen en su sitio y miran hacia arriba, asustados.
3. **Menú:** con la imagen de tormenta de fondo, el título **PLOMO Y PAJA** baja un poco y aparecen los botones. Cada 12–20 s cae un relámpago lejano (destello suave, sin trueno fuerte).
4. **Jugar:** bombeo de escopeta, fundido a negro y empieza el libro de la intro **con la misma música, sin cortarse**.

---

## 2. Textos (exactos)
| Dónde | Texto |
|---|---|
| Pantalla de espera | Pulsa cualquier tecla |
| Botones | Jugar · Opciones · Créditos · Salir |
| Opciones | Volumen general · Música · Sensibilidad del ratón · Pantalla completa · Volver |
| Pausa | Salir al menú |

**Créditos** (borrador; David: completa el apellido de Cristian y ajusta los roles):
```
PLOMO Y PAJA

Programación y dirección — David Cabezas
Arte y personajes — Edgar Arteaga
Voces, música y sonido — Cristian

Universidad Cooperativa de Colombia · Pasto · 2026
Hecho con Unity
```

---

## 3. Arte (David con ChatGPT)
Las imágenes actuales están en `PLOMO_Y_PAJAV_1/_entrante_menu/actuales/` (`menu_inicio_01.png`, `menu_inicio_02.png`, `boton_placa.png`).
**Reglas:** **ninguna imagen lleva texto**; son imágenes completas (fondo + personajes juntos), **sin croma ni recorte**. **Todas del mismo tamaño** (lo que dé ChatGPT en horizontal, 1536×1024). Si se mezclan con las viejas de 1672×941, el menú las ajusta y avisa.

| Archivo | De dónde sale | Carpeta final | ¿Obligatorio? |
|---|---|---|---|
| `menu_inicio_01` | mejorar la actual (maestra) | `Art/UI/Menu/` (reemplaza la vieja) | opcional: si no, se queda la de hoy |
| `menu_inicio_02` | editando `menu_inicio_01` nuevo | `Art/UI/Menu/` | opcional |
| `menu_rayo_01` | editando `menu_inicio_01` (el nuevo, o el actual si no lo rehaces) | `Art/UI/Menu/` | **sí** |
| `menu_rayo_02` | editando `menu_rayo_01` | `Art/UI/Menu/` | opcional |
| `menu_logo` | a mano (Photopea/Canva), PNG transparente ~2048×768 | `Art/UI/Menu/` | opcional |

Pídelas todas **en el mismo chat**, adjuntando `menu_inicio_01.png` (la actual) en el primer mensaje.

**`menu_inicio_01` (mejorar la actual):**
```
This is the title screen of my 1930s rubber-hose cartoon video game. Redraw this exact image with higher quality: keep the same composition, the same three pig brothers in the same fighting poses and the same positions, the same tree, brick house, garden and sunset. Cleaner, thicker ink outlines, richer Technicolor colors, more texture, a warm aged-film look. Wide landscape image. No text, no letters, no numbers.
```

**`menu_inicio_02`:**
```
Same image. Keep everything identical, only move the pigs' ears, tails and the chimney smoke very slightly, as the next frame of a hand-drawn breathing animation.
```

**`menu_rayo_01`:**
```
Same image, same composition, same positions. Now a thunderstorm has suddenly arrived: the sunset sky turns dark stormy purple-grey with a jagged lightning bolt, and a hard white lightning light falls on the scene with long black shadows. Behind the brick house, rising huge into the sky, is the giant dark shadow-silhouette of a menacing wolf gangster wearing a fedora hat, with pointed ears and two small glowing eyes, looming over the three pigs. The pig with the shotgun stays in exactly the same place but now takes the shotgun off his shoulder and aims it up at the giant wolf shadow, brave and determined. The other two pigs stay in the same places, looking up at the shadow, startled. No text, no letters, no numbers.
```

**`menu_rayo_02`:**
```
Same image. Keep everything identical, only move the pigs' ears and tails very slightly and make the wolf's glowing eyes a little brighter, as the next frame of a hand-drawn animation.
```

Si sale con letras: *"Same image, remove all text, letters and numbers."* Si un cerdito cambia de diseño o de lugar: *"Keep the three pigs exactly as in the first image, same design and same positions."*

---

## 4. Audio (Cristian)
WAV, 44 100 Hz, máx. 0.1 s de silencio al inicio y al final.
| id | Qué es | ¿Obligatorio? |
|---|---|---|
| `musica_intro` | **ya existe** (90 s). Suena en el menú y sigue sin cortarse en el libro de la intro. Cristian: revisa de oído que el loop empalme sin clic. | ya está |
| `sfx_trueno` | rayo que cae cerca: chasquido seco y trueno que retumba, 2–3 s | sí (si no llega, el rayo es solo destello) |
| `sfx_trueno_lejos` | trueno lejano y suave, para los relámpagos del menú | opcional (si no llega, se usa `sfx_trueno` bajito) |
| `amb_menu_vinilo` | loop de crujido de disco viejo, muy bajito | opcional |
| `sfx_ui_hover` | tic suave de madera / tecla de máquina de escribir | opcional |
| `sfx_ui_click` | golpe de tecla de máquina de escribir con campanita | opcional |
| `sfx_ui_jugar` | bombeo de escopeta (si no llega, se usa `sfx_escopeta_bombeo`) | opcional |
