# Repository Guidelines

## Project Structure & Module Organization
- Keep runtime code in `Runtime/`, grouped by feature domain to align with Unity assembly definitions.
- Use `Runtime/State/Machine/` for generic state-machine logic and `Runtime/State/Machine/Component/` for MonoBehaviour-driven transitions.
- Place gameplay stack utilities under `Runtime/State/Stack/`, keeping components, messages, and reusable states in the matching subfolders.
- Update `Runtime/WallstopStudios.DxState.asmdef` whenever you add new folders or external dependencies so Unity resolves assemblies correctly.
- Do not use underscores in function names, especially test function names.
- Do not use regions, anywhere, ever.
- Avoid `var` wherever possible, use expressive types.

## Build, Test, and Development Commands
- Target Unity `2021.3`; link this package via a host project’s `Packages/manifest.json` during development.
- Run edit-mode tests headlessly: `Unity -batchmode -quit -projectPath <path> -runTests -testPlatform editmode -testResults ./Artifacts/editmode.xml`.
- Export or package updates with your build method hook, e.g. `Unity -batchmode -quit -projectPath <path> -executeMethod WallstopStudios.Build.Packages.ExportDxState`.
- Replace `<path>` with the Unity project consuming this package and ensure CLI runs from your continuous integration agent.
- Do not use Description annotations for tests.
- Do not create `async Task` test methods; rely on `IEnumerator`-based Unity test methods instead.
- Do not use `Assert.ThrowsAsync` because it is unavailable.
- When checking UnityEngine.Objects for null, compare directly (`thing != null`, `thing == null`) to respect Unity's object existence rules.
- Do not use underscores in function names, especially test function names.
- Do not use regions, anywhere, ever.
- Avoid `var` wherever possible, use expressive types.

## Coding Style & Naming Conventions
- Use four-space indentation, braces on the same line for types/members, and keep `using` directives inside the namespace.
- Prefer `sealed` classes for behaviours, prefix private fields with `_`, and reserve PascalCase for public APIs; interfaces start with `I`.
- Leverage `readonly` for injected references and favor guard clauses or extension helpers (`GetOrAdd`) over verbose null checks.
- Run the IDE formatter or Unity-aware lint configuration before committing to avoid noise in diffs.
- Do not use underscores in function names, especially test function names.
- Do not use regions, anywhere, ever.
- Avoid `var` wherever possible, use expressive types.

## Testing Guidelines
- Mirror the `Runtime` layout under `Tests/EditMode/` and `Tests/PlayMode/`, naming fixtures `<Feature>Tests.cs`.
- Cover state transitions, message dispatch, and logging toggles with the Unity Test Framework (`NUnit`).
- Store supporting prefabs or scenes under `Tests/TestAssets/` to keep runtime assets clean.
- Capture Unity CLI test output (XML or console excerpt) and attach it to pull requests for reviewer context.
- Do not use regions.
- Try to use minimal comments and instead rely on expressive naming conventions and assertions.
- Do not use Description annotations for tests.
- Do not create `async Task` test methods - the Unity test runner does not support this. Make do with `IEnumerator` based UnityTestMethods.
- Do not use `Assert.ThrowsAsync`, it does not exist.
- When asserting that UnityEngine.Objects are null or not null, please check for null directly (thing != null, thing == null), to properly adhere to Unity Object existence checks.

## Commit & Pull Request Guidelines
- Write concise, imperative commit subjects (`Compilation fixes`, `Bump actions/checkout from 4 to 5`) and reference GitHub issues as `#123` when applicable.
- Describe behaviour changes, dependency bumps, and manual test evidence in the pull request body; add Editor screenshots when visuals are impacted.
- Highlight state-machine API changes or assembly definition edits so reviewers prioritise those surfaces.
- Ensure CI links or local CLI results accompany the PR before requesting review.
