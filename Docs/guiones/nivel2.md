# Guion — Nivel 2 "El Campamento", Don Lobo y los finales
**Versión:** 1.1 (4 nov 2026) · **Fase:** N2 (`Docs/phases/Fase-N2-Campamento.md`)

> Copiar este archivo al repo como `Docs/guiones/nivel2.md`. Claude Code lo lee en N2.1–N2.4.
> **Cambios en 1.1:** se **reutilizan las voces y los dibujos viejos de Don Lobo** (de la primera fase): las líneas marcadas **♻** ya están grabadas en `PLOMO_Y_PAJAV_1/_entrante_audio/Dialogos/donlobo/` y su texto se cambió para que coincida con lo grabado. Los dibujos viejos (`Art/Characters/donlobo/`) cubren el combate y la escena hasta que Edgar haga los nuevos. Nuevas líneas de golpe (`lobo_dano_01/02`). El grito de Porky usa su línea vieja `porky_mentira_01`.
> Nada de esto bloquea el código: si un archivo no llega, se usa un placeholder y se reemplaza después volviendo a correr el menú.
> **Las pistas del nivel 2 las hace Edgar** (`Docs/phases/Fase-Pistas.md`). El nivel solo deja las anclas donde van.

---

## 1. El nivel
Un campamento de la banda de Don Lobo escondido en el bosque, junto a un aserradero viejo. De noche, con niebla suave, lluvia fina y faroles cálidos. Tiene que verse bien: oscuro de ambiente, pero se lee todo.

| Zona | Qué hay | Tamaño aprox. |
|---|---|---|
| **Z0 — El bosque** | Wilbur aparece al inicio del sendero. Las huellas de lobo y cerdo siguen en el barro. Árboles densos a los lados, niebla, grillos (`amb_bosque`). | sendero de ~40 m |
| **Z1 — Portón del aserradero** | Portón de tablas con dos faroles, una camioneta vieja estacionada, cajas. 1 centinela + 1 patrullero. | 20 × 15 m |
| **Z2 — Barracones** | 3 barracones de madera (uno se puede entrar: ahí está la mesa con la **foto de la reunión**), carpas, una fogata y un gramófono que suena jazz (`amb_campamento`). 1 patrullero y **el Contable** (se rinde y se le interroga). | 35 × 25 m |
| **Z3 — Patio de madera** | Pilas de troncos y tablas para cubrirse, una sierra grande apagada. **El informante Colmillo** (se rinde y se le interroga; pasó aquí desde el nivel 1). | 25 × 20 m |
| **Z4 — La guarida** | Cabaña grande de troncos. Pasillo → **oficina** de Don Lobo (escritorio con la **carta de Don Lobo**) → **salón** (la pelea final). Al fondo, la puerta cerrada del **cuarto de atrás** (ahí está Porky; solo se ve en los finales). 1 guardia en la puerta. | salón ~18 × 14 m |

**Bordes (sin muros invisibles):** líneas de árboles densos, un río al oriente con baranda (al otro lado se ven las luces de Porcópolis), un barranco con una baranda de madera rota y la cerca de tablas del aserradero con huecos.

**Salón de Don Lobo:** escritorio grande, 4 columnas, una barra, una mesa de billar, un sofá. Dos puertas laterales por donde entran los refuerzos.

**Objetos (en muebles o contenedores, nunca sueltos):**
- En el campamento: 2 cajas de cartuchos y 1 botiquín.
- Antes del salón (pasillo/oficina): 1 botiquín, 1 caja de cartuchos y 1 caja de balas de revólver.
- Dentro del salón: 1 caja de cartuchos en un gabinete y 1 caja de balas detrás de la barra.

### Llegada
| id | Quién | Texto |
|---|---|---|
| `wilbur_bosque_01` | Wilbur | El bosque de Don Lobo. Hasta los árboles parecen deberle plata. |

Después sale un pensamiento de la API (contexto: acaba de entrar al bosque siguiendo las huellas, se oye jazz a lo lejos). Si no hay conexión o tarda más de 6 s:
- Jazz en medio del bosque. O el Lobo tiene buen gusto… o alguien está celebrando algo.
- Las huellas siguen juntas. Si lo arrastraban, se cansaron muy pronto.
- Un campamento entero para cobrar una deuda. Porky debía más que plata.

