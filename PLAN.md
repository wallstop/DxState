# Improvement Plan for DxState

1. **Stabilize core state machine transitions (P0) — Completed**  
   Implemented dictionary seeding for `transition.to`, guarded `Update()` lookups, cached the previous state for logging, and introduced `TransitionContext`/`TransitionExecutionContext` so transitions retain allocation-friendly metadata about how they executed.  
   *Outcome*: Prevents crash paths, yields accurate diagnostics, and enables consumers to inspect the cause of each transition.

2. **Fix ticking semantics and respect `TickWhenInactive` (P0) — Completed**  
   Reworked `StateStack.PerformUpdate` to resolve the correct Unity delta for each tick phase, tick the active state, and fan out to inactive states that opt into background ticking. Added helper coverage to keep top-level behaviour consistent even when the stack is empty or transitions are pending.  
   *Outcome*: Engine-driven updates now honor the API contract, and background states keep pace without manual polling.

3. **Add regression tests for stack operations (P0)**  
   *Problem*: There is no coverage in `Tests/EditMode` mirroring `Runtime/State/Stack`, so push/pop/flatten/remove flows, progress callbacks, and messaging contracts can regress silently.  
   *Actions*: Edit-mode suites now cover `StateStack` transitions, `StateGroup` sequencing, `TimeState` time-scale management, and `SceneState` validation. Play-mode coverage exists for ticking semantics. Still to add: `StateStackManager` messaging integration and in-depth async scene flows.  
   *Outcome*: Confidence to refactor internals while guaranteeing the behavioural contract new consumers rely on.

4. **Document architecture and onboarding (P0) — Completed**  
   Added a comprehensive README covering installation, quickstart wiring with `StateStackManager`, provided building blocks, and test execution guidance. Messaging hooks and architecture notes now call out how DxState fits alongside DxMessaging.  
   *Outcome*: Shortens time-to-first-state and lowers the support load for both internal and external adopters.

5. **Ship reference prefabs and bootstrap utilities (P1) — Completed**  
   Added `StateStackBootstrapper` to auto-provision messaging, discover `GameState` components, and push an initial state; exposed a `StateStack_Bootstrap` prefab and example state via the new package sample (`Samples~/Bootstrap`). Included regression coverage ensuring the bootstrapper registers and activates the initial state.  
   *Outcome*: Makes the package plug-and-play and showcases best practices for extending it.

6. **Queue or coalesce transitions to avoid hard failures (P1) — Completed**  
   Added a FIFO transition queue inside `StateStack` so overlapping push/pop requests are sequenced automatically; awaiting callers receive completion tasks instead of exceptions, and `WaitForTransitionCompletionAsync` now accounts for queued work. Regression coverage verifies that back-to-back `PushAsync` calls succeed without manual debouncing.  
   *Outcome*: Simplifies external code and prevents runtime exceptions in busy gameplay loops.

7. **Improve observability and debugging ergonomics (P1) — Completed**  
   Instrumented the stack with `StateStackDiagnostics`, exposing transition history and progress snapshots via `StateStackManager.Diagnostics`. Added optional logging, a runtime overlay (`StateStackDiagnosticsOverlay`), and a sample prefab wiring everything together, plus regression tests validating diagnostics history.  
   *Outcome*: Faster diagnosis, especially when onboarding teams or debugging complex flows.

8. **Rationalize the messaging surface (P1) — Completed**  
   Removed the unused `StateStackHistoryRemovedMessage`, documented each remaining message with XML summaries and strongly-named accessors, and expanded the README to describe when each event fires.  
   *Outcome*: Messaging consumers know which contracts to rely on and avoid dead code paths.

9. **Trim per-transition allocations (P2) — Completed**  
   Replaced `GetRange` copies in `StateStack.InternalRemoveAsync` with reusable buffers, pooled parallel-progress reporting in `StateGroup` via a custom aggregator backed by `ArrayPool<float>`, and removed per-call `Progress<float>`/`List<Task>` churn.  
   *Outcome*: Keeps the system responsive in projects with frequent transitions or limited platforms (Quest, mobile).

10. **Tighten async utilities around `AsyncOperation` (P2)**  
   *Problem*: `Runtime/Extensions/UnityExtensions.cs` relies on `Task.Yield()` polling, which can overschedule continuations and lacks cancellation hooks.  
   *Actions*: Use the `completed` callback or `AsyncOperationAwaiter` pattern to resume only on completion, accept an optional `CancellationToken`, and document threading expectations.  
   *Outcome*: More precise progress reporting and fewer hidden frame costs when loading scenes or assets.
