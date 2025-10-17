# Hierarchical State Graph Sample Walkthrough

This guide accompanies the `StateGraph` sample (available via **Package Manager ▸ DxState ▸ Samples**) and demonstrates how to inspect and run the included hierarchical state machine scene.

## 1. Import The Sample
1. Open **Window ▸ Package Manager**.
2. Select **DxState**, expand **Samples**, and click **Import** next to **State Graph**.

## 2. Open The Demo Scene
1. Navigate to `Packages/com.wallstop-studios.dxstate/Samples~/StateGraph/Scenes/StateGraph_HFSM.unity`.
2. Open the scene. You will find:
   - A `StateStackManager` configured to apply `GraphAssets/MainGameplayGraph.asset` on start.
   - UI buttons bound to `StateStackFacade` methods (`PushStateByName`, `Flatten`, etc.).
   - A diagnostics overlay (toggle key `F9`) for runtime inspection.

## 3. Inspect The Graph
1. Select `GraphAssets/MainGameplayGraph.asset` in the Project window.
2. Double-click the asset or choose **Assets ▸ Wallstop Studios ▸ DxState ▸ Open State Graph View**.
3. Explore the stacks, labelled transitions, and inspector panel:
   - Hierarchical nodes highlight active edges during play mode (when the scene runs).
   - Use **Validate** to surface inline warnings/errors.
   - Right-click nodes to set the initial state or remove entries.

## 4. Run The Scene
1. Enter play mode.
2. Click the on-screen buttons to trigger transitions (e.g., `Start Gameplay`, `Pause`, `Return To Menu`).
3. Press `F9` to toggle the overlay and observe:
   - Stack tab: active state list and pinning controls.
   - Events tab: transition feed with severity filters.
   - Timeline tab: recent transitions rendered as a sparkline.

## 5. Export Graph JSON
1. Stop play mode and select the graph asset.
2. Use **Assets ▸ Wallstop Studios ▸ DxState ▸ Export State Graph JSON** to generate a diff-friendly snapshot.
3. Pair the JSON with bug reports or code reviews.

## 6. Reset Workflow With Snapshots
- During play mode, press the console button (if included) to call `StateStackSnapshot.Capture`. Use `RestoreAsync` to jump back to the captured stack—handy when testing branches.

## 7. Profiling (Optional)
- Define `DXSTATE_PROFILING` in **Project Settings ▸ Player ▸ Scripting Define Symbols** and toggle `StateStack.ProfilingEnabled`/`StateMachine<T>.ProfilingEnabled` at runtime to wrap transitions with Unity profiler markers.

## 8. Customize
- Replace the sample `GameState` components with your own `ScriptableObject` or `MonoBehaviour` states.
- Add additional stacks to the graph and use the toolbar to switch between them in the authoring window.

The sample is intentionally lightweight so you can copy/paste it into your project or extend it to cover more complex HFSM flows.
