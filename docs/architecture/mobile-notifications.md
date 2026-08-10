# Mobile notifications

## Local reminders

The MAUI client owns local workout reminders; they are not copied to the server
and are not an offline workout-data store. A reminder contains only the calendar
day id, training-plan id, display name, local trigger time, and an internal route
to `/workouts/{trainingPlanId}`.

The calendar requests notification permission only when the user explicitly
creates a reminder. Android uses `AlarmManager` and a private broadcast receiver;
Android 13+ requests `POST_NOTIFICATIONS`. iOS uses
`UNUserNotificationCenter`. Both platforms use stable identifiers so a reminder
can be replaced or cancelled. A notification tap is passed through a small
navigation service that accepts only positive-id workout routes. Authentication
still applies after navigation, so an expired session goes to login rather than
showing protected data.

Android mirrors scheduled reminder metadata into a private, versioned
SharedPreferences index. `BOOT_COMPLETED` and app replacement recreate only
future alarms; a fired or cancelled reminder is removed from that index. The
receiver is not exported to other applications, and the index contains no
credential or workout details beyond the local notification fields.

The local index in MAUI Preferences exists only to show and cancel scheduled
reminders. It contains no credentials. Logout, password change, access revocation,
token invalidation, and account deletion clear indexed reminders before local
session state is discarded. Platform-wide cancellation does not depend on the
MAUI display index, so a corrupt index cannot leave Android reboot alarms or iOS
pending notifications behind. Android alarms are recreated after a full device
reboot. Delivery after reboot still requires device acceptance testing across
the supported Android versions.

## Remote push foundation

`RegisterPushDeviceRequest`, `PushDeviceRegistrationResponse`, and
`IRemotePushRegistrationService` define the future boundary only. There is no
Firebase, APNs, provider SDK, server endpoint, credential, entitlement, or fake
production configuration in this stage.

Before remote push is enabled:

1. Choose and configure the provider separately for each platform; keep APNs,
   Firebase, signing, and service-account credentials outside the repository.
2. Add a user-owned server device-registration entity. Store a generated
   installation id, platform, protected provider token, timestamps, and revocation
   state; never accept a user id in the request.
3. Add authenticated register/update/delete endpoints with validation,
   ownership checks, rate limits, token rotation, and account-deletion cleanup.
4. Implement the MAUI registration service, permission UX, token refresh, logout
   unregister, deep-link allowlist, and foreground/background handlers.
5. Add provider delivery workers with retry/idempotency, observability, expired
   token cleanup, and integration tests that use a fake provider rather than
   production credentials.
