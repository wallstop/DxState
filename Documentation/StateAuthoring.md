# DxState Authoring Best Practices

## Choosing Between ScriptableObject and MonoBehaviour States

- **ScriptableObject states** are asset-based and reusable across scenes. Use them when the behaviour has no strong dependency on scene hierarchy or when you want to version-control state logic independently. Pair them with `SerializedReference` fields for injectable collaborators.
- **MonoBehaviour states** live on GameObjects. Prefer them when state transitions must drive or depend on scene components (e.g. animation hooks, UI hierarchies) or when you want per-instance overrides via inspector fields.
- Avoid mixing the two for the same logical state; instead wrap GameObject dependencies inside a Scene Service and feed the service into a ScriptableObject via dependency injection.

## Lifecycle Contracts

Each implementation of `IState` (and `GameState`) must respect the following lifecycle guarantees:

- `Enter` and `Exit` will always be awaited. Report progress via the provided `IProgress<float>` and ensure the final value is `1f` when the phase completes.
- `Remove` is called when the state is explicitly removed from the stack (flatten, pop, remove). It should clear durable side-effects and is the symmetrical counterpart to `Enter`.
- Use `Tick(TickMode mode, float delta)` only for work that must run every frame. Expensive operations belong in async transitions or background jobs.
- For cancellable states (`ICancellableState` implementations), honour the `CancellationToken` in the overloads of `Enter`, `Exit`, and `Remove`. Always throw `OperationCanceledException` if cancellation is requested.

## Dependency Injection Guidelines

- Prefer constructor or serialized injection over service location. For ScriptableObjects, expose `[SerializeField] private` fields and keep them `readonly` in runtime code by wrapping writes in validation methods.
- When a state depends on scene objects, provide a lightweight context interface that the manager populates prior to pushing the state.
- Avoid storing raw `GameObject` or `Component` references in ScriptableObjects; instead, resolve them at runtime via context data or `FindObjectOfType` wrappers assigned through `[SerializedReference]`.

## Stack Composition Tips

- Group related states into dedicated `StateGraphAsset` stacks so that validation and tooling can surface issues quickly. Run the built-in **Validate** action after structural edits.
- Keep transition metadata (labels, tooltips, causes, and flags) up to date. This ensures diagnostics and exported JSON remain diff-friendly and understandable in reviews.
- Use placeholder states (generated via templates) during prototyping, then replace them with concrete implementations before shipping.
- Leverage `StateStack.TransitionHistory` (or `CopyTransitionHistory`) and `StateMachine<T>.TransitionHistory`/`ActiveRegions` when building analytical tooling; these APIs expose recent transitions and active hierarchical regions without wiring new events.
- Capture and restore stacks in tests via `StateStackSnapshot.Capture(...)` and `RestoreAsync(...)` to reproduce complex hierarchies or replay save states.
- When running large stacks/machines, both `StateStack` and `StateMachine<T>` now rent internal buffers from the Wallstop pools. Dispose stacks/machines (or rely on `StateStackManager` / trigger machines) when finished so queues/history arrays return to the pool.
- Transition queues, deferred buffers, and history collections reuse pooled arrays—avoid holding references to internal arrays returned by helper APIs.
- Profiling hooks remain opt-in: define `DXSTATE_PROFILING` and toggle the static `ProfilingEnabled` flags if you want Unity profiler traces around transitions/updates.
- When deeper measurements are required, define `DXSTATE_PROFILING` and set `StateStack.ProfilingEnabled` / `StateMachine<T>.ProfilingEnabled` at runtime to wrap transitions and updates in Unity profiler markers.

## Testing and Validation

- Export rule-heavy graphs to JSON and commit them alongside assets to simplify code reviews.
- Leverage the validation tooling to catch missing references, duplicate initial states, and invalid transitions before entering play mode.
- Write edit-mode tests against `StateStackConfiguration` where possible. Build configurations programmatically and assert transitions using the runtime APIs to guarantee behaviour outside the editor.
