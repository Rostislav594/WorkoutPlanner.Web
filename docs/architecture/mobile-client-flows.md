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

## Boundary

The MAUI pages never accept or send a user id. The authenticated server principal
selects the current user. HTTP failures remain visible and retryable; the client
does not replace centralized server state with a local Identity database.
