# Mobile exercise photos

Exercise photos remain server-owned files under `App_Data/WorkoutImages`. The
mobile client uploads one multipart field named `file` to
`POST /api/v1/exercises/{exerciseId}/photo` and downloads it through the same
authenticated resource route. Internal file names and storage paths are not
returned in API DTOs.

Before linking a photo, the server verifies that the exercise belongs to the
authenticated user. Uploads are limited to 5 MB both by declared length and by
the actual copied byte count. Only JPEG, PNG, and WebP MIME types are accepted,
and their file signatures are checked. Client file names are ignored; storage
uses a random server-generated identifier and trusted extension.

Database and file operations use compensation: a newly written file is removed
if linking it fails, while a failed physical deletion restores the database
reference. Replacing a photo removes the previous file after the new reference
is saved. Another user's photo route returns `404 Not Found`.

The MAUI client uses the built-in platform photo picker and camera through an
application abstraction, so no additional package is required. Camera access is
requested at the point of use; denial, unsupported hardware, cancellation,
unsupported formats, files over 5 MB, read errors, and network failures are
reported without changing the exercise record.

The selected bytes are sent as multipart content through the authenticated HTTP
client. Existing photos are also downloaded through that client and rendered
from an in-memory data URI, rather than exposing a bearer-free public URL to the
BlazorWebView. The client can replace or explicitly delete a photo and updates
its local `HasPhoto` projection only after the server confirms the mutation.
