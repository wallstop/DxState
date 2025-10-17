# DxState Improvement Plan

## Completed Work
- [x] State stack runtime instrumentation: cancellation-aware transitions, diagnostics export, global transitions, composite state helpers, stack regions.
- [x] Authoring surfaces: StateGraph editor with validation/search, live manager controls, diagnostics overlay integration, GraphView window with runtime highlighting.

## High Priority
1. [ ] Enrich GraphView authoring and debugging for complex state machines.
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

3. [ ] Improve authoring data surfaces (serialization & ScriptableObjects).
   - Add ScriptableObject templates for common state machine patterns (HFSM nodes, trigger states) and integrate them into the GraphView create menu. (Completed – template assets now bootstrap automatically, GraphView lists them for quick insertion, and placeholder states are generated as sub-assets.)
   - Implement asset validation pipeline (editor utility) that scans `StateGraphAsset` and `StateStackConfiguration` for missing states, duplicate initials, or mismatched history flags; surface results in console and GraphView warnings. (Completed – validation tooling now logs detailed reports, highlights issues in GraphView, and is available via toolbar and Assets menu.)
   - Provide serialization hooks to export/import graphs/stacks to JSON for diff-friendly reviews and potential runtime loading. (Completed – JSON utilities now export/import via menu commands, storing GUID/local IDs so teams can diff and reconstruct stacks.)
   - Document best practices for using ScriptableObject vs MonoBehaviour states, including lifecycle requirements (enter/exit/tick) and dependency injection (e.g. via SerializedReference). (Completed – guidance now lives in `Documentation/StateAuthoring.md` and is linked from the README.)

4. [ ] Expand runtime introspection APIs for tooling integration.
   - Add query APIs on `StateStack` and `StateMachine<T>` to retrieve transition history, branch structures, and region diagnostics for custom tooling. (Completed – new `TransitionHistory`/`CopyTransitionHistory` surfaces on both stacks and machines expose recent transitions, while `ActiveRegions` helpers surface hierarchical activity.)
   - Provide editor-level configuration (ScriptableObject singleton) for diagnostics tooling to persist view preferences across sessions. (Completed – StateMachineDiagnosticsSettings now drives the diagnostics window.)
   - Emit editor events (UnityEvent or callback) when GraphView edits modify the underlying asset to allow other tools (e.g. CI lint) to react. (Completed – `StateGraphViewWindow.GraphAssetChanged` now fires whenever serialized graph data changes.)
   - Provide serialization helpers to snapshot/restore entire hierarchical machine stacks for replay or unit testing. (Completed – `StateStackSnapshot` captures/restores stack contents, and state machines expose transition histories/regions for replay tooling.)

## Medium Priority
5. [ ] Extend state machine performance options.
   - Wallstop buffer integration (In Progress – transition queues/history rent from Wallstop pools and scoped list helpers reduce removal allocations; evaluate remaining hot paths.)
     - Swap transient collections in `StateMachine<T>` and `StateStack` over to `WallstopArrayPool`/`WallstopFastArrayPool` where appropriate (transition queues, history buffers, temporary lists). (In Progress – queues/history updated; assess additional caches.)
     - [x] Introduce scoped helpers that rent/release buffers during transition execution and update loops without changing the public API.
     - Document pool expectations (e.g. lifetime, thread restrictions) so users understand the trade-offs. (Completed – README and authoring docs now cover disposal/usage guidance.)
   - Transition rule pooling (In Progress – pooled transition rules now rent from the shared pool, builder helpers cover delegates and rule structs, and machines release rentals on dispose; evaluate runtime diagnostics before marking complete.)
     - [x] Provide a lightweight `PooledTransitionRule` wrapper that captures delegates or structs and recycles them via `WallstopArrayPool`.
     - [x] Add opt-in factory methods (`StateMachineBuilder<T>.RentTransition(...)`) so heavy projects can limit allocations while preserving compatibility with existing code.
     - [x] Ensure pooled rules are disposed or returned correctly on machine shutdown to avoid leaking closures.
   - Benchmark guidance
     - Capture before/after profiler timings for high frequency transition scenarios using the existing `DXSTATE_PROFILING` markers. (Completed – optional profiler scopes already wrap stack/machine transition/update paths; expand docs once pooling work lands.)

6. [ ] Sample content and documentation.
   - Ship sample scenes demonstrating HFSM usage, GraphView authoring workflow, and overlay diagnostics in action. (Completed – README now links to walkthroughs covering bootstrap and graph samples.)
   - Publish step-by-step guides for common tasks (building hierarchical graphs, wiring overlays, debugging live transitions) and link them from the README/editor window. (Completed – new guides under `Documentation/Guides/` are linked from the README.)
   - Provide onboarding docs for the bootstrap prefab so newcomers can get a stack running quickly. (Completed – see `Documentation/Guides/StateStackBootstrap.md`.)

## Low Priority
7. [ ] Integrate with complementary state paradigms.
   - Provide adapters to sync Animator state machines, Behaviour Trees, or GOAP planners into DxState (e.g. nodes that react to Animator parameters). (Completed – `AnimatorParameterState` drives Animator parameters, with docs covering usage.)
   - Allow GraphView to embed references to external assets (Animator Controllers, Timeline assets) with context-specific icons and metadata. (Completed – State graph inspector now surfaces Animator/Timeline references for selected states.)

8. [ ] Collaborative tooling niceties.
   - [x] Add change tracking annotations in GraphView (highlight nodes modified since last save) to aid code reviews. (Completed – GraphView now snapshots state and transition signatures, highlights modified nodes, and exposes a Mark Saved control to reset baselines.)
