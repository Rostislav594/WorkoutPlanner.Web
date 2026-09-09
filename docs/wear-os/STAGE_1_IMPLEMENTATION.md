# Wear OS integration — Этап 1

Дата: 2026-09-04

## Выполнено

Выделен переиспользуемый `IActiveWorkoutService`, не зависящий от Blazor-страницы или будущего watch endpoint.

Операции:

- `GetActiveWorkoutAsync` — получает активный `WorkoutDay` текущего аутентифицированного пользователя, детерминированно сортирует упражнения по `Exercise.Id` и подходы по `SetNumber`, возвращает постоянный `ExerciseTemplateSet.Id` и индивидуальные weight/repetitions;
- `UpdateSetAsync` — изменяет один конкретный подход (completion/undo/weight/repetitions), проверяя через серверный authenticated user, что подход принадлежит именно активной сегодняшней тренировке пользователя;
- существующий `IWorkoutCompletionService.CompleteTodayAsync` оставлен единственной транзакционной операцией scheduled workout completion и теперь используется Blazor Today page вместо отдельной ручной записи history/progress/day state.

Blazor checkbox выполнения подхода использует `IActiveWorkoutService.UpdateSetAsync`, временно блокируется на время запроса, откатывает локальное значение при отказе и сообщает об ошибке. Разметка и CSS не менялись.

## Архитектурные решения

- Сохранена фактическая модель active workout: `WorkoutDay` + mutable `TrainingPlan`; `TrainingSession` не вводилась в рабочий flow.
- User ID не принимается от клиента. Сервис получает его через `CurrentUserService` из server-side principal/authentication state.
- Set mutation выполняет один `SaveChangesAsync` и затрагивает только найденный owned active set.
- Read DTO отделён от EF entities.
- `UpdateSetAsync` поддерживает complete, undo и изменение per-set weight/repetitions на application layer. Будущий Minimal Watch API должен экспонировать только узкую completion operation до этапа Extended Wear OS.
- Optimistic concurrency намеренно не реализована: у модели ещё нет `Version`, а его добавление требует миграции следующего этапа. До этого обновления остаются last-write-wins.

## Тесты

`ActiveWorkoutServiceTests` проверяет:

- active workout ownership;
- детерминированный порядок упражнений/подходов;
- постоянный set ID в application DTO;
- индивидуальный вес каждого подхода;
- атомарное изменение одного owned active set;
- cross-user isolation;
- запрет изменения set завершённой тренировки.

## Не выполнялось

- Watch API endpoints;
- watch authentication и pairing;
- watch entities;
- migrations и concurrency token;
- Wear OS-проект;
- SignalR;
- offline/idempotency infrastructure.

На момент завершения этого этапа Этап 2 ещё не был начат. Его последующая реализация
описана в [STAGE_2_IMPLEMENTATION.md](STAGE_2_IMPLEMENTATION.md).
