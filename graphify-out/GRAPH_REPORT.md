# Graph Report - Scripts  (2026-10-30)

## Corpus Check
- 53 files · ~53,838 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 64 file(s) not represented in the graph (top: .meta 64)

## Summary
- 822 nodes · 2326 edges · 32 communities (24 shown, 8 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 222 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `8ae2818b`
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
- PlayerHealth
- MarkerType
- GameManager
- MonoBehaviour
- RubberHoseAnimator
- CluePickup
- EnemyBarks
- .Configure
- IEnumerator
- EnemyState
- WallFace
- WindowDamage
- BillboardType
- EnemyType

## God Nodes (most connected - your core abstractions)
1. `CartoonKit` - 79 edges
2. `BillboardSprite` - 50 edges
3. `EnemyAI` - 49 edges
4. `CartoonFx` - 35 edges
5. `ShotgunController` - 33 edges
6. `DialogueNPC` - 32 edges
7. `EnemyHealth` - 31 edges
8. `DialogueUI` - 29 edges
9. `LevelBuilder_NivelPorky` - 27 edges
10. `AICompanion` - 26 edges

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

## Communities (32 total, 8 thin omitted)

### Community 0 - "Vector3"
Cohesion: 0.08
Nodes (3): CartoonKit, LevelBuilder_NivelPorky, Particle

### Community 1 - "unityengine"
Cohesion: 0.07
Nodes (3): GameAudioImporter, CharacterSpriteImporter, EnvironmentModelImporter

### Community 2 - "DialogueUI"
Cohesion: 0.05
Nodes (7): DialogueUI, Exists, Instance, IsOpen, LightFlicker, FirstPersonViewmodel, ShotgunFeedback

### Community 3 - "Material"
Cohesion: 0.07
Nodes (10): CharacterLookBuilder, CharacterSpriteSetBuilder, EditorMenuGuard, LevelBuilderUtils, WallOpening, RainBuilder, TestPrefabGenerator, URPVerifier (+2 more)

### Community 4 - "DialogueNPC"
Cohesion: 0.08
Nodes (15): DialogueDatabase, DialogueNode, IsEnd, DialogueOption, DialogueTree, DialogueNPC, DisplayName, WasInterrogated (+7 more)

### Community 5 - "AudioLibrary"
Cohesion: 0.09
Nodes (10): APISettings, ApiKey, IsConfigured, AudioLibrary, Active, SfxEntry, VoiceEntry, AudioLibraryLoader (+2 more)

### Community 6 - "EnemyCombatFx"
Cohesion: 0.09
Nodes (7): EnemyCombatFx, CharacterLook, BlobMaterial, PlaceholderMaterial, SpriteMaterial, RubberHoseMotion, Target

### Community 7 - "CartoonFx"
Cohesion: 0.10
Nodes (11): CartoonFx, FlashMeshMaterial, FlashSpriteMaterial, Instance, Decal, Kind, Blood, None (+3 more)

### Community 8 - "UIManager"
Cohesion: 0.09
Nodes (5): KitMesh, Matrix, Submesh, UIManager, Instance

### Community 9 - "BillboardSprite"
Cohesion: 0.17
Nodes (4): BillboardSprite, HasSprites, CharacterSpriteSet, HasAnyFrames

### Community 10 - "AICompanion"
Cohesion: 0.11
Nodes (11): AICompanion, Instance, AITrigger, ClueFound, EnemyKilled, FinalConfrontation, InterrogationEnded, LowHealth (+3 more)

### Community 11 - "EnemyAI"
Cohesion: 0.16
Nodes (5): EnemyAI, ChaseSpeed, CurrentSpeed, CurrentState, IsSurrendered

### Community 14 - "GameStateTracker"
Cohesion: 0.12
Nodes (11): GameEnding, BlindJustice, PartialTruth, TruthAndForgiveness, GameStateTracker, AIContext, CluesFound, Instance (+3 more)

### Community 15 - "EnemyHealth"
Cohesion: 0.13
Nodes (6): EnemyHealth, CanSurrender, HealthPercentage, IsDead, IsSurrendered, NpcId

### Community 16 - "ShotgunController"
Cohesion: 0.24
Nodes (4): ShotgunController, CurrentAmmo, IsReloading, ReserveAmmo

### Community 17 - "PlayerHealth"
Cohesion: 0.22
Nodes (5): PlayerHealth, CurrentHealth, HealthPercentage, IsDead, MaxHealth

### Community 18 - "MarkerType"
Cohesion: 0.19
Nodes (10): LevelMarker, Id, Type, MarkerType, Clue, InterrogableGuard, LevelExit, PatrolWaypoint (+2 more)

### Community 19 - "GameManager"
Cohesion: 0.18
Nodes (4): GameManager, Instance, IsGameOver, IsGamePaused

### Community 20 - "MonoBehaviour"
Cohesion: 0.17
Nodes (4): AICompanionTester, BlobShadow, CloudDrift, DegreesPerSecond

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
- **92 isolated node(s):** `ClueFound`, `InterrogationEnded`, `EnemyKilled`, `FinalConfrontation`, `LowHealth` (+87 more)
  These have ≤1 connection - possible missing edges. (Counts symbols only; 188 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **8 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `EnemyAI` connect `EnemyAI` to `Vector3`, `unityengine`, `Material`, `DialogueNPC`, `EnemyCombatFx`, `BillboardSprite`, `EnemyHealth`, `MonoBehaviour`, `RubberHoseAnimator`, `EnemyBarks`, `EnemyState`, `EnemyType`?**
  _High betweenness centrality (0.110) - this node is a cross-community bridge._
- **What connects `ClueFound`, `InterrogationEnded`, `EnemyKilled` to the rest of the system?**
  _92 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Vector3` be split into smaller, more focused modules?**
  _Cohesion score 0.0847457627118644 - nodes in this community are weakly interconnected._
- **Why does `BillboardSprite` connect `BillboardSprite` to `Vector3`, `unityengine`, `DialogueUI`, `Material`, `DialogueNPC`, `EnemyCombatFx`, `EnemyAI`, `EnemyHealth`, `MonoBehaviour`, `.ApplyUnlitLook`, `BillboardType`?**
  _High betweenness centrality (0.095) - this node is a cross-community bridge._
- **Should `unityengine` be split into smaller, more focused modules?**
  _Cohesion score 0.06720321931589537 - nodes in this community are weakly interconnected._
- **Why does `CartoonKit` connect `Vector3` to `unityengine`, `Material`, `AudioLibrary`, `UIManager`, `WallFace`, `WindowDamage`?**
  _High betweenness centrality (0.077) - this node is a cross-community bridge._
- **Should `DialogueUI` be split into smaller, more focused modules?**
  _Cohesion score 0.05357142857142857 - nodes in this community are weakly interconnected._