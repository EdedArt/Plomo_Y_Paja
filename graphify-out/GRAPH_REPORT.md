# Graph Report - Scripts  (2026-10-29)

## Corpus Check
- Corpus is ~18,085 words - fits in a single context window. You may not need a graph.

## Summary
- 409 nodes · 845 edges · 18 communities (16 shown, 2 thin omitted)
- Extraction: 95% EXTRACTED · 5% INFERRED · 0% AMBIGUOUS · INFERRED: 40 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Dialogue UI Canvas
- Core Files & API Settings
- Dialogue Database
- Player Health & Shotgun
- NivelPorky Level Builder
- AI Companion
- Enemy AI States
- GameManager & Finale
- Endings & State Tracker
- Test Prefab Generator
- Clues & Test Tools
- Enemy Health & Surrender
- Noir Post-Processing
- Level Markers
- Billboard Sprites
- Player Controller
- EnemyState Enum
- EnemyType Enum

## God Nodes (most connected - your core abstractions)
1. `EnemyAI` - 34 edges
2. `DialogueNPC` - 30 edges
3. `DialogueUI` - 29 edges
4. `ShotgunController` - 28 edges
5. `AICompanion` - 26 edges
6. `UIManager` - 25 edges
7. `EnemyHealth` - 20 edges
8. `PlayerController` - 19 edges
9. `GameStateTracker` - 18 edges
10. `LevelBuilder_NivelPorky` - 18 edges

## Surprising Connections (you probably didn't know these)
- `DialogueNPC` --references--> `EnemyHealth`  [EXTRACTED]
  Dialogue/DialogueNPC.cs → Enemies/EnemyHealth.cs
- `DialogueNPC` --references--> `PlayerController`  [EXTRACTED]
  Dialogue/DialogueNPC.cs → Player/PlayerController.cs
- `DialogueNPC` --references--> `ShotgunController`  [EXTRACTED]
  Dialogue/DialogueNPC.cs → Player/ShotgunController.cs
- `EnemyAI` --references--> `BillboardSprite`  [EXTRACTED]
  Enemies/EnemyAI.cs → Enemies/BillboardSprite.cs
- `EnemyAI` --references--> `EnemyHealth`  [EXTRACTED]
  Enemies/EnemyAI.cs → Enemies/EnemyHealth.cs

## Import Cycles
- None detected.

## Communities (18 total, 2 thin omitted)

### Community 0 - "Dialogue UI Canvas"
Cohesion: 0.06
Nodes (6): DialogueUI, Exists, Instance, IsOpen, UIManager, Instance

### Community 1 - "Core Files & API Settings"
Cohesion: 0.11
Nodes (3): APISettings, ApiKey, IsConfigured

### Community 2 - "Dialogue Database"
Cohesion: 0.12
Nodes (12): DialogueDatabase, DialogueNode, IsEnd, DialogueOption, DialogueTree, DialogueNPC, DisplayName, WasInterrogated (+4 more)

### Community 3 - "Player Health & Shotgun"
Cohesion: 0.10
Nodes (9): PlayerHealth, CurrentHealth, HealthPercentage, IsDead, MaxHealth, ShotgunController, CurrentAmmo, IsReloading (+1 more)

### Community 4 - "NivelPorky Level Builder"
Cohesion: 0.20
Nodes (3): LevelBuilder_NivelPorky, LevelBuilderUtils, WallOpening

### Community 5 - "AI Companion"
Cohesion: 0.10
Nodes (11): AICompanion, Instance, AITrigger, ClueFound, EnemyKilled, FinalConfrontation, InterrogationEnded, LowHealth (+3 more)

### Community 6 - "Enemy AI States"
Cohesion: 0.15
Nodes (3): EnemyAI, CurrentState, IsSurrendered

### Community 7 - "GameManager & Finale"
Cohesion: 0.14
Nodes (5): FinaleTrigger, GameManager, Instance, IsGameOver, IsGamePaused

### Community 8 - "Endings & State Tracker"
Cohesion: 0.12
Nodes (11): GameEnding, BlindJustice, PartialTruth, TruthAndForgiveness, GameStateTracker, AIContext, CluesFound, Instance (+3 more)

### Community 10 - "Clues & Test Tools"
Cohesion: 0.15
Nodes (3): AICompanionTester, CluePickup, LightFlicker

### Community 11 - "Enemy Health & Surrender"
Cohesion: 0.15
Nodes (6): EnemyHealth, CanSurrender, HealthPercentage, IsDead, IsSurrendered, NpcId

### Community 13 - "Level Markers"
Cohesion: 0.19
Nodes (10): LevelMarker, Id, Type, MarkerType, Clue, InterrogableGuard, LevelExit, PatrolWaypoint (+2 more)

### Community 14 - "Billboard Sprites"
Cohesion: 0.19
Nodes (4): BillboardSprite, BillboardType, Full, YAxisOnly

### Community 15 - "Player Controller"
Cohesion: 0.21
Nodes (3): PlayerController, IsMoving, IsRunning

### Community 16 - "EnemyState Enum"
Cohesion: 0.25
Nodes (8): EnemyState, Alert, Attack, Chase, Dead, Idle, Patrol, Surrender

### Community 17 - "EnemyType Enum"
Cohesion: 0.67
Nodes (3): EnemyType, Centinela, Patrullero

## Knowledge Gaps
- **65 isolated node(s):** `ClueFound`, `InterrogationEnded`, `EnemyKilled`, `FinalConfrontation`, `LowHealth` (+60 more)
  These have ≤1 connection - possible missing edges. (Counts symbols only; 127 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **2 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `EnemyAI` connect `Enemy AI States` to `Core Files & API Settings`, `NivelPorky Level Builder`, `Test Prefab Generator`, `Clues & Test Tools`, `Enemy Health & Surrender`, `Billboard Sprites`, `EnemyState Enum`, `EnemyType Enum`?**
  _High betweenness centrality (0.170) - this node is a cross-community bridge._
- **What connects `ClueFound`, `InterrogationEnded`, `EnemyKilled` to the rest of the system?**
  _65 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Dialogue UI Canvas` be split into smaller, more focused modules?**
  _Cohesion score 0.05660377358490566 - nodes in this community are weakly interconnected._
- **Why does `DialogueNPC` connect `Dialogue Database` to `Core Files & API Settings`, `Player Health & Shotgun`, `NivelPorky Level Builder`, `Test Prefab Generator`, `Clues & Test Tools`, `Enemy Health & Surrender`, `Player Controller`?**
  _High betweenness centrality (0.160) - this node is a cross-community bridge._
- **Should `Core Files & API Settings` be split into smaller, more focused modules?**
  _Cohesion score 0.1076923076923077 - nodes in this community are weakly interconnected._
- **Why does `AICompanion` connect `AI Companion` to `Dialogue UI Canvas`, `Core Files & API Settings`, `Clues & Test Tools`?**
  _High betweenness centrality (0.132) - this node is a cross-community bridge._
- **Should `Dialogue Database` be split into smaller, more focused modules?**
  _Cohesion score 0.11561561561561562 - nodes in this community are weakly interconnected._