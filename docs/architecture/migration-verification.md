# Migration verification status

## Verified in the current environment

- `WorkoutPlanner.Web`, shared contracts/domain/RCL, Android, and iOS simulator
  targets compile without warnings or errors.
- The automated test suite covers per-user starter plans, ownership isolation,
  per-set weights, immutable history, atomic completion, progress, onboarding,
  protected photos, token revocation, and transactional account deletion.
- `dotnet list WorkoutPlanner.Web.slnx package --vulnerable --include-transitive`
  reported no known vulnerable direct or transitive packages from NuGet.org on
  2026-08-10.
- MAUI references Domain, API.Contracts, and UI but not Web. It uses
  `blazor.webview.js`, has no Interactive Server render mode, no server database,
  and no external font/icon CDN.
- Native Back events are bridged to Razor route history without JavaScript;
  window resume triggers token-expiry validation through the authentication
  service. Duplicate Back and resume events are guarded.
- Safe-area insets are owned once by each layout, fixed navigation includes the
  bottom inset, compact-height authentication scrolls, and Android requests
  `AdjustResize` so the software keyboard reduces the WebView viewport.
- API DTOs contain no trusted `UserId`; protected endpoint groups require the
  mobile-session bearer policy. EF entities and navigation graphs remain inside
  Web.
- The development client URL uses backend HTTPS port `7196`. Device/production
  builds can override it through configuration, environment, or an MSBuild
  package property; only an absolute HTTPS base URI is accepted. Certificate
  validation is not bypassed.

## Device acceptance still required

The Windows build host cannot replace real Android/iOS acceptance testing. Run
the following before a production release:

- install a certificate trusted by the emulator/device and configure an HTTPS
  endpoint reachable from it;
- verify registration, token refresh/expiry, logout, password change, access
  revocation, and account deletion against a non-production database;
- verify camera/gallery selection, permission denial, cancellation, 5 MB/type
  validation, network loss, protected viewing, replacement, and deletion;
- verify notification permission, delivery, cancellation, foreground display,
  and tap navigation on supported Android and iOS versions;
- verify safe areas, keyboard overlap, system Back behavior, pause/resume,
  process recreation, narrow/tablet sizes, both orientations, and accessibility;
- verify the Web UI and Material Symbols with the network disabled;
- verify signed Release builds, production entitlements, package identifiers,
  privacy declarations, and store-specific requirements.

Android reminders survive process termination and are restored from a private,
versioned index after reboot or app replacement. Device delivery remains an
acceptance gate. Remote push and full offline sync are deliberately future
stages; no provider secret or local Identity database has been added.
