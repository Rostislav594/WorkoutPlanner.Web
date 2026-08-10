# Mobile authentication

## Current decision

The web UI continues to use the ASP.NET Core Identity application cookie. Mobile
API endpoints use the built-in ASP.NET Core Identity bearer-token handler through
`AddIdentityApiEndpoints`. These are opaque Data Protection tokens, not custom
JWTs. No signing key, application secret, or production credential is stored in
the repository.

The API accepts bearer authentication only. A cookie authenticated browser does
not implicitly become an authenticated mobile API client. User ownership is
derived from the validated server principal; API contracts never accept a
trusted `UserId`.

Access tokens expire after 15 minutes. Refresh tokens expire after seven days.
Each login also creates a server-side `MobileSession` whose identifier is stored
as a protected claim in both tokens. Protected API requests and refreshes are
rejected when the session is expired, revoked, belongs to another user, or when
Identity security-stamp validation fails. Production deployments must persist
and protect the Data Protection key ring and expose the API only through HTTPS.

## Server flow

1. `POST /api/v1/auth/register` creates the Identity user and personal starter
   plans. A failed plan provision attempts to roll back the new Identity user.
2. `POST /api/v1/auth/login` validates credentials with lockout enabled, creates
   a per-device session, and returns the access/refresh token response.
3. `POST /api/v1/auth/refresh` validates expiry and the security stamp before
   issuing replacement tokens for an active session.
4. `POST /api/v1/auth/logout` revokes only the current mobile session.
5. `POST /api/v1/account/revoke-access` revokes every mobile session for the
   authenticated user without signing out the existing web cookie.
6. `POST /api/v1/account/change-password` changes the Identity password and
   revokes all mobile sessions. `DELETE /api/v1/account` uses the transactional
   account-deletion service and removes session rows with the account.
7. Protected endpoints require the mobile-session authorization policy and
   never read a user identifier from the request body.

The migration that introduced `MobileSessions` is additive. It creates a new
table and indexes with a cascading foreign key to `AspNetUsers`; it does not
rewrite or delete existing history, plans, profiles, or Identity rows.

## Implemented MAUI client flow

The mobile client stores one serialized authentication record in MAUI
`SecureStorage`. It contains the access token, refresh token, access-token
expiry, and the email needed to restore local `AuthenticationState`. Android
application backup is disabled so encrypted preferences cannot be restored
without their device-bound encryption key. No token is written to configuration,
logs, source control, or ordinary preferences.

Before an authenticated request, the client refreshes an access token that has
less than one minute remaining. A rejected refresh, a 401 response, or a 403
response clears local authentication. Logout revokes the current server-side
device session before clearing SecureStorage. If the network is unavailable,
logout leaves the local credentials in place so revocation can be retried rather
than silently leaving an active server session. The optional device name is
diagnostic metadata only and is never used as an authorization decision.

Development defaults match the backend HTTPS launch profile:
`https://10.0.2.2:7196` for the Android emulator and
`https://localhost:7196` for the iOS simulator. Certificate validation is never
disabled. Physical devices and production packages require a trusted public
HTTPS endpoint. Supply it with `MobileApi:BaseAddress`, the
`GYMPLANNER_API_BASE_ADDRESS` environment variable, or at package build time:

```powershell
dotnet publish GymPlanner.Mobile/GymPlanner.Mobile.csproj `
  -f net10.0-android -c Release `
  -p:GymPlannerApiBaseAddress=https://api.example.com/
```

The client rejects non-HTTPS addresses, embedded credentials, query strings,
and fragments. The endpoint is configuration, not a secret; production
credentials and certificate private keys remain outside the repository.
