# Game Design Document — Plomo y Paja
**Versión:** 1.0 | **Fecha:** Octubre 2026 | **Equipo:** David Cabezas, Edgar Arteaga, Cristian

---

## 1. Concepto

**Género:** FPS noir, thriller narrativo con decisiones  
**Estilo visual:** Cartoon rubber-hose de los años 30 en 3D (inspirado en Mouse P.I. For Hire)  
**Duración objetivo:** 25–40 minutos por partida  
**Motor:** Unity 2022.3 LTS  

**Logline:**  
*Wilbur, el hermano mayor, irrumpe en la ciudad del mafioso Don Lobo para rescatar a su hermano menor. Pero la verdad es más retorcida de lo que parece: su hermanito no es ninguna víctima.*

---

## 2. Historia

### Trasfondo
En la ciudad de **Porcópolis**, los tres hermanos cerditos vivían cada uno en su casa. Porky, el menor, tenía la casa más precaria y trabajaba para **Don Lobo**, el capo local. Porky cometió el error de traicionar a Don Lobo: le robó dinero y lo humilló en público. Don Lobo, en lugar de matarlo, lo "tomó como garantía" — lo tiene en su guarida, esperando que pague.

Wilbur (el jugador), el hermano mayor, no sabe nada de esto. Solo ve que la casa de Porky está destruida y que su hermano desapareció. Asume lo peor: secuestro, violencia. Toma su escopeta y va a buscar a Don Lobo.

### Personajes

**Wilbur** (jugador)  
Hermano mayor, detective aficionado, escopeta en mano. Leal pero impulsivo. Su voz interna (la IA) lo acompaña —y a veces lo confunde.

**Don Lobo**  
El "villano" que no es del todo villano. Capo respetado. No quiso hacerle daño a Porky, solo quería lo que le debía. En el Final C, es un aliado inesperado.

**Porky (hermano menor)**  
El que aparenta ser víctima. Mentiroso, cobarde, pero con algo de vergüenza. Traicionó a Don Lobo porque quería más de lo que le correspondía.

**Max (hermano del medio)**  
Aparece en el Final C como testigo. Siempre supo algo pero nunca dijo nada.

**La IA**  
Una voz en el oído de Wilbur. Origen desconocido. Sabe cosas que Wilbur no debería saber. A veces ayuda, a veces confunde. Cada partida es diferente porque es una IA real (API de Claude).

**Los Lobos Guardias** (enemigos)  
Secuaces de Don Lobo. Algunos son agresivos, algunos se rinden y pueden ser interrogados. Algunos saben la verdad sobre Porky.

---

## 3. Mecánicas

### 3.1 Movimiento
- FPS con CharacterController
- Caminar (4 m/s), correr (7 m/s, hace ruido y alerta enemigos)
- Sin salto (mantiene el tono serio y noir)
- Agacharse opcional (Ctrl) — no implementado en MVP

### 3.2 Escopeta (única arma)
- 6 balas por cargador, 18 en reserva
- Recarga manual (R) o automática al vaciar
- 8 perdigones con dispersión = daño a área corta
- Recarga tarda 2.5 segundos — tensión real al estar rodeado

### 3.3 Sistema de pistas
5 pistas repartidas en los dos niveles:

| ID | Nombre | Ubicación | Qué revela |
|---|---|---|---|
| nota_porky_01 | Nota de Porky | Casa de Porky, cajón | Lenguaje cómplice con el Lobo |
| contrato_01 | Contrato de deuda | Casa de Porky, debajo del sofá | Porky le debía dinero al Lobo |
| foto_reunion_01 | Foto de reunión | Campamento del lobo, barracón | Porky y el Lobo reuniéndose antes |
| registro_01 | Registro de deudas | Guardia rendido (pista de interrogatorio) | El monto exacto que Porky robó |
| carta_lobo_01 | Carta de Don Lobo | Guarida final | El Lobo explica su versión |

### 3.4 Interrogaciones
- 3 guardias pueden ser interrogados (los que se rinden al bajar 30% HP)
- Árbol de diálogo con 2-3 opciones por NPC
- Resultado: Helpful / Misleading / Refused
- Se registra en GameStateTracker

### 3.5 La IA (voz de Wilbur)
- Llama a la API de Claude con el contexto del juego
- Responde con 1-2 oraciones, tono noir ambiguo
- Aparece en:
  - Al recoger una pista
  - Al terminar un interrogatorio
  - Al matar a un enemigo (¿valió la pena?)
  - Al llegar a la confrontación final
  - Cuando el jugador tiene poca vida
- Si no hay conexión/API: líneas de respaldo hardcoded

### 3.6 Sistema de decisiones → Finales
El `GameStateTracker` lleva el registro de:
- Pistas recogidas (0-5)
- Interrogaciones completadas (0-3)
- Si el lobo fue matado o perdonado

