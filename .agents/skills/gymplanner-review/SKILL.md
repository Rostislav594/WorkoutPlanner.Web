---
name: gymplanner-review
description: Review GymPlanner code changes for correctness, regressions, security, data integrity, Blazor lifecycle issues, database migration safety, maintainability, and project-rule violations. Use for diffs, pull requests, or pre-commit review.
---

# GymPlanner code review workflow

Review in this priority order:

1. Data loss, authorization, secret exposure, and destructive behavior.
2. Incorrect business rules, especially per-set weight handling and user scoping.
3. Database migration safety and query correctness.
4. Blazor lifecycle, stale state, duplicate events, and async issues.
5. Regression risk and missing tests.
6. Performance problems with meaningful user impact.
7. Maintainability and consistency with existing architecture.
8. Style only when it affects clarity or correctness.

For every finding provide:
- severity;
- exact file/location;
- why it matters;
- a concrete safe fix.

Do not invent findings. State when no material issue is found and mention verification gaps.
