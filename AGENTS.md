# Repository Guidelines

## Project Structure & Module Organization
- This is a .NET 9 console app. Current layout:
  - `Program.cs` – entry point
  - `Bot.csproj` – project configuration
  - `obj/` – build artifacts (do not edit)
- When expanding, prefer:
  - `src/` for application code (`src/Bot/*`)
  - `tests/` for test projects (e.g., `tests/Bot.Tests`)

## Build, Test, and Development Commands
- Restore dependencies: `dotnet restore`
- Build (Debug/Release): `dotnet build -c Debug` | `dotnet build -c Release`
- Run locally: `dotnet run` (from repo root)
- Create test project (once): `dotnet new xunit -o tests/Bot.Tests && dotnet sln add tests/Bot.Tests` (if using a solution)
- Run tests: `dotnet test`
- Format code: `dotnet format` (checks and fixes style)

## Coding Style & Naming Conventions
- Language: C# with `Nullable` and `ImplicitUsings` enabled.
- Indentation: 4 spaces; max line length ~120 characters.
- Naming:
  - Types, methods, properties: PascalCase
  - Locals, parameters: camelCase
  - Private fields: `_camelCase`
  - Async methods: suffix `Async`
- Use expression-bodied members for simple accessors; prefer `var` when the type is obvious.
- Keep files small and cohesive; one public type per file under `src/`.

## Testing Guidelines
- Framework: xUnit recommended (`tests/Bot.Tests`).
- Conventions: mirror namespaces and folder structure; name files `XyzTests.cs`.
- Coverage: target ≥ 80%. Example: `dotnet test --collect:"XPlat Code Coverage"`.
- Write unit tests for public behavior; avoid testing implementation details.

## Commit & Pull Request Guidelines
- Use Conventional Commits:
  - `feat:`, `fix:`, `chore:`, `docs:`, `test:`, `refactor:`, `build:`
  - Scope optional, e.g., `feat(core): add scheduler`.
- Commits: small, logical changes with clear messages.
- PRs: include a concise description, rationale, and any screenshots/logs; link related issues; check CI (build, tests, format) passes.

## Security & Configuration Tips
- Do not commit secrets. Prefer environment variables or `dotnet user-secrets` for local dev.
- Review dependencies during updates; keep the target framework (`net9.0`) current.
- Validate input and handle failures; enable nullable annotations to prevent null bugs.

