# DxState Improvement Plan

## Completed Work
- [x] State stack runtime instrumentation: cancellation-aware transitions, diagnostics export, global transitions, composite state helpers, stack regions.
- [x] Authoring surfaces: StateGraph editor with validation/search, live manager controls, diagnostics overlay integration, GraphView window with runtime highlighting.

## High Priority
1. [ ] Enrich GraphView authoring and debugging for complex state machines.
   - Add edge-level metadata (transition cause, guard summary, context flags) and tooltips sourced from `TransitionContext` to mirror non-linear logic.
   - Support multi-edge branching: allow linking a node to multiple successors with labeled connections, including drag-to-create edges that inject new transition definitions back into the asset.
   - Provide inline metrics overlays (transition count, average duration, last triggered timestamp) by streaming data from `StateStackDiagnostics`; visually animate active edges. (Completed – GraphView nodes now surface diagnostics and active transitions pulse along highlighted edges.)
   - Implement state/node inspector syncing: selecting a node opens its serialized state (MonoBehaviour or ScriptableObject) in an embedded inspector panel for quick edits. (Completed – GraphView window now hosts a docked inspector that tracks the active node.)
   - Enable drag-and-drop authoring from Project view into GraphView to create new state references and auto-wire initial transitions. (Completed – GraphView now accepts dragged state assets and appends them with undo support.)
   - Record undoable operations for node reordering, edge edits, and state insertion/removal to ensure editor workflows stay safe.

2. [ ] Deepen diagnostics overlay UX for live debugging.
   - Offer preset layouts (corner, docked strip, compact HUD) and a lock toggle to prevent accidental repositioning.
   - Expose filters (e.g. only failures/manual transitions) and severity color-coding to focus on actionable events during play mode.
   - Add pause/step controls to temporarily halt automatic updates, step through queued transitions, or examine snapshots without losing context.
   - Provide timeline visualization (small sparkline/timeline) of the past N transitions with durations and causes; allow bookmarking/pinning a state for closer inspection.
   - Support theming (color/font scale) for readability on various backgrounds and accessibility needs.

3. [ ] Improve authoring data surfaces (serialization & ScriptableObjects).
   - Add ScriptableObject templates for common state machine patterns (HFSM nodes, trigger states) and integrate them into the GraphView create menu.
   - Implement asset validation pipeline (editor utility) that scans `StateGraphAsset` and `StateStackConfiguration` for missing states, duplicate initials, or mismatched history flags; surface results in console and GraphView warnings.
   - Provide serialization hooks to export/import graphs/stacks to JSON for diff-friendly reviews and potential runtime loading.
   - Document best practices for using ScriptableObject vs MonoBehaviour states, including lifecycle requirements (enter/exit/tick) and dependency injection (e.g. via SerializedReference).

4. [ ] Expand runtime introspection APIs for tooling integration.
   - Add query APIs on `StateStack` and `StateMachine<T>` to retrieve transition history, branch structures, and region diagnostics for custom tooling. (In Progress – StateMachineDiagnostics now aggregates executed/deferred events, per-state enter/exit metrics, and cause counts.)
   - Emit editor events (UnityEvent or callback) when GraphView edits modify the underlying asset to allow other tools (e.g. CI lint) to react.
   - Provide serialization helpers to snapshot/restore entire hierarchical machine stacks for replay or unit testing.

## Medium Priority
5. [ ] Extend state machine performance options.
   - Offer Burst/DOTS-friendly state machine variants (struct-based, jobified evaluation) and bridge them to existing APIs.
   - Introduce pooling for frequently created transitions/rules to minimise GC churn in high frequency updates.
   - Benchmark and expose profiling hooks (Unity Profiler markers) around `StateStack` operations.

6. [ ] Sample content and documentation.
   - Ship sample scenes demonstrating HFSM usage, GraphView authoring workflow, and overlay diagnostics in action.
   - Publish step-by-step guides for common tasks (building hierarchical graphs, wiring overlays, debugging live transitions) and link them from the README/editor window.

## Low Priority
7. [ ] Integrate with complementary state paradigms.
   - Provide adapters to sync Animator state machines, Behaviour Trees, or GOAP planners into DxState (e.g. nodes that react to Animator parameters).
   - Allow GraphView to embed references to external assets (Animator Controllers, Timeline assets) with context-specific icons and metadata.

8. [ ] Collaborative tooling niceties.
   - Add change tracking annotations in GraphView (highlight nodes modified since last save) to aid code reviews.
   - Provide CLI utilities to export diagnostics snapshots for automated bug reports or CI validation.
