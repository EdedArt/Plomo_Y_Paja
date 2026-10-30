# Graph Report - Scripts  (2026-10-30)

## Corpus Check
- 54 files · ~55,469 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 65 file(s) not represented in the graph (top: .meta 65)

## Summary
- 845 nodes · 2391 edges · 35 communities (22 shown, 13 thin omitted)
- Extraction: 91% EXTRACTED · 9% INFERRED · 0% AMBIGUOUS · INFERRED: 226 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `f789a863`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Vector3
- unityengine
- DialogueUI
- Material
- DialogueNPC
- AudioLibrary
- EnemyCombatFx
- CartoonFx
- UIManager
- BillboardSprite
- AICompanion
- EnemyAI
- NightSkyBuilder
- NoirPostProcess
- GameStateTracker
- EnemyHealth
- ShotgunController
- .HasFrames
- MarkerType
- ShotgunFeedback
- FirstPersonViewmodel
- RubberHoseAnimator
- MonoBehaviour
- EnemyBarks
- .Configure
- CharacterLook
- EnemyState
- WallFace
- WindowDamage
- BillboardType
- EnemyType
- Light
- RubberHoseMotion

## God Nodes (most connected - your core abstractions)
1. `CartoonKit` - 79 edges
2. `BillboardSprite` - 50 edges
3. `EnemyAI` - 49 edges
4. `CartoonFx` - 35 edges
5. `ShotgunController` - 33 edges
6. `DialogueNPC` - 32 edges
7. `AudioLibrary` - 31 edges
8. `EnemyHealth` - 31 edges
9. `DialogueUI` - 29 edges
10. `LevelBuilder_NivelPorky` - 27 edges

## Surprising Connections (you probably didn't know these)
- `NoirPostProcess` --references--> `PlayerHealth`  [EXTRACTED]
  PLOMO_Y_PAJAV_1/Assets/_Game/Scripts/Core/NoirPostProcess.cs → PLOMO_Y_PAJAV_1/Assets/_Game/Scripts/Player/PlayerHealth.cs
- `DialogueNPC` --references--> `BillboardSprite`  [EXTRACTED]
  PLOMO_Y_PAJAV_1/Assets/_Game/Scripts/Dialogue/DialogueNPC.cs → PLOMO_Y_PAJAV_1/Assets/_Game/Scripts/Enemies/BillboardSprite.cs
- `DialogueNPC` --references--> `EnemyHealth`  [EXTRACTED]
  PLOMO_Y_PAJAV_1/Assets/_Game/Scripts/Dialogue/DialogueNPC.cs → PLOMO_Y_PAJAV_1/Assets/_Game/Scripts/Enemies/EnemyHealth.cs
- `DialogueNPC` --references--> `ShotgunController`  [EXTRACTED]
  PLOMO_Y_PAJAV_1/Assets/_Game/Scripts/Dialogue/DialogueNPC.cs → PLOMO_Y_PAJAV_1/Assets/_Game/Scripts/Player/ShotgunController.cs
- `BillboardSprite` --references--> `EnemyAI`  [EXTRACTED]
  PLOMO_Y_PAJAV_1/Assets/_Game/Scripts/Enemies/BillboardSprite.cs → PLOMO_Y_PAJAV_1/Assets/_Game/Scripts/Enemies/EnemyAI.cs

## Import Cycles
- None detected.

## Communities (35 total, 13 thin omitted)

### Community 0 - "Vector3"
Cohesion: 0.09
Nodes (3): CartoonKit, LevelBuilder_NivelPorky, Particle

### Community 1 - "unityengine"
Cohesion: 0.07
Nodes (3): GameAudioImporter, CharacterSpriteImporter, EnvironmentModelImporter

### Community 2 - "DialogueUI"
Cohesion: 0.16
Nodes (4): DialogueUI, Exists, Instance, IsOpen

### Community 3 - "Material"
Cohesion: 0.07
Nodes (10): BarkDatabase, AudioLibraryBuilder, CharacterLookBuilder, CharacterSpriteSetBuilder, EditorMenuGuard, LevelBuilderUtils, WallOpening, RainBuilder (+2 more)

### Community 4 - "DialogueNPC"
Cohesion: 0.08
Nodes (15): DialogueDatabase, DialogueNode, IsEnd, DialogueOption, DialogueTree, DialogueNPC, DisplayName, WasInterrogated (+7 more)

### Community 5 - "AudioLibrary"
Cohesion: 0.06
Nodes (14): APISettings, ApiKey, IsConfigured, AudioLibrary, Active, Mix, SfxVolume, MixSettings (+6 more)

