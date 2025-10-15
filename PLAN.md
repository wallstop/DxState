# DxState Improvement Plan

## Observation Summary
- **Usability**: Authoring states requires verbose manual setup (manual `Transition<T>` construction, hand-wired `SceneState` instances, explicit registration in `StateStackManager`), which raises the barrier to quick prototyping and increases the risk of inconsistent configuration across scenes.
- **Correctness**: `StateMachine<T>` allows re-entrant transitions during `Enter`/`Exit`, `SceneState` does not protect against double-load/unload paths, and `StateStack` lacks explicit failure handling for async state entry/exit; these gaps can yield corrupted stacks or duplicate scene loads under stress.
- **Feature Coverage**: The runtime mainly covers pushdown stacks and boolean-rule state machines; there are no out-of-the-box helpers for common Unity flows (scene swapping sets, addressable loads, timed waits, timeline gating), nor an interface-driven machine where states decide transitions based on richer triggers.
- **Performance**: Transition queuing uses `TaskCompletionSource` and `Task.WhenAll`, `StateGroup` allocates per transition, and rule delegates capture closures; there is room to cut GC pressure by adopting pooled value-task sources and struct-based predicates for hot paths.
- **Ease-of-Understanding**: The mental model for when to use the stack versus the component state machine is under-documented; diagnostics exist but are not surfaced in samples, and there is no end-to-end narrative showing onboarding, scene orchestration, and advanced usage.
- **Robustness**: Tests cover only the async progress helper; there are no edit-mode fixtures safeguarding state machine invariants, queue ordering, or scene state re-entrancy, leaving regressions likely when extending the system.

- [x] Harden `StateMachine<T>` transition execution
  - `_transitionDepth` guard and deferred transition event wired via `TransitionDeferred` to surface queued re-entrancy (`Runtime/State/Machine/StateMachine.cs`).
  - Pending transitions now capture immutable descriptors, materialising `TransitionContext` before state swaps.
  - Updated `StateMachineTests` to assert deferred logging semantics and lifecycle ordering (`Tests/EditMode/State/Machine/StateMachineTests.cs`).
- [x] Seal scene loading race conditions
  - Added forward reference counting and guarded release path to prevent duplicate additive loads/unloads under rapid stack churn (`Runtime/State/Stack/States/SceneState.cs`).
  - Early exit now decrements references safely when other `SceneState` instances remain on the stack and when `RevertOnRemoval` is disabled.
  - Expanded edit-mode coverage with multi-reference removal scenarios (`Tests/EditMode/State/Stack/SceneStateTests.cs`).
- [x] Strengthen `StateStack` transition pipeline
  - Wrapped `Enter`/`Exit`/`Remove` invocations with `ExecuteStateOperationAsync`, surfacing `StateTransitionException` diagnostics that capture failing phase and state (`Runtime/State/Stack/StateStack.cs`).
  - Added `StateTransitionPhase` metadata and ensured rollback paths utilise the same guard rails.
  - Updated edit-mode coverage (`Tests/EditMode/State/Stack/StateStackOperationsTests.cs`) to assert fault-handling semantics and persisted zero-progress protections via stack-level reporting.

## Priority 1 — Expressive Authoring & New Machine Model
- [x] Introduce declarative trigger-driven state machine (`TriggerStateMachine`)
  - Implementation available in `Runtime/State/Machine/Trigger/` with coverage in `Tests/EditMode/State/Machine/TriggerStateMachineTests.cs`.
- [~] Build state factory helpers for scenes and common Unity flows
  - [x] Added `AsyncOperationState`, `TimelineState`, and Addressables helpers (`Runtime/State/Stack/States/AsyncOperationState.cs`, `/Addressables/`).
  - [x] Published scenario pack states: `BootstrapState`, `LoadingScreenState`, `WarmupAddressablesState`, `ShaderPreloadState`, `ExclusiveSceneSetState`, `ChunkStreamingState`, `SceneGroupStateFactory`, `UiScreenState`, `ModalStateStack`, `VirtualCameraState`, `GameplayLoopState`.
  - [ ] Remaining planned states (e.g. `TimelineCutsceneState`, `DialogueState`, `TutorialStepState`, networking helpers) to be evaluated and implemented.
  - [x] Tests mirror new runtime surface under `Tests/EditMode/State/Stack/` (e.g. `AsyncOperationStateTests.cs`, `AddressableAssetStateTests.cs`, `ExclusiveSceneSetStateTests.cs`).
