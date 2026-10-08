namespace WorkoutPlanner.Api.Contracts;

public sealed record TrainingPlanApiResponse(int Id, string WorkoutName, DateTime Date, IReadOnlyList<ExerciseApiResponse> Exercises, int RestBetweenExercisesSeconds = RestTimerDefaults.BetweenExercisesSeconds);
public sealed record CreateTrainingPlanRequest(string WorkoutName);
public sealed record RenameTrainingPlanRequest(string WorkoutName);
/// <param name="RestAfterExerciseSeconds">Отдых после упражнения; <c>null</c> — отдых между упражнениями шаблона.</param>
public sealed record ExerciseApiResponse(int Id, string Name, int SetsCount, string Status, int TrainingPlanId, int? ExerciseDefinitionId, bool HasPhoto, IReadOnlyList<ExerciseSetApiResponse> Sets, int? SupersetGroupId = null, int RestBetweenSetsSeconds = RestTimerDefaults.BetweenSetsSeconds, int? RestAfterExerciseSeconds = null);
/// <param name="RestAfterSeconds">Отдых после подхода; <c>null</c> — отдых упражнения по умолчанию.</param>
public sealed record ExerciseSetApiResponse(int SetNumber, int Repetitions, double Weight, bool Completed, bool IsWarmup = false, int? RestAfterSeconds = null);
/// <param name="RestBetweenSetsSeconds">
/// Отдых между подходами этого упражнения. <c>null</c> — не менять
/// сохранённый (у нового упражнения — значение по умолчанию): так правки
/// подходов, суперсетов и старые сборки не сбрасывают таймер.
/// </param>
/// <param name="RestAfterExerciseSeconds">Отдых после упражнения; <c>null</c> — не менять сохранённый.</param>
public sealed record SaveExerciseRequest(string Name, int SetsCount, string Status, int? ExerciseDefinitionId, IReadOnlyList<SaveExerciseSetRequest>? Sets, int? SupersetGroupId = null, int? RestBetweenSetsSeconds = null, int? RestAfterExerciseSeconds = null);
public sealed record ReorderSupersetRequest(IReadOnlyList<int> ExerciseIds);
/// <param name="RestAfterSeconds">Отдых после подхода; <c>null</c> — не менять сохранённый.</param>
public sealed record SaveExerciseSetRequest(int SetNumber, int Repetitions, double Weight, bool Completed, bool IsWarmup = false, int? RestAfterSeconds = null);
/// <param name="Name">Название на языке из Accept-Language.</param>
/// <param name="BodyPart">
/// Часть тела для разделов окна выбора: chest, back, shoulders, arms, core, legs,
/// glutes или other. <c>null</c> — сервер старой версии, раздела нет.
/// </param>
/// <param name="MuscleName">Основная мышца на языке из Accept-Language.</param>
public sealed record ExerciseDefinitionApiResponse(
    int Id,
    string Name,
    string? BodyPart = null,
    string? MuscleName = null);

/// <summary>
/// Таймеры отдыха шаблона: отдых между упражнениями по умолчанию, отдых
/// упражнения по умолчанию, отдых после отдельных подходов и после отдельных
/// упражнений. Незаданное остаётся как было.
/// </summary>
public sealed record UpdateRestTimersRequest(
    int? RestBetweenExercisesSeconds,
    IReadOnlyList<ExerciseRestTimerRequest>? Exercises,
    IReadOnlyList<SetRestTimerRequest>? Sets = null,
    IReadOnlyList<ExerciseRestAfterRequest>? AfterExercises = null);
/// <summary>Отдых после упражнения перед следующим.</summary>
public sealed record ExerciseRestAfterRequest(int ExerciseId, int RestAfterSeconds);
public sealed record ExerciseRestTimerRequest(int ExerciseId, int RestBetweenSetsSeconds);
/// <summary>Отдых после подхода <paramref name="SetNumber"/> упражнения.</summary>
public sealed record SetRestTimerRequest(int ExerciseId, int SetNumber, int RestAfterSeconds);
