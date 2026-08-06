# Mobile workout lifecycle

The mobile lifecycle remains online-first and uses server state only:

1. `POST /api/v1/calendar` schedules or replaces the authenticated user's plan
   for a date. A plan owned by another user is returned as `404 Not Found`.
2. `GET /api/v1/workouts/today` and `POST /api/v1/workouts/today/start` return
   the current incomplete scheduled plan. Start is intentionally idempotent; no
   separate client-owned session or local Identity database is created.
3. The client saves exercise status and individual set values through the
   exercise API while the workout is in progress.
4. `POST /api/v1/workouts/today/complete` validates the server-side plan and
   atomically claims the calendar day, writes history and progress, and marks
   the day complete. A second completion returns `409 Conflict`.

Calendar, today-workout, and history routes all use the mobile-session policy.
Identifiers are filtered against the authenticated server user, and another
user's calendar or history item is indistinguishable from a missing item.

This stage introduces no schema migration. The normalized training-session
tables remain untouched until an explicit analytics dual-write and compatibility
plan is implemented.
