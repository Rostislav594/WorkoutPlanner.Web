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

Access tokens expire after 15 minutes. Refresh tokens expire after seven days
and are rejected when expired or when Identity security-stamp validation fails.
Production deployments must persist and protect the Data Protection key ring and
must expose the API only through HTTPS.

## Implemented flow

1. `POST /api/v1/auth/register` creates the Identity user and personal starter
   plans. A failed plan provision attempts to roll back the new Identity user.
2. `POST /api/v1/auth/login` validates credentials with lockout enabled and
   returns the standard access/refresh token response.
3. `POST /api/v1/auth/refresh` validates expiry and the security stamp before
   issuing replacement tokens.
4. `/api/v1/profile` requires the Identity bearer scheme and never reads a user
   identifier from the request body.

## Remaining session work

The MAUI client will keep tokens only in platform SecureStorage and discard them
on logout. Server-side per-device revocation is not represented by the built-in
opaque token format. Before remote push/device registration is introduced, add
an explicit user-device/session model if selective per-device revocation is a
requirement. Password change and account deletion will update the Identity
security stamp so refresh credentials are invalidated; short-lived access tokens
bound the remaining exposure window.
