# AGENTS.md

## Project identity

This repository contains GymPlanner / WorkoutPlanner, a workout-planning application built with .NET, Blazor, Razor components, C#, CSS, SQLite, and Entity Framework Core where already present in the repository.

Treat the existing codebase as the source of truth. Inspect current models, services, pages, components, styles, migrations, and naming conventions before making changes.

## Core working rules

- Make the smallest coherent change that fully solves the requested task.
- Do not redesign architecture unless the user explicitly requests it.
- Reuse existing services, components, CSS classes, patterns, and models before creating new ones.
- Do not add a NuGet package, JavaScript dependency, MCP dependency, or new framework without explaining why it is necessary.
- Do not silently delete, rename, or repurpose existing public members, database columns, routes, or user-visible behavior.
- Preserve backward compatibility for existing user data whenever practical.
- Never hardcode secrets, tokens, passwords, connection credentials, or machine-specific absolute paths.
- Before editing, identify the files and data flow involved.
- After editing, summarize what changed, why, and how it was verified.

## Project behavior and business rules

- Exercise templates store an individual weight for every set rather than one shared working weight.
- Do not reintroduce automatic weight progression unless the user explicitly requests it.
- Exercise cards may include exercise selection, editable name, set count, and dynamically generated weight fields for each set.
- Progress and history calculations must use actual per-set values or their sum, according to the existing feature's semantics.
- Existing progress pages should remain conceptually consistent unless a redesign is requested.
- User-specific records must remain scoped to the authenticated user.
- Account/profile functionality is a planned project capability and should fit the existing authentication architecture.

## Blazor and Razor rules

- Follow the existing render mode and component conventions.
- Keep UI markup readable; extract a component only when it meaningfully improves reuse or clarity.
- Avoid unnecessary JavaScript interop when Blazor can implement the behavior directly.
- Use asynchronous APIs for database and I/O operations where the existing architecture supports them.
- Prevent duplicate event execution and accidental double submission.
- Provide loading, empty, and error states when a user-facing operation can noticeably wait or fail.
- Preserve accessibility: use semantic controls, labels, keyboard-accessible actions, and meaningful button text.

## C# rules

- Use nullable reference types correctly.
- Prefer clear domain names over abbreviations.
- Keep methods focused and avoid hidden side effects.
- Validate externally supplied or user-entered data at the appropriate boundary.
- Do not catch exceptions only to ignore them.
- Respect the repository's current dependency-injection and service-lifetime patterns.
- Avoid static mutable state for user or session data.
- Use cancellation tokens for potentially long-running operations when supported by the surrounding code.

## Database and migration rules

- Inspect the current DbContext, entities, mappings, indexes, services, and migrations before changing persistence.
- Never edit an already-applied migration merely to change production schema history. Create a new migration.
- A schema change must include all required model, mapping, migration, service, and validation updates.
- Preserve existing rows and define a deliberate migration path for new required fields.
- Add indexes only when they match real query patterns.
- Avoid loading entire tables when filtering, projection, or pagination can be used.
- Confirm that user-owned data queries filter by UserId or the equivalent ownership key.

## UI and CSS rules

- Match the application's established visual language: typography, spacing, border radii, shadows, controls, and card styles.
- Reuse CSS variables and existing classes where possible.
- Keep pages responsive for narrow mobile screens and desktop layouts.
- Avoid inline styles unless the current component pattern specifically requires them.
- Do not introduce a new color system or component library for a single feature.
- New controls must have coherent hover, focus, active, disabled, validation, and loading states where relevant.

## Debugging workflow

When fixing a bug:

1. Reproduce or trace the failure before editing.
2. Identify the root cause rather than masking the symptom.
3. Check related data flow, lifecycle, state update, database, and rendering behavior.
4. Apply the smallest reliable fix.
5. Add or update a regression test when the repository has a suitable test layer.
6. Verify that adjacent flows still work.

Do not make broad speculative refactors while fixing a focused bug.

## Refactoring workflow

- Preserve observable behavior unless behavior change is requested.
- Refactor in reviewable steps.
- Remove duplication only after confirming the duplicated paths are semantically equivalent.
- Do not rename many files or symbols merely for aesthetics.
- Keep database migrations and behavioral changes separate from unrelated formatting where practical.

## Verification

Use the solution and project files that actually exist in the repository. Typical checks may include:

- `dotnet restore`
- `dotnet build`
- `dotnet test`

Run the narrowest useful checks first, then broader checks when the change warrants them. If a command cannot run because of environment limitations, state exactly what was not verified.

## Git safety

- Inspect `git status` before broad changes.
- Do not discard unrelated user changes.
- Do not force-push, rewrite shared history, or delete branches without explicit instruction.
- Do not commit secrets, local databases, generated build output, or machine-specific configuration.
- Keep commits focused when the user asks Codex to commit.

## Definition of done

A task is complete only when:

- The requested behavior is implemented.
- The solution is consistent with current architecture and styling.
- Persistence and migrations are correct when data changes.
- Relevant build/tests/checks were run or limitations were disclosed.
- No unrelated functionality was intentionally changed.
- The final response lists changed files, verification performed, and any remaining risk.
