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
Identity security-stamp validation fails.
Production deployments must persist and protect the Data Protection key ring and
must expose the API only through HTTPS.

## Implemented flow

1. `POST /api/v1/auth/register` creates the Identity user and personal starter
   plans. A failed plan provision attempts to roll back the new Identity user.
2. `POST /api/v1/auth/login` validates credentials with lockout enabled, creates
   a per-device session, and returns the standard access/refresh token response.
3. `POST /api/v1/auth/refresh` validates expiry and the security stamp before
   issuing replacement tokens for an active session.
4. `POST /api/v1/auth/logout` revokes only the current mobile session.
5. `POST /api/v1/account/revoke-access` revokes every mobile session for the
   authenticated user without signing out the existing web cookie.
6. `POST /api/v1/account/change-password` changes the Identity password and
   revokes all mobile sessions. `DELETE /api/v1/account` uses the transactional
   account-deletion service and removes the session rows with the account.
7. Protected endpoints require the mobile-session authorization policy and
   never read a user identifier from the request body.

## Client responsibilities

The MAUI client will keep tokens only in platform SecureStorage and discard them
on logout, password change, account deletion, or failed refresh. It must attempt
a refresh before access-token expiry and return to authentication when refresh is
rejected. The optional device name is diagnostic metadata only and is trimmed to
120 characters; it is never used as an authorization decision.

The migration that introduces `MobileSessions` is additive. It creates a new
table and indexes with a cascading foreign key to `AspNetUsers`; it does not
rewrite or delete existing history, plans, profiles, or Identity rows.
