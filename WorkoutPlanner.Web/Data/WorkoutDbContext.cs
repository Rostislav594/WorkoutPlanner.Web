using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Data;

public class WorkoutDbContext : IdentityDbContext<IdentityUser>
{
    public WorkoutDbContext(
        DbContextOptions<WorkoutDbContext> options)
        : base(options)
    {
    }

    public DbSet<Exercise> Exercises =>
    Set<Exercise>();

    public DbSet<TrainingPlan> TrainingPlans =>
        Set<TrainingPlan>();

    public DbSet<WorkoutHistory> WorkoutHistory =>
        Set<WorkoutHistory>();

    public DbSet<AppSettings> Settings =>
        Set<AppSettings>();

    public DbSet<WorkoutDay> WorkoutDays =>
        Set<WorkoutDay>();

    public DbSet<ProgressSnapshot> ProgressSnapshots =>
        Set<ProgressSnapshot>();

    public DbSet<ExerciseProgressSnapshot> ExerciseProgressSnapshots => Set<ExerciseProgressSnapshot>();

    public DbSet<Muscle> Muscles =>
    Set<Muscle>();
    public DbSet<ExerciseDefinition> ExerciseDefinitions =>
    Set<ExerciseDefinition>();
    public DbSet<ExerciseSecondaryMuscle> ExerciseSecondaryMuscles =>
    Set<ExerciseSecondaryMuscle>();
    public DbSet<TrainingSession> TrainingSessions =>
    Set<TrainingSession>();

    public DbSet<TrainingSessionExercise> TrainingSessionExercises =>
        Set<TrainingSessionExercise>();

    public DbSet<ExerciseSet> ExerciseSets =>
        Set<ExerciseSet>();

    public DbSet<ExerciseTemplateSet> ExerciseTemplateSets =>
    Set<ExerciseTemplateSet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Exercise>()
            .HasOne(x => x.ExerciseDefinition)
            .WithMany()
            .HasForeignKey(x => x.ExerciseDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ExerciseTemplateSet>()
            .HasOne(x => x.Exercise)
            .WithMany(x => x.Sets)
            .HasForeignKey(x => x.ExerciseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TrainingPlan>()
            .HasMany(x => x.Exercises)
            .WithOne(x => x.TrainingPlan)
            .HasForeignKey(x => x.TrainingPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TrainingSession>()
            .HasMany(x => x.Exercises)
            .WithOne(x => x.TrainingSession)
            .HasForeignKey(x => x.TrainingSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TrainingSessionExercise>()
            .HasOne(x => x.ExerciseDefinition)
            .WithMany()
            .HasForeignKey(x => x.ExerciseDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TrainingSessionExercise>()
            .HasMany(x => x.Sets)
            .WithOne(x => x.TrainingSessionExercise)
            .HasForeignKey(x => x.TrainingSessionExerciseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ExerciseSecondaryMuscle>()
            .HasKey(x => new
            {
                x.ExerciseDefinitionId,
                x.MuscleId
            });

        modelBuilder.Entity<ExerciseSecondaryMuscle>()
            .HasOne(x => x.ExerciseDefinition)
            .WithMany(x => x.SecondaryMuscles)
            .HasForeignKey(x => x.ExerciseDefinitionId);

        modelBuilder.Entity<ExerciseSecondaryMuscle>()
            .HasOne(x => x.Muscle)
            .WithMany(x => x.SecondaryMuscleLinks)
            .HasForeignKey(x => x.MuscleId);
    }

}