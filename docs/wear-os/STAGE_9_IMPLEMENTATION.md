# Этап 9. Подключение Minimal Wear OS к реальному API — результат

Дата проверки: 9 сентября 2026 года.

## Выполнено

Minimal Wear OS клиент подключён к реальному `/api/watch` backend:

- одноразовый pairing-код обменивается на access/refresh token;
- refresh token хранится в Android Keystore, access token — только в памяти;
- refresh token ротируется при обновлении сессии;
- активная тренировка загружается с сервера и кэшируется в Room;
- текущим считается первый невыполненный подход в серверном порядке упражнений и
  подходов;
- `CompleteSet` создаёт постоянный GUID `operationId`, передаёт `clientVersion` и
  после sync применяет авторитетный set state сервера;
- `409` применяет авторитетный conflict snapshot без silent retry;
- `400`, `401`, `403`, `404`, `409`, `410`, `429`, выбранные `5xx`, timeout и
  отсутствие сети классифицируются отдельно для terminal/retryable поведения.

Production DI использует `RetrofitWatchRemoteDataSource`. Fake datasource оставлен
как тестовый double и не участвует в работе приложения.

## Конфигурация и безопасность

- debug URL: Gradle property `WEAR_DEBUG_API_BASE_URL`, по умолчанию
  `http://10.0.2.2:5121/api/watch/`;
- release URL: `WEAR_RELEASE_API_BASE_URL`, fail-safe default
  `https://example.invalid/api/watch/`;
- cleartext разрешён только debug manifest для локального Android Emulator;
- debug network log содержит только HTTP method, encoded path, status и duration;
  headers, тела запросов и токены не логируются;
- `408`, `429` и `5xx` при refresh не уничтожают рабочий refresh token.

## Автоматизированная проверка

В `WorkoutPlanner.WearOS` успешно выполнены JVM tests, lint, debug build и сборка
instrumentation APK. Добавлены тесты ротации refresh token, очистки невалидной
сессии и сохранения credentials при transient refresh failure.

Opt-in `RealApiSmokeInstrumentedTest` принимает одноразовый `pairingCode` через
instrumentation arguments. Он на Wear OS Emulator выполняет pairing, нажимает
«Обновить», проверяет реальное упражнение, завершает подход и ждёт успешного
завершения unique WorkManager queue. Без аргумента тест пропускается и не создаёт
зависимость обычного CI от локального backend.

## Реальная end-to-end проверка

На `WorkoutPlanner_Wear_Square_API_36`, API 36, 360 × 360:

1. поднят отдельный backend на `http://localhost:5131` с отдельной временной SQLite;
2. через реальные mobile endpoints создан аккаунт, план, упражнение с весами
   `42.5` и `47.5`, а также активная тренировка;
3. через реальный pairing endpoint создан и введён одноразовый код;
4. часы загрузили упражнение «Жим на часах» и первый подход `42.5 кг × 8`;
5. первый подход завершён на часах, WorkManager queue завершилась успешно;
6. mobile API подтвердил авторитетное состояние: первый подход выполнен, второй
   (`47.5 кг × 6`) не выполнен;
7. после перезапуска приложения часы показали второй подход и статус
   «Синхронизировано».

Экран визуально проверен на эмуляторе: элементы не обрезаны, touch target основной
кнопки достаточен, вес и повторы читаются. `ui-ux-pro-max` использован как финальный
checklist. Остаточные mock-подсказки удалены из pairing/rest экранов.

## Миграции и границы

Новых EF или Room migrations на Этапе 9 нет. Серверные модели и расширенные API не
изменялись. Не реализованы Stage 10 milestone packaging, полный devices UI,
SignalR, редактирование подходов, overview и завершение тренировки с часов.

## Стоп-условие

Критерий Этапа 9 выполнен: Wear OS Emulator подключился к реальному аккаунту,
загрузил реальную активную тренировку и синхронизировал реальное завершение подхода.
Этап 10 не начат.
