# DxState

DxState is Wallstop Studios' state management package for Unity 2021.3, combining a lightweight state stack with message-aware MonoBehaviours and reusable gameplay states. The library is designed to be easy to drop into an existing project, straightforward to extend, and fast enough for production workloads.

> **Status**: Alpha. Public API surface may change; please pin explicit versions when shipping.

## Installation

1. Open your consuming project's `Packages/manifest.json` and add the Git package reference:

    ```json
    {
        "dependencies": {
            "com.wallstop-studios.dxstate": "https://github.com/wallstop/DxState.git#main"
        }
    }
    ```

2. Ensure DxState's dependencies resolve by also referencing the matching utility packages (versions should stay aligned with the ones declared in this repository's `package.json`):

    ```json
    {
        "dependencies": {
            "com.wallstop-studios.unity-helpers": "2.0.0",
            "com.wallstop-studios.dxmessaging": "2.0.0"
        }
    }
    ```

3. Regenerate the project files so Unity pulls the packages. The `WallstopStudios.DxState` assembly definition will appear under `Packages/com.wallstop-studios.dxstate/Runtime`.

## Quickstart

1. **Add a stack manager in the scene**

    - Create an empty `GameObject` (for example, `GameStack`) and add `StateStackManager`.
    - The manager automatically ensures a `MessagingComponent` is present and exposes a **Beginner Setup** foldout so you can register child `GameState` components, drag additional states into a list, and choose an initial state without writing code.
    - Enable *Diagnostics HUD Preset* to attach the built-in overlay (toggle key defaults to `F9`) so newcomers see the active stack, progress, and metrics immediately.
    - Enable *Push Initial State On Start* if you want the configured initial state to become active automatically when play mode begins.

2. **Expose a beginner-friendly API (optional)**

    - Add `StateStackFacade` to the same object to mirror the stack through inspector-friendly `UnityEvent<GameState>` hooks.
    - Wire HUD buttons to `PushState`, `PopState`, or `ReplaceState` methods and listen for the provided events (`On State Pushed`, `On State Popped`, `On State Changed`) to update UI without touching `ValueTask` workflows.
    - Scaffold new states quickly via **Assets ▸ Create ▸ Wallstop Studios ▸ DxState ▸ Game State**, which generates a correctly wired `GameState` subclass.

3. **Author game states**

    ```csharp
    using System;
    using System.Threading.Tasks;
    using WallstopStudios.DxState.State.Stack;

    public sealed class MainMenuState : GameState
    {
        public override string Name => "MainMenu";

        public override async ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            await base.Enter(previousState, progress, direction);
            // Show UI, play audio, etc.
        }

        public override ValueTask Exit<TProgress>(
            IState nextState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            // Hide UI, clean up.
            return base.Exit(nextState, progress, direction);
        }
    }
    ```

    - Derive from `GameState` for MonoBehaviour-based gameplay flows.
    - Override `TickMode` if the state needs `Update`, `FixedUpdate`, or `LateUpdate` callbacks.

4. **Register states at runtime (advanced)**

    ```csharp
    using System.Threading.Tasks;
    using UnityEngine;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Components;

    public sealed class GameBootstrapper : MonoBehaviour
    {
        [SerializeField]
        private StateStackManager _stackManager;

        [SerializeField]
        private MainMenuState _mainMenu;

        [SerializeField]
        private GameplayState _gameplay;

        private async void Start()
        {
            _stackManager.TryRegister(_mainMenu, force: true);
            _stackManager.TryRegister(_gameplay, force: true);

            await _stackManager.PushAsync(_mainMenu);
        }
    }
    ```

    - The manager mirrors all `StateStack` APIs (`PushAsync`, `PopAsync`, `FlattenAsync`, `RemoveAsync`, etc.) and emits DxMessaging events automatically.

5. **(Optional) load stack configurations from assets**

    - Assign a `StateGraphAsset` to `StateStackManager` and enable *Apply State Graph On Start* to ingest designer-authored stacks without code.
    - Set *State Graph Stack Name* to choose which stack from the asset applies at runtime. Leave blank to take the first defined stack.
    - Toggle *Force Register Graph States* / *Ensure Graph Initial Active* to control registration behaviour and whether the asset’s initial selection is activated immediately.

