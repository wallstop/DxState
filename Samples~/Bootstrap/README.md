# State Stack Bootstrap Sample

This sample installs a ready-to-use GameObject (`StateStack_Bootstrap.prefab`) that demonstrates how to:

- Host a `StateStackManager` on a persistent object.
- Ensure a `MessagingComponent` is present so stack events emit DxMessaging notifications.
- Register `GameState` components automatically and push an initial state during startup using `StateStackBootstrapper`.

## Using the sample

1. Import the sample via **Window ▸ Package Manager ▸ DxState ▸ Samples**.
2. Drag `StateStack_Bootstrap.prefab` into your scene.
3. Add or duplicate `GameState` components as children to build your flow; the bootstrapper will discover them on Awake and optionally push the first state.
4. Use the attached `StateStackDiagnosticsOverlay` (toggle with F9 at runtime) to inspect the active stack and recent events.
5. Adjust the bootstrapper options to control registration, forced overrides, or the initial state to activate.

The included `ExampleGameState` script shows how to extend `GameState` to react to stack transitions.