---

## 2. Don Lobo
**Cómo es (para Edgar):** lobo gris grande, de hombros anchos, con un bigote fino. Sombrero fedora negro con banda roja, traje de rayas morado oscuro, corbata roja, cadena de reloj dorada, guantes blancos de cuatro dedos y un puro. Lleva una pistola grande y dorada. Elegante y amenazante, no ridículo. *(Si los dibujos viejos de `donlobo` ya lo muestran distinto, manda el dibujo viejo: que los nuevos se parezcan a él.)*

**Voz (para Cristian):** la **misma voz base de las grabaciones viejas** de `donlobo`: grave, gruesa, con voz de mando y **acento francés** en español (*mon ami, cochon, voilà, non, alors*). Habla despacio, como un caballero que no necesita gritar.

### Combate (gritos) — ♻ = ya grabado
| id | ♻ archivo viejo | Cuándo | Texto | Sprite |
|---|---|---|---|---|
| `lobo_combate_01` | ♻ `donlobo_encuentro_01` | el jugador entra al salón | Ah, monsieur Wilbur… por fin. Pasa, pasa, la lluvia no es buena para el tocino. | `idle` → de pie |
| `lobo_combate_02` | ♻ `donlobo_combate_01` | disparando | ¡Qué maleducado, mon ami! | `shoot` |
| `lobo_combate_03` | ♻ `donlobo_combate_03` | disparando | No me obligues a ensuciarme los guantes. | `peek` |
| `lobo_combate_04` | ♻ `donlobo_combate_02` | al 50 % de vida, llama refuerzos | ¡Muchachos, ocúpense de él! | `call` |
| `lobo_combate_05` | ♻ `donlobo_encuentro_02` | se cubre | Has hecho mucho ruido para ser un cerdo tan pequeño. | `cover` |
| `lobo_dano_01` | ♻ `donlobo_dano_01` | recibe un golpe (de vez en cuando) | ¡Mon Dieu! | — |
| `lobo_dano_02` | ♻ `donlobo_dano_02` | recibe un golpe (de vez en cuando) | ¡Sacrebleu! ¡Mi traje! | — |
| `lobo_cae_01` | ♻ `donlobo_rendir_01` | cae de rodillas | Basta. Baja el arma y escucha la verdad. | `hit` → `rodillas` |
| `lobo_rodillas_01` | ♻ `donlobo_rendir_02` | de rodillas, Wilbur lejos | ¿Quieres a tu hermano? Pregúntale cuánto me debe. | `rodillas` |

Los golpes (`lobo_dano_*`) suenan como mucho una vez cada 3 s y nunca encima de otro grito.

---

## 3. Sprites de Don Lobo — `Art/Characters/lobo/`
**Mientras no haya dibujos nuevos, se usan los viejos** de `Art/Characters/donlobo/` (8 dibujos, lienzo 512) con esta equivalencia:
| Cuadro nuevo | Sale del viejo |
|---|---|
| `idle_01`, `idle_02` | `donlobo_idle_01`, `donlobo_idle_02` |
| `shoot_01` | `donlobo_shoot_01` |
| `peek_01` | `donlobo_shoot_02` |
| `cover_01`, `call_01` | `donlobo_idle_01` (se hunde detrás del escritorio) |
| `hit_01` | `donlobo_surrender_01` |
| `rodillas_01` / `rodillas_02` | `donlobo_surrender_01` / `donlobo_surrender_02` |
| `talk_01` / `talk_02` | `donlobo_talk_01` / `donlobo_talk_02` |
| `angry_*`, `laugh_*` | `talk_*` |
| `death_01` | (sin dibujo: el código lo aplasta) |

**Dibujos nuevos (Edgar, si alcanza el tiempo):** fondo **magenta puro `#FF00FF`**, lienzo **1024×1024**, sin texto, la parte más baja del dibujo tocando el borde inferior. Originales en `_entrante_personajes/Lobo/`, recorte con `python Tools/character_key.py Lobo`. Cada uno reemplaza al viejo solo con volver a correr el menú. **Prioridad:** `rodillas_01/02` → `death_01` → `cover_01` → `hit_01` → `call_01` → `angry_01/02` → `laugh_01/02`.

