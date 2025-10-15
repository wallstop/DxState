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
- [ ] Seal scene loading race conditions
  - Extend `SceneState` with internal state (`_isTransitioning`, reference counts) to prevent duplicate `LoadSceneAsync`/`UnloadSceneAsync` when the stack oscillates quickly.
  - Detect same-scene additive loads and short-circuit if the target scene is already active (optionally via `SceneManager.GetSceneByName`).
  - Add failure handling and logging when `AsyncOperation` returns null, ensuring `Revert` can tolerate canceled or failed loads.
  - Cover the new behaviour with edit-mode harness that fakes `AsyncOperation` and validates revert semantics.
- [ ] Strengthen `StateStack` transition pipeline
  - Surface transition failures by wrapping `Enter`/`Exit`/`Remove` in try/catch, propagating diagnostics and ensuring `_isTransitioning` is reset.
  - Prevent zero-progress hangs by enforcing a final `Report(1f)` in every path and verifying progress monotonicity in tests.
  - Add exhaustive coverage for queue ordering, `FlattenAsync`, and manual removal to lock in current semantics before feature work.

## Priority 1 — Expressive Authoring & New Machine Model
- [x] Introduce declarative trigger-driven state machine (`TriggerStateMachine`)
  - Implementation available in `Runtime/State/Machine/Trigger/` with coverage in `Tests/EditMode/State/Machine/TriggerStateMachineTests.cs`.
- [~] Build state factory helpers for scenes and common Unity flows
  - [x] Added `AsyncOperationState`, `TimelineState`, and Addressables helpers (`Runtime/State/Stack/States/AsyncOperationState.cs`, `/Addressables/`).
  - [x] Published scenario pack states: `BootstrapState`, `LoadingScreenState`, `WarmupAddressablesState`, `ShaderPreloadState`, `ExclusiveSceneSetState`, `ChunkStreamingState`, `SceneGroupStateFactory`, `UiScreenState`, `ModalStateStack`, `VirtualCameraState`, `GameplayLoopState`.
  - [ ] Remaining planned states (e.g. `TimelineCutsceneState`, `DialogueState`, `TutorialStepState`, networking helpers) to be evaluated and implemented.
  - [x] Tests mirror new runtime surface under `Tests/EditMode/State/Stack/` (e.g. `AsyncOperationStateTests.cs`, `AddressableAssetStateTests.cs`, `ExclusiveSceneSetStateTests.cs`).
- [ ] Authoring ergonomics & configuration DSL
  - Provide a fluent builder (`StateGraphBuilder`) to declare stacks/groups/scenes in code or ScriptableObjects, outputting immutable graphs consumable at runtime.
  - Add editor tooling hooks (custom inspectors) to compose groups and preview execution order, relying on existing assembly definitions.
  - Document interplay between factories, builders, and the new trigger machine to guide selection per use case.

## Priority 2 — Performance & Allocation Optimisation
- [x] Replace `TaskCompletionSource`-backed queues with pooled value-task sources
  - `Runtime/State/Stack/Internal/TransitionCompletionSource.cs` now provides a custom pooled awaitable with no dependency on `ManualResetValueTaskSourceCore<T>` collisions.
  - `StateStack` transition queuing updated to consume the pooled source (`Runtime/State/Stack/StateStack.cs`).
- [ ] Optimise `StateGroup` parallel execution path
  - Swap `Task.WhenAll` for custom `ValueTask` combinators that run child states without allocating intermediate `Task` objects.
  - Reuse `List<Task>`/`ParallelProgressAggregator` buffers via `ArrayPool` or persistent fields sized to child count.
- [ ] Reduce delegate and LINQ overhead in hot paths
  - Replace LINQ (`ToArray`, `Contains`) in `StateStackBootstrapper` and `StateGroup` initialisation with manual loops or span helpers to avoid per-frame GC.
  - Introduce struct-based transition predicates (`ITransitionRule` implementors) so common rules can avoid closure allocation.
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
