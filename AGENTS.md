# Repository Guidelines

## Design Philosophy
Default experiences must be simple, predictable, and easy to wire up in plain Unity scenes. All advanced behaviors (scene orchestration, nested groups, messaging fan-out) should remain optional layers on top of the core `StateStack`. Favor obvious APIs, descriptive method names, and low-ceremony setup—extended/custom integrations can live in separate helpers so new contributors see the minimal path first.

## Project Structure & Module Organization
DxState ships as a Unity 2021.3 package. Runtime code lives under `Runtime/`, which is compiled via `Runtime/WallstopStudios.DxState.asmdef`. `Runtime/State/Stack` implements the stack-driven state machine (core types: `StateStack`, `StateGroup`, `SceneState`, plus transition messages). `Runtime/State/Machine` holds component-facing glue (`StateMachine`, `StateComponent`, transitions, contexts). `Runtime/Extensions` exposes Unity helpers that integrate the stack into MonoBehaviours, while `DxMessageAwareSingleton.cs` and `SerializedMessageAwareComponent.cs` connect to `com.wallstop-studios.dxmessaging`. Keep editor utilities, tests, or samples in sibling Unity packages (`Editor/`, `Tests/`) so the runtime assembly stays clean.

## Build, Test, and Development Commands
- `dotnet tool restore` — installs the pinned local tools described in `.config/dotnet-tools.json`.
- `dotnet tool run csharpier format Runtime/**/*.cs` — formats touched C# files; run before every commit.
- `pre-commit run --all-files` — exercises the tool restore + formatter the same way CI does.
- `"<UnityEditorPath>/Unity.exe" -batchmode -projectPath "<host-project>" -runTests -testPlatform editmode -assemblyNames WallstopStudios.DxState -logFile "-"` — executes Unity EditMode tests that reference this package; swap `editmode` for `playmode` when needed.

## Coding Style & Naming Conventions
Formatting is enforced by CSharpier plus `.editorconfig`: spaces only, 4-space indentation for `.cs`, CRLF line endings, UTF-8 BOM. Prefer explicit types over `var`. Interfaces use the `ITypeName` prefix, events are PascalCase, Unity serialized fields stay camelCase. Always wrap blocks in braces and keep using directives inside the namespace. When adding APIs, match the existing namespace depth (`WallstopStudios.DxState.*`) so asmdef references stay stable.

## Testing Guidelines
The repo currently ships without automated tests, so add Unity Test Framework suites under `Tests/EditMode` or `Tests/PlayMode` inside the consuming project. Name files after the subject (`StateStackTests.cs`, `SceneTransitionModeTests.cs`) and keep methods descriptive (`Pop_RemovesTopState`). Mock `IDxMessenger` interactions with lightweight fakes. Aim for coverage on stack mutation paths (push/pop/flatten) and message dispatch so regressions surface quickly. Capture expected messaging with assertions on `TransitionStartMessage` / `TransitionCompleteMessage`. Include the Unity batchmode command output in PR discussions when adding or changing behavior.

## Commit & Pull Request Guidelines
Follow the existing history: short, imperative commit subjects (`Add pre-commit config`, `Bump actions/setup-node to 5 (#1)`), optionally referencing the tool or dependency. Squash noisy WIP commits before opening a PR. Every PR should summarize the change, link the tracked issue, note any DX messaging protocol updates, and attach Unity test logs or screenshots for behavioral/UI changes. Highlight breaking API adjustments and update `package.json` + `CHANGELOG` (once added) in the same PR.
