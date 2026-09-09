# Wear OS — Этап 4: Minimal Watch API активной тренировки

## Выполнено

Добавлены два защищённых watch endpoint:

- `GET /api/watch/workouts/active`;
- `POST /api/watch/sets/{setId}/complete`.

Read API возвращает компактный DTO, детерминированный порядок и первый
незавершённый подход. Completion использует authenticated watch owner, проверяет
активную тренировку и ownership, set version, применяет только `Completed = true` и
возвращает актуальный set и следующий шаг.

## Idempotency и conflict policy

Изменение подхода и `WatchSyncOperation` записываются в одной serializable
транзакции. Повтор того же `operationId` возвращает сохранённый ответ без повторного
увеличения версии. Повторное использование ID для другой операции отклоняется.

Stale version и уже выполненный set возвращают `409` с авторитетным set state.
Полный контракт описан в [OFFLINE_SYNC.md](OFFLINE_SYNC.md).

## Честные ограничения модели

- `StartedAtUtc` возвращается как `null`;
- workout-level version и `CompletedAtUtc` отсутствуют;
- API не разделяет planned/actual weight и reps, потому что текущая модель хранит
  единственные индивидуальные значения каждого подхода;
- текущий подход определяется как первый незавершённый по `Exercise.Id`, затем
  `SetNumber` и `SetId`;
- состояние active workout всё ещё хранится в mutable training-plan template, как
  было явно принято для Minimal MVP.

## Граница этапа

Этап 4 завершён. Этап 5 не начат: UI создания pairing-кода в основном приложении не
добавлялся. Wear OS/Gradle-проект не создавался.
