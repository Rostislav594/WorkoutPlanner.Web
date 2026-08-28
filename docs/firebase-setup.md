# Firebase setup

The mobile app expects `GymPlanner.Mobile/google-services.json` locally. Obtain it from the Firebase Android app whose package name is `com.gymplanner.mobile`; it is client configuration, not a backend private key. The file is ignored by Git.

The backend uses Firebase Admin SDK 3.6.0 with Application Default Credentials. Set `Push:Enabled=true` and `Firebase:ProjectId` in configuration, and provide the service-account JSON through `GOOGLE_APPLICATION_CREDENTIALS`. Keep that JSON outside the repository and never copy it into the Android project.

Before enabling delivery, enable the FCM HTTP v1 API in Firebase Console. Android 13+ notification permission is requested at runtime. The stable channel id is `gplanner-notifications`.

## Manual activation checklist

1. In Firebase Console, create or select the project used by the backend and add an Android app with package name `com.gymplanner.mobile`.
2. Download that app's `google-services.json` to `GymPlanner.Mobile/google-services.json`. Do not commit it.
3. Enable Firebase Cloud Messaging HTTP v1 for the project.
4. Create a service account with permission to send Firebase Cloud Messaging messages. Store its JSON outside the repository and set `GOOGLE_APPLICATION_CREDENTIALS` to its absolute path for the Web process.
5. Set `Push:Enabled=true` and `Firebase:ProjectId=<firebase-project-id>` in the Web deployment environment. Keep the default disabled provider in every environment where credentials are not configured.
6. Run the Web migration before sending the first message. The migration is `20260828132217_AddPushDeviceRegistrations`.

## Device E2E checklist

With an Android 13+ device connected through `adb devices`:

1. Install and launch the app, accept notification permission, and sign in.
2. Confirm a row for the installation appears through `POST /api/v1/push/devices` and that the token is never logged or returned to the client.
3. Create an Inbox message from the support/admin flow and verify delivery while the app is foregrounded, backgrounded, and fully stopped.
4. Tap the notification and verify it opens the Inbox message route and only displays data belonging to the signed-in user.
5. Sign out, sign in as another account on the same device, and verify the previous account no longer receives messages.
6. Trigger an FCM token refresh and verify the existing installation row is updated rather than duplicated.
7. Repeat registration from a second device and verify both active installations receive the same user-scoped notification.

Record the Android version, app build, Firebase project id, message id, and timestamps for each case. Never record the raw FCM token or service-account contents.
