using Microsoft.EntityFrameworkCore;

namespace WorkoutPlanner.Web.Data;

/// <summary>
/// Та же модель, что <see cref="WorkoutDbContext"/>, но для PostgreSQL. Отдельный
/// тип нужен только ради своего набора миграций (папка Migrations/Postgres):
/// EF сопоставляет миграции с контекстом по типу, а SQL у двух баз разный.
/// Всё приложение по-прежнему работает с <see cref="WorkoutDbContext"/>.
/// </summary>
public sealed class PostgresWorkoutDbContext : WorkoutDbContext
{
    public PostgresWorkoutDbContext(DbContextOptions<PostgresWorkoutDbContext> options)
        : base(options)
    {
    }
}
