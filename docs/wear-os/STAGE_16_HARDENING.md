# Этап 16. Полное тестирование и hardening

## Добавленное покрытие

- File-backed Room test закрывает process death/restart gap: completed set, rest
  deadline, server version и pending operation сохраняются после close/reopen.
- Opt-in real-backend instrumentation test проходит полный путь одного подхода:
  pairing → active workout → complete → rest skip → finish confirmation → no-active.
- После e2e main-app history API проверяется отдельно и возвращает ровно одну запись.
- Release URL получает fail-closed validation: без явного безопасного HTTPS URL
  release build не создаётся.

## Матрица требований

Серверные integration tests покрывают pairing code creation/expiry/reuse/invalid,
rate limits, device connection, refresh rotation/replay, password security stamp,
single/all revoke, watch/main policy separation, ownership, cross-user isolation,
active workout ordering, complete/update/undo, operation replay/conflict, finish и
repeated finish.

Wear OS JVM/instrumentation tests покрывают ViewModel actions and haptic events,
next set/exercise, Room atomic writes, retry, token refresh, conflict cascade,
coalescing, complete+undo cancellation, migration, restart persistence, rest
deadline, extended editing, finish barrier и round/square Compose layouts.

## Производительность и батарея

- Периодического polling нет; active workout обновляется по действию пользователя.
- WorkManager использует один `enqueueUniqueWork(..., KEEP)` и network constraint.
- Retry — exponential backoff; временные ошибки не создают tight loop.
- Очередь ограничена 100 нерешёнными операциями.
- Active DTO не содержит историю, изображения или длинные описания.
- Debug log не содержит body/header/credentials; release log отключён.
- Exact alarm и foreground service не добавлены, поэтому отсутствуют постоянные
  wakeups и новые battery-sensitive permissions.

## Ручная/интерактивная проверка

Минимальный online/offline/restart/revocation loop ранее проверен на реальном backend
в Этапе 10. В Этапе 16 дополнительно выполнен finish-flow на Wear OS API 36 emulator
с новой временной SQLite-базой и реальным HTTP backend; история подтверждена через
main-app API. Полные suites запускаются на круглом и квадратном API 36 профилях.

Физических Wear OS-часов в окружении нет; Redmi Note 8 является телефоном и не
используется как подмена. OEM haptic/Always-On/Doze остаются release checklist item.

## Итоговая валидация

- Watch server integration filter: 12/12 успешно.
- Полный instrumented suite на круглом Wear OS API 36: 26 тестов, 4 opt-in
  real-backend сценария ожидаемо skipped без аргументов, падений нет.
- Полный instrumented suite на квадратном Wear OS API 36: 26 тестов, 4 opt-in
  real-backend сценария ожидаемо skipped без аргументов, падений нет.
- Отдельный opt-in real-backend finish-flow на квадратном эмуляторе: 1/1 успешно;
  завершение тренировки подтверждено через main-app history API.
- File-backed Room restart/persistence test входит в оба полных instrumented suite.
- `testDebugUnitTest`, `lintDebug` и `assembleDebugAndroidTest`: успешно.
- `dotnet ef migrations has-pending-model-changes`: изменений EF-модели нет.
- Интерактивный запуск debug APK на круглом эмуляторе: launcher/splash icon и
  pairing screen отображаются без clipping; disabled state кнопки видим.
