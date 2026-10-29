# CLAUDE.md — Plomo y Paja

## Contexto del proyecto
Videojuego FPS noir en Unity 2022.3 LTS (C#). Inspirado en Mouse P.I. For Hire:
estilo cartoon rubber-hose de los años 30, escenarios 3D, personajes como sprites billboard 2D.
Historia de los 3 cerditos reinterpretada: noir, traición, decisiones morales, 3 finales.

## Equipo
- **David Cabezas** — Player, Escopeta, GameManager, integración
- **Edgar Arteaga** — Enemigos (IA, estados, combate)
- **Cristian** — Escenas, nivel, iluminación, NavMesh, UI

## Estructura de carpetas
```
Assets/_Game/
├── Scripts/
│   ├── Player/          → David
│   ├── Enemies/         → Edgar
│   ├── AI/              → David (Claude API)
│   ├── Dialogue/        → Edgar
│   ├── Clues/           → Cristian
│   ├── UI/              → Cristian
│   ├── Core/            → David (GameManager, GameStateTracker)
│   └── Audio/           → Todos
├── Scenes/              → Cristian
├── Prefabs/             → Todos
├── Sprites/             → Todos (sprites 2D de personajes)
└── Settings/            → APISettings.asset (GITIGNOREADO - cada uno crea el suyo)
```

## Convenciones de código
- Idioma: español en comentarios, inglés en nombres de variables/métodos
- Un script por archivo, nombre igual a la clase
- Eventos con UnityEvent o Action (no coroutines para lógica de juego)
- Singleton patrón: GameManager, GameStateTracker, UIManager, AudioManager
- Scripts con [Header] y [Tooltip] en todos los campos del Inspector

## Estilo visual (Mouse P.I.)
- URP con Global Volume: Film Grain + Vignette + Color Grading (desaturado -70)
- Enemigos: SpriteRenderer con BillboardSprite.cs (siempre miran a la cámara)
- Escenarios: geometría 3D simple, materiales gris/negro con algunos acentos de color
- Iluminación: direccional tenue + point lights naranjas/azules para el noir

## IA del juego
La voz "consciente" de Wilbur usa la API de Claude (Anthropic).
- Script: Assets/_Game/Scripts/AI/AICompanion.cs
- Requiere: Assets/_Game/Settings/APISettings.asset (NO subir al repo, cada dev crea el suyo)
- Cómo crear APISettings: en Unity → clic derecho en _Game/Settings → Create → Game → APISettings
- Obtener API key: https://console.anthropic.com

## Ramas Git
- `main` — versión estable, solo merge cuando algo funciona completo
- `dev` — integración diaria
- `player` — David
- `enemies` — Edgar  
- `levels` — Cristian

## Flujo de trabajo diario
1. `git pull origin dev` al empezar
2. Trabajar en tu rama
3. `git push origin tu-rama`
4. Al final del día: merge a dev juntos (un video call de 15 min)

## Versión Unity
Unity 2022.3 LTS — TODOS deben usar EXACTAMENTE la misma versión menor.
Verificar en Unity Hub → Installs.
