# Privacy notes for Wear OS publication

Этот файл описывает фактический data flow для владельца продукта и не является
готовым юридическим текстом privacy policy.

Wear OS-клиент обрабатывает:

- account-bound watch identity;
- app-generated device ID, display name, model и app version;
- active workout name, exercises, set IDs, individual weight/repetitions and status;
- pending operation IDs, versions, timestamps, retry/error metadata;
- encrypted refresh credential и memory-only access credential;
- HTTP method/path/status/duration в debug build.

Клиент не запрашивает health sensors, location, contacts, microphone, camera или
Bluetooth permissions. Он не отправляет пароль пользователя и не логирует request
bodies, authorization headers, access/refresh tokens или pairing codes.

Перед store submission опубликованная policy и Play Console Data safety должны
отражать account/device/workout data, цели синхронизации, server retention,
удаление аккаунта и отзыв устройства. Юридическую формулировку и production
retention policy должен утвердить владелец продукта.
