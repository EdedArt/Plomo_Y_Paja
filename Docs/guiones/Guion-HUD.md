# Guion y assets — HUD, disparos y objetos recogibles
**Versión:** 1.0 (2 nov 2026) · **Fase:** HUD (`Docs/phases/PHASE-HUD.md`)

> Copiar este archivo al repo como `Docs/guiones/hud.md`. Claude Code lo lee para la fase HUD.
> Nada de esto bloquea el código: si un archivo no llega, el juego usa un placeholder y se reemplaza después volviendo a correr el menú.

---

## 1. Cómo se ve
- **Abajo a la izquierda**, todo junto (abajo a la derecha están las manos): la cara de Wilbur, a su lado la barra de vida y debajo los cartuchos del arma con la reserva (×18).
- La cara de Wilbur se va viendo más golpeada según la vida que le queda. Al recibir un golpe hace una mueca.
- Cada disparo "saca" un cartucho del dibujo; al recargar se vuelven a llenar.
- Con poca vida, los bordes de la pantalla laten en rojo y se oye un corazón.
- Una cuña roja alrededor del centro de la pantalla indica de dónde vino el disparo.
- Los guardias, Centavo y Don Lobo disparan **desde el arma**: fogonazo en el cañón, una estela de la bala y polvo donde pega.
- Botiquines y cajas de munición en el mapa: se acercan, aparece "[E] Tomar…" y se usan al instante.

---

## 2. Textos del jugador (exactos)
| Dónde | Texto |
|---|---|
| Arriba de los cartuchos, cargador vacío con reserva | ¡RECARGA! |
| Arriba de los cartuchos, sin nada | SIN MUNICIÓN |
| Aviso para recoger | [E] Tomar botiquín · [E] Tomar cartuchos · [E] Tomar balas |
| Aviso al recoger | + Botiquín · +6 cartuchos · +4 cartuchos · +6 balas de revólver |
| Si está lleno | Salud completa · Munición llena |

*(La tecla "[E]" se cambia por la que use el proyecto para interactuar.)*

### Líneas opcionales de Wilbur (Cristian)
Suenan de vez en cuando (máx. una cada 20 s). Si no llegan, no pasa nada.
| id | Cuándo | Texto |
|---|---|---|
| `wilbur_botiquin_01` | usa un botiquín | Esto va a doler… menos. |
| `wilbur_municion_01` | recoge munición | El plomo nunca sobra. |
| `wilbur_vida_baja_01` | baja de 25 % de vida | Todavía no, Wilbur… todavía no. |

---

## 3. Arte (Edgar, o David con ChatGPT)
**Regla de siempre:** fondo **magenta puro `#FF00FF`**, lienzo **1024×1024**, **sin texto**. Cada variante se edita desde su maestra, nunca encadenando ediciones.

### 3.1 HUD → `_entrante_hud/HUD/` (queda en `Art/UI/HUD/`)
| Archivo | Qué es |
|---|---|
| `hud_wilbur_01` (maestra) | cara de Wilbur, de frente, serio y entero |
| `hud_wilbur_02` | con un curita y un ojo un poco hinchado |
| `hud_wilbur_03` | ojo morado, curitas, sudando |
| `hud_wilbur_04` | muy golpeado, vendado, mareado (estrellitas) |
| `hud_wilbur_dano` | mueca de dolor, ojos apretados |
| `hud_cartucho_lleno` / `hud_cartucho_vacio` | cartucho rojo de escopeta, parado / el mismo vacío y gris |
| `hud_bala_llena` / `hud_bala_vacia` | bala de revólver dorada / casquillo vacío |
| `hud_barra_marco` *(opcional)* | marco de la barra de vida, alargado, borde grueso negro irregular |

**Prompt maestra (adjuntar la imagen de Wilbur):**
```
Game HUD portrait, 1930s rubber-hose cartoon style, Technicolor colors, clean thick ink outlines. Head and neck only of WILBUR, the oldest pig from the reference image, seen from the front, serious and tough, keep his face and colors exactly as in the reference. Centered, filling about 80% of the image. Flat solid pure magenta background (#FF00FF), no shadows on the background. Square image. No text.
```
**Variantes (editando la maestra, una por mensaje):**
```
Same image. Keep everything identical, only add: a small band-aid on his cheek and one slightly swollen eye.
Same image. Keep everything identical, only add: a black eye, two band-aids and sweat drops.
Same image. Keep everything identical, only add: a bandage around his head, a black eye, bruises and little dizzy stars around his head.
Same image. Keep everything identical, only change the expression: grimace of pain, eyes squeezed shut, teeth clenched.
```
**Iconos:**
```
Game HUD icon, 1930s rubber-hose cartoon style, clean thick ink outlines. A single red shotgun shell with a brass base, standing upright, centered, slightly squashed cartoon shape with a small shine. Flat solid pure magenta background (#FF00FF). Square image. No text.
Same image. Make the shell empty and spent: grey faded colors, open crimped top. Keep the same shape and position.
Game HUD icon, 1930s rubber-hose cartoon style, clean thick ink outlines. A single golden revolver bullet standing upright, centered, chunky cartoon shape with a small shine. Flat solid pure magenta background (#FF00FF). Square image. No text.
Same image. Make it an empty brass casing with no bullet tip, darker and faded. Keep the same shape and position.
```

