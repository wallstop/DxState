# State Graph Builder Sample

This sample demonstrates how to assemble multiple `StateStack` configurations programmatically using `StateGraphBuilder`.

## Included Assets

- `StateGraphBootstrap.prefab` – hosts a `StateStackManager` along with a `StateGraphBootstrap` MonoBehaviour that applies a graph during `Awake`.
- `States/` – contains starter `GameState` components used by the bootstrap script.
- `Scripts/StateGraphBootstrap.cs` – builds a `StateGraph`, applies one of the stack configurations to the manager, and exposes inspector knobs for experimentation.

## Trying the Sample

1. Import the sample via **Window ▸ Package Manager ▸ DxState ▸ Samples**.
2. Open the `StateGraphSample` scene (or drag the prefab into an empty scene).
3. Enter Play Mode and use the exposed buttons to push/flatten/remove states defined in the graph.
4. Inspect the `StateStackManager` component at runtime—its custom inspector lists the active stack and registered states.
5. Modify `StateGraphBootstrap` to add additional states or stacks, then reapply the configuration at runtime.

The sample intentionally keeps states lightweight so you can focus on how the builder wires stacks together. Swap in your own state implementations to see how larger graphs behave.
