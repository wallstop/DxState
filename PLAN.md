# DxState Improvement Plan

## Observation Summary
- **Usability**: Authoring states requires verbose manual setup (manual `Transition<T>` construction, hand-wired `SceneState` instances, explicit registration in `StateStackManager`), which raises the barrier to quick prototyping and increases the risk of inconsistent configuration across scenes.
- **Correctness**: `StateMachine<T>` allows re-entrant transitions during `Enter`/`Exit`, `SceneState` does not protect against double-load/unload paths, and `StateStack` lacks explicit failure handling for async state entry/exit; these gaps can yield corrupted stacks or duplicate scene loads under stress.
- **Feature Coverage**: The runtime mainly covers pushdown stacks and boolean-rule state machines; there are no out-of-the-box helpers for common Unity flows (scene swapping sets, addressable loads, timed waits, timeline gating), nor an interface-driven machine where states decide transitions based on richer triggers.
- **Performance**: Transition queuing uses `TaskCompletionSource` and `Task.WhenAll`, `StateGroup` allocates per transition, and rule delegates capture closures; there is room to cut GC pressure by adopting pooled value-task sources and struct-based predicates for hot paths.
- **Ease-of-Understanding**: The mental model for when to use the stack versus the component state machine is under-documented; diagnostics exist but are not surfaced in samples, and there is no end-to-end narrative showing onboarding, scene orchestration, and advanced usage.
- **Robustness**: Tests cover only the async progress helper; there are no edit-mode fixtures safeguarding state machine invariants, queue ordering, or scene state re-entrancy, leaving regressions likely when extending the system.

## Priority 0 — Guardrails and Correctness Foundations
1. Harden `StateMachine<T>` transition execution
   - Add an `_transitionDepth` guard and pending transition queue so `Enter`/`Exit` cannot recursively call `ForceTransition` and corrupt `_states`.
   - Introduce immutable transition descriptors and ensure `TransitionToState` materialises context before state swap; add logging hooks for blocked transitions.
   - Write edit-mode tests covering re-entrant `ForceTransition`, rule-triggered loops, and `IStateContext<T>` lifecycle ordering.
2. Seal scene loading race conditions
   - Extend `SceneState` with internal state (`_isTransitioning`, reference counts) to prevent duplicate `LoadSceneAsync`/`UnloadSceneAsync` when the stack oscillates quickly.
   - Detect same-scene additive loads and short-circuit if the target scene is already active (optionally via `SceneManager.GetSceneByName`).
   - Add failure handling and logging when `AsyncOperation` returns null, ensuring `Revert` can tolerate canceled or failed loads.
   - Cover the new behaviour with edit-mode harness that fakes `AsyncOperation` and validates revert semantics.
3. Strengthen `StateStack` transition pipeline
   - Surface transition failures by wrapping `Enter`/`Exit`/`Remove` in try/catch, propagating diagnostics and ensuring `_isTransitioning` is reset.
   - Prevent zero-progress hangs by enforcing a final `Report(1f)` in every path and verifying progress monotonicity in tests.
   - Add exhaustive coverage for queue ordering, `FlattenAsync`, and manual removal to lock in current semantics before feature work.

## Priority 1 — Expressive Authoring & New Machine Model
1. Introduce declarative trigger-driven state machine (`TriggerStateMachine`)
   - Define `ITriggerState<TState, TTrigger>` exposing `bool TryGetTrigger(out TTrigger trigger, out TransitionContext context)` plus data payload accessors.
   - Implement a scheduler that polls active state each `Update`, debounces repeated triggers, and resolves transitions via a static map (no manual `SetState`).
   - Provide adapters so existing `Transition<T>` rules can wrap into trigger providers, easing migration.
   - Ship unit tests validating trigger prioritisation, data propagation, and guard against re-entrancy.
