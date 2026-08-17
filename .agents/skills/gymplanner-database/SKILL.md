---
name: gymplanner-database
description: Change GymPlanner SQLite or Entity Framework Core persistence, entities, DbContext mappings, queries, indexes, services, and migrations. Use for schema or data-access tasks; do not use for UI-only styling.
---

# GymPlanner database workflow

1. Inspect the current entity, DbContext, mapping, related service, queries, and latest migrations.
2. Identify whether the change is schema, query, validation, or business logic.
3. Preserve existing user data and ownership boundaries.
4. Create a new migration for schema changes; never rewrite applied migration history.
5. Update all affected layers together: entity, mapping, service, migration, validation, and tests.
6. Confirm user-owned queries filter by the authenticated user's identifier.
7. Prefer projection/filtering over loading unnecessary rows.
8. Run applicable build and database tests.
9. Explain migration impact and rollback risk.

Project rule:
- Exercise set weights are stored individually per set.
- Do not reintroduce one shared working weight or automatic progression without explicit instruction.
