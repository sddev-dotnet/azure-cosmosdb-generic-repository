# Repository Guidelines

## Project Structure & Module Organization
Core library `SDDev.Net.GenericRepository` holds Cosmos abstractions—key folders include `CosmosDB` for repository logic, `Caching` and `Indexing` for opt-in behaviors, and `Utilities` for shared helpers. Contracts and base entity definitions live in `SDDev.Net.GenericRepository.Contracts`. `SDDev.Net.GenericRepository.Tests` hosts MSTest suites with supporting `TestModels`. The runnable sample in `SDDev.Net.GenericRepository.Example` demonstrates integration patterns. Solution file `SDDev.Net.GenericRepository.sln` orchestrates all projects.

## Build, Test, and Development Commands
- `dotnet restore SDDev.Net.GenericRepository.sln` pulls NuGet dependencies.
- `dotnet build SDDev.Net.GenericRepository.sln` compiles all projects and surfaces compiler warnings.
- `dotnet test SDDev.Net.GenericRepository.Tests/SDDev.Net.GenericRepository.Tests.csproj --configuration Release` runs MSTest suites; add `--filter TestCategory=...` to target subsets.
- `dotnet run --project SDDev.Net.GenericRepository.Example` executes the sample app; configure Cosmos settings beforehand.

## Coding Style & Naming Conventions
Use C#/.NET defaults: four-space indentation, braces on new lines, `PascalCase` for public classes, interfaces, and methods, and `camelCase` for locals and private fields (prefix `_` for private readonly). Favor async method suffixes (`DoWorkAsync`), guard clauses for argument validation, and expression-bodied members where they aid clarity. Keep `using` directives sorted and remove unused imports; run `dotnet format` locally before opening a PR.

## Testing Guidelines
Tests rely on MSTest attributes (`[TestClass]`, `[TestMethod]`) and follow a `WhenScenario_ThenResult` naming pattern. Mirror new repository behaviors with Arrange-Act-Assert structure and prefer in-memory fakes over live Cosmos connections. Treat `appsettings.json` as a template only; override secrets via environment variables. Extend coverage for new public APIs and run `dotnet test /p:CollectCoverage=true` when validating broader changes.

## Commit & Pull Request Guidelines
Write concise, present-tense commit subjects (`Add Cosmos patch helpers`) and include rationale in the body when touching multiple components. For PRs, describe the scenario, summarize functional impacts, list new commands/config, and reference GitHub issues. Attach test evidence (command output or screenshots) and flag breaking changes or migration steps.

## Security & Configuration Tips
Never commit real Cosmos or Azure Search keys; use the provided examples for local testing and inject secrets via user secrets or environment variables. Review logging for accidental data leakage before merging, and scrub captured `TestResults` prior to publishing artifacts.
