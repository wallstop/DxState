# Live Diagnostics Walkthrough

DxState ships with multiple tooling layers for monitoring stacks and machines at runtime. This walkthrough shows how to enable them, capture snapshots, and export data for investigation.

## 1. Enable The Diagnostics Overlay
1. Add `StateStackDiagnosticsOverlay` to the same GameObject as `StateStackManager`.
2. Configure the toggle key (default `F9`), layout preset, and initial visibility in the inspector.
3. Enter play mode and press the toggle key to reveal the overlay:
   - **Stack tab** lists active states, pinned states, and stack depth.
   - **Events tab** shows recent transition messages with severity filters.
   - **Metrics tab** surfaces average/longest transition durations.
   - **Timeline tab** renders a sparkline of the last transitions.
4. Use **Pause** and **Step** to freeze the feed and inspect the captured snapshot.

## 2. Capture Snapshots Programmatically
You can still access history directly from the stack:

```csharp
List<StateStack.StateStackTransitionRecord> history = new List<StateStack.StateStackTransitionRecord>();
manager.Stack.CopyTransitionHistory(history);
```

Each record includes timestamp, operation, requested target, and whether events were raised.

## 3. Monitor State Machines
For localised `StateMachine<T>` instances:

```csharp
List<TransitionExecutionContext<MyState>> machineHistory = new List<TransitionExecutionContext<MyState>>();
machine.CopyTransitionHistory(machineHistory);
List<IStateRegion> activeRegions = new List<IStateRegion>();
machine.CopyActiveRegions(activeRegions);
```

Enable `DXSTATE_PROFILING` and set `StateStack.ProfilingEnabled` / `StateMachine<T>.ProfilingEnabled` during investigations to wrap transitions/updates with profiler markers.

## 4. Use The State Stack Debugger Window
1. Open **Window ▸ Wallstop Studios ▸ DxState ▸ State Stack Debugger**.
2. Select a live manager from the dropdown to push/pop states, flatten the stack, or clear it entirely.
3. The window listens to `StateGraphViewWindow.GraphAssetChanged`, so authoring edits refreshing a graph automatically update the debugger when the same asset is applied.

## 5. Export Graph Metadata
1. Select the active `StateGraphAsset`.
2. Use **Assets ▸ Wallstop Studios ▸ DxState ▸ Export State Graph JSON** to capture the blueprint alongside diagnostics output.
3. Pair the exported JSON with your captured stack snapshot for reproducible bug reports.

## 6. Automate In Tests
- Call `StateStackSnapshot.Capture(stack)` at the beginning of an integration test and `await snapshot.RestoreAsync(stack)` in teardown to reset the stack quickly.
- `StateMachineDiagnostics<T>` exposes `CopyStateMetrics` and `CopyRecentEvents` for assertions without depending on the overlay UI.

## 7. CI & Tooling Hooks
- Listen to `StateGraphViewWindow.GraphAssetChanged` in editor scripts to trigger custom validation or export flows whenever designers mutate graphs.
- Combine `CopyTransitionHistory` with your favourite logger to stream transitions into analytics dashboards or replay tools.
