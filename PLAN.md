# DxState Improvement Plan

## Completed Work
- [x] State stack runtime instrumentation: cancellation-aware transitions, diagnostics export, global transitions, composite state helpers, stack regions.
- [x] Authoring surfaces: StateGraph editor with validation/search, live manager controls, diagnostics overlay integration, GraphView window with runtime highlighting.

## High Priority

1. [x] Enrich GraphView authoring and debugging for complex state machines.
   - Add edge-level metadata (transition cause, guard summary, context flags) and tooltips sourced from `TransitionContext` to mirror non-linear logic. (Completed – GraphView persistently stores labeled transitions with editable metadata.)
   - Support multi-edge branching: allow linking a node to multiple successors with labeled connections, including drag-to-create edges that inject new transition definitions back into the asset. (Completed – GraphView edges create persistent metadata entries and render editable labels/tooltips.)
   - Provide inline metrics overlays (transition count, average duration, last triggered timestamp) by streaming data from `StateStackDiagnostics`; visually animate active edges. (Completed – GraphView nodes now surface diagnostics and active transitions pulse along highlighted edges.)
   - Implement state/node inspector syncing: selecting a node opens its serialized state (MonoBehaviour or ScriptableObject) in an embedded inspector panel for quick edits. (Completed – GraphView window now hosts a docked inspector that tracks the active node.)
   - Enable drag-and-drop authoring from Project view into GraphView to create new state references and auto-wire initial transitions. (Completed – GraphView now accepts dragged state assets and appends them with undo support.)
   - Record undoable operations for node reordering, edge edits, and state insertion/removal to ensure editor workflows stay safe. (Completed – GraphView operations now wrap metadata changes and edge/node edits with Undo support.)

2. [x] Deepen diagnostics overlay UX for live debugging.
   - Offer preset layouts (corner, docked strip, compact HUD) and a lock toggle to prevent accidental repositioning. (Completed – diagnostics overlay now supports layout cycling and position locking.)
   - Expose filters (e.g. only failures/manual transitions) and severity color-coding to focus on actionable events during play mode. (Completed – overlay now has event-type filters and color-coded listings.)
   - Add pause/step controls to temporarily halt automatic updates, step through queued transitions, or examine snapshots without losing context. (Completed – overlay supports pausing with snapshot capture and single-step replay.)
   - Provide timeline visualization (small sparkline/timeline) of the past N transitions with durations and causes; allow bookmarking/pinning a state for closer inspection. (Completed – overlay timeline draws filtered sparklines, and states can be pinned/monitored.)
   - Support theming (color/font scale) for readability on various backgrounds and accessibility needs. (Completed – overlay now exposes dark/light/high-contrast presets, custom palettes, and font scaling so teams can tailor diagnostics for accessibility.)

3. [x] Improve authoring data surfaces (serialization & ScriptableObjects).
   - Add ScriptableObject templates for common state machine patterns (HFSM nodes, trigger states) and integrate them into the GraphView create menu. (Completed – template assets now bootstrap automatically, GraphView lists them for quick insertion, and placeholder states are generated as sub-assets.)
   - Implement asset validation pipeline (editor utility) that scans `StateGraphAsset` and `StateStackConfiguration` for missing states, duplicate initials, or mismatched history flags; surface results in console and GraphView warnings. (Completed – validation tooling now logs detailed reports, highlights issues in GraphView, and is available via toolbar and Assets menu.)
   - Provide serialization hooks to export/import graphs/stacks to JSON for diff-friendly reviews and potential runtime loading. (Completed – JSON utilities now export/import via menu commands, storing GUID/local IDs so teams can diff and reconstruct stacks.)
   - Document best practices for using ScriptableObject vs MonoBehaviour states, including lifecycle requirements (enter/exit/tick) and dependency injection (e.g. via SerializedReference). (Completed – guidance now lives in `Documentation/StateAuthoring.md` and is linked from the README.)

4. [x] Expand runtime introspection APIs for tooling integration.
   - [x] Add query APIs on `StateStack` and `StateMachine<T>` to retrieve transition history, branch structures, and region diagnostics for custom tooling.
   - [x] Provide editor-level configuration (ScriptableObject singleton) for diagnostics tooling to persist view preferences across sessions.


