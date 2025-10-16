# DxState Improvement Plan

## High Priority
1. [x] Add hierarchical and orthogonal state machinery across stack and component surfaces.
   - Status: Completed — hierarchical contexts, global transitions, history, coordinators, composite/MonoBehaviour helpers, stack regions, and priority policies are in place.
   - Observations: `Runtime/State/Machine/StateMachine.cs:17` only models flat transitions and lacks enter/exit propagation to child machines; `Runtime/State/Stack/StateStack.cs:95` manages a simple pushdown stack without nested sub-stacks or history states.
   - Approach: Introduce composite `IStateContext`/`IState` implementations that can own child machines, add history/any-state semantics, and surface APIs for configurable parallel regions. Provide migration helpers so existing states can opt in without breaking changes.
   - Impact: Unlocks complex game flows (UI overlays, combat modes) without spinning up additional managers, improves usability and extendability for teams expecting HFSM/statechart capabilities similar to Animator/Playmaker.

2. [ ] Ship visual authoring & debugging tools for `StateGraphAsset` and live stacks.
   - Status: In progress — editor window offers stack editing/search, validation cues, live manager/overlay controls, diagnostics export, graph preview, and a dedicated GraphView window; next up: richer graph interactions and overlay UX polish.
   - Observations: `Runtime/State/Stack/Builder/StateGraphAsset.cs:13` defines data containers but there is no dedicated editor (only `Editor/State/StateStackManagerEditor.cs` for runtime inspection).
   - Approach: Build a UI Toolkit/GraphView editor to author graphs, generate transitions, simulate flows, and push changes into play mode. Add a runtime inspector overlay showing queued transitions, progress, and diagnostics beyond the existing overlay.
   - Impact: Dramatically lowers onboarding cost, aligns with designer expectations (graph editing), and improves understandability/debuggability.

3. [x] Harden asynchronous transitions with cancellation, timeouts, and fault recovery.
   - Status: Completed — `StateTransitionOptions` added for cancellation/timeout, `StateStack` propagates tokens, exposes timeout/cancel exceptions, and tests cover new behaviours.
   - Observations: Entry/exit paths in `Runtime/State/Stack/StateStack.cs:215` await `ValueTask` without cancellation; `FunctionalState`/`GameplayLoopState` rely on user-supplied delegates that can hang.
   - Approach: Thread a `CancellationToken` (or scoped transition token) through `Enter/Exit/Remove`, expose timeout policies, and surface diagnostics when a state stalls. Ensure `TransitionCompletionSource` can propagate cancellations cleanly.
   - Impact: Improves robustness, avoids hard locks in production, and gives teams tooling to abort problematic states safely.

## Medium Priority
4. [ ] Refactor transition rules to support allocation-free structs and preallocated pools.
   - Status: Not started
   - Observations: `StateMachineBuilder.AddTransition` (Runtime/State/Machine/StateMachineBuilder.cs:32) captures `Func<bool>` delegates; `ComponentStateTransition.cs:24` wraps target state checks with new lambdas; `FunctionalState.cs:11` and `GameplayLoopState.cs:19` keep delegate fields. Each instantiation allocates and prevents Burst-friendly usage.
   - Approach: Introduce generic `Transition<TState, TRule>` where `TRule : struct, ITransitionRule`, add pooled rule instances, and extend builders/factories to work with `WallstopArrayPool`/`WallstopFastArrayPool` for reusable buffers. Provide source analyzers to flag high-frequency closures.
   - Impact: Reduces GC pressure in hot paths, enabling deterministic behaviour for performance-sensitive projects and easing integration with Burst/DOTS code.

5. [ ] Provide persistent snapshot & restore support for stacks and machines.
   - Status: Not started
   - Observations: Neither `StateStack` nor `StateMachine` expose serialization beyond transient diagnostics.
   - Approach: Define serializable descriptors (state id, progress, queued transitions), integrate with `SerializedMessageAwareComponent`, and offer save/load APIs plus editor validation. Support partial restores (e.g., restoring only specific stacks) for usability.
   - Impact: Enables save systems, level restarts, and tooling that require durable state, boosting robustness and ease of use in real games.

6. [ ] Expand pooled collection usage in scenario states and builders.
   - Status: Not started
   - Observations: `StateGroup.CopyChildStates` and various factories (`SceneStateFactory.cs:65`, `ExclusiveSceneSetState.cs:42`) allocate new `List<>` buffers each call; graph builders call `_states.ToArray()` (`StateStackBuilder.cs:66`).
   - Approach: Replace ad-hoc `List` snapshots with `PooledArray<T>`/`WallstopArrayPool<T>` backed spans, provide helper utilities for deterministic cleanup, and benchmark gains with the existing test harness.
   - Impact: Shrinks transient GC allocations when composing states at runtime or building graphs repeatedly, improving performance for dynamic content pipelines.

7. [ ] Add DOTS/job-system friendly adapters and Burst-compatible state flows.
   - Status: Not started
   - Observations: Current APIs rely on managed delegates and UnityEngine types, blocking use inside ECS systems.
   - Approach: Introduce pure C# struct-based state machines (no UnityEngine dependency), add conversion layers to mirror stack changes into Entities, and document scheduling patterns.
   - Impact: Extends the feature set to projects using Entities or high-performance gameplay code.

8. [ ] Strengthen automated testing around high-load scenarios and designer tooling.
   - Status: Not started
   - Observations: Test suite is rich but lacks stress/performance regression cases and editor-tool coverage.
   - Approach: Add long-running transition queue tests, cancellation tests once implemented, and play-mode coverage for `StateGraphAsset` authoring plus diagnostics overlays.
   - Impact: Improves confidence as the library grows and guards against regressions in critical orchestration flows.

## Low Priority
9. [ ] Offer bridges to complementary state paradigms (behaviour trees, GOAP, Animator) and richer sample content.
   - Status: Not started
   - Observations: README lists scenarios but no direct integration with Unity Animator, Playables, or third-party AI frameworks.
   - Approach: Ship adapter states that sync Animator sub-state machines, expose behaviour-tree entry points, and expand Samples with end-to-end gameplay loops demonstrating the new tooling.
   - Impact: Enhances usability for teams mixing paradigms, showcasing flexibility and lowering adoption friction.
