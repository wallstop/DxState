# Improvement Plan

## Priority 0 – Usability & API Clarity
- [x] Collapse the two-component bootstrap flow into a single, beginner-friendly surface by folding the registration/push logic from `Runtime/State/Stack/Components/StateStackBootstrapper.cs:40-140` directly into `Runtime/State/Stack/Components/StateStackManager.cs:36-118`, with serialized lists for states and an opt-in auto-add of `MessagingComponent` so newcomers do not have to script anything before they can push a state.
- [x] Add a high-level "Beginner" facade on top of `Runtime/State/Stack/StateStack.cs` and `Runtime/State/Stack/Components/StateStackManager.cs` that exposes synchronous `PushState`, `PopState`, and `ReplaceState` helpers (wrapping the existing `ValueTask` APIs internally) plus optional `UnityEvent` callbacks, making the core API obvious to users who are unfamiliar with async flows.
- [x] Expand onboarding material (README plus a quick-start sample scene) to mirror the simplified workflow above, including a diagram of stack transitions and a glossary of `IState`, `GameState`, and diagnostics concepts so entry-level users can follow along without digging through code.

## Priority 1 – Correctness & Observability
- [x] Fix the misleading deferred-transition counter by tracking both "current" and "lifetime" metrics and emitting balanced events from `Runtime/State/Stack/StateStack.cs:626-688`, then update `Runtime/State/Stack/Diagnostics/StateStackDiagnostics.cs:123-171` to display both values so tooling reflects the actual queue depth novices expect.
- [x] Cache the DxMessaging bridge delegates created in `Runtime/State/Stack/Components/StateStackManager.cs:36-79` and unregister them inside `OnDestroy` to avoid leaking manager references and ensure diagnostics/overlays behave correctly when stacks are reloaded in play mode.
- [x] Harden `Runtime/State/Stack/Components/StateStackBootstrapper.cs:70-140` so it validates duplicate registrations and surfaces clear inspector errors (instead of silent returns) when the initial state is missing or when required dependencies are absent.

## Priority 2 – Runtime Performance & Allocations
- [x] Replace the two `Progress<float>` instances allocated in the `StateStack` constructor (`Runtime/State/Stack/StateStack.cs:87-95`) with a pooled, struct-based reporter so transition progress updates stop capturing lambdas on every stack creation.
- [x] Refactor `InternalPushAsync`, `InternalPopAsync`, `InternalFlattenAsync`, and `InternalRemoveAsync` in `Runtime/State/Stack/StateStack.cs:200-520` to eliminate the per-transition closures passed into `ExecuteStateOperationAsync` and instead route through reusable struct commands (or cached delegates) to meet the zero-closure goal.
- [x] Rework the `TransitionTask` machinery in `Runtime/State/Stack/StateStack.cs:620-707` to avoid allocating `Func` instances and `TaskCompletionSource` objects; mirror the custom `TransitionCompletionSource` pooling pattern for the queue itself.
- [x] Swap the `TaskCompletionSource` inside `Runtime/Extensions/UnityExtensions.cs:40-114` for a `ManualResetValueTaskSourceCore<bool>` (or reuse `TransitionCompletionSource`) so `AwaitWithProgress` can run without heap allocations or closure captures.
- [x] Remove the local function allocation in `Runtime/State/Stack/Components/StateStackBootstrapper.cs:92-123` by lifting it into a private helper method and pre-sizing the working lists to avoid repeated list growth during registration scans.

## Priority 3 – Feature Depth & Extensibility
- [x] Let `Runtime/State/Stack/Components/StateStackManager.cs` ingest a `StateGraphAsset` (`Runtime/State/Stack/Builder/StateGraphAsset.cs:13-112`) at runtime so designers can author stacks entirely in data without touching code, matching the usability goals.
- [x] Extend diagnostics with a beginner-friendly HUD preset that ships enabled (building on `Runtime/State/Stack/Diagnostics/StateStackDiagnostics.cs` and the overlay component) so new users immediately see what the stack is doing without extra wiring.
- [x] Provide script templates or menu items that scaffold `GameState` subclasses with the recommended overrides, reducing guesswork and keeping implementations consistent.

## Priority 4 – Test Coverage & Tooling
- [x] Add edit-mode coverage that asserts DxMessaging events fire for every transition hook (augmenting `Tests/EditMode/State/Stack/StateStackLoggingTests.cs` by injecting a test receiver) so the bridge cannot regress silently.
- [x] Cover the corrected deferred-transition metrics with explicit unit tests in `Tests/EditMode/State/Stack/Diagnostics/StateStackDiagnosticsTests.cs`, ensuring both current-depth and lifetime counts stay accurate when transitions queue and drain.
- [x] Introduce allocation-focused play-mode tests (using `UnityEngine.Profiling.Recorder` or `Unity.PerformanceTesting`) around `StateStack.PushAsync`/`PopAsync` and `UnityExtensions.AwaitWithProgress` to lock in the zero-closure, zero-GC contract.
- [x] Add regression tests for bootstrap failure scenarios (missing initial state, duplicate registrations) once the improved validation is in place so onboarding errors surface through the test suite as well as the inspector.

## Priority 5 – Robustness & Runtime Safety
- [x] Ensure every public async entry point (`Runtime/State/Stack/StateStack.cs`, `StateStackManager.cs`) guards against misuse from background threads and logs a clear error instead of proceeding, keeping behaviour deterministic for inexperienced users.
- [x] Audit and clamp stack operations that rely on `IState.Name` uniqueness (e.g., `Runtime/State/Stack/StateStack.cs:133-173`) so null or duplicate names trigger explicit exceptions with remediation hints.
- [x] Provide safe fallbacks in `Runtime/Extensions/UnityExtensions.cs` when editor-only reflection fails (lines 56-63), logging guidance instead of silently skipping progress to aid debugging on stripped player builds.