6. **(Optional) build stack configurations fluently**

    For projects that compose stacks at runtime, use `StateStackBuilder` to register states and define the initial state declaratively:

    ```csharp
    using System.Threading.Tasks;
    using UnityEngine;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Builder;

    public sealed class BuilderBootstrap : MonoBehaviour
    {
        [SerializeField]
        private StateStackManager _stackManager;

        [SerializeField]
        private MainMenuState _mainMenu;

        [SerializeField]
        private GameplayState _gameplay;

        private async void Awake()
        {
            StateStackConfiguration configuration = new StateStackBuilder()
                .WithInitialState(_mainMenu)
                .WithState(_gameplay)
                .Build();

            await configuration.ApplyAsync(_stackManager.Stack, forceRegister: true);
        }
    }
    ```

    `ApplyAsync` registers every state with the stack (respecting `forceRegister`) and ensures the configured initial state is active by pushing or flattening as needed.

7. **Compose multi-stack graphs** (optional)

    When multiple stacks need to be bootstrapped together, use `StateGraphBuilder` to declare each stack, its states, and initial selection in a single fluent block:

    ```csharp
    StateGraph graph = new StateGraphBuilder()
        .Stack("Menu", stack => stack
            .Scene("MainMenu", SceneTransitionMode.Addition, setAsInitial: true)
        )
        .Stack("Gameplay", stack => stack
            .State(worldBootstrap, setAsInitial: true)
            .Group(
                "Loading",
                new IState[] { warmupAddressables, shaderPreload },
                StateGroupMode.Parallel
            )
        )
        .Build();

    if (graph.TryGetStack("Gameplay", out StateStackConfiguration configuration))
    {
        await configuration.ApplyAsync(_stackManager.Stack, forceRegister: true);
    }
    ```

    A `StateGraph` simply maps stack names to `StateStackConfiguration` instances, making it easy to apply the right registration set on demand.

6. **Respond to stack messages** (optional)

    - Subscribe to untargeted DxMessaging messages such as `StatePushedMessage`, `TransitionStartMessage`, or `TransitionProgressChangedMessage` to keep UI and analytics in sync.
    - Messages are declared under `Runtime/State/Stack/Messages` and decorated with `[DxUntargetedMessage]`, so any listener can register without specifying a receiver.

## Choosing the Right Surface

- **Scenario states & factories** (`Runtime/State/Stack/States/Scenarios`): Use the built-in states for cutscenes, dialogue, tutorials, networking, waiting, and scene orchestration when you need drop-in behaviour.
- **`StateGraphBuilder` / `StateStackBuilder`** (`Runtime/State/Stack/Builder`): Compose stacks programmatically—`StateGraphBuilder` handles multi-stack graphs, while `StateStackBuilder` configures a single stack.
- **Trigger/Component state machines** (`Runtime/State/Machine`): For localized logic, rely on `StateMachine<T>` or `TriggerStateMachine<TState, TTrigger>` and attach `StateMachineDiagnostics<T>` when you need transition history, per-state enter/exit metrics, and cause breakdowns in tooling.

## Diagnostics

- `StateStackManager.Diagnostics` exposes a rolling history of transitions and the latest progress values for each active state. Use it to surface history in custom tooling or logs.
- `StateMachineDiagnostics<T>` now captures recent executed/deferred transitions, aggregates cause counts, and tracks per-state enter/exit moments so editor tooling can render lightweight insights without instrumenting production code.
- Open **Window ▸ Wallstop Studios ▸ DxState ▸ State Machine Diagnostics** to inspect registered machines, transition causes, per-state metrics, and recent events; adjust defaults under **Project Settings ▸ Wallstop Studios ▸ DxState ▸ Diagnostics**.
- In the Editor, the custom `StateStackManager` inspector surfaces the live stack, registered states (with push/flatten controls), diagnostics, and a one-click clear button while in play mode.
- Subscribe to `StateGraphViewWindow.GraphAssetChanged` when building editor extensions or CI hooks that react to authoring edits.
- Enable the `DXSTATE_PROFILING` scripting define and toggle `StateStack.ProfilingEnabled` / `StateMachine<T>.ProfilingEnabled` to wrap transitions and updates with Unity profiler markers when you need deep instrumentation.

