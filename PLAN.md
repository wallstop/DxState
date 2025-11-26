# Repository Improvement Plan

## Dimension Summary
- **Correctness** – Misapplied tick conditions (`StateGroup.cs:299-343`) and incorrect delta usage (`StateStack.cs:433-450`) block runtime logic; state-machine logging and transition tables (`StateMachine.cs:18-66`) can throw or misreport state history; no automated tests exist to detect regressions.
- **Performance** – State updates are skipped or supplied with the wrong delta, leading to inconsistent physics/animation timing; registration lookups repeatedly allocate strings/dictionaries without caching and duplicates silently overwrite, creating hard-to-diagnose spikes.
- **Ease of Use** – There is no “happy path” tutorial, sample scene, or editor UI, so new users must digest the advanced stack/messaging APIs just to push a state; errors such as missing states throw opaque runtime exceptions instead of surfacing during authoring.
- **Ease of Understanding** – The public API spans stack, machine, and messaging layers but documentation is limited to README + AGENTS; there is no architecture guide, sequence diagrams, or naming guidance for concrete states, making simple onboarding difficult.
- **Maintainability** – Lack of CI/tests, no changelog, and no analyzer configuration mean regressions ship unnoticed; event subscriptions in `StateStackManager` never unsubscribe, and error handling/logging is inconsistent.

## Prioritized Action Plan
1. **Fix critical ticking defects (P0, correctness/performance)**  
   - In `Runtime/State/Stack/States/StateGroup.cs:299-343`, invert the `TickWhenInactive` gate so active groups tick; add unit tests covering sequential and parallel groups.  
   - In `Runtime/State/Stack/StateStack.cs:433-450`, pass `Time.deltaTime` for Update/LateUpdate and `Time.fixedDeltaTime` only for FixedUpdate. Cover with tests asserting tick deltas.

2. **Harden `StateMachine<T>` transitions (P0, correctness/maintainability)**  
   - Ensure `_states` contains an entry for every `Transition.to` and the initial state; guard `Update` when no transitions exist to avoid `KeyNotFoundException` (lines 18-47).  
   - Capture `previousState` before assignment so the log in `TransitionToState` (lines 50-66) accurately reports from/to.  
   - Add editor-unit tests using lightweight mock `StateComponent` contexts to lock in the behavior.

3. **Improve stack registration and diagnostics (P1, correctness/ease-of-use)**  
   - Add null/empty-state guards in `TryRegister`, `PushAsync`, and `FlattenAsync` to fail fast with descriptive errors.  
   - When `TryRegister` is called twice with the same name (without `force`), emit a `Debug.LogWarning` so collisions are visible instead of silently reusing the previous instance.  
   - Emit structured logs (or Dx messages) whenever transitions are blocked because `_isTransitioning` is already true.

4. **Codify a simple default workflow (P1, ease-of-use/understanding)**  
   - Ship a pared-down `SimpleStateStack` prefab + sample scene that demonstrates pushing/popping states without DxMessaging so hobbyists can drop it into any project.  
   - Write a “5-minute setup” guide that walks through adding `StateStackManager`, registering two `GameState` subclasses, and driving transitions via inspector buttons.  
   - Gate advanced features (Dx messages, nested groups, SceneState orchestration) behind optional components so the default namespace stays minimal and approachable.

5. **Introduce automated validation (P1, maintainability)**  
   - Add Unity EditMode test assemblies (`Tests/EditMode`) that simulate push/pop/flatten/remove flows, StateGroup ticking, SceneState revert paths, and TimeState stack interactions.  
   - Wire tests into GitHub Actions using `game-ci/unity-runner` so PRs gate on EditMode (and future PlayMode) suites.  
   - Replace the placeholder `npm test` script with documentation on invoking `unity -batchmode -runTests`.

6. **Tighten lifecycle & messaging hooks (P2, correctness/maintainability)**  
   - Unsubscribe the lambdas attached in `StateStackManager.Awake` during `OnDestroy` to prevent leaks when the singleton reloads in edit mode.  
   - Allow consumers to opt into scoped subscriptions or override emit logic so multiple stacks can coexist in tests.  
   - Provide structured events (duration, cause) alongside the existing Dx messages for easier telemetry.

7. **Add developer-facing tooling and samples (P2, ease-of-use/understanding)**  
   - Create a sample scene showing `StateStackManager`, `SceneState`, and `TimeState` working together; include prefabs/setups under `Samples~/BasicStack`.  
   - Supply ScriptableObject templates or Odin drawers for configuring state graphs in the inspector, reducing manual wiring.

8. **Document the architecture (P3, understanding)**  
   - Author an `ARCHITECTURE.md` describing the stack lifecycle, messaging flow, and recommended patterns for implementing states.  
   - Expand `README.md` to include quick-start steps, dependency requirements (`com.wallstop-studios.dxmessaging`, Unity 2021.3), and troubleshooting for common exceptions.

9. **Establish release hygiene (P3, maintainability)**  
   - Add a `CHANGELOG.md`, semantic versioning policy, and CONTRIBUTING pointers to formatter/test commands.  
   - Configure analyzers (e.g., `dotnet_format`, Roslynator) and enforce via pre-commit to keep style drift low.
