# Guion y assets — Pantalla de inicio
**Versión:** 1.1 (3 nov 2026) · **Fase:** MENU (`Docs/phases/Fase-Menu.md`)

> Copiar este archivo al repo como `Docs/guiones/menu.md`. Claude Code lo lee en MENU.1.
> **Cambios en 1.1:** se deja el estudio de Wilbur (era demasiado parecido a Mouse). La pantalla es la de **Edgar, los tres cerditos abrazados**, mejorada y animada. Nuevos sprites del grupo (sección 3.2) y una sombra de lobo opcional (3.3).
> Nada de esto bloquea el código: si un archivo no llega, se usa lo que hay hoy (la pantalla de Edgar) y se reemplaza después volviendo a correr el menú.

---

## 1. Cómo se ve
- Se conserva **la idea y la composición de la pantalla de Edgar**: los tres hermanos abrazados. MENU.0 copia sus imágenes a `_entrante_menu/actuales/` y David las rehace con ChatGPT usando la de Edgar como referencia.
- **Fondo:** de noche en Porcópolis, con lluvia suave. Las tres casitas (ladrillo, madera, paja) en una loma y una farola torcida que ilumina los adoquines mojados. A color (Technicolor), oscuro y cálido. Si el fondo de Edgar ya tiene otra idea que te guste, se respeta esa.
- **Los tres cerditos se mueven como caricatura de los 30:**
  - respiran (lo hace el código) y cada uno parpadea a su ritmo;
  - de vez en cuando **Porky mira de reojo, nervioso**, y le sale una gota de sudor (el jugador no lo entiende todavía, pero es una pista);
  - opcional: cada 20–35 s pasa por la pared de la derecha la **sombra de un lobo con sombrero**. Justo en ese momento Porky mira de reojo.
- Arriba va el título **PLOMO Y PAJA**, que se mece un poquito.
- Suena el **jazz de Cristian** con un leve crujido de disco. La farola titila y la imagen tiene **grano y ruido de película vieja, suaves**: se sienten, pero no se notan.
- Primero aparece **"Pulsa cualquier tecla"**. Al pulsar, **los tres miran a la cámara y sonríen** (Wilbur saluda con la mano) y aparecen los botones.
- Al darle **Jugar** suena el bombeo de escopeta, hay un fundido a negro y empieza el libro de la intro **con la misma música, sin cortarse**.

---

## 2. Textos (exactos)
| Dónde | Texto |
|---|---|
| Pantalla de espera | Pulsa cualquier tecla |
| Botones | Jugar · Opciones · Créditos · Salir |
| Opciones | Volumen general · Música · Sensibilidad del ratón · Pantalla completa · Volver |
| Pausa (si existe) | Salir al menú |

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
**Reglas de siempre:** **ninguna imagen lleva texto** y cada variante se edita **desde su maestra**, nunca encadenando ediciones. Los originales van en `_entrante_menu/` (no se suben a git).

| Archivo | Tamaño | Fondo | Carpeta de entrada → final |
|---|---|---|---|
| `menu_fondo` | 1536×1024 (horizontal) | normal, sin croma | `_entrante_menu/` → `Art/Menu/` |
| `cerditos_idle_01` (maestra), `cerditos_idle_02`, `cerditos_parpadeo_wilbur`, `cerditos_parpadeo_max`, `cerditos_parpadeo_porky`, `cerditos_porky_mira`, `cerditos_mira_01` | 1536×1024 (horizontal) | magenta `#FF00FF` | `_entrante_menu/Cerditos/` → `Art/Menu/cerditos/` |
| `menu_sombra_lobo` *(opcional)* | 1024×1024 | magenta `#FF00FF` | `_entrante_menu/Sombra/` → `Art/Menu/` |
| `menu_logo` *(opcional)* | ~2048×768, PNG transparente | — | hecho **a mano** (Photopea/Canva con una fuente), directo a `Art/Menu/` |

El fondo se recorta arriba y abajo para llenar la pantalla, así que lo importante tiene que quedar en la franja del medio.

