# Mobile onboarding API

Mobile onboarding state is stored centrally in the existing ASP.NET Core
Identity user-token store. The mobile client does not persist authoritative
completion state or copy Identity data to a local database.

The stateless `IOnboardingStateService` is separate from the circuit-scoped
interactive guide service used by the web UI. It validates step identifiers and
outcomes against the server guide catalog before saving progress. Requests never
contain a `UserId`; Identity resolves the authenticated user.

The API supports reading state, saving a resumable step, marking onboarding
complete, and explicitly resetting it. Reset affects only the authenticated
user. The mobile navigation tour uses existing validated navigation step IDs
and keeps its presentation content local. A reusable coach card and the existing
character image are served by the Razor Class Library to both hosts. The API
carries state only and does not expose Web-only spotlight or JavaScript details.

No schema migration or new external dependency is required.
