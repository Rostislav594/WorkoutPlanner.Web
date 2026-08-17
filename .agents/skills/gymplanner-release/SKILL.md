---
name: gymplanner-release
description: Prepare GymPlanner for a release, publication, or deployment by checking build configuration, migrations, secrets, versioning, platform settings, tests, generated artifacts, and release notes.
---

# GymPlanner release workflow

1. Identify the target platform and release type.
2. Inspect git status and current versioning.
3. Check for secrets, local databases, debug-only settings, and machine paths.
4. Restore, build, and test the relevant configuration.
5. Review pending migrations and compatibility with existing user data.
6. Verify platform manifests, permissions, icons, signing assumptions, and environment configuration.
7. Produce a concise release checklist and release notes.
8. Never claim store readiness when signing, device testing, or store validation was not performed.
