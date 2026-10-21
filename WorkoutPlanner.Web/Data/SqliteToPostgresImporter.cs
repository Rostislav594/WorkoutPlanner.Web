using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace WorkoutPlanner.Web.Data;

/// <summary>
/// Разовый перенос всех данных из файла SQLite в пустую базу PostgreSQL.
/// Запуск: <c>dotnet run -- --import-sqlite путь/к/workoutplanner.db</c> при
/// <c>Database:Provider=Postgres</c>. Номера записей сохраняются, поэтому все
/// связи остаются прежними; счётчики новых номеров потом продолжают с максимума.
/// В конце число строк каждой таблицы сверяется с исходным.
/// </summary>
public static class SqliteToPostgresImporter
{
    public const string CommandLineSwitch = "--import-sqlite";

    private static readonly MethodInfo CopyMethod =
        typeof(SqliteToPostgresImporter).GetMethod(nameof(CopyTable), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static async Task RunAsync(IServiceProvider services, string sqlitePath, ILogger logger)
    {
        if (!File.Exists(sqlitePath))
            throw new FileNotFoundException("Файл SQLite не найден.", sqlitePath);

        await using var scope = services.CreateAsyncScope();
        var target = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
        if (target is not PostgresWorkoutDbContext)
            throw new InvalidOperationException("Перенос возможен только при Database:Provider=Postgres.");

        await target.Database.MigrateAsync();

        var sourceOptions = new DbContextOptionsBuilder<WorkoutDbContext>()
            .UseSqlite($"Data Source={sqlitePath};Mode=ReadOnly")
            .Options;
        await using var source = new WorkoutDbContext(sourceOptions);

        var tables = OrderByDependencies(target.Model);
        EnsureNoShadowData(tables);

        foreach (var entityType in tables)
        {
            if (await CountAsync(target, entityType) > 0)
                throw new InvalidOperationException(
                    $"Таблица {entityType.GetTableName()} в PostgreSQL уже не пустая — перенос делается только в чистую базу.");
        }

        await using var transaction = await target.Database.BeginTransactionAsync();
        foreach (var entityType in tables)
        {
            var copy = CopyMethod.MakeGenericMethod(entityType.ClrType);
            var copied = await (Task<int>)copy.Invoke(null, [source, target])!;
            logger.LogInformation("{Table}: перенесено {Count}", entityType.GetTableName(), copied);
        }
        await transaction.CommitAsync();

        await ResetIdentitySequencesAsync(target, tables);

        var mismatches = new List<string>();
        foreach (var entityType in tables)
        {
            var expected = await CountAsync(source, entityType);
            var actual = await CountAsync(target, entityType);
            if (expected != actual)
                mismatches.Add($"{entityType.GetTableName()}: в SQLite {expected}, в PostgreSQL {actual}");
        }
        if (mismatches.Count > 0)
            throw new InvalidOperationException("Число строк не совпало: " + string.Join("; ", mismatches));

        logger.LogInformation("Перенос завершён: {Tables} таблиц, число строк совпадает.", tables.Count);
    }

    private static async Task<int> CopyTable<TEntity>(WorkoutDbContext source, WorkoutDbContext target)
        where TEntity : class
    {
        var rows = await source.Set<TEntity>().AsNoTracking().ToListAsync();
        if (rows.Count == 0)
            return 0;

        target.Set<TEntity>().AddRange(rows);
        await target.SaveChangesAsync();
        target.ChangeTracker.Clear();
        return rows.Count;
    }

    private static Task<int> CountAsync(WorkoutDbContext db, IEntityType entityType)
    {
        var method = typeof(SqliteToPostgresImporter)
            .GetMethod(nameof(CountTable), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(entityType.ClrType);
        return (Task<int>)method.Invoke(null, [db])!;
    }

    private static Task<int> CountTable<TEntity>(WorkoutDbContext db) where TEntity : class =>
        db.Set<TEntity>().CountAsync();

    // Сначала таблицы, на которые ссылаются, потом те, что ссылаются на них.
    private static List<IEntityType> OrderByDependencies(IModel model)
    {
        var types = model.GetEntityTypes()
            .Where(x => !x.IsOwned() && x.GetTableName() is not null && x.BaseType is null)
            .ToList();
        var ordered = new List<IEntityType>();
        var visiting = new HashSet<IEntityType>();
        var done = new HashSet<IEntityType>();

        void Visit(IEntityType type)
        {
            if (done.Contains(type))
                return;
            if (!visiting.Add(type))
                throw new InvalidOperationException($"Циклическая связь между таблицами около {type.GetTableName()}.");
            foreach (var principal in type.GetForeignKeys().Select(x => x.PrincipalEntityType.GetRootType()).Distinct())
            {
                if (principal != type)
                    Visit(principal);
            }
            visiting.Remove(type);
            done.Add(type);
            ordered.Add(type);
        }

        foreach (var type in types.OrderBy(x => x.GetTableName(), StringComparer.Ordinal))
            Visit(type);
        return ordered;
    }

    // Копирование идёт через объекты, поэтому значения, которых нет в классах
    // (теневые свойства), потерялись бы. Проверяем, что таких нет.
    private static void EnsureNoShadowData(IEnumerable<IEntityType> tables)
    {
        var shadow = tables
            .Where(x => x.HasSharedClrType || x.ClrType == typeof(Dictionary<string, object>))
            .Select(x => x.Name)
            .Concat(tables.SelectMany(x => x.GetProperties().Where(p => p.IsShadowProperty()).Select(p => $"{x.GetTableName()}.{p.Name}")))
            .ToList();
        if (shadow.Count > 0)
            throw new InvalidOperationException("Есть данные без свойств в классах, перенос их потеряет: " + string.Join(", ", shadow));
    }

    // Записи вставлены со старыми номерами — счётчики должны продолжать с максимума.
    private static async Task ResetIdentitySequencesAsync(WorkoutDbContext target, IEnumerable<IEntityType> tables)
    {
        foreach (var entityType in tables)
        {
            var key = entityType.FindPrimaryKey();
            if (key is not { Properties.Count: 1 })
                continue;
            var property = key.Properties[0];
            if (property.ValueGenerated != ValueGenerated.OnAdd || property.ClrType is not ({ } t) || (t != typeof(int) && t != typeof(long)))
                continue;

            var table = entityType.GetTableName()!;
            var column = property.GetColumnName(StoreObjectIdentifier.Table(table, entityType.GetSchema()))!;
            var sql =
                $"SELECT setval(pg_get_serial_sequence('\"{table}\"', '{column}'), " +
                $"COALESCE((SELECT MAX(\"{column}\") FROM \"{table}\"), 1), " +
                $"(SELECT COUNT(*) FROM \"{table}\") > 0)";
            await target.Database.ExecuteSqlRawAsync(sql);
        }
    }
}