**Prompt maestra (de pie)** — adjuntar un dibujo viejo de `donlobo` como referencia:
```
Game character sprite, 1930s rubber-hose cartoon style, Technicolor colors, clean thick ink outlines. The wolf gangster boss from the reference image, exactly the same design, colors and clothes. Full body, standing, three-quarter view. Flat solid pure magenta background (#FF00FF), no shadows on the background. Square image. No text.
```
**Prompt maestra (de rodillas):**
```
Same wolf gangster, same style, same clothes, same magenta background (#FF00FF). Now he is on his knees on the floor, wounded, one hand on the floor, the hat a bit crooked, cartoon blood on his suit, proud but defeated expression, mouth closed. Full body. Square image. No text.
```

---

## 4. La confrontación (Don Lobo de rodillas)
Funciona igual que la de Centavo (retrato grande, papel, lip-sync), pero sin sistema de confianza. Las líneas sin ♻ **faltan por grabar**; sin audio, salen como texto en el papel.

### Ronda 0
| id | Quién | Texto | Sprite |
|---|---|---|---|
| `lobo_int_00` | Don Lobo | Voilà… el hermano mayor. Me arruinaste el tapete, mon ami. Dispara si quieres… pero un caballero escucha antes de disparar. | `rodillas` |

**Opciones de Wilbur:**
| # | id | Texto | Condición |
|---|---|---|---|
| 1 | `wilbur_lobo_0A` | ¿Dónde está Porky? | siempre |
| 2 | `wilbur_lobo_0B` | Dame una razón para no terminar esto ahora. | siempre |
| 3 | `wilbur_lobo_0C` | Sé lo de la caja fuerte, Lobo. Tres mil doscientos pesos. | solo si tiene `registro_01` o `contrato_01` |

**Respuestas:**
| id | Tras | Texto | Sprite |
|---|---|---|---|
| `lobo_int_0A` | 1 | En el cuarto de atrás, comiendo mejor que yo. Nadie le ha tocado un pelo… y todavía no sé si eso fue un error. | `talk` |
| `lobo_int_0B` | 2 | ¿Una razón? Te doy tres mil doscientas. Pregúntale a tu hermanito de dónde salieron. | `laugh` |
| `lobo_int_0C` | 3 | Ah… alors, no eres solo un cerdo con escopeta. Entonces ya sabes que el ladrón aquí no soy yo. | `talk` |

### Ronda 1
**Opciones de Wilbur:**
| # | id | Texto |
|---|---|---|
| 1 | `wilbur_lobo_1A` | Tus hombres destrozaron su casa. |
| 2 | `wilbur_lobo_1B` | Mi hermano no es un ladrón. |
| 3 | `wilbur_lobo_1C` | ¿Qué quieres de él? |

**Respuestas:**
| id | ♻ | Opción | Condición | Texto | Sprite |
|---|---|---|---|---|---|
| `lobo_int_1A` | | 1 | siempre | Mis hombres fueron a cobrar, y la casa era de paja, mon ami: se cae si la miras feo. Él ya se había ido… y vino aquí solito, a pedir tiempo. | `talk` |
| `lobo_int_1B` | | 2 | Centavo perdonado o sin resolver | Non? Entonces pregúntale quién abrió mi caja fuerte y quién se rió de mí en mi propio club. Yo tengo memoria… y tengo un contador. | `angry` |
| `lobo_int_1B_muerto` | | 2 | Centavo muerto | Non? Entonces pregúntale quién abrió mi caja fuerte y quién se rió de mí en mi propio club. Yo tengo memoria… y tenía un contador. Tú sabrás qué le pasó. | `angry` |
| `lobo_int_1C` | ♻ `donlobo_finalc_01` | 3 | siempre | Tu hermano trabajará para mí hasta pagar el último centavo. Es lo justo, non? | `talk` |

### Ronda 2 — Grito de Porky (desde el cuarto de atrás, sin retrato propio)
| id | ♻ | Quién | Texto | Sprite del Lobo |
|---|---|---|---|---|
| `porky_grito_01` | ♻ `porky_mentira_01` (si existe) | PORKY (fuera de cuadro) | ¡Hermano! ¡Me tenían secuestrado, te lo juro! | `laugh` |
| `lobo_int_2` | | Don Lobo | Escucha a tu hermanito, cochon. Siempre tiene una historia. La pregunta es cuál te vas a creer. | `talk` |

