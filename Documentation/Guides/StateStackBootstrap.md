# State Stack Bootstrap Sample Walkthrough

This guide explains how to use the `StateStack_Bootstrap.prefab` sample to get a playable stack up and running in minutes.

## 1. Import the Sample
1. Open **Window ▸ Package Manager**.
2. Select **DxState** and expand the **Samples** section.
3. Click **Import** next to **State Stack Bootstrap Sample**.

## 2. Drop The Prefab Into A Scene
1. Locate `Packages/com.wallstop-studios.dxstate/Samples~/Bootstrap/Prefabs/StateStack.prefab` (aliased as `StateStack_Bootstrap`).
2. Drag the prefab into an empty scene. It contains:
   - `StateStackManager` preconfigured with Beginner Setup options enabled.
   - `StateStackDiagnosticsOverlay` (toggle key `F9`) for instant visibility.
   - `StateStackFacade` UnityEvents: `OnStateChanged`, `OnStatePushed`, `OnStatePopped`.
   - Example child `GameState` components (`ExampleGameState`) demonstrating enter/exit logging.

## 3. Configure States
- Duplicate the sample `ExampleGameState` objects under the prefab to create additional flow steps.
- Alternatively, attach your own `GameState` subclasses; the manager auto-discovers children on Awake if **Register Child Game States** is ticked.
- Set the initial entry via **Beginner Setup ▸ Initial State** or enable **Push Initial State On Start** to push immediately in play mode.

## 4. Wiring UI Without Code
- Use the `StateStackFacade` events to drive UI buttons:
  - `OnStateChanged` → update on-screen labels.
  - `OnStatePushed` / `OnStatePopped` → animate menus or sounds.
- Hook up buttons to the `StateStackFacade` public methods (`PushStateByName`, `Pop`, etc.) to control the stack from UnityEvents.

## 5. Observe Diagnostics
- Enter play mode and press `F9` to show the overlay. Switch tabs to inspect stack depth, recent events, metrics, and the timeline sparkline.
- Use Pause/Step to freeze the overlay and examine the captured snapshot.

## 6. Customize Bootstrap Logic
- The prefab includes an optional `StateStackBootstrapper` script if you need legacy behaviour (manual registration list). You can remove it if the manager’s Beginner Setup covers your needs.
- Enable **Apply State Graph On Start** on the manager and assign a `StateGraphAsset` to drive the sample from authored graphs.

## 7. Extend Further
- Combine with `StateStackSnapshot.Capture` to save/restore the stack on demand (e.g., quick restart tests).
- Toggle `DXSTATE_PROFILING` and set `StateStack.ProfilingEnabled` to capture detailed transition metrics when profiling this sample.