### 3.2 Objetos del mapa → `_entrante_hud/Objetos/` (queda en `Art/Props/pickups/`)
> El botiquín lleva un **corazón**, no una cruz roja: la cruz roja sobre blanco es un emblema protegido y los juegos la evitan.

| Archivo | Prompt |
|---|---|
| `pickup_botiquin_01` | `Game item sprite, 1930s rubber-hose cartoon style, Technicolor colors, clean ink outlines. An old dented tin first-aid box, cream white with a big red heart emblem on the lid, a rolled bandage and a small brown tonic bottle on top. Seen at three-quarter view, resting on the ground. Flat solid pure magenta background (#FF00FF). Square image. No text, no cross symbol.` |
| `pickup_cartuchos_01` | `Game item sprite, 1930s rubber-hose cartoon style, Technicolor colors, clean ink outlines. A small open cardboard box of red shotgun shells, a few shells peeking out and one lying next to it. Three-quarter view, resting on the ground. Flat solid pure magenta background (#FF00FF). Square image. No text, no labels.` |
| `pickup_balas_01` | `Game item sprite, 1930s rubber-hose cartoon style, Technicolor colors, clean ink outlines. A small open tin box of golden revolver bullets, a few bullets standing inside. Three-quarter view, resting on the ground. Flat solid pure magenta background (#FF00FF). Square image. No text, no labels.` |

### 3.3 Efectos → `_entrante_hud/FX/` (queda en `Art/FX/`)
| Archivo | Prompt |
|---|---|
| `fx_impacto_01`, `fx_impacto_02` | `Cartoon dust puff effect, 1930s rubber-hose animation style, a small burst of grey-brown dust cloud with a few tiny debris chips flying out, clean ink outlines. Centered. Flat solid pure magenta background (#FF00FF). Square image. No text.` (la 02: *"Same image, the puff is bigger and starting to fade, more spread out."*) |
| `fx_agujero_bala` | `Cartoon bullet hole decal, 1930s rubber-hose style, a black round hole with small cracked splinters around it, seen straight-on, centered. Flat solid pure magenta background (#FF00FF). Square image. No text.` |

*El fogonazo de los enemigos usa el mismo `fx_` del jugador; no hace falta arte nuevo.*

---

## 4. Audio (Cristian)
WAV, 44 100 Hz, mono, máx. 0.1 s de silencio al inicio y al final; los loops empalman sin corte. Carpeta de SFX del proyecto; voces en `Audio/Voices/wilbur/`.
| id | Qué es |
|---|---|
| `sfx_botiquin` | abrir una lata y desenrollar una venda, con un "glup" de tónico |
| `sfx_municion_recoger` | puñado de cartuchos/balas que se guardan en el bolsillo |
| `sfx_latido` | loop de corazón latiendo, grave y lento |
| `sfx_bala_zumbido_01`, `_02` | zumbido de bala que pasa cerca de la oreja |
| `sfx_impacto_01` … `_03` | bala pegando en madera / ladrillo |
| `sfx_rebote` | rebote de bala en metal (ricochet de caricatura) |
| `sfx_disparo_guardia` | pistola de los años 30 (si ya existe un disparo de guardia, se usa ese) |
| `sfx_sin_balas` | clic seco del gatillo sin balas |
| `sfx_hud_aviso` | "pop" suave de papel para los avisos |
| `wilbur_botiquin_01`, `wilbur_municion_01`, `wilbur_vida_baja_01` | líneas opcionales (sección 2), misma voz de Wilbur |

---

## 5. Resumen de entregas
- **Edgar / David:** 10 imágenes de HUD (5 caras + 4 iconos + 1 marco opcional), 3 objetos y 3 efectos (16 en total).
- **Cristian:** 12 SFX + 3 líneas opcionales de Wilbur.