## Further Reading

- Review [State Authoring Best Practices](Documentation/StateAuthoring.md) for guidance on choosing between ScriptableObject and MonoBehaviour states, meeting lifecycle contracts, and wiring dependencies safely.
- Follow the [Building Hierarchical Graphs](Documentation/Guides/BuildingHierarchicalGraphs.md) walkthrough to author complex stacks in the State Graph view.
- Use the [Live Diagnostics Walkthrough](Documentation/Guides/LiveDiagnosticsWalkthrough.md) to configure overlays, snapshots, and automation hooks for runtime monitoring.
- Start with the [State Stack Bootstrap Sample Walkthrough](Documentation/Guides/StateStackBootstrap.md) to drop a ready-to-use stack prefab into any scene.
- Explore the [Hierarchical State Graph Sample Walkthrough](Documentation/Guides/HFSMSampleWalkthrough.md) to inspect the included multi-stack scene and graph asset.
- Drive Animator flows with the [Animator Parameter Adapter](Documentation/Guides/AnimatorParameterAdapter.md).
- Open **Window ▸ Wallstop Studios ▸ DxState ▸ State Stack Debugger** to monitor stacks in a dedicated editor window, push new states by name, and inspect diagnostics without selecting the manager object.
- Drop the `StateStackDiagnosticsOverlay` MonoBehaviour on the same object as `StateStackManager` (included in the sample prefab) to toggle an in-game overlay that lists the active stack and recent events (default hotkey: `F9`).
  Use the toolbar buttons to cycle between floating/docked presets or lock the overlay to avoid accidental drags, pause/step through diagnostics snapshots, pin states you care about, switch to the timeline tab to visualize recent transitions as a sparkline, and apply event-type filters to focus on actionable entries.
- Utility helpers such as `AwaitWithProgress` now support cancellation tokens while driving progress updates via a lightweight, pooled driver.
- Snapshot and restore stacks in automation by calling `StateStackSnapshot.Capture(stack)` and `snapshot.RestoreAsync(stack)`; pair with `StateMachine<T>.TransitionHistory` when you need deterministic replays.

## State Machine Authoring

- Reach for `StateMachineBuilder<TState>` whenever you want to materialise transition graphs without manually building lists. The builder preserves unique transitions, honours the validation inside `StateMachine<T>`, and keeps the graph readable for tooling.

    ```csharp
    StateMachineBuilder<IStateComponent> builder = new StateMachineBuilder<IStateComponent>();
    ComponentStateTransition toActive = new ComponentStateTransition(idleComponent, activeComponent);

    builder.AddTransition(toActive)
           .AddTransition(activeComponent, idleComponent, () => shouldReturnToIdle);

    StateMachine<IStateComponent> machine = builder.Build(idleComponent);
    machine.Update();
    ```

- `ComponentStateTransition` composes rule delegates with `IStateComponent.ShouldEnter`, so tag-gated components or custom predicates can block transitions even if the machine rule allows entry.
- Prefer encapsulating component states behind `IStateComponent` implementations—MonoBehaviour subclasses inherit the helper base `StateComponent`, while plain C# states can implement the interface directly for tests and headless contexts.
- `StateMachine<T>` now exposes `TransitionHistory`, `ActiveRegions`, and helper methods such as `CopyTransitionHistory` and `TryGetActiveHierarchicalState`, making it easy to build diagnostics overlays or replay tests without instrumenting the machine.
- Designers can build the same graphs visually with `StateGraphAsset` (Assets ▸ Create ▸ Wallstop Studios ▸ DxState ▸ State Graph). Each stack definition records a name, ordered `IState` references, and which entry is active by default. Call `StateGraphAsset.BuildGraph()` at runtime to obtain `StateStackConfiguration` instances and apply them to your stacks. The State Graph view now supports multiple labeled transitions between nodes with editable causes/flags and tooltips.
  The State Graph editor window exposes the same transition metadata, allowing you to author labels, tooltips, and transition causes directly alongside the state list.
    - The State Graph view now supports multiple labelled transitions per pair of states; select an edge to edit its label, tooltip, cause, and flags, or drag between nodes to create new metadata-backed transitions.
    - Selecting a state now surfaces connected Animator Controllers and Timeline assets directly in the inspector sidebar for quick cross-referencing.
