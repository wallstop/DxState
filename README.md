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

1. **Create a stack driver**

    - In your scene, add an empty `GameObject` named `GameStack`.
    - Attach `MessagingComponent` (from DxMessaging) and `StateStackManager` (from DxState).
    - Optionally mark the object as `DontDestroyOnLoad` via your boot code if the stack needs to persist across scenes.

2. **Author game states**

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

3. **Register states at runtime**

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

4. **Respond to stack messages** (optional)

    - Subscribe to untargeted DxMessaging messages such as `StatePushedMessage`, `TransitionStartMessage`, or `TransitionProgressChangedMessage` to keep UI and analytics in sync.
    - Messages are declared under `Runtime/State/Stack/Messages` and decorated with `[DxUntargetedMessage]`, so any listener can register without specifying a receiver.

## Runtime Architecture

- **State Stack** (`StateStack`, `StateStackManager`)
  - Maintains a history of `IState` instances.
  - Guarantees that only one transition runs at a time, raising lifecycle events (`OnStatePushed`, `OnTransitionStart`, etc.) and exposing live progress values.
  - Supports stack manipulation: `PushAsync`, `PopAsync`, `TryPopAsync`, `FlattenAsync`, `RemoveAsync`, `ClearAsync`, and transition waiting (`WaitForTransitionCompletionAsync`).

- **Reusable States**
  - `GameState`: MonoBehaviour-based state with Unity serialization, time tracking, and message awareness.
  - `SceneState`: Orchestrates additive scene loads/unloads, with automatic reverts when popped or removed.
  - `TimeState`: Temporarily overrides `Time.timeScale`, restoring the previous value when removed or reversed.
  - `StateGroup`: Aggregates multiple `IState` instances in sequential or parallel mode and forwards progress to child states.

- **Component State Machine**
  - `StateMachine<T>` runs purely in-memory graphs using `Transition<T>` definitions and optional `IStateContext<T>` hooks.
  - `StateComponent` and `ComponentStateTransition` bring MonoBehaviour-driven transitions that cooperate with DxMessaging.

- **Messaging Integration**
  - `DxMessageAwareSingleton<T>` and `SerializedMessageAwareComponent` bootstrap messaging registrations and Odin serialization.
  - Stack events emit untargeted messages (`StatePushedMessage`, `TransitionStartMessage`, `StateStackFlattenedMessage`, etc.) so gameplay systems can observe transitions without tight coupling.

## Provided Building Blocks

| Type | Location | Purpose |
| ---- | -------- | ------- |
| `StateStackManager` | `Runtime/State/Stack/Components` | Singleton MonoBehaviour wrapping `StateStack`, wiring DxMessaging notifications.
| `StateStack` | `Runtime/State/Stack` | Core stack engine with async transitions, progress reporting, and background ticking support.
| `GameState` | `Runtime/State/Stack` | Base MonoBehaviour for authoring Unity-friendly states.
| `SceneState` | `Runtime/State/Stack/States` | Declarative scene load/unload state with revert-on-exit semantics.
| `TimeState` | `Runtime/State/Stack/States` | Time scaling utility state.
| `StateGroup` | `Runtime/State/Stack/States` | Sequential/parallel composition of multiple `IState` instances.
| `StateMachine<T>` | `Runtime/State/Machine` | Lightweight transition graph for non-stack state logic.

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