### Pensamiento de la IA (texto arriba, sin voz)
Lo genera la API con el contexto (lo que dijo el Lobo, qué pistas tiene Wilbur, qué pasó con Centavo, los interrogatorios, el grito de Porky). Si no hay conexión:
- Un lobo de rodillas sigue siendo un lobo. Pero esta noche es el único que no te ha pedido nada.
- Porky grita lo que quieres oír. Por eso mismo deberías escucharlo dos veces.
- Tres mil doscientos pesos. ¿Cuánto vale la palabra de cada uno?
- *(solo si tiene 3 pistas o más)* Ya lo leíste, ya lo viste, ya te lo dijeron. Lo difícil no es saber, Wilbur. Es aceptarlo.
- *(solo si tiene menos de 2 pistas)* Sabes muy poco de esta historia. Una bala no te va a contar el resto.

### Decisión
Dos botones grandes: **"Jalar el gatillo"** / **"Bajar el arma"**.
| id | ♻ | Rama | Quién | Texto | Sprite |
|---|---|---|---|---|---|
| `wilbur_lobo_matar` | | matar | Wilbur | Se acabó, Lobo. | — |
| `lobo_muerte_01` | ♻ `donlobo_morir_01` | matar | Don Lobo | Idiota… nunca… supiste… la verdad. *(se corta con el disparo)* | `talk` → `death` |
| `wilbur_lobo_perdonar` | | perdonar | Wilbur | Levántate. Vamos a hablar con mi hermano. | — |
| `lobo_perdon_01` | | perdonar | Don Lobo | Enfin… un cerdo con cerebro. | `laugh` |

Después de la decisión (o si lo rematan de rodillas antes de hablar), a los 2.5 s empieza el final.

---

## 5. Los finales (en el libro)
Mismo libro de la intro. Ilustración a la izquierda (si no hay, las guardas) y texto a máquina a la derecha. Si no hay voz del narrador, cada página dura según el largo del texto. Al final: tarjeta **FIN** con el título del final y la frase, los créditos, y vuelta al menú.

### Final A — "La bala fácil" *(Lobo muerto, menos de 2 pistas)*
| Página | Texto | Ilustración |
|---|---|---|
| 1 | Don Lobo cayó, y con él se apagó el jazz del campamento. | `ilus_final_A` |
| 2 | En el cuarto de atrás, Porky lloraba. —Gracias, hermano. Me tenían secuestrado. | — |
| 3 | Wilbur lo abrazó y lo llevó a casa. Por fin, todo había terminado. | — |
| FIN | **La bala fácil** · *A veces la justicia ciega solo ciega.* | — |
| Post-crédito | Mientras tanto, lejos de allí, alguien contaba billetes y se reía. Tres mil doscientos pesos. Ni uno menos. | `ilus_final_A_post` |

### Final B — "Confianza equivocada", versión Lobo muerto *(Lobo muerto, 2 pistas o más)*
| Página | Texto | Ilustración |
|---|---|---|
| 1 | Don Lobo cayó. Pero algo no cuadraba: las cuentas, las huellas, las palabras de todos. | `ilus_final_B` |
| 2 | Porky salió del cuarto de atrás gritando el nombre del Lobo… y se arrodilló junto a él. | — |
| 3 | —¡Era mi única salida, Wilbur! ¡Le debía todo! | — |
| 4 | Wilbur levantó la escopeta sin saber a quién le apuntaba. Y por primera vez no supo a quién creerle. | — |
| FIN | **Confianza equivocada** · *Seguir creyendo lo que queremos creer tiene un precio.* | — |

### Final B — "Confianza equivocada", versión Lobo perdonado *(perdonado sin pruebas suficientes)*
| Página | Texto | Ilustración |
|---|---|---|
| 1 | Wilbur bajó el arma, sin estar seguro de nada. | `ilus_final_B` |
| 2 | Porky salió corriendo del cuarto de atrás y se escondió detrás de su hermano. —¡Vámonos, Wilbur, antes de que cambie de idea! | — |
| 3 | Don Lobo los dejó ir. —La cuenta sigue abierta, cochon. Y yo sé dónde vives. | — |
| 4 | Wilbur nunca supo a quién había salvado esa noche. | — |
| FIN | **Confianza equivocada** · *Seguir creyendo lo que queremos creer tiene un precio.* | — |

