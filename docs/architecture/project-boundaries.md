# Project boundaries

`WorkoutPlanner.Domain` contains framework-independent workout calculations.
It has no ASP.NET Core, EF Core, Identity, UI, or filesystem dependency. Domain
inputs explicitly carry each set's own weight and repetitions.

`WorkoutPlanner.Api.Contracts` contains the mobile HTTP request and response
DTOs. It has no reference to the web project or EF entities and is consumed by
both ASP.NET Core endpoints and the MAUI client.

`WorkoutPlanner.Web` remains the composition root and owner of ASP.NET Core
Identity, EF Core entities and migrations, SQLite, server implementations,
files, web authentication, and API authorization. Moving the contracts does not
move the database or server runtime into a client project.

Only independent calculations were moved to Domain. Persistence entities and
their navigation properties intentionally remain in Web.

`WorkoutPlanner.UI` is the Razor Class Library shared by the ASP.NET Core and
MAUI hosts. Its components must not depend on Web, EF Core, Identity, or a
server render mode. Hosts select interactivity and inject platform services.
Server-bound pages remain in Web until HTTP client implementations exist.
The RCL references the cross-platform `Microsoft.AspNetCore.Components.Web`
package rather than the server-only `Microsoft.AspNetCore.App` shared runtime.

`GymPlanner.Mobile` is an Android/iOS .NET MAUI Blazor Hybrid host. It references
Domain, API.Contracts, and UI, but never Web. Its host page uses
`blazor.webview.js`; it has no Interactive Server circuit or reconnect UI. The
initial `HttpClient` uses HTTPS development addresses by default. Android
emulators use `10.0.2.2`, while iOS simulators use `localhost`. A physical device
can use a configuration- or build-supplied HTTP address only in Debug and only
when the host is localhost or a private/link-local IP address. Android Debug
permits the local `10.0.2.2` ASP.NET development certificate whose subject and
issuer are `CN=localhost` and enables cleartext through a Debug-only manifest
overlay. Release builds require HTTPS, normal certificate validation, and a
manifest that disables cleartext traffic.

Mobile session storage, refresh, revocation, and Blazor authentication-state
behavior are documented in [mobile-authentication.md](mobile-authentication.md).