### Community 7 - "CartoonFx"
Cohesion: 0.08
Nodes (12): BlobShadow, CartoonFx, FlashMeshMaterial, FlashSpriteMaterial, Instance, Decal, Kind, Blood (+4 more)

### Community 8 - "UIManager"
Cohesion: 0.09
Nodes (5): KitMesh, Matrix, Submesh, UIManager, Instance

### Community 9 - "BillboardSprite"
Cohesion: 0.15
Nodes (4): BillboardSprite, HasSprites, CharacterSpriteSet, HasAnyFrames

### Community 10 - "AICompanion"
Cohesion: 0.11
Nodes (11): AICompanion, Instance, AITrigger, ClueFound, EnemyKilled, FinalConfrontation, InterrogationEnded, LowHealth (+3 more)

### Community 11 - "EnemyAI"
Cohesion: 0.14
Nodes (5): EnemyAI, ChaseSpeed, CurrentSpeed, CurrentState, IsSurrendered

### Community 12 - "NightSkyBuilder"
Cohesion: 0.18
Nodes (3): NightSkyBuilder, CloudDrift, DegreesPerSecond

### Community 14 - "GameStateTracker"
Cohesion: 0.12
Nodes (11): GameEnding, BlindJustice, PartialTruth, TruthAndForgiveness, GameStateTracker, AIContext, CluesFound, Instance (+3 more)

### Community 15 - "EnemyHealth"
Cohesion: 0.22
Nodes (6): EnemyHealth, CanSurrender, HealthPercentage, IsDead, IsSurrendered, NpcId

### Community 16 - "ShotgunController"
Cohesion: 0.05
Nodes (15): CluePickup, FinaleTrigger, GameManager, Instance, IsGameOver, IsGamePaused, PlayerHealth, CurrentHealth (+7 more)

### Community 18 - "MarkerType"
Cohesion: 0.19
Nodes (10): LevelMarker, Id, Type, MarkerType, Clue, InterrogableGuard, LevelExit, PatrolWaypoint (+2 more)

### Community 25 - "CharacterLook"
Cohesion: 0.27
Nodes (4): CharacterLook, BlobMaterial, PlaceholderMaterial, SpriteMaterial

### Community 26 - "EnemyState"
Cohesion: 0.25
Nodes (8): EnemyState, Alert, Attack, Chase, Dead, Idle, Patrol, Surrender

### Community 27 - "WallFace"
Cohesion: 0.40
Nodes (5): WallFace, None, Plain, Straw, Wainscot

### Community 29 - "WindowDamage"
Cohesion: 0.50
Nodes (4): WindowDamage, Boarded, Cracked, None

### Community 30 - "BillboardType"
Cohesion: 0.67
Nodes (3): BillboardType, Full, YAxisOnly

### Community 31 - "EnemyType"
Cohesion: 0.67
Nodes (3): EnemyType, Centinela, Patrullero

## Knowledge Gaps
- **95 isolated node(s):** `ClueFound`, `InterrogationEnded`, `EnemyKilled`, `FinalConfrontation`, `LowHealth` (+90 more)
  These have ≤1 connection - possible missing edges. (Counts symbols only; 195 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **13 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `EnemyAI` connect `EnemyAI` to `Vector3`, `unityengine`, `Collider`, `Material`, `DialogueNPC`, `BillboardSprite`, `EnemyHealth`, `ShotgunController`, `.HasFrames`, `RubberHoseAnimator`, `MonoBehaviour`, `EnemyBarks`, `EnemyState`, `EnemyType`?**
  _High betweenness centrality (0.105) - this node is a cross-community bridge._
- **What connects `ClueFound`, `InterrogationEnded`, `EnemyKilled` to the rest of the system?**
  _95 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Vector3` be split into smaller, more focused modules?**
  _Cohesion score 0.08768051871500147 - nodes in this community are weakly interconnected._
- **Why does `BillboardSprite` connect `BillboardSprite` to `Vector3`, `unityengine`, `Material`, `DialogueNPC`, `EnemyCombatFx`, `EnemyAI`, `EnemyHealth`, `ShotgunController`, `.HasFrames`, `ShotgunFeedback`, `MonoBehaviour`, `BillboardType`?**
  _High betweenness centrality (0.092) - this node is a cross-community bridge._
- **Should `unityengine` be split into smaller, more focused modules?**
  _Cohesion score 0.0676056338028169 - nodes in this community are weakly interconnected._
- **Why does `CartoonKit` connect `Vector3` to `unityengine`, `Material`, `CartoonFx`, `UIManager`, `WallFace`, `WindowDamage`?**
  _High betweenness centrality (0.074) - this node is a cross-community bridge._
- **Should `Material` be split into smaller, more focused modules?**
  _Cohesion score 0.06604324956165984 - nodes in this community are weakly interconnected._