### Final C — "La verdad pesa más" *(Lobo perdonado, 3 pistas o más y 2 interrogatorios o más)*
| Página | Texto | Ilustración |
|---|---|---|
| 1 | Wilbur bajó la escopeta. Esta vez sabía exactamente a quién le estaba creyendo. | `ilus_final_C` |
| 2 | El Lobo y Wilbur abrieron juntos la puerta del cuarto de atrás. Max ya estaba ahí: lo había sabido todo el tiempo y nunca dijo nada. | — |
| 3 | Porky miró a sus dos hermanos y, por una vez, no inventó nada. —Fui yo. La caja fuerte, el club… todo. | — |
| 4 | Porky trabajaría para el Lobo hasta pagar el último centavo. Max prometió no volver a callarse. Y Wilbur volvió a su casa de ladrillo. | — |
| 4b *(solo si Centavo fue perdonado, va después de la 4)* | Las cuentas las llevó Centavo, con su calculadora todavía a plazos. | — |
| FIN | **La verdad pesa más** · *La verdad duele más que una bala, pero sana mejor.* | — |

### Créditos
El texto de `Docs/guiones/menu.md` §2, en una página del libro.

---

## 6. Arte de los finales (opcional — David con ChatGPT)
Mismo chat de las ilustraciones de la intro (para que los cerditos salgan iguales), mismo estilo: **1024×1024**, sin texto, el mismo encabezado de estilo de `Docs/guiones/intro.md` §7.2. Van a `Art/Intro/`.
| Archivo | Escena |
|---|---|
| `ilus_final_A` | Wilbur abrazando a Porky bajo la lluvia, frente al campamento apagado. Porky mira de reojo, con una sonrisita. |
| `ilus_final_A_post` | Sombra de Porky en un cuarto oscuro contando un fajo de billetes bajo una lámpara, riéndose. |
| `ilus_final_B` | Wilbur de pie, escopeta en mano, entre dos sombras que le gritan; relámpago, mucho contraste. |
| `ilus_final_C` | Los tres hermanos y el lobo gangster en un despacho; Porky con la cabeza gacha, Max cruzado de brazos, Wilbur con la escopeta abajo. |

---

## 7. Resumen de audios para Cristian
WAV, 44 100 Hz, mono, máx. 0.1 s de silencio al inicio y al final. El nombre del archivo es exactamente el id. **Misma voz base que las grabaciones viejas.**

**Ya grabado (♻, solo se copia con el nombre nuevo):** lobo_combate_01…05, lobo_dano_01, lobo_dano_02, lobo_cae_01, lobo_rodillas_01, lobo_int_1C, lobo_muerte_01, y porky_grito_01 si existe `porky_mentira_01`.

**Falta grabar, en este orden:**
1. **Don Lobo (9)** → `Audio/Voices/lobo/`: lobo_int_00, lobo_int_0A, lobo_int_0B, lobo_int_0C, lobo_int_1A, lobo_int_1B, lobo_int_1B_muerto, lobo_int_2, lobo_perdon_01
2. **Wilbur (9)** → `Audio/Voices/wilbur/`: wilbur_lobo_0A, wilbur_lobo_0B, wilbur_lobo_0C, wilbur_lobo_1A, wilbur_lobo_1B, wilbur_lobo_1C, wilbur_lobo_matar, wilbur_lobo_perdonar, wilbur_bosque_01
3. **Porky (1, solo si no existe `porky_mentira_01`)** → `Audio/Voices/porky/`: porky_grito_01
4. **Ambientes:** `amb_bosque` (bosque de noche), `amb_campamento` (gramófono con jazz años 30 lejano + fogata).
5. Opcional: `musica_final` (jazz lento años 30, CC0) y el narrador de los finales (`Audio/Voices/narrador/`: narrador_final_A_01…03 + narrador_final_A_post, narrador_final_Bm_01…04, narrador_final_Bp_01…04, narrador_final_C_01…04 + narrador_final_C_04b).

Sin audio, todo sale igual como texto: nada se rompe.
