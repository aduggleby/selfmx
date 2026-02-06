# Repository Guidelines

## Project Structure & Module Organization
- `src/SelfMX.Api/` contains the .NET 9 backend (minimal APIs, services, jobs, data, auth).
- `tests/SelfMX.Api.Tests/` holds xUnit unit tests (C# files like `DomainService.Tests.cs`).
- `client/` is the React 19 + Vite frontend; UI code in `client/src/` and E2E tests in `client/e2e/`.
- `website/` hosts the Astro docs site.
- `deploy/` includes installer and production compose files; `docker-compose.dev.yml` is for local SQL Server.
- Root build artifacts: `build.csando`, `ando-pre.csando`, `Dockerfile`, `SelfMX.slnx`, `mise.toml`.

## Build, Test, and Development Commands
- `mise install` installs the pinned .NET 9 toolchain.
- `docker compose -f docker-compose.dev.yml up -d` starts the local SQL Server on port `17402`.
- `dotnet run --project src/SelfMX.Api` runs the API on port `17400`.
- `dotnet build SelfMX.slnx` builds the backend solution.
- `dotnet test SelfMX.slnx` runs all xUnit tests.
- `cd client && npm install && npm run dev` starts the Vite dev server on port `17401`.
- `cd client && npm run build` performs TypeScript checks and a production build.
- `ando` runs the full Ando build (backend + frontend + tests) in Docker.

## Coding Style & Naming Conventions
- Match existing formatting; C# uses 4-space indentation, TypeScript/React uses 2 spaces.
- Use `SelfMX` casing for namespaces, project names, and user-facing text.
- Public C# types/methods use `PascalCase`; locals and fields use `camelCase`.
- Prefer explicit, descriptive names in APIs and services (e.g., `DomainService`, `VerifyDomainsJob`).
- Linting: `cd client && npm run lint` (ESLint). Keep the client clean before PRs.

## Testing Guidelines
- Backend tests: xUnit in `tests/SelfMX.Api.Tests/` with filenames like `*Tests.cs` or `*.Tests.cs`.
- Frontend E2E: Playwright specs in `client/e2e/*.spec.ts`.
- Run targeted tests with `dotnet test --filter "FullyQualifiedName~DomainService"`.

## Commit & Pull Request Guidelines
- Commits follow a Conventional-Commits style: `feat:`, `fix(ui):`, `docs:`, `chore:`, `refactor(api):`.
- Version bumps use `Bump version to X.Y.Z`.
- PRs should include a clear summary, testing performed, and screenshots for UI changes.
- Link related issues and note any config changes (ports `17400-17402`, new env vars).

## Configuration Notes
- SQL Server is the only supported database; see `appsettings.json`/env vars for `ConnectionStrings:DefaultConnection`.
- CORS and admin UI defaults rely on `App:Fqdn`; local dev assumes `http://localhost:17401`.
