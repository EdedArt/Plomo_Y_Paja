# Graph Report - Scripts  (2026-10-30)

## Corpus Check
- Corpus is ~27,757 words - fits in a single context window. You may not need a graph.

## Summary
- 590 nodes · 1331 edges · 19 communities (13 shown, 6 thin omitted)
- Extraction: 95% EXTRACTED · 5% INFERRED · 0% AMBIGUOUS · INFERRED: 61 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Level Builder NivelPorky
- Billboard Sprites & Animation
- Script Files Hub
- Audio Library & API Settings
- Clues, Finale & AI Tester
- Player Controller & Dialogue Data
- Enemy Combat FX
- AI Companion
- Dialogue UI
- Prefab & Sprite Set Builders
- Barks & UI Manager
- Post-Process Volumes
- Game State & Endings
- Shotgun Feedback & Player Prefab
- Viewmodel & Canvas UI
- Level Markers
- Enemy Barks
- Enemy States
- Light Flicker

## God Nodes (most connected - your core abstractions)
1. `EnemyAI` - 35 edges
2. `CartoonFx` - 35 edges
3. `BillboardSprite` - 34 edges
4. `ShotgunController` - 33 edges
5. `DialogueNPC` - 30 edges
6. `DialogueUI` - 29 edges
7. `AICompanion` - 26 edges
8. `EnemyHealth` - 25 edges
9. `UIManager` - 25 edges
10. `AudioLibrary` - 24 edges

## Surprising Connections (you probably didn't know these)
- `NoirPostProcess` --references--> `PlayerHealth`  [EXTRACTED]
  Core/NoirPostProcess.cs → Player/PlayerHealth.cs
- `DialogueNPC` --references--> `EnemyHealth`  [EXTRACTED]
  Dialogue/DialogueNPC.cs → Enemies/EnemyHealth.cs
- `DialogueNPC` --references--> `ShotgunController`  [EXTRACTED]
  Dialogue/DialogueNPC.cs → Player/ShotgunController.cs
- `FirstPersonViewmodel` --references--> `CharacterSpriteSet`  [EXTRACTED]
  Player/FirstPersonViewmodel.cs → Visual/CharacterSpriteSet.cs
- `DialogueNPC` --references--> `PlayerController`  [EXTRACTED]
  Dialogue/DialogueNPC.cs → Player/PlayerController.cs

## Import Cycles
- None detected.

## Communities (19 total, 6 thin omitted)

### Community 0 - "Level Builder NivelPorky"
Cohesion: 0.07
Nodes (15): LevelBuilder_NivelPorky, LevelBuilderUtils, WallOpening, CartoonFx, FlashMeshMaterial, FlashSpriteMaterial, Instance, Decal (+7 more)

### Community 1 - "Billboard Sprites & Animation"
Cohesion: 0.07
Nodes (13): BillboardSprite, HasSprites, BillboardType, Full, YAxisOnly, EnemyAI, CurrentState, IsSurrendered (+5 more)

### Community 3 - "Audio Library & API Settings"
Cohesion: 0.07
Nodes (13): APISettings, ApiKey, IsConfigured, AudioLibrary, Active, SfxEntry, VoiceEntry, AudioLibraryLoader (+5 more)

### Community 4 - "Clues, Finale & AI Tester"
Cohesion: 0.06
Nodes (12): AICompanionTester, CluePickup, FinaleTrigger, GameManager, Instance, IsGameOver, IsGamePaused, PlayerHealth (+4 more)

### Community 5 - "Player Controller & Dialogue Data"
Cohesion: 0.08
Nodes (15): DialogueDatabase, DialogueNode, IsEnd, DialogueOption, DialogueTree, DialogueNPC, DisplayName, WasInterrogated (+7 more)

### Community 6 - "Enemy Combat FX"
Cohesion: 0.08
Nodes (9): EnemyCombatFx, EnemyHealth, CanSurrender, HealthPercentage, IsDead, IsSurrendered, NpcId, RubberHoseMotion (+1 more)

### Community 7 - "AI Companion"
Cohesion: 0.10
Nodes (11): AICompanion, Instance, AITrigger, ClueFound, EnemyKilled, FinalConfrontation, InterrogationEnded, LowHealth (+3 more)

### Community 8 - "Dialogue UI"
Cohesion: 0.12
Nodes (4): DialogueUI, Exists, Instance, IsOpen

### Community 9 - "Prefab & Sprite Set Builders"
Cohesion: 0.13
Nodes (4): CharacterSpriteSetBuilder, TestPrefabGenerator, URPVerifier, AnimClip

### Community 10 - "Barks & UI Manager"
Cohesion: 0.09
Nodes (3): BarkDatabase, UIManager, Instance

### Community 12 - "Game State & Endings"
Cohesion: 0.12
Nodes (11): GameEnding, BlindJustice, PartialTruth, TruthAndForgiveness, GameStateTracker, AIContext, CluesFound, Instance (+3 more)

### Community 15 - "Level Markers"
Cohesion: 0.19
Nodes (10): LevelMarker, Id, Type, MarkerType, Clue, InterrogableGuard, LevelExit, PatrolWaypoint (+2 more)

### Community 17 - "Enemy States"
Cohesion: 0.25
Nodes (8): EnemyState, Alert, Attack, Chase, Dead, Idle, Patrol, Surrender

## Knowledge Gaps
- **77 isolated node(s):** `ClueFound`, `InterrogationEnded`, `EnemyKilled`, `FinalConfrontation`, `LowHealth` (+72 more)
  These have ≤1 connection - possible missing edges. (Counts symbols only; 160 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **6 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `EnemyAI` connect `Billboard Sprites & Animation` to `Level Builder NivelPorky`, `Script Files Hub`, `Clues, Finale & AI Tester`, `Enemy Combat FX`, `Prefab & Sprite Set Builders`, `Enemy Barks`, `Enemy States`?**
  _High betweenness centrality (0.111) - this node is a cross-community bridge._
- **What connects `ClueFound`, `InterrogationEnded`, `EnemyKilled` to the rest of the system?**
  _77 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Level Builder NivelPorky` be split into smaller, more focused modules?**
  _Cohesion score 0.06550632911392405 - nodes in this community are weakly interconnected._
- **Why does `DialogueNPC` connect `Player Controller & Dialogue Data` to `Level Builder NivelPorky`, `Script Files Hub`, `Audio Library & API Settings`, `Clues, Finale & AI Tester`, `Enemy Combat FX`, `Prefab & Sprite Set Builders`, `Shotgun Feedback & Player Prefab`?**
  _High betweenness centrality (0.106) - this node is a cross-community bridge._
- **Should `Billboard Sprites & Animation` be split into smaller, more focused modules?**
  _Cohesion score 0.0734006734006734 - nodes in this community are weakly interconnected._
- **Why does `CartoonFx` connect `Level Builder NivelPorky` to `Script Files Hub`, `Clues, Finale & AI Tester`, `AI Companion`?**
  _High betweenness centrality (0.103) - this node is a cross-community bridge._
- **Should `Script Files Hub` be split into smaller, more focused modules?**
  _Cohesion score 0.08708272859216255 - nodes in this community are weakly interconnected._