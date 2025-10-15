# Improvement Plan

## P0 – Critical / Blockers
- [ ] Restore `TimeState` to always reinstate the previous `Time.timeScale` when it stops being the active state (forward transitions currently leave the global timescale altered). Add explicit coverage so both forward pushes and nested removal paths stay in sync (Runtime/State/Stack/States/TimeState.cs:57, Tests/EditMode/State/Stack/TimeStateTests.cs:24).
- [ ] Harden `StateMachine<T>` construction by validating transitions up front (null states, duplicate entries, missing reverse mappings) so we fail fast instead of letting the dictionary throw or leaving `_states` inconsistent at runtime (Runtime/State/Machine/StateMachine.cs:21).

## P1 – High Value
- [ ] Introduce an interface-first contract for component-driven states so `StateMachine` users are not forced to inherit from `StateComponent`/`MonoBehaviour`; keep the current base type as a helper but let the machine depend on an injectable `IStateComponent` instead (Runtime/State/Machine/Component/StateComponent.cs:14).
- [ ] Replace the LINQ-backed `ImmutableUnableToEnterIfHasTag` cache with pooled storage or static arrays to avoid the per-Awake allocation and deferred iterator cost in production builds (Runtime/State/Machine/Component/StateComponent.cs:32).
- [ ] Provide a fluent/builder API (or scriptable recipe) for authoring transition graphs so teams can compose machines declaratively, validate them, and share presets, instead of manually materialising `Transition<T>` lists (Runtime/State/Machine/StateMachine.cs:21).
- [ ] Remove closure allocations in `StateStack.PerformTransition` by hoisting the local `ExecuteTransition` delegate to a reusable static helper or command struct (Runtime/State/Stack/StateStack.cs:623).
- [ ] Swap the ad-hoc `ArrayPool<ValueTask>.Shared` usage inside `StateGroup` for `WallstopFastArrayPool`/`Buffering` utilities so large parallel groups stop zeroing arrays every frame and reuse buffers efficiently (Runtime/State/Stack/States/StateGroup.cs:181).
- [ ] Update `SceneState` to expose `ValueTask`-based awaiting (or cached task instances) so scene operations stop allocating on every transition and can optionally plug into `WallstopArrayPool` progress drivers (Runtime/State/Stack/States/SceneState.cs:299).

## P2 – Medium / Supporting
- [ ] Backfill tests for `StateComponent` tag-based gating and `TransitionDeferred` logging so the behavioural contract is covered before refactors (Runtime/State/Machine/Component/StateComponent.cs:47, Tests/EditMode/State/Machine/StateMachineTests.cs:10).
- [ ] Add an opt-in scriptable configuration (potentially a `ScriptableObjectSingleton`) that mirrors `StateGraphBuilder` so designers can serialize stack layouts without code (Runtime/State/Stack/Builder/StateGraphBuilder.cs:8).
- [ ] Extend diagnostics to surface transition backlog depth and deferred counts, helping teams spot misconfigured rules in play mode without attaching a debugger (Runtime/State/Stack/Diagnostics/StateStackDiagnostics.cs:52).
- [ ] Document and sample the pooling patterns (`WallstopArrayPool`, `WallstopFastArrayPool`) across runtime states so contributors follow consistent allocation-free conventions (README.md:1).