- [~] Authoring ergonomics & configuration DSL
  - [x] Provide a fluent builder (`StateGraphBuilder`) to declare stacks/groups/scenes in code, producing reusable stack configurations (`Runtime/State/Stack/Builder/StateGraphBuilder.cs`, `Tests/EditMode/State/Stack/StateGraphBuilderTests.cs`).
  - [ ] Add editor tooling hooks (custom inspectors) to compose groups and preview execution order, relying on existing assembly definitions.
  - [ ] Document interplay between factories, builders, and the new trigger machine to guide selection per use case.

## Priority 2 — Performance & Allocation Optimisation
- [x] Replace `TaskCompletionSource`-backed queues with pooled value-task sources
  - `Runtime/State/Stack/Internal/TransitionCompletionSource.cs` now provides a custom pooled awaitable with no dependency on `ManualResetValueTaskSourceCore<T>` collisions.
  - `StateStack` transition queuing updated to consume the pooled source (`Runtime/State/Stack/StateStack.cs`).
- [x] Optimise `StateGroup` parallel execution path
  - Removed `Task.WhenAll` and per-transition task allocations in favour of pooled `ValueTask` coordination (`Runtime/State/Stack/States/StateGroup.cs`).
  - `ParallelProgressAggregator` still drives shared progress while `ArrayPool<ValueTask>` backs the temporary buffers.
- [~] Reduce delegate and LINQ overhead in hot paths
  - [x] Eliminated `Contains`/`ToArray` usage in bootstrapper and state-group construction in favour of explicit loops and pooled buffers (`Runtime/State/Stack/Components/StateStackBootstrapper.cs`, `Runtime/State/Stack/States/StateGroup.cs`).
  - [x] Introduced `ITransitionRule` with struct-based evaluation to bypass delegate allocations, plus coverage in `StateMachineTests` (`Runtime/State/Machine/Component/Transition.cs`, `Tests/EditMode/State/Machine/StateMachineTests.cs`).
- [ ] Profile and expose metrics
  - Add optional sampling hooks (counts of transitions, average progress duration) to feed diagnostics and verify optimisation impact.

## Priority 3 — Understanding, Samples, and Tooling
- [ ] Expand documentation and samples
  - Update `README.md` with layered onboarding: quick-start scene stack, scene factory usage, trigger machine example, and performance tips.
  - Provide sample scenes demonstrating additive scene swaps, trigger-driven gameplay flow, and diagnostic overlay usage.
- [ ] Improve diagnostics visibility
  - Extend `StateStackDiagnosticsOverlay` with tabs for trigger machine metrics, progress graphs, and active scene groups.
  - Ship an editor-only assembly (`Editor/StateStack/`) with a custom inspector for `StateStackManager` plus a dockable `StateStackDebuggerWindow` (UI Toolkit) that live-streams stack contents, queued transitions, and diagnostics history via `StateStackDiagnostics`.
  - Provide play-mode toolbar controls (push/pop buttons, scene factory shortcuts) guarded by `UNITY_EDITOR` to accelerate iteration without scripting entry points.
  - Offer a CLI/logging integration (ScriptableObject config) so teams can opt into structured logs for transitions.
- [~] Testing & CI scaffolding
  - [x] Mirrored the new runtime surface with edit-mode fixtures (e.g. `AsyncOperationStateTests`, `ExclusiveSceneSetStateTests`, `ChunkStreamingStateTests`).
  - [ ] Integrate Unity batchmode test execution guidance into `PLAN.md` follow-up or CI documentation to ensure contributors verify changes locally.
