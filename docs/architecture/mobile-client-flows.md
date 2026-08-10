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
- Workout history displays server-projected immutable snapshots, including the
  recorded value and completion state of every set. Malformed legacy snapshots
  remain visible as metadata-only archive entries and are never rewritten by
  the client. Deletion uses the ownership-checked history endpoint.
- Workout and exercise progress use server-computed percentage points rendered
  by a dependency-free SVG component from the shared Razor Class Library. The
  client can clear server-owned analytical scopes after confirmation but cannot
  submit scores or trigger automatic progression.
- Mobile onboarding is a resumable navigation tour backed by the protected
  onboarding state API. Its coach card and character are local RCL assets; the
  MAUI client does not copy the Web spotlight JavaScript or server circuit.
- Exercise photos use the MAUI system gallery or camera, validate the 5 MB and
  JPG/PNG/WebP boundary before upload, and travel only through the protected
  multipart API. Viewing and deletion also use the bearer-authenticated client;
  no server file path or public image URL is exposed to the WebView.
- Calendar entries can schedule and cancel platform-local reminders after an
  explicit permission request. Taps open only an allowlisted workout route and
  still pass through mobile authentication. Remote push remains a documented
  contract boundary with no provider SDK, endpoint, or repository secret.
- Shared character images and Material Symbols are packaged as local RCL static
  assets. Neither MAUI nor the Web shell depends on a font or icon CDN at
  runtime.

## Boundary

The MAUI pages never accept or send a user id. The authenticated server principal
selects the current user. HTTP failures remain visible and retryable; the client
does not replace centralized server state with a local Identity database.
