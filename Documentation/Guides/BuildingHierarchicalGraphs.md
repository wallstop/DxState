# Building Hierarchical Graphs with DxState

This quick guide walks designers and engineers through authoring a hierarchical stack with the State Graph window.

## Prerequisites
- Unity 2021.3 or later.
- The DxState package linked inside your host project.
- A project-level `StateGraphAsset` created via **Assets ▸ Create ▸ Wallstop Studios ▸ DxState ▸ State Graph**.

## 1. Open The State Graph View
1. Select the `StateGraphAsset` in the Project window.
2. Choose **Assets ▸ Wallstop Studios ▸ DxState ▸ Open State Graph View** (or double-click the asset) to launch the authoring window.
3. In the toolbar, assign the graph asset and pick a stack name (create one if the list is empty).

## 2. Add Placeholder States
You can create states three ways:
- **Context menu**: Right-click the canvas, choose **Add Template**, and select `Hierarchical Node`. This inserts a composite root and child placeholders with undo support (the default templates ship with the package).
- **Drag & drop**: Drag an existing `GameState` (MonoBehaviour) or ScriptableObject that implements `IState` into the view; DxState registers it automatically.
- **Project asset**: Use **Assets ▸ Create ▸ Wallstop Studios ▸ DxState ▸ Game State** to scaffold a runtime component, then drag it into the graph.

Each node displays its type, template metadata, and validation badges if the graph contains configuration issues.

## 3. Wire Transitions
1. Hover over a node to reveal its output port.
2. Drag to another node to create a transition.
3. Select the edge to edit the label, tooltip, transition cause, and flags. Metadata persists inside the asset so diagnostics and tooling can reuse it.
4. Use the context menu on the edge to delete or duplicate transitions. Multiple labelled transitions between the same nodes are supported.

## 4. Define The Initial State
- Right-click the desired node and choose **Set As Initial**. Validation will emit warnings if zero or multiple nodes are marked as initial.

## 5. Validate The Stack
- Press the **Validate** toolbar button (or **Assets ▸ Wallstop Studios ▸ DxState ▸ Validate State Graph**). The view highlights problem nodes/edges with severity-coloured badges, and the Console logs a full report.

## 6. Export A JSON Snapshot (Optional)
1. Select the `StateGraphAsset`.
2. Choose **Assets ▸ Wallstop Studios ▸ DxState ▸ Export State Graph JSON** and pick a destination.
3. Commit the JSON file alongside the asset to make code reviews easier.

## 7. Apply At Runtime
Use the following snippet to apply the stack to a `StateStackManager`.

```csharp
[SerializeField] private StateStackManager _manager;
[SerializeField] private StateGraphAsset _graph;
[SerializeField] private string _stackName = "Gameplay";

private async void Start()
{
    StateGraph graph = _graph.BuildGraph();
    if (graph.TryGetStack(_stackName, out StateStackConfiguration configuration))
    {
        await configuration.ApplyAsync(_manager.Stack, forceRegister: true);
    }
}
```

## Tips
- Listen to `StateGraphViewWindow.GraphAssetChanged` if you need to refresh custom editor tooling when designers mutate graphs.
- Export graphs as part of CI to lint for missing assets or mismatched causes using the JSON payload.
