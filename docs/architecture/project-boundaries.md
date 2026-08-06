# Project boundaries

`WorkoutPlanner.Domain` contains framework-independent workout calculations.
It has no ASP.NET Core, EF Core, Identity, UI, or filesystem dependency. Domain
inputs explicitly carry each set's own weight and repetitions.

`WorkoutPlanner.Api.Contracts` contains the mobile HTTP request and response
DTOs. It has no reference to the web project or EF entities and can be consumed
by both ASP.NET Core endpoints and the future MAUI client.

`WorkoutPlanner.Web` remains the composition root and owner of ASP.NET Core
Identity, EF Core entities and migrations, SQLite, server implementations,
files, web authentication, and API authorization. Moving the contracts does not
move the database or server runtime into a client project.

Only independent calculations were moved to Domain. Persistence entities and
their navigation properties intentionally remain in Web.