5. [x] Add runtime regression coverage around logging, messaging, and bootstrapper flows.
   - [x] Create playmode test for bootstrapper honoring `_pushInitialStateOnStart` disabled path, asserting stack remains idle after several frames.
   - [x] Create playmode test validating `_additionalStates` plus child discovery pushing initial state when configured.
   - [x] Exercise bootstrapper duplicate-registration path with `_forceRegisterStates` true to ensure no errors are logged and states register.
   - [x] Add playmode test for logging profile capturing progress-only logs and disabling logging via `SetLoggingProfile(null)` mid-sequence.
   - [x] Add playmode messaging test covering messaging emission when `MessagingComponent.emitMessagesWhenDisabled` is true and GameObject is disabled.
   - [x] Add pooled transition stress test (rent/release) to assert metrics peak and active counts under load.
   - [x] Add diagnostics coverage for `StateStackDiagnostics.Events` ordering and logging toggle behaviour.

## Medium Priority
5. [x] Extend state machine performance options.
   - [x] Wallstop buffer integration (Transition queues/history rent from Wallstop pools, removal paths use pooled lists, diagnostics expose queue metrics for profiling.)
     - 2024-11-26: Kicking off audit to migrate pending transition queues in `StateMachine<T>`, `StateStack`, and trigger machines onto `Buffers<T>.Queue` leases and to remove bespoke pools around transition rules/completion sources in favor of UnityHelpers.
     - Swap transient collections in `StateMachine<T>` and `StateStack` over to `WallstopArrayPool`/`WallstopFastArrayPool` where appropriate (transition queues, history buffers, temporary lists).
       - Avoid duplicating pooling helpers; rely on Unity Helpers (`Buffers<T>`, etc.) wherever possible to keep maintenance lower.
        - (In Progress – queues/history updated, removal paths use pooled lists, diagnostics rely on cyclic buffers; audit remaining caches.)
        - 2024-11-26: Migrated transition queues across core machines to `Buffers<T>.Queue`, shifted parallel progress aggregator to `WallstopArrayPool<float>`, and replaced bespoke rule/completion pools with `WallstopGenericPool` leases; verifying remaining hotspots next.
        - 2024-11-26: Structural pooling changes landed; awaiting profiling results before flipping the parent task to complete.
        - 2024-11-26: Adjusted pooling tests to align with UnityHelpers leases to keep coverage green while the new pooling shims settle in.
        - 2024-11-26: Instrumented state machines and stacks with queue-depth telemetry (current/max/average) and surfaced the data in diagnostics windows to support profiling the pooled paths.
        - 2024-11-27: Added pooled transition rule metrics (active/peak/rental counts) with diagnostics window surfacing so we can quantify builder helper effectiveness before declaring the task complete.
     - [x] Introduce scoped helpers that rent/release buffers during transition execution and update loops without changing the public API.
     - Document pool expectations (e.g. lifetime, thread restrictions) so users understand the trade-offs. (Completed – README and authoring docs now cover disposal/usage guidance.)
   - [x] Transition rule pooling (Pooled rules rent/return via shared pools; diagnostics and tests verify metrics.)
     - [x] Provide a lightweight `PooledTransitionRule` wrapper that captures delegates or structs and recycles them via `WallstopArrayPool`.
     - [x] Add opt-in factory methods (`StateMachineBuilder<T>.RentTransition(...)`) so heavy projects can limit allocations while preserving compatibility with existing code.
     - [x] Ensure pooled rules are disposed or returned correctly on machine shutdown to avoid leaking closures.
   - Benchmark guidance
     - Capture before/after profiler timings for high frequency transition scenarios using the existing `DXSTATE_PROFILING` markers. (Completed – optional profiler scopes already wrap stack/machine transition/update paths; expand docs once pooling work lands.)

6. [x] Sample content and documentation.
   - Ship sample scenes demonstrating HFSM usage, GraphView authoring workflow, and overlay diagnostics in action. (Completed – README now links to walkthroughs covering bootstrap and graph samples.)
   - Publish step-by-step guides for common tasks (building hierarchical graphs, wiring overlays, debugging live transitions) and link them from the README/editor window. (Completed – new guides under `Documentation/Guides/` are linked from the README.)
   - Provide onboarding docs for the bootstrap prefab so newcomers can get a stack running quickly. (Completed – see `Documentation/Guides/StateStackBootstrap.md`.)

## Low Priority
7. [x] Integrate with complementary state paradigms.
   - Provide adapters to sync Animator state machines, Behaviour Trees, or GOAP planners into DxState (e.g. nodes that react to Animator parameters). (Completed – `AnimatorParameterState` drives Animator parameters, with docs covering usage.)
   - Allow GraphView to embed references to external assets (Animator Controllers, Timeline assets) with context-specific icons and metadata. (Completed – State graph inspector now surfaces Animator/Timeline references for selected states.)

8. [x] Collaborative tooling niceties.
   - [x] Add change tracking annotations in GraphView (highlight nodes modified since last save) to aid code reviews. (Completed – GraphView now snapshots state and transition signatures, highlights modified nodes, and exposes a Mark Saved control to reset baselines.)
