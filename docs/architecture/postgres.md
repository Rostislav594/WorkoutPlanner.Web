# База данных: PostgreSQL и SQLite

Сервер (`WorkoutPlanner.Web`) работает с одной базой. Какой — решает настройка
`Database:Provider`:

| Значение | База | Где используется |
|---|---|---|
| `Postgres` | PostgreSQL | разработка (`appsettings.Development.json`) и рабочий сервер |
| `Sqlite` (по умолчанию) | файл SQLite | автотесты и запасной режим |

Строка подключения в обоих случаях — `ConnectionStrings:WorkoutDatabase`.
Код приложения всегда работает с `WorkoutDbContext`; для PostgreSQL сервер
создаёт `PostgresWorkoutDbContext` — ту же модель, но со своими миграциями.

## Локальный PostgreSQL

```bash
docker compose -f docker-compose.postgres.yml up -d
```

Пароль в `docker-compose.postgres.yml` и `appsettings.Development.json` —
тестовый, только для этой машины. Рабочий сервер получает строку подключения
из переменной окружения `ConnectionStrings__WorkoutDatabase` и
`Database__Provider=Postgres`; в репозиторий она не попадает.

## Миграции — два набора

- `Migrations/` — SQLite (`WorkoutDbContext`), на них работают тесты.
- `Migrations/Postgres/` — PostgreSQL (`PostgresWorkoutDbContext`).

Любое изменение модели требует миграции в **оба** набора:

```bash
cd WorkoutPlanner.Web
# PostgreSQL
dotnet ef migrations add ИмяМиграции --context PostgresWorkoutDbContext --output-dir Migrations/Postgres --namespace WorkoutPlanner.Web.Migrations.Postgres
# SQLite: явно переключаем провайдер, иначе среда Development отдаст PostgreSQL
Database__Provider=Sqlite ConnectionStrings__WorkoutDatabase="Data Source=:memory:" dotnet ef migrations add ИмяМиграции --context WorkoutDbContext
```

Имена миграций в двух наборах должны различаться (EF не даёт двум классам
одного имени в сборке): для PostgreSQL добавляйте суффикс `Postgres`, например
`AddSupportTicketKindPostgres`. На 2026-10-23 команда для `WorkoutDbContext`
всё равно выбирала `PostgresWorkoutDbContext` (даже с переменными окружения),
поэтому SQLite-миграцию `AddSupportTicketKind` пришлось написать вручную:
файл миграции, `.Designer.cs` (копия снимка с атрибутом `[Migration]`) и правка
`WorkoutDbContextModelSnapshot.cs`.

Если `dotnet ef` положит снимок модели PostgreSQL в `WorkoutPlanner/Web/Migrations/Postgres`
(путь по пространству имён), перенесите его в `Migrations/Postgres`.

SQL в ограничениях и фильтрах пишется с именами колонок в `[скобках]` и
оборачивается в `Sql(...)` внутри `WorkoutDbContext`: для PostgreSQL скобки
сами заменяются на кавычки.

## Даты

Для PostgreSQL включён прежний режим Npgsql
(`Npgsql.EnableLegacyTimestampBehavior`): `DateTime` хранится как
`timestamp without time zone`, ровно как в SQLite. Поля `*Utc` содержат UTC,
остальные — календарные даты.

## Перенос данных из SQLite в PostgreSQL

Разовая команда переносит всё из файла SQLite в **пустую** базу PostgreSQL,
сохраняя номера записей, затем выравнивает счётчики и сверяет число строк
в каждой таблице:

```bash
cd WorkoutPlanner.Web
Database__Provider=Postgres ConnectionStrings__WorkoutDatabase="Host=...;Database=...;Username=...;Password=..." \
  dotnet run --no-build -- --import-sqlite workoutplanner.db
```

Если в целевой базе уже есть данные, команда ничего не меняет и останавливается.
