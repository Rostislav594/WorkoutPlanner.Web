using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Services;

public static class WorkoutProgressionService
{
    public static void ProgressExercise(
        Exercise exercise)
    {
        var exerciseCompleted = exercise.IsCompleted;

        foreach (var set in exercise.Sets)
        {
            if (exerciseCompleted)
            {
                set.Repetitions++;
            }

            set.Completed = false;
        }

        // Keep the legacy flags cleared for existing databases.
        exercise.Set1Completed = false;
        exercise.Set2Completed = false;
        exercise.Set3Completed = false;
        exercise.Status = ExerciseStatus.NotCompleted;
    }
}