- The edit-mode suites `StateMachineBuilderTests`, `StateComponentTests`, and `StateStackDiagnosticsTests` cover builder usage, component gating, and deferred queue reporting.

## Time and Timescale

- `TimeState` captures the previous `Time.timeScale` on enter and restores it on every exit (forward or backward) as well as nested removal paths, ensuring gameplay returns to its prior pacing even when states are removed out of order.
- Stack multiple `TimeState` instances to create layered slow-motion effects—downstream states inherit the previous scale so rewinding the stack automatically returns to baseline.
- Tests in `TimeStateTests` and `StateStackOperationsTests` exercise common flows; mirror their expectations whenever you extend or subclass time-affecting states.

## Memory Pooling

- `WallstopArrayPool<T>` provides cleared buffers when you need to avoid leaking references during editor tooling or long-lived gameplay systems.
- `WallstopFastArrayPool<T>` skips zeroing for hot paths like `StateGroup` parallel scheduling, pairing with manual resets when you own the lifecycle.
- `PooledArray<T>` wraps the pools in a disposable scope so temporary buffers (progress aggregators, transition scratchpads) automatically return to the pool.
- Prefer pooling for per-frame operations—composing stack transitions, snapshotting tag sets, or batching async awaits—to keep allocations out of play mode builds.
- Unit tests under `Tests/EditMode/Pooling` and the expanded `StateGroupTests` demonstrate how to validate pooling behaviour without peeking into implementation details.

## Messaging Surface

`StateStackManager` mirrors key stack lifecycle moments via DxMessaging. These untargeted messages allow ancillary systems (HUD, analytics, audio) to stay informed without tight coupling:

| Message | When Raised | Payload |
| --- | --- | --- |
| `TransitionStartMessage` | Immediately before exiting the current state | `PreviousState`, `NextState` |
| `TransitionCompleteMessage` | After the new state finishes entering | `PreviousState`, `CurrentState` |
| `TransitionProgressChangedMessage` | Whenever transition progress updates | `State`, `Progress (0-1)` |
| `StatePushedMessage` | After a state is pushed | `PreviousState`, `CurrentState` |
| `StatePoppedMessage` | After pop completes | `RemovedState`, `CurrentState` |
| `StateManuallyRemovedMessage` | After `RemoveAsync` succeeds for a non-top state | `State` |
| `StateStackFlattenedMessage` | After `FlattenAsync` completes | `TargetState` |

Use `MessagingComponent` helpers to subscribe to these events from any GameObject.

## Runtime Architecture

- **State Stack** (`StateStack`, `StateStackManager`)
  - Maintains a history of `IState` instances.
  - Guarantees that only one transition runs at a time, raising lifecycle events (`OnStatePushed`, `OnTransitionStart`, etc.) and exposing live progress values.
  - Supports stack manipulation: `PushAsync`, `PopAsync`, `TryPopAsync`, `FlattenAsync`, `RemoveAsync`, `ClearAsync`, and transition waiting (`WaitForTransitionCompletionAsync`).

- **Reusable States**
  - `GameState`: MonoBehaviour-based state with Unity serialization, time tracking, and message awareness.
  - `SceneState`: Orchestrates additive scene loads/unloads, with automatic reverts when popped or removed.
  - `TimeState`: Temporarily overrides `Time.timeScale`, restoring the previous value when removed or reversed.
  - `InputModeState`: Enables and disables input action maps (requires the new Input System).
  - `AudioSnapshotState`: Transitions to a Unity `AudioMixerSnapshot` over a configurable duration.
  - `TimeScaleState`: Applies time-scale overrides with optional automatic revert on exit.
  - `StateGroup`: Aggregates multiple `IState` instances in sequential or parallel mode and forwards progress to child states.

- **Component & Trigger State Machines**
  - `StateMachine<T>` runs purely in-memory graphs using `Transition<T>` definitions and optional `IStateContext<T>` hooks.
  - `StateComponent` and `ComponentStateTransition` bring MonoBehaviour-driven transitions that cooperate with DxMessaging.
  - `TriggerStateMachine<TState, TTrigger>` lets states decide when to transition by emitting triggers, avoiding manual `SetState` calls while still queuing transitions safely.
  - `TransitionTriggerState<TState>` adapts existing `Transition<TState>` rule sets for trigger-driven execution.

- **Messaging Integration**
  - `DxMessageAwareSingleton<T>` and `SerializedMessageAwareComponent` bootstrap messaging registrations and Odin serialization.
  - Stack events emit untargeted messages (`StatePushedMessage`, `TransitionStartMessage`, `StateStackFlattenedMessage`, etc.) so gameplay systems can observe transitions without tight coupling.

## Provided Building Blocks

| Type | Location | Purpose |
| ---- | -------- | ------- |
| `StateStackManager` | `Runtime/State/Stack/Components` | Singleton MonoBehaviour wrapping `StateStack`, wiring DxMessaging notifications.
| `StateStack` | `Runtime/State/Stack` | Core stack engine with async transitions, progress reporting, and background ticking support.
| `StateStackBuilder` | `Runtime/State/Stack/Builder` | Fluent API for registering states and ensuring an initial stack configuration.
| `StateStackConfiguration` | `Runtime/State/Stack/Builder` | Immutable description of stack registration that can be applied at runtime.
| `GameState` | `Runtime/State/Stack` | Base MonoBehaviour for authoring Unity-friendly states.
| `SceneState` / `SceneStateFactory` | `Runtime/State/Stack/States` | Declarative scene load/unload helpers and scene swap presets.
| `WaitForSecondsState`, `ConditionState` | `Runtime/State/Stack/States` | Lightweight utility states that block until timers or arbitrary predicates complete.
| `TimeState`, `TimeScaleState` | `Runtime/State/Stack/States` and `States/Systems` | Time scaling utilities for gameplay and system-level adjustments.
| `InputModeState` | `Runtime/State/Stack/States/Systems` | Activates input action maps while the state is active.
| `AudioSnapshotState` | `Runtime/State/Stack/States/Systems` | Crossfades to an audio mixer snapshot during the state's lifetime.
| `PhysicsIsolationState` | `Runtime/State/Stack/States/Systems` | Toggles collision relationships between specified physics layers.
| `StateGroup` | `Runtime/State/Stack/States` | Sequential/parallel composition of multiple `IState` instances.
| `TimelineCutsceneState` | `Runtime/State/Stack/States/Scenarios` | Runs a timeline-driven cutscene with optional skip behaviour.
| `DialogueState` | `Runtime/State/Stack/States/Scenarios` | Bridges into dialogue systems with optional skip handling.
| `TutorialStepState` | `Runtime/State/Stack/States/Scenarios` | Polls completion predicates for tutorial steps while reporting custom progress.
| `NetworkConnectState` / `NetworkDisconnectState` | `Runtime/State/Stack/States/Scenarios` | Coordinates network connect/disconnect attempts using user-provided connectors with optional timeouts.
| `TriggerStateMachine<TState, TTrigger>` | `Runtime/State/Machine/Trigger` | Trigger-driven transition scheduler for state machines.
| `StateMachine<T>` | `Runtime/State/Machine` | Lightweight transition graph for non-stack state logic.
| `StateMachineDiagnostics<T>` | `Runtime/State/Stack/Diagnostics` | Captures recent state machine transitions for tooling and logging.

## Testing

Run tests headlessly from your CI agent or terminal (replace `<path>` with the Unity project consuming this package):

- **Edit mode**

    ```bash
    Unity -batchmode -quit -projectPath <path> -runTests -testPlatform editmode -testResults ./Artifacts/editmode.xml
    ```

- **Play mode**

    ```bash
    Unity -batchmode -quit -projectPath <path> -runTests -testPlatform playmode -testResults ./Artifacts/playmode.xml
    ```

Attach the generated XML to pull requests so reviewers can verify coverage.

## Contributing

- Format changes with [CSharpier](https://csharpier.com/) prior to sending a pull request (editor integration or pre-commit hooks are recommended).
- Follow the repository guidelines in `AGENTS.md` for naming, layout, and testing expectations.
- Highlight assembly definition changes and state-machine API tweaks in your PR description to help reviewers focus on the most impactful areas.
