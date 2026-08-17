---
name: gymplanner-debug
description: Diagnose and fix GymPlanner bugs, exceptions, incorrect state updates, rendering problems, database failures, authentication issues, and regressions. Use when the user reports something broken or unexpected.
---

# GymPlanner debugging workflow

1. Restate the observable failure internally as a precise condition.
2. Inspect logs, stack traces, relevant files, recent changes, and data flow.
3. Reproduce when possible or construct a concrete trace when reproduction is unavailable.
4. Separate root cause from secondary symptoms.
5. Share the root cause before or alongside the fix when confidence is sufficient.
6. Apply the smallest reliable correction.
7. Add a regression test when a suitable test layer exists.
8. Run focused verification first, then broader build/tests.
9. Mention anything that remains unverified.

Do not:
- suppress exceptions without addressing the cause;
- add arbitrary delays;
- broadly refactor unrelated code;
- reset or delete user data as a first-line fix.
