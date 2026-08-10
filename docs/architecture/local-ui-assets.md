# Local UI assets

GymPlanner does not require external CDN styles or fonts at runtime. Bootstrap
continues to come from the existing Web static files. Material Symbols Rounded
is served by the shared Razor Class Library from
`_content/WorkoutPlanner.UI/css/material-symbols.css`; both Web and MAUI load the
same local stylesheet. The previously referenced Font Awesome stylesheet was
removed because no application component uses its classes.

The checked-in Material Symbols WOFF2 is the static 24 px, weight 500,
unfilled/grade 0 variant previously requested from Google Fonts. Its Apache-2.0
license is stored beside the font as `LICENSE-material-symbols.txt`. Updating the
font requires reviewing the official source, replacing the binary and license if
needed, and rebuilding both hosts.