**Final A — "La bala fácil"**  
*Condición:* < 2 pistas + lobo muerto  
Wilbur cree haber ganado. Rescata a Porky. Porky miente: "gracias, me tenían secuestrado". Créditos. Post-crédito (5 seg): flashback donde Porky entrega el dinero robado a alguien y se ríe.  
*Mensaje:* A veces la justicia ciega solo ciega.

**Final B — "Confianza equivocada"**  
*Condición:* 2-3 pistas + lobo muerto  
Wilbur sabía algo pero no suficiente. En la confrontación final, Porky sale a defender al Lobo — y Wilbur le apunta a su propio hermano sin entender nada. Pantalla en negro.  
*Mensaje:* Seguir creyendo lo que queremos creer tiene un precio.

**Final C — "La verdad pesa más"**  
*Condición:* ≥ 3 pistas + ≥ 2 interrogaciones + lobo perdonado  
Wilbur lo entiende todo. El Lobo y Wilbur confrontan a Porky juntos. Max (el hermano del medio) aparece como testigo. Porky confiesa. Porky irá a trabajar para el Lobo como pago de su deuda. Todos en paz... más o menos.  
*Mensaje:* La verdad duele más que una bala, pero sana mejor.

---

## 4. Niveles

### Nivel 1: Casa de Porky ("La Escena del Crimen")
- Interior + exterior de una casa destruida
- Ambiente: noche, lluvia, luz de farola rota
- Contiene: 2 pistas + 1 enemigo patrullero + 1 centinela
- Salida: puerta trasera que lleva al campamento
- Duración estimada: 8-12 min

### Nivel 2: Campamento de Don Lobo ("Territorio Enemigo")  
- Exterior: callejones, un edificio principal (guarida), barracones
- Ambiente: noche, niebla, letreros rotos, jazz de fondo
- Contiene: 3 pistas + 4 enemigos (2 patrulleros, 1 centinela, 1 rendible) + Don Lobo (jefe final)
- Duración estimada: 15-20 min

---

## 5. Estilo visual (implementación técnica)

### La clave: 3D + sprites 2D billboard
Los personajes (enemigos, Don Lobo) son sprites 2D con animación frame-by-frame que siempre miran a la cámara del jugador. Esto replica el efecto de Mouse P.I. For Hire.

### Post-procesado URP
Configurar en el Volume global:
- Color Adjustments: Saturation -70, Contrast +25
- Vignette: Intensity 0.35, Color negro
- Film Grain: Thin1, Intensity 0.35
- Bloom: Threshold 1.2, Intensity 0.1 (muy sutil)

### Paleta de colores
- Base: blancos, grises, negros (B&N casi total)
- Acentos: naranja cálido (farolas, fuego)
- Azul frío (sombras nocturnas)
- Rojo solo en daño y muerte

### Audio
- Música: jazz instrumental de los 30s (libre de derechos en freemusicarchive.org)
- SFX: disparo escopeta, impacto, pasos en baldosa, lluvia

---

## 6. Distribución de trabajo diaria

| Día | David | Edgar | Cristian |
|---|---|---|---|
| **Jue 29** | PlayerController + ShotgunController | EnemyAI + EnemyHealth | Escena Nivel 1 (geometría básica) |
| **Vie 30** | AICompanion (API Claude) + PlayerHealth | DialogueNPC + árbol diálogos guardias | Escena Nivel 2 + NavMesh Bake |
| **Sáb 31** | GameStateTracker + CluePickup | Don Lobo AI (BossAI) | 5 pistas en escenas + UIManager |
| **Dom 1** | FinaleTrigger + 3 finales | Bug fix + balance enemigos | HUD + pantallas de final |
| **Lun 2** | Integración total + NoirPostProcess | SFX + Audio | Pulido visual + luces |
| **Mar 3** | Build final Windows | Pruebas en otra PC | README + capturas |
| **Mié 4** | **ENTREGA** | | |

---

## 7. Riesgos y mitigaciones

| Riesgo | Probabilidad | Plan B |
|---|---|---|
| API de Claude no disponible | Baja | Líneas de respaldo hardcoded (ya implementadas) |
| NavMesh difícil de configurar | Media | Enemigos con movimiento simple (sin NavMesh) |
| Sprites rubber-hose difíciles de conseguir | Alta | Usar formas geométricas simples (cuadrados) como placeholder |
| Conflictos Git en escenas .unity | Alta | Cada uno trabaja en su escena, merge al final |
| Poco tiempo para los 3 finales | Media | Los 3 finales son la misma escena con diferente texto |

---

## 8. Assets y recursos

### Sprites (placeholder → buscar en)
- itch.io: "retro cartoon characters free"
- Kenney.nl: assets de juegos gratis
- OpenGameArt.org

### Música
- freemusicarchive.org (filtrar: jazz, 1930s, CC0)
- incompetech.com (Kevin MacLeod)

### SFX
- freesound.org
- zapsplat.com

### Fuentes tipográficas (noir)
- "Bebas Neue" o "Oswald" para títulos
- "Special Elite" para textos de pistas (parece escrito a máquina)
