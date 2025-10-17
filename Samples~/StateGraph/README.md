# State Graph Authoring Sample

This sample demonstrates how to author and apply hierarchical state graphs with DxState.

## Contents
- `Scenes/StateGraph_HFSM.unity`: Minimal setup with a `StateStackManager` referencing a `StateGraphAsset` that drives a menu ↔ gameplay flow.
- `GraphAssets/MainGameplayGraph.asset`: Shows multiple stacks, labelled transitions, and validation hints.
- `Scripts/States/` contains a few example `GameState` components used by the graph.

## Usage
1. Import the sample via **Window ▸ Package Manager ▸ DxState ▸ Samples**.
2. Open `Scenes/StateGraph_HFSM.unity`.
3. Enter play mode and press `F9` to reveal the diagnostics overlay; use the on-screen buttons to trigger transitions defined in the graph.
4. Open `GraphAssets/MainGameplayGraph.asset` to inspect the graph in the authoring window. Use the **Validate** toolbar button to see inline issue badges.
5. Export the graph to JSON via **Assets ▸ Wallstop Studios ▸ DxState ▸ Export State Graph JSON** and review the diff-friendly layout.

The scene also includes a `StateStackSnapshot` console command that captures the current stack and restores it when pressed, showcasing the new snapshot utilities.
