# Mobile client flows

## Completed

- Authentication and registration use `/api/v1/auth/*`, opaque ASP.NET Core
  bearer tokens, refresh, SecureStorage, and a client AuthenticationStateProvider.
- Protected navigation redirects anonymous users to the mobile login page.
- Profile setup and editing use `GET/PUT /api/v1/profile` through a dedicated
  HTTP client interface and API contracts rather than EF entities.
- Password change, all-device access revocation, logout, and account deletion
  call their server endpoints before local credentials are cleared. Account
  deletion has an explicit second confirmation step.
- Training-plan list, creation, rename, deletion, and read-only exercise details
  use the protected training-plan API and public DTOs. Plan deletion has an
  explicit confirmation and never performs a local cascade.
- Exercise creation, editing, and deletion use request DTOs with one explicit
  weight and repetition value for every set. Set rows are resized deliberately;
  the mobile client performs no automatic weight or repetition progression.
- The monthly calendar loads only the visible date range and schedules,
  replaces, or removes user-owned workout days through the protected lifecycle
  API. Past dates remain read-only and completed days cannot be overwritten.
- Today's workout starts through the lifecycle API. The client keeps weight,
  repetitions, completion, and effort status explicit for every set/exercise,
  saves them through exercise DTOs, and only then asks the server to atomically
  create the immutable history snapshot and progress records.

## Boundary

The MAUI pages never accept or send a user id. The authenticated server principal
selects the current user. HTTP failures remain visible and retryable; the client
does not replace centralized server state with a local Identity database.