### 3.1 Fondo (chat aparte; adjunta la pantalla de Edgar como referencia de ambiente)
```
1930s vintage rubber-hose cartoon background painting, Technicolor colors, ink lines with watercolor, warm aged film look. Wide landscape image. Use the attached image only as a reference for the mood. Night in Porcópolis, a crooked 1930s cartoon town in light rain. In the middle ground, three little houses in a row on a small grassy hill: a solid red-brick house with warm lit windows, a wooden house with shiny gold trim, and a flimsy straw hut. A crooked street lamp on the left casts a warm orange glow on wet cobblestones in the foreground. On the right side, a brick wall lit by the street lamp, plain and empty. The lower center of the image is open, clear ground (characters will be added there later). The top center is a calm, darker night sky with a few clouds (a title will go there). No characters, no people, no animals. No text, no letters, no numbers.
```

### 3.2 Los tres cerditos (chat aparte; adjunta **la pantalla de Edgar** y **la imagen de referencia de los 3 hermanos**)
**`cerditos_idle_01` (maestra):**
```
Game character sprite, 1930s rubber-hose cartoon style, Technicolor colors, clean ink outlines. The three pig brothers from the second reference image, standing together in a warm group hug, in the same arrangement as in the first attached image. Keep each pig's face, body, colors and clothes exactly as in the reference. WILBUR, the oldest, has his arms around his brothers, calm and proud. MAX, the middle one, smiles smugly and shows off a shiny gold ring. PORKY, the youngest, smiles a little too wide, slightly nervous. Full bodies, facing the viewer at a slight three-quarter angle, feet flat on the ground. Draw ONLY the three characters. Flat solid pure magenta background (#FF00FF), no shadows on the background. Wide landscape image. No text.
```
**Variantes (editando la maestra, una por mensaje):**
| Archivo | Prompt |
|---|---|
| `cerditos_idle_02` | `Same image. Keep everything identical, only move their ears and the curls of their tails very slightly, as the next frame of a hand-drawn breathing animation.` |
| `cerditos_parpadeo_wilbur` | `Same image. Keep everything identical, only close Wilbur's eyes.` |
| `cerditos_parpadeo_max` | `Same image. Keep everything identical, only close Max's eyes.` |
| `cerditos_parpadeo_porky` | `Same image. Keep everything identical, only close Porky's eyes.` |
| `cerditos_porky_mira` | `Same image. Keep everything identical, only Porky's eyes glance nervously to the side and a small sweat drop appears on his forehead.` |
| `cerditos_mira_01` | `Same image. Keep their bodies identical, only all three look straight at the viewer with big happy smiles, and Wilbur lifts one hand in a small wave.` |

Si sale con letras: *"Same image, remove all text, letters and numbers."* Si un cerdito cambia de diseño: *"Keep Wilbur/Max/Porky exactly as in the reference image."*

### 3.3 Sombra del lobo (opcional, chat aparte)
```
A solid flat black silhouette of a tall wolf wearing a fedora hat and a long coat, walking, seen from the side, 1930s rubber-hose cartoon shape with pointed ears clearly visible. No details inside the silhouette. Flat solid pure magenta background (#FF00FF). Square image. No text.
```

---

## 4. Audio (Cristian)
WAV, 44 100 Hz, máx. 0.1 s de silencio al inicio y al final; los loops tienen que empalmar sin corte.
| id | Qué es | ¿Obligatorio? |
|---|---|---|
| `musica_menu` | **el jazz que ya hiciste**; la misma pista sigue en el libro de la intro (si se llama distinto, Claude Code la encuentra en MENU.0 y la usa) | sí |
| `amb_menu_vinilo` | loop de crujido de disco viejo, muy bajito | opcional |
| `sfx_ui_hover` | tic suave de madera / tecla de máquina de escribir | opcional |
| `sfx_ui_click` | golpe de tecla de máquina de escribir con campanita | opcional |
| `sfx_ui_jugar` | bombeo de escopeta (si no llega, se usa `sfx_escopeta_bombeo`) | opcional |
