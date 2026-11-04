# AUDIT MENU.2 — Cierre de la fase MENU

ESTADO: CERRADA (David probó el menú en Unity y quedó aprobado)

**Fecha:** 3 nov 2026 · **Rama:** `integracion/dia4`

## Cierre
- **Prueba:** David probó el menú en Unity y lo aprobó. Esta sesión no corrió Unity ni repitió la prueba, y solo registra lo que David informó.
- **Se commitea lo generado por Crear menu de inicio:** `MenuPrincipal.unity`, `PauseMenu_Canvas.prefab`, los atlas del título y los `.meta` de las fuentes.
- **Revisión de lo generado, por archivos:** la escena no tiene `Borde` ni referencias a los PNG borrados y lleva los valores de MENU.1.3. Los dos atlas son estáticos, con `PLOMAJ` y `y`. La pausa dice "Salir al menú".
- **Estado de las auditorías:** MENU.1, 1.1, 1.2 y 1.3 pasan a `LUZ VERDE (orquestador, 3 nov)`.

## Fuera de este commit (de otras fases)
- `Docs/guiones/Guion-Nivel2.md`, `Docs/guiones/Guion-Tutorial-Huellas.md`, `Docs/phases/Fase-N1-Mapa-Tutorial.md`, `Docs/phases/Fase-N2-Campamento.md`
- `.meta` de `Art/FX` (más `Art/FX.meta`), `Art/Props` (más `Art/Props.meta` y `pickups.meta`) y 7 de `Art/UI/HUD`
- `.meta` de `Scripts/Pickups` (más `Scripts/Pickups.meta`) y `Scripts/Editor/PickupBuilder.cs.meta`

## Bloque para el Registro de fases

**MENU — Pantalla de inicio (3 nov 2026)**

**Resultado:** cerrada; David la probó en Unity y la aprobó. El juego abre en `MenuPrincipal`: espera con fundido lento y movimiento de cámara, "Pulsa cualquier tecla", rayo que corta a la tormenta, título por letras, botones con ratón y teclado, opciones, créditos y `musica_intro` que sigue sin reiniciarse hasta el libro.

**Subfases:**
- **MENU.0:** reconocimiento e inventario.
- **MENU.1:** estados espera → rayo → menú, `LightningFlash`, `MusicPlayer`, grano con `OldFilmFilter`, opciones, créditos y Jugar async.
- **MENU.1.1:** título `MenuTitleLogo` con Luckiest Guy y atlas estáticos.
- **MENU.1.2:** botones con `boton_placa` (se quitó el `Borde` negro) y cuadros sin marca de agua.
- **MENU.1.3:** la espera pasa a fundido de 2.0 s + 1.2 s, con cámara lenta en espera y tormenta. Reemplaza al libro animado de 1.1 y 1.2.

**Decisiones:**
- Se extendieron los scripts y el builder de Cristian; `Plomo y Paja → Crear menu de inicio` es la única fuente de la escena.
- Música: `musica_intro`. Los jazz no se usan.
- Grano: se reutilizó `OldFilmFilter`, sin extraer `FilmOverlay`.
- La pausa es la de Cristian; solo cambió el texto a "Salir al menú".
- Cuadros: los toma el builder por nombre (`menu_inicio_NN`, `menu_rayo_NN`), sin importar el formato; hoy son 8 JPG de 2752×1536.
- Luckiest Guy es Apache 2.0, no OFL; su licencia está en `Fonts/`.

**Para los demás:**
- `GameManager`: Esc ya no alterna la pausa en `MenuPrincipal` ni en `Intro`.
- `BotonCartoon` apaga su `Borde` si `grosorBorde` es 0. El prefab de pausa aún guarda 4 `Borde`, que se apagan en Play. Sus botones también seleccionan con el ratón y usan `sfx_ui_hover` / `sfx_ui_click`, mudos si faltan.
- `MusicPlayer` se apaga solo fuera de `MenuPrincipal` e `Intro`; `StoryBookDirector` le manda la bajada bajo el narrador y el fundido final.
- `OptionsPanel.Sensibilidad` multiplica la mirada en `PlayerController` y en `IntroCinematicDirector`.
- `LightningFlash` expone `StrikeStarted` y `AmbientFlashStarted`.
- `InterrogationUIBuilder.EnsureFont` tiene una sobrecarga para atlas con glifos a medida.
- Al cambiar el menú hay que volver a correr `Crear menu de inicio`, porque la escena guarda los valores serializados.

**Pendientes:**
- Perfilador de GC del título (`MenuTitleLogo.Update`).
- Probar en 21:9.
- Clic del loop de `musica_intro`; si lo hace, poner `loopCrossfadeSeconds` en 1–2.
- Pausa → Salir al menú, si no se probó.
- Audio con placeholder: `sfx_trueno`, `sfx_trueno_lejos`, `sfx_ui_jugar`; mudos: `sfx_ui_hover`, `sfx_ui_click`.
- `menu_logo` no existe: se usa el título por letras.
- Completar el apellido de Cristian en los créditos.
- Decidir si se quitan los `FondoOscuro` de Opciones y Créditos.

**Commits:** `5181553`, `d8c981d`, `8801486`, `b62b3ef`, `c419723`, `3352fec`, `ff7001f`, `d76b49f`, `e8bdb11`, `df91313`, `fe5e17b`, `aeebf05` y el commit de cierre `dia4(david): MENU cerrada: escena, prefab de pausa, atlas del titulo y auditoria de cierre`.
