# Design QA — first-entry flow and icon cleanup

## Evidence

- The signed Android APK was installed and inspected on a physical Redmi Note 8 at 1080 × 2340.
- Native launch, creator introduction, application loading, the restarted application, frameless system shortcuts, and the final welcome were captured from the running app.
- Runtime DOM and computed styles were inspected through the Android WebView debugging interface.

## Findings

- The restored native circular GP emblem remains the first visible application frame.
- The one-time creator introduction follows the native splash and uses a plain black background without a frame, particle effects, or decorative traces.
- All creator copy and the `НАЧАТЬ` label are uppercase; `GPLANNER` is highlighted with the application accent.
- The `НАЧАТЬ` control uses the shared primary-button visual language.
- Clicking `НАЧАТЬ` stores `CreatorIntroSeenV1=true`, reveals the ordinary `GPlanner / Загрузка` screen, and continues into the application.
- With the two local preference files safely backed up and temporarily absent, the clean-device branch continued from loading to the real `/account/register` screen.
- A full process restart does not show the creator introduction again.
- Calendar, notebook, notification, and contextual-help shortcuts remain present without decorative frames or animation. The contextual-help control computes to a transparent background, no border, no shadow, and no animation.
- The real final route displays the restored `Добро пожаловать в GymPlanner` screen, fades out, records completion, and returns to `/`.
- The temporary completion-state reset used for the final-route check was automatically restored by the production flow.
- Both preference files used for the clean-device check were restored with matching SHA-256 hashes; the original authenticated account reopened successfully and the temporary backups were removed.

## Verification

- `dotnet build WorkoutPlanner.Web/WorkoutPlanner.Web.csproj --no-restore -p:UseAppHost=false`: passed, 0 warnings, 0 errors.
- `dotnet build GymPlanner.Mobile/GymPlanner.Mobile.csproj -f net10.0-android --no-restore`: passed, 0 warnings, 0 errors.
- `dotnet test WorkoutPlanner.Web.Tests/WorkoutPlanner.Web.Tests.csproj --no-restore`: passed, 24 tests, 0 failed, 0 skipped.
- Signed Android APK installed and the complete affected flow was inspected on the physical device.

final result: passed
