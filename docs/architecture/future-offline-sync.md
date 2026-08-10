# Future offline synchronization

## Current boundary

GymPlanner Mobile is intentionally online-first. Identity, user data, history,
photos, and ownership decisions remain centralized in the ASP.NET Core backend.
The app does not copy the Identity database or the server SQLite file. Local
Preferences contain only notification metadata; SecureStorage contains only the
mobile session record.

## Proposed future architecture

Offline support should be introduced as a separate migration after device
acceptance of the online client:

1. Add a versioned local database that stores only cache DTOs required for
   read-only screens. Partition every row by a locally generated account scope,
   purge it on logout/account deletion, and decide platform encryption and backup
   policy before storing profile or workout data.
2. Add a durable outbox for explicitly supported offline commands. Each command
   carries an opaque idempotency key, resource version, creation time, attempt
   count, and payload contract version. It never carries or chooses a `UserId`;
   the server derives ownership from the refreshed bearer session.
3. Add server idempotency records and optimistic concurrency (`ETag` or an
   explicit version) before replaying mutations. Retries must not duplicate
   workout completion, history, calendar entries, or photo operations.
4. Run synchronization through a lifecycle-aware coordinator after successful
   authentication and when connectivity returns. Process commands in dependency
   order, use bounded retry with jitter, stop on authentication failure, and
   expose actionable failures instead of silently discarding edits.
5. Define conflicts per aggregate: immutable completed history is append-only;
   account deletion and session revocation always win; calendar replacement and
   plan/exercise edits require version checks and explicit user resolution when
   both sides changed. Photos use upload replacement tokens rather than merging
   file content.
6. Add schema migrations, corruption recovery, cache size/retention rules,
   telemetry without workout payloads, and integration tests for duplicate
   delivery, expired sessions, partial batches, time-zone changes, and two-device
   conflicts.

Read-only caching should ship before offline writes. Workout completion is the
last mutation to enable offline because it atomically creates history and
progress and must preserve exactly-once server semantics.
