# Migration verification status

## Verified in the current environment

- `dotnet build WorkoutPlanner.Web.slnx -c Release --no-restore -m:1
  /nodeReuse:false` builds Web, tests, shared contracts/domain/RCL, Android
  Release, and iOS simulator Release with 0 warnings and 0 errors.
- `dotnet test WorkoutPlanner.Web.Tests/WorkoutPlanner.Web.Tests.csproj
  -c Release --no-build --no-restore` passes all 17 tests with no skips.
- `dotnet ef migrations has-pending-model-changes` reports that the current EF
  model matches the latest migration.
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
- The default development client URL uses backend HTTPS port `7196`. Builds can
  override it through configuration, environment, or an MSBuild package
  property. Android Debug uses `http://127.0.0.1:5121` and configures
  `adb reverse` after deployment so a physical device reaches the local API.
  It additionally accepts HTTP only for localhost and private or link-local IP
  addresses and enables cleartext traffic through a Debug-only manifest
  overlay for physical-device LAN testing. Release still requires an absolute
  HTTPS URI and explicitly disables cleartext traffic. Android Debug accepts
  the local `10.0.2.2` ASP.NET development certificate only when its subject
  and issuer are `CN=localhost`.

These checks were last run on 2026-08-10 through commit `1fbe63f`; Android and
iOS Release targets were run again after the final notification and navigation
fixes in `0eb8129` and `301e390`. Android's merged manifest also contains the
camera, network, notification, and boot permissions plus non-exported
notification receivers.
iOS simulator compilation validates `Info.plist` and the privacy manifest, but
does not replace a signed device archive produced on a Mac build host.

## Release boundary

The migration branch is source/build/test ready, but it is not claimed to be
store ready. A release operator must provide the public HTTPS API endpoint,
Android keystore, Apple signing team/provisioning profile, production Data
Protection key persistence, deployment database backup, and environment
credentials outside source control. `ApplicationDisplayVersion` and
`ApplicationVersion` must be advanced for each submitted package.

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
