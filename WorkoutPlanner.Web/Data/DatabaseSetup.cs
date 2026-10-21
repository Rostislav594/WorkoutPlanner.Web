using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WorkoutPlanner.Web.Data;

/// <summary>
/// Выбор базы данных по настройке <c>Database:Provider</c>: <c>Sqlite</c>
/// (по умолчанию) или <c>Postgres</c>. Строка подключения — <c>ConnectionStrings:WorkoutDatabase</c>.
/// Приложение в обоих случаях получает <see cref="WorkoutDbContext"/>.
/// </summary>
/// <remarks>
/// Настройки читаются при первом обращении к базе, а не при регистрации
/// сервисов: тестовый хост подменяет конфигурацию уже после того, как
/// Program зарегистрировал сервисы.
/// </remarks>
public static class DatabaseSetup
{
    public const string ProviderKey = "Database:Provider";
    public const string ConnectionStringName = "WorkoutDatabase";
    public const string DefaultSqliteConnection = "Data Source=workoutplanner.db";

    public static bool UsesPostgres(IConfiguration configuration) =>
        string.Equals(configuration[ProviderKey], "Postgres", StringComparison.OrdinalIgnoreCase);

    public static IServiceCollection AddWorkoutDatabase(this IServiceCollection services)
    {
        services.AddSingleton<IDbContextFactory<WorkoutDbContext>>(CreateFactory);
        services.AddScoped<WorkoutDbContext>(provider =>
            provider.GetRequiredService<IDbContextFactory<WorkoutDbContext>>().CreateDbContext());
        return services;
    }

    private static IDbContextFactory<WorkoutDbContext> CreateFactory(IServiceProvider provider)
    {
        var configuration = provider.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        var loggerFactory = provider.GetService<ILoggerFactory>();

        if (!UsesPostgres(configuration))
        {
            var sqlite = new DbContextOptionsBuilder<WorkoutDbContext>()
                .UseSqlite(connectionString ?? DefaultSqliteConnection)
                .UseLoggerFactory(loggerFactory)
                .Options;
            return new Factory(() => new WorkoutDbContext(sqlite));
        }

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"Для PostgreSQL нужна строка подключения ConnectionStrings:{ConnectionStringName}.");

        var postgres = PostgresOptions(connectionString).UseLoggerFactory(loggerFactory).Options;
        return new Factory(() => new PostgresWorkoutDbContext(postgres));
    }

    internal static DbContextOptionsBuilder<PostgresWorkoutDbContext> PostgresOptions(string connectionString)
    {
        EnablePostgresDateTimeMapping();
        return new DbContextOptionsBuilder<PostgresWorkoutDbContext>().UseNpgsql(connectionString);
    }

    /// <summary>
    /// Даты в приложении хранятся как есть, без часового пояса: поля *Utc уже
    /// в UTC, остальные — календарные. Режим Npgsql по умолчанию требует у каждой
    /// даты явный пояс и падает на смешанных, поэтому включаем прежнее поведение:
    /// DateTime ↔ timestamp without time zone, ровно как было в SQLite.
    /// </summary>
    public static void EnablePostgresDateTimeMapping() =>
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    private sealed class Factory(Func<WorkoutDbContext> create) : IDbContextFactory<WorkoutDbContext>
    {
        public WorkoutDbContext CreateDbContext() => create();
    }
}

/// <summary>
/// Для <c>dotnet ef migrations add ... --context PostgresWorkoutDbContext</c>.
/// Подключение берётся из переменной ConnectionStrings__WorkoutDatabase, иначе —
/// локальный PostgreSQL из docker-compose.postgres.yml.
/// </summary>
public sealed class PostgresWorkoutDbContextDesignFactory : IDesignTimeDbContextFactory<PostgresWorkoutDbContext>
{
    public PostgresWorkoutDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__WorkoutDatabase")
            ?? "Host=localhost;Port=5432;Database=gymplanner;Username=gymplanner;Password=gymplanner_local_dev";
        return new PostgresWorkoutDbContext(DatabaseSetup.PostgresOptions(connectionString).Options);
    }
}
