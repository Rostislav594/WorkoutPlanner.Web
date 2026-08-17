---
name: gymplanner-blazor
description: Implement or modify GymPlanner Blazor and Razor UI, pages, components, forms, event handlers, state updates, navigation, and CSS. Use for .razor files and Blazor behavior; do not use for database-only work.
---

# GymPlanner Blazor workflow

1. Read the closest `AGENTS.md`.
2. Inspect the target page/component and at least one comparable existing component.
3. Trace injected services, route parameters, authentication state, and data loading.
4. Reuse existing CSS and component patterns.
5. Keep markup, state, and event logic easy to follow.
6. Prevent duplicate submissions and stale UI state.
7. Handle loading, empty, validation, and failure states where relevant.
8. Verify with the narrowest applicable `dotnet build` or test command.
9. Report changed files and any behavior not manually verified.

Avoid:
- unnecessary JavaScript interop;
- creating a new component for one trivial fragment;
- replacing existing styling with a new design system;
- broad architecture changes for a local UI task.
