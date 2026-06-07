## SentryNode: Stealth Game - AI for Games course project

This repository contains a Unity stealth-game prototype focused on modular guard AI. The project demonstrates a first-person player moving through an indoor level while AI guards patrol, notice the player through vision, react to sound, chase when fully alerted, investigate last-known positions, search locally, and eventually return to patrol.

The code is structured as a small AI architecture exercise rather than a content-heavy game. Most of the interesting work lives in `Assets/Scripts`: perception systems, a behavior tree, guard behavior nodes, adapter interfaces, debug visualization, sound feedback, and an editor tool that can rebuild a complete demo level.

## Project Info

- Unity version: `2022.3.62f1`
- Main engine systems used: built-in input, physics, audio, NavMesh AI, editor scripting
- Main scene files:
  - `Assets/level1.unity`
  - `Assets/level1_working.unity`
  - `Assets/level-improvements.unity`
- Generated/working content:
  - `Assets/level*/NavMesh.asset`
  - `Assets/Audio/*`

## What This Prototype Shows

The prototype is built around a typical stealth loop:

1. The player explores the level in first person.
2. Guards wander through patrol zones using NavMesh destinations.
3. Vision gradually increases suspicion when the player enters a guard's field of view.
4. Footsteps emit noise events whose radius depends on movement style.
5. Guards investigate suspicious sights and heard sounds.
6. When detection becomes certain, guards chase the player.
7. If contact is lost, guards move to the last known position and search the area.
8. After the search completes or times out, guards return to patrol.

This makes the project useful for demonstrating behavior trees, perception-driven state changes, Unity NavMesh movement, and data-driven tuning through serialized fields.

## Controls

Default player controls are implemented in `PlayerController`.

| Action | Input |
| --- | --- |
| Move | `W`, `A`, `S`, `D` |
| Look | Mouse |
| Sprint | `Left Shift` |
| Crouch | `C` |

Movement also affects stealth:

- Walking creates moderate footstep noise.
- Sprinting is faster but much louder.
- Crouching is slower and can be configured to create no hearing noise.

## Running the Project

1. Open the repository folder in Unity Hub.
2. Use Unity `2022.3.62f1` or a compatible Unity 2022 LTS version.
3. Open one of the scenes in `Assets`.
4. Press Play.

If the level needs to be regenerated, use the editor menu:

```text
Tools > Build Demo Level
```

That command is defined in `Assets/Editor/LevelBuilder.cs`. It rebuilds the indoor demo layout, creates the player and guards, configures layers/tags, assigns AI components, applies audio settings, and bakes the NavMesh.

## Repository Layout

```text
Assets/
  Audio/                 Audio clips for footsteps and guard state cues
  Editor/
    LevelBuilder.cs      Unity editor tool for generating the demo level
  Scripts/
    BehaviorTree/        Minimal behavior tree primitives
    GuardAI/             Guard behavior tree factory, nodes, adapters, context, interfaces
    GuardAI.cs           Main MonoBehaviour that owns guard decision-making
    PlayerController.cs  First-person movement, crouch/sprint, and player noise
    VisionSystem.cs      Field-of-view, line-of-sight, suspicion, and last-known position
    HearingSystem.cs     Static noise event bus and per-guard hearing memory
    PatrolSystem.cs      Random NavMesh patrol movement
    SearchSystem.cs      Local search-point generation around last-known positions
    GuardAlertSystem.cs  Shared alert memory between guards
    GuardSoundSystem.cs  Audio feedback for guard state changes and movement
    GuardVisionRenderer.cs
    GuardDebugVisualizer.cs
Packages/
  manifest.json          Unity package manifest
ProjectSettings/         Unity project and editor settings
```

## AI Architecture

The guard AI is coordinated by `GuardAI`, but most behavior is split into small services and tree nodes.

### Behavior Tree

The behavior tree primitives live in `Assets/Scripts/BehaviorTree`:

- `Node` defines the common tree API and node state.
- `Selector` evaluates children by priority until one succeeds or keeps running.
- `Sequence` evaluates ordered steps until one fails or keeps running.
- `ConditionNode` wraps boolean-style checks.
- `ActionNode` wraps behavior execution.

`GuardBehaviorTreeFactory` builds the guard's root selector from ordered branch providers. The default branch priority is:

1. Chase visible detected player.
2. Turn toward suspicious visual focus.
3. Investigate last known player position and search nearby.
4. Investigate heard noise.
5. Patrol as the fallback behavior.

Because branches are provided through `IGuardBehaviorBranchProvider`, extra behaviors can be inserted without rewriting `GuardAI`.

