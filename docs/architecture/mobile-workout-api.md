# Mobile workout API

## Scope of this stage

The authenticated mobile API exposes CRUD operations for training plans and
their exercise templates, plus the read-only exercise-definition catalog. The
web UI continues to call the same application interfaces directly, while the
mobile boundary uses dedicated request and response DTOs.

Routes:

- `GET|POST /api/v1/training-plans`
- `GET|PUT|DELETE /api/v1/training-plans/{id}`
- `GET|POST /api/v1/training-plans/{planId}/exercises`
- `GET|PUT|DELETE /api/v1/exercises/{id}`
- `GET /api/v1/exercise-definitions`

## Ownership and contracts

Every route requires the mobile bearer-session policy. Services derive the user
identifier from the authenticated server principal. Requests contain no
`UserId`, owner override, photo path, or EF navigation object. A resource owned
by another user is returned as `404 Not Found`, preventing the API from exposing
whether that identifier exists.

Responses are projections of application contracts and do not serialize EF Core
entities. Exercise photos are represented only by `HasPhoto`; storage paths are
not disclosed. Photo transfer has a separate future API stage.

Each exercise request must provide one set entry for every declared set. Set
numbers are unique and sequential, and every entry carries its own repetitions,
weight, and completion state. The server does not calculate progression or copy
one shared weight across sets.

This stage changes queries, validation, and HTTP endpoints only. It adds no
database columns or migrations and does not transform existing workout data.
