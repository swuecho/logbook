# Repository Guidelines

## Project Structure & Module Organization
- `api/`: F# ASP.NET Core backend (`Program.fs`, `*.fsproj`), DB queries in `api/queries/` and SQL in `api/sql/`.
- `api/tests/`: xUnit test project (`unit.fsproj`, `Tests.fs`).
- `web/`: Vue 3 frontend (`src/`, `public/`) built with Rsbuild, Element Plus, and element-tiptap 2.
- `scripts/`: small Python utilities for maintenance tasks.
- Root `docker-compose.yml`: local container orchestration.

## Build, Test, and Development Commands
- Backend dev: `cd api && dotnet restore && dotnet build && dotnet watch run` (build and run with hot reload).
- Backend tests: `cd api/tests && dotnet test` (runs xUnit tests).
- Frontend dev: `cd web && yarn dev` (Rsbuild dev server with Vue 3).
- Frontend build: `cd web && yarn build` (production assets).
- Full stack (containers): `docker-compose up -d` (see `docker-compose.yml`).

## Coding Style & Naming Conventions
- F#: follow existing formatting (4-space indent, PascalCase file/module names such as `JwtService.fs`).
- Vue/JS: 2-space indent, single quotes, PascalCase component filenames (e.g., `DiaryEditor.vue`); UI via Element Plus.
- Keep new files near related modules (e.g., API routes next to `Program.fs`, UI components in `web/src/components/`).

## Frontend UI Style
- Keep the UI minimal and quiet: plain white backgrounds, thin gray borders, light hover states, and no decorative gradients, glassmorphism, or heavy shadows.
- Prefer simple structure over marketing-style sections. Use compact header bars, restrained spacing, and flat panels that feel like application chrome rather than landing-page blocks.
- Reuse shared tokens and layout helpers in `web/src/styles/ui.css` before adding page-local styling.
- Keep typography understated. Use the existing app font stack, avoid large display headings, and use muted text for secondary information.
- Actions should stay lightweight: icon buttons and text buttons should be subtle, with small radius and low-contrast hover states.
- Cards, dialogs, editors, and strips should feel consistent across pages: white surface, 1px border, small radius, minimal or no shadow.
- When polishing a page, preserve the current interaction model and information density; prefer consistency and refinement over redesign.

## Testing Guidelines
- Framework: xUnit in `api/tests/`.
- Test names use backticks and descriptive phrases (see `api/tests/Tests.fs`).
- Run via `dotnet test` or `make test` in `api/tests/`.

## Commit & Pull Request Guidelines
- Commit messages in history are short, sentence-case summaries (e.g., "Refactor date handling...") and occasional "update"; keep them concise and action-oriented.
- PRs should include a clear description, steps to verify, and screenshots for UI changes.
- Link related issues when applicable.

## Configuration & Secrets
- Frontend backend URL: set in `web/.env` or `web/.env.development`.
- Backend relies on environment variables such as `JWT_SECRET`, `JWT_AUDIENCE`, and `DATABASE_URL` (see `api/Makefile`).
- Do not commit secrets; use local env files or CI secrets.
