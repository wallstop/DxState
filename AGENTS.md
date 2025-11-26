# Repository Guidelines

## Design Philosophy
Default experiences must be simple, predictable, and easy to wire up in plain Unity scenes. Ship the minimal `StateStack` path first; scene orchestration, nested groups, and DxMessaging broadcasts stay optional layers. Favor descriptive APIs, zero-reflection patterns, and low-ceremony setup so newcomers can follow the code without consulting docs.

## Project Structure & Module Organization
- `Runtime/State/Stack`: Stack primitives (`StateStack`, `StateGroup`, `SceneState`, tick modes, Dx messages).
- `Runtime/State/Machine`: Component-driven state machine helpers (`StateComponent`, `StateMachine<T>`, transitions, contexts).
- `Runtime/Extensions`: Unity helpers (async progress, MonoBehaviour utilities).
- `Runtime/DxMessageAwareSingleton.cs` & `SerializedMessageAwareComponent.cs`: Bridges into `com.wallstop-studios.dxmessaging`.
- Keep editor tooling, samples, and tests in sibling packages (`Editor/`, `Tests/`, `Samples~/`) to avoid bloating the runtime assembly.

## Build, Test, and Development Commands
- `dotnet tool restore` — installs the pinned local tools defined in `.config/dotnet-tools.json`.
- `dotnet tool run csharpier format Runtime/**/*.cs` — format C# before every commit (pre-commit calls this automatically).
- `pre-commit run --all-files` — exercises hooks exactly how CI runs them.
- Unity tests (once added): `"<UnityEditorPath>/Unity.exe" -batchmode -projectPath "<host>" -runTests -testPlatform EditMode -assemblyNames WallstopStudios.DxState -logFile "-"`.

## Coding Style & Naming Conventions
- `.editorconfig` + CSharpier govern style: 4 spaces for C#, 2 for JSON/YAML/asmdefs, CRLF endings, UTF-8 BOM. No tabs.
- Prefer explicit types instead of `var`; interfaces prefixed `I`, type parameters `TName`, events `OnEventName`, Unity serialized fields camelCase.
- Always wrap blocks in braces, keep `using` statements inside namespaces, and never use `#region`.
- Avoid nullable reference types, underscores in method names, and reflection unless absolutely necessary (document any unavoidable cases).

## Reflection & API Access
- Rely on explicit APIs instead of runtime reflection; use `internal` + `InternalsVisibleTo` when tests or editors need deeper access.
- Centralize identifier strings (e.g., state names) via `nameof` or constants; only serialize raw strings when Unity requires it.
- If reflection is unavoidable (Unity serialization hooks, third-party glue), keep it quarantined with clear comments explaining the constraint.

## Testing Guidelines
- Target Unity Test Framework (NUnit attributes) and mirror runtime folders for future `Tests/EditMode` and `Tests/PlayMode`.
- Name files `*Tests.cs` and methods with behavior-focused descriptions (`PopAsync_RemovesTopState`), avoiding `async Task` tests—prefer `IEnumerator` for `[UnityTest]`.
- Keep tests deterministic and fast; fake `IDxMessenger` interactions rather than spinning up the full DxMessaging stack.
- Include Unity batchmode logs/screenshots in PRs when behavior changes to prove the simple/default flows still work.

## Commit & Pull Request Guidelines
- Use short, imperative commits (`Fix StateStack flatten progress`, `Bump CSharpier to 1.2.1`) and squash noisy WIP history.
- PRs must describe the change, link issues, call out DX messaging or API surface updates, and attach relevant test output or GIFs.
- Update `package.json` + future `CHANGELOG.md` whenever the runtime API or version changes; keep docs and samples in sync.

## Security & Configuration Tips
- Keep `.meta` files committed, but never commit `Library/`, `obj/`, or secrets. Unity target version is 2021.3—verify asmdef references when adding namespaces.
- Dependabot runs daily for GitHub Actions, npm, and .NET tools; review and merge its PRs promptly.
- CI formatting uses `dotnet tool run csharpier -- check .`; ensure local hooks run cleanly before pushing.

## Agent-Specific Notes
- Scope edits to the relevant folder (mostly `Runtime/`); keep the default developer experience easy to grok and document any added complexity.
- When extending functionality, provide a basic sample or doc snippet showing how to use it without advanced setup.
