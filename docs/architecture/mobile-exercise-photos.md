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

The future MAUI implementation will use the platform photo picker or camera and
send the selected stream as multipart content. Permission denial, user cancel,
and network loss remain client concerns for that stage. No schema migration or
new package is required here.
