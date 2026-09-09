using Contracts = WorkoutPlanner.Web.Application.Contracts;
using Entities = WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Application.Mapping;

internal static class ApplicationContractMapper
{
    public static Contracts.TrainingPlan ToContract(
        this Entities.TrainingPlan source)
    {
        return new Contracts.TrainingPlan
        {
            Id = source.Id,
            WorkoutName = source.WorkoutName,
            Date = source.Date,
            Exercises = source.Exercises
                .Select(ToContract)
                .ToList()
        };
    }

    public static Contracts.Exercise ToContract(this Entities.Exercise source)
    {
        return new Contracts.Exercise
        {
            Id = source.Id,
            Name = source.Name,
            PhotoPath = source.PhotoPath,
            WorkoutName = source.WorkoutName,
            SetsCount = source.SetsCount,
            Status = (Contracts.ExerciseStatus)source.Status,
            TrainingPlanId = source.TrainingPlanId,
            ExerciseDefinitionId = source.ExerciseDefinitionId,
            SupersetGroupId = source.SupersetGroupId,
            ExerciseDefinition = source.ExerciseDefinition?.ToContract(),
            Sets = source.Sets
                .OrderBy(x => x.SetNumber)
                .Select(ToContract)
                .ToList()
        };
    }

    public static Contracts.ExerciseTemplateSet ToContract(
        this Entities.ExerciseTemplateSet source)
    {
        return new Contracts.ExerciseTemplateSet
        {
            Id = source.Id,
            SetNumber = source.SetNumber,
            Repetitions = source.Repetitions,
            Weight = source.Weight,
            Completed = source.Completed,
            IsWarmup = source.IsWarmup,
            Version = source.Version
        };
    }

    public static Contracts.ExerciseDefinition ToContract(
        this Entities.ExerciseDefinition source)
    {
        return new Contracts.ExerciseDefinition
        {
            Id = source.Id,
            Name = source.Name
        };
    }

    public static Contracts.WorkoutDay ToContract(
        this Entities.WorkoutDay source)
    {
        return new Contracts.WorkoutDay
        {
            Id = source.Id,
            Date = source.Date,
            TrainingPlanId = source.TrainingPlanId,
            IsCompleted = source.IsCompleted
        };
    }

    public static Contracts.WorkoutHistory ToContract(
        this Entities.WorkoutHistory source)
    {
        return new Contracts.WorkoutHistory
        {
            Id = source.Id,
            WorkoutName = source.WorkoutName,
            Date = source.Date,
            Summary = source.Summary,
            Details = source.Details
        };
    }

    public static Contracts.ProgressSnapshot ToContract(
        this Entities.ProgressSnapshot source)
    {
        return new Contracts.ProgressSnapshot
        {
            Id = source.Id,
            Date = source.Date,
            WorkoutName = source.WorkoutName,
            Score = source.Score
        };
    }

    public static Contracts.ProgressChartPoint ToContract(
        this Entities.ProgressChartPoint source)
    {
        return new Contracts.ProgressChartPoint
        {
            Date = source.Date,
            Percent = source.Percent
        };
    }
}
