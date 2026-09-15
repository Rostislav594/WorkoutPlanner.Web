# Wear OS pairing and authentication

## Pairing flow

Часы подключаются двумя способами. Основной — подтверждение на телефоне;
ручной ввод кода остаётся запасным путём, когда телефон недоступен.

### Подтверждение на телефоне (основной путь)

1. На часах пользователь нажимает «Подтвердить на телефоне».
2. Часы вызывают `POST /api/watch/pair/request` и получают `requestId`,
   `pollToken` и `approveUrl`. На этом шаге ещё неизвестно, кому принадлежат часы.
3. Часы открывают `approveUrl` на сопряжённом телефоне через
   `RemoteActivityHelper` из Wear OS. Если телефона рядом нет, часы честно
   предлагают ввести код вручную.
4. Ссылка открывает мобильное приложение на экране подтверждения. Пользователь
   уже вошёл в аккаунт, поэтому он видит модель часов и одну кнопку.
5. Подтверждение вызывает `POST /api/watch/pair/requests/{requestId}/approve`
   и привязывает заявку к аутентифицированному пользователю.
6. Часы всё это время опрашивают `POST /api/watch/pair/status` и получают
   пару токенов вместе с первым ответом `approved`.

Заявка живёт три минуты, одноразова, а `pollToken` хранится на сервере только
в виде SHA-256. Знание `requestId` без `pollToken` не позволяет забрать токены,
а знание `pollToken` без подтверждения на телефоне — тем более.

Экран подтверждения показывает модель часов и предупреждение подтверждать только
собственный запрос: это стандартная защита device-подобных потоков от того, что
злоумышленник подсунет пользователю свою ссылку.

### Ввод кода (запасной путь)

1. В мобильном приложении пользователь открывает
   `Профиль → Смарт-часы Wear OS` и создаёт одноразовый код. Мобильный bearer-сеанс
   вызывает `POST /api/watch/pairing-codes`. Веб-профиль остаётся дополнительным
   интерфейсом к тому же endpoint.
2. The server invalidates that user's previous active code and returns a new random
   six-digit code with an explicit UTC expiry.
3. The watch submits the code and bounded device metadata to `POST /api/watch/pair`.
4. The server verifies the salted code hash, expiry and single-use state inside a
   serializable transaction.
5. A new device is created, or the same user's existing device installation is
   re-paired. A client-selected device ID already owned by another account is not
   transferred.
6. The code is consumed and the server returns an access/refresh token pair.

Оба пути заканчиваются одним и тем же кодом выдачи устройства и токенов, поэтому
ownership, ротация refresh и отзыв работают одинаково независимо от способа.

Pairing codes live for 10 minutes by default. The plaintext code is returned only
once and is never logged or persisted. Invalid attempts are logged using only a
short hash of the submitted device identifier. Pairing is rate-limited by source IP.

## Token model

The access token reuses the existing ASP.NET Identity bearer-ticket protection and
adds two claims identifying the principal as a watch and binding it to the internal
`WatchDevice.Id`. Its default lifetime is 15 minutes.

The refresh token is a separate Data Protection token containing the internal device
ID, a random nonce, expiry and the user's security-stamp snapshot. Only SHA-256 of
the complete protected token is stored in `WatchDevice`. Refresh rotates the token
atomically with a conditional database update, so concurrent replay can succeed only
once. The default refresh lifetime is 30 days.

Changing the account password changes the Identity security stamp, invalidates watch
access tokens and causes the next refresh attempt to revoke the device. The existing
mobile `revoke-access` / logout-everywhere action revokes mobile sessions only and
does not automatically revoke watches. Watches are revoked explicitly through
`DELETE /api/watch/devices/{deviceId}` or all at once through
`DELETE /api/watch/devices`. Both operations immediately invalidate refresh and
already-issued access credentials. Account deletion still removes all watch records
through database cascades.

The authenticated main-app user can rename an active device through
`PUT /api/watch/devices/{deviceId}`. Rename, list, and revoke operations are always
scoped to the authenticated owner; revoked devices remain visible in the management
history but cannot be renamed or authorized again without a new pairing flow.

Production deployments must preserve ASP.NET Data Protection keys and use HTTPS;
otherwise protected access and refresh tokens cannot remain valid across restarts or
instances.

## Authorization boundaries

- management endpoints accept authenticated main-app identities and reject watch
  identities;
- bearer-based main-app calls must also reference an active mobile session;
- future watch workload endpoints must require the `WatchDevice` policy;
- that policy validates the Identity security stamp, device claim, ownership,
  revocation state and expiry against the database;
- no endpoint accepts a user ID from request JSON as an ownership source.