### Runtime Context and Adapters

`GuardRuntimeContext` is the shared data object passed into behavior nodes. It tracks current state, active leaf node, last-known position, search timing, noise source, and service references.

The interfaces in `IGuardAiAbstractions.cs` keep behavior nodes independent from concrete Unity components:

- `IGuardVisionService`
- `IGuardHearingService`
- `IGuardPatrolService`
- `IGuardSearchService`
- `IGuardAlertService`
- `IGuardNavigationService`
- `IPlayerLocator`
- `IGuardBehaviorBranchProvider`

`GuardAiAdapters.cs` connects those interfaces to Unity components like `VisionSystem`, `HearingSystem`, `PatrolSystem`, `SearchSystem`, and `NavMeshAgent`.

## Guard States

`GuardAI.GuardState` exposes the high-level runtime state:

| State | Meaning |
| --- | --- |
| `Patrolling` | Normal fallback movement through random NavMesh patrol points |
| `Suspicious` | The guard has partial visual awareness and turns toward the focus |
| `Chasing` | The player is fully detected and the guard follows directly |
| `Investigating` | The guard moves toward a last-known player/noise position |
| `Searching` | The guard samples nearby NavMesh points around the last-known position |

The current state and active leaf node are serialized for debugging in the Unity Inspector.

## Perception Systems

### Vision

`VisionSystem` performs field-of-view and line-of-sight checks. It tracks suspicion from `0` to `100` and maps that value to:

- `Unaware`
- `Suspicious`
- `Detected`

Suspicion rises faster when the player is close or clearly visible, can rise through proximity awareness, and decays when the player is no longer a meaningful target. When detection is strong enough, the system records the player's last known position.

### Hearing

`HearingSystem` acts as a simple global noise broadcaster. Player movement calls:

```csharp
HearingSystem.ReportNoise(position, radius, HearingSystem.NoiseType.Footstep);
```

Every active hearing system checks whether it is inside the noise radius. If it is, the guard remembers the noise position for a configurable duration and the behavior tree can route the guard into investigation.

## Movement and Search

`PatrolSystem` selects random reachable NavMesh points around a patrol origin, waits briefly at destinations, and recovers from stalls or off-NavMesh placement.

`SearchSystem` generates a small set of NavMesh points around a last-known position. During the `Searching` state, the guard visits those points in sequence until all are checked or the search timer reaches its limit.

## Debugging and Feedback

Several scripts exist to make the AI readable while testing:

- `GuardVisionRenderer` draws a runtime vision cone.
- `GuardDebugVisualizer` shows guard state and active behavior information.
- `VisionSystem.OnDrawGizmos` draws field-of-view and raycast direction lines.
- `HearingSystem.OnDrawGizmos` draws heard-noise radius.
- `SearchSystem.OnDrawGizmos` draws generated search points.
- `GuardSoundSystem` plays state-entry cues and movement loops for patrol, chase, and search.

These systems are intentionally visible and inspectable because the project is about understanding AI behavior, not hiding it behind production polish.

## Extending the Guard AI

To add a new guard behavior:

1. Create one or more `GuardConditionNode` or `GuardActionNode` classes.
2. Implement `IGuardBehaviorBranchProvider`.
3. Return a `Sequence`, `Selector`, or custom `Node` from `CreateBranch`.
4. Choose an `Order` value that places the branch at the right priority.
5. Add the provider as a component implementing `IGuardBehaviorBranchProvider` and assign it to `GuardAI.branchProviderBehaviours`, or add it to `DefaultGuardBehaviorBranches.Create()`.

The branch-provider design keeps the root behavior tree open for extension while preserving the default stealth loop.

## Notes for Reviewers

- This is a Unity prototype, so `Library/`, `Logs/`, and other generated local folders should not be treated as source.
- The project relies on Unity's built-in NavMesh pipeline. If guard movement behaves oddly after changing the level, rebuild the NavMesh.
- The editor level builder is designed to create a reproducible test scene quickly. Manual scene edits may be overwritten when running `Tools > Build Demo Level`.
- Audio clips under `Assets/Audio` are referenced by name when missing assignments need to be restored.

## Current Status

The repo currently represents a playable stealth AI demo with:

- First-person movement.
- Guard patrol, suspicion, chase, investigation, and search.
- Vision and hearing perception.
- Shared alert reporting.
- Debug visuals and audio state feedback.
- A reproducible editor-generated level.

It is best understood as an AI systems prototype and coursework-style demonstration of stealth behavior architecture in Unity.