2. Build state factory helpers for scenes and common Unity flows
   - Create `SceneStateFactory` with presets: `LoadAdditive`, `Unload`, `SwapExclusive`, including automatic `StateGroup` creation that tags with the current active scene for re-entry safety.
   - Add helpers for `Addressables` (address key load/unload), `TimelineState`, `WaitForSecondsState`, `ConditionState` (polling predicate), and `AsyncOperationState` wrappers, each favouring struct parameters to limit allocations.
   - Introduce scenario packs with ready-made states/factories:
     * **Loading & Boot**: `BootstrapState`, `LoadingScreenState` (fades + progress binding), `WarmupAddressablesState`, `ShaderPreloadState`.
     * **Scene Orchestration**: `ExclusiveSceneSetState`, `ChunkStreamingState` (grid/zone-based additive streaming), `SceneGroupStateFactory` that auto-creates child groups per active scene for re-entrancy safe toggles.
     * **UI & Navigation**: `UiScreenState` (canvas enable/disable + input routing), `ModalStateStack` helpers for overlays, `VirtualCameraState` for Cinemachine priority swapping.
     * **Gameplay Flow**: `GameplayLoopState` bundle (menu → gameplay → pause), `TimelineCutsceneState`, `DialogueState` (Ink/Yarn hooks), `TutorialStepState` with completion predicates.
     * **Systems Control**: `InputModeState` (new Input System map activation), `TimeScaleState`, `AudioSnapshotState`, `PostProcessingState`, `PhysicsIsolationState` for toggling layers.
     * **Data & Networking**: `SaveGameState` (auto-save checkpoint), `MatchmakingState`, `NetworkConnectState`, `SessionTearDownState`, all exposing cancellation/timeout semantics.
   - Allow factories to optionally auto-register states with a supplied `StateStack`/`StateStackManager` to reduce boilerplate.
3. Authoring ergonomics & configuration DSL
   - Provide a fluent builder (`StateGraphBuilder`) to declare stacks/groups/scenes in code or ScriptableObjects, outputting immutable graphs consumable at runtime.
   - Add editor tooling hooks (custom inspectors) to compose groups and preview execution order, relying on existing assembly definitions.
   - Document interplay between factories, builders, and the new trigger machine to guide selection per use case.

## Priority 2 — Performance & Allocation Optimisation
1. Replace `TaskCompletionSource`-backed queues with pooled value-task sources
   - Introduce an internal `TransitionAwaitable` using `ManualResetValueTaskSourceCore<bool>` to eliminate heap allocations during rapid transitions.
   - Pool `QueuedTransition` instances or reuse a ring buffer to avoid frequent queue churn.
2. Optimise `StateGroup` parallel execution path
   - Swap `Task.WhenAll` for custom `ValueTask` combinators that run child states without allocating intermediate `Task` objects.
   - Reuse `List<Task>`/`ParallelProgressAggregator` buffers via `ArrayPool` or persistent fields sized to child count.
3. Reduce delegate and LINQ overhead in hot paths
   - Replace LINQ (`ToArray`, `Contains`) in `StateStackBootstrapper` and `StateGroup` initialisation with manual loops or span helpers to avoid per-frame GC.
   - Introduce struct-based transition predicates (`ITransitionRule` implementors) so common rules can avoid closure allocation.
4. Profile and expose metrics
   - Add optional sampling hooks (counts of transitions, average progress duration) to feed diagnostics and verify optimisation impact.

## Priority 3 — Understanding, Samples, and Tooling
1. Expand documentation and samples
   - Update `README.md` with layered onboarding: quick-start scene stack, scene factory usage, trigger machine example, and performance tips.
   - Provide sample scenes demonstrating additive scene swaps, trigger-driven gameplay flow, and diagnostic overlay usage.
2. Improve diagnostics visibility
   - Extend `StateStackDiagnosticsOverlay` with tabs for trigger machine metrics, progress graphs, and active scene groups.
   - Ship an editor-only assembly (`Editor/StateStack/`) with a custom inspector for `StateStackManager` plus a dockable `StateStackDebuggerWindow` (UI Toolkit) that live-streams stack contents, queued transitions, and diagnostics history via `StateStackDiagnostics`.
   - Provide play-mode toolbar controls (push/pop buttons, scene factory shortcuts) guarded by `UNITY_EDITOR` to accelerate iteration without scripting entry points.
   - Offer a CLI/logging integration (ScriptableObject config) so teams can opt into structured logs for transitions.
3. Testing & CI scaffolding
   - Mirror the new runtime surface in `Tests/EditMode/State/...`, adding play-mode coverage for scene orchestration factories.
   - Integrate Unity batchmode test execution guidance into `PLAN.md` follow-up or CI documentation to ensure contributors verify changes locally.
