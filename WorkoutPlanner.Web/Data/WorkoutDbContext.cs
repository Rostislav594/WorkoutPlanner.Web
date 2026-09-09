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

    public DbSet<UserProfile> UserProfiles =>
        Set<UserProfile>();

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

    public DbSet<MobileSession> MobileSessions =>
        Set<MobileSession>();

    public DbSet<WatchDevice> WatchDevices =>
        Set<WatchDevice>();

    public DbSet<WatchPairingCode> WatchPairingCodes =>
        Set<WatchPairingCode>();

    public DbSet<WatchSyncOperation> WatchSyncOperations =>
        Set<WatchSyncOperation>();

    public DbSet<SupportTicket> SupportTickets =>
        Set<SupportTicket>();

    public DbSet<InboxMessage> InboxMessages =>
        Set<InboxMessage>();

    public DbSet<InboxPublication> InboxPublications => Set<InboxPublication>();
    public DbSet<InboxPublicationRead> InboxPublicationReads => Set<InboxPublicationRead>();

    public DbSet<SupportMessage> SupportMessages => Set<SupportMessage>();

    public DbSet<UserActivity> UserActivities => Set<UserActivity>();

    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();

    public DbSet<PushDeviceRegistration> PushDeviceRegistrations => Set<PushDeviceRegistration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserProfile>()
            .HasIndex(x => x.UserId)
            .IsUnique();

        modelBuilder.Entity<UserProfile>()
            .HasOne<IdentityUser>()
            .WithOne()
            .HasForeignKey<UserProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserProfile>()
            .Property(x => x.FirstName)
            .HasMaxLength(80);

        modelBuilder.Entity<UserProfile>()
            .Property(x => x.LastName)
            .HasMaxLength(80);

        modelBuilder.Entity<UserProfile>()
            .Property(x => x.Gender)
            .HasMaxLength(30);

        modelBuilder.Entity<MobileSession>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MobileSession>()
            .HasIndex(x => new { x.UserId, x.RevokedAtUtc });

        modelBuilder.Entity<MobileSession>()
            .HasIndex(x => x.ExpiresAtUtc);

        modelBuilder.Entity<MobileSession>()
            .Property(x => x.DeviceName)
            .HasMaxLength(120);

        modelBuilder.Entity<WatchDevice>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WatchDevice>()
            .HasIndex(x => x.DeviceId)
            .IsUnique();

        modelBuilder.Entity<WatchDevice>()
            .HasIndex(x => new { x.UserId, x.RevokedAtUtc });

        modelBuilder.Entity<WatchDevice>()
            .HasIndex(x => x.RefreshTokenExpiresAtUtc);

        modelBuilder.Entity<WatchDevice>()
            .Property(x => x.DeviceId)
            .HasMaxLength(160);

        modelBuilder.Entity<WatchDevice>()
            .Property(x => x.DisplayName)
            .HasMaxLength(120);

        modelBuilder.Entity<WatchDevice>()
            .Property(x => x.Platform)
            .HasMaxLength(30);

        modelBuilder.Entity<WatchDevice>()
            .Property(x => x.RefreshTokenHash)
            .HasMaxLength(256);

        modelBuilder.Entity<WatchDevice>()
            .Property(x => x.AppVersion)
            .HasMaxLength(40);

        modelBuilder.Entity<WatchDevice>()
            .Property(x => x.DeviceModel)
            .HasMaxLength(120);

        modelBuilder.Entity<WatchDevice>()
            .ToTable(table => table.HasCheckConstraint(
                "CK_WatchDevices_RefreshTokenExpiry",
                "[RefreshTokenExpiresAtUtc] > [CreatedAtUtc]"));

        modelBuilder.Entity<WatchPairingCode>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WatchPairingCode>()
            .HasIndex(x => x.CodeHash);

        modelBuilder.Entity<WatchPairingCode>()
            .HasIndex(x => new { x.UserId, x.UsedAtUtc, x.ExpiresAtUtc });

        modelBuilder.Entity<WatchPairingCode>()
            .HasIndex(x => x.ExpiresAtUtc);

        modelBuilder.Entity<WatchPairingCode>()
            .Property(x => x.CodeHash)
            .HasMaxLength(256);

        modelBuilder.Entity<WatchPairingCode>()
            .ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_WatchPairingCodes_Expiry",
                    "[ExpiresAtUtc] > [CreatedAtUtc]");
                table.HasCheckConstraint(
                    "CK_WatchPairingCodes_AttemptCount",
                    "[AttemptCount] >= 0");
            });

        modelBuilder.Entity<WatchSyncOperation>()
            .HasKey(x => x.OperationId);

        modelBuilder.Entity<WatchSyncOperation>()
            .HasOne(x => x.WatchDevice)
            .WithMany()
            .HasForeignKey(x => x.WatchDeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WatchSyncOperation>()
            .HasIndex(x => new { x.WatchDeviceId, x.ReceivedAtUtc });

        modelBuilder.Entity<WatchSyncOperation>()
            .HasIndex(x => x.ExpiresAtUtc);

        modelBuilder.Entity<WatchSyncOperation>()
            .Property(x => x.OperationType)
            .HasMaxLength(40);

        modelBuilder.Entity<WatchSyncOperation>()
            .Property(x => x.ResultJson)
            .HasMaxLength(16000);

        modelBuilder.Entity<WatchSyncOperation>()
            .ToTable(table => table.HasCheckConstraint(
                "CK_WatchSyncOperations_Expiry",
                "[ExpiresAtUtc] > [ReceivedAtUtc]"));

        modelBuilder.Entity<SupportTicket>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SupportTicket>()
            .HasIndex(x => x.TicketNumber)
            .IsUnique();

        modelBuilder.Entity<SupportTicket>()
            .HasIndex(x => x.UserId);

        modelBuilder.Entity<SupportTicket>()
            .HasIndex(x => x.CreatedAtUtc);

        modelBuilder.Entity<SupportTicket>()
            .HasIndex(x => new { x.Status, x.UpdatedAtUtc });

        modelBuilder.Entity<WorkoutHistory>()
            .HasIndex(x => x.Date);

        modelBuilder.Entity<SupportTicket>()
            .Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        modelBuilder.Entity<SupportTicket>()
            .Property(x => x.TelegramDeliveryStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<SupportMessage>()
            .HasOne(x => x.SupportTicket)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.SupportTicketId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SupportMessage>()
            .HasIndex(x => new { x.SupportTicketId, x.CreatedAtUtc });

        modelBuilder.Entity<SupportMessage>()
            .Property(x => x.SenderType)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<UserActivity>()
            .HasOne<IdentityUser>()
            .WithOne()
            .HasForeignKey<UserActivity>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserActivity>()
            .HasIndex(x => x.LastSeenAtUtc);

        modelBuilder.Entity<AdminAuditLog>()
            .HasIndex(x => x.CreatedAtUtc);

        modelBuilder.Entity<AdminAuditLog>()
            .HasIndex(x => new { x.AdminUserId, x.CreatedAtUtc });

        modelBuilder.Entity<PushDeviceRegistration>()
            .HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PushDeviceRegistration>()
            .HasIndex(x => new { x.UserId, x.InstallationId }).IsUnique();
        modelBuilder.Entity<PushDeviceRegistration>().HasIndex(x => x.PushToken);
        modelBuilder.Entity<PushDeviceRegistration>().Property(x => x.InstallationId).HasMaxLength(120);
        modelBuilder.Entity<PushDeviceRegistration>().Property(x => x.Platform).HasMaxLength(20);
        modelBuilder.Entity<PushDeviceRegistration>().Property(x => x.PushToken).HasMaxLength(4096);

        modelBuilder.Entity<InboxMessage>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InboxMessage>()
            .HasOne(x => x.SupportTicket)
            .WithMany(x => x.InboxMessages)
            .HasForeignKey(x => x.SupportTicketId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<InboxMessage>()
            .HasIndex(x => new { x.UserId, x.ReadAtUtc, x.CreatedAtUtc });

        modelBuilder.Entity<InboxMessage>()
            .HasIndex(x => x.TelegramMessageId)
            .IsUnique()
            .HasFilter("[TelegramMessageId] IS NOT NULL");

        modelBuilder.Entity<InboxMessage>()
            .Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(30);

        modelBuilder.Entity<InboxPublication>()
            .HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<InboxPublication>().HasIndex(x => x.PublishedAtUtc);
        modelBuilder.Entity<InboxPublication>().Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
        modelBuilder.Entity<InboxPublicationRead>().HasKey(x => new { x.PublicationId, x.UserId });
        modelBuilder.Entity<InboxPublicationRead>().HasOne(x => x.Publication).WithMany(x => x.Reads)
            .HasForeignKey(x => x.PublicationId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<InboxPublicationRead>().HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

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

        modelBuilder.Entity<ExerciseTemplateSet>()
            .Property(x => x.Version)
            .IsConcurrencyToken();

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
