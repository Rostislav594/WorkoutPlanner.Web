using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Models;
using WorkoutPlanner.Web.Tests.Infrastructure;

namespace WorkoutPlanner.Web.Tests.Integration;

public sealed class WearOsPersistenceTests
{
    [Fact]
    public async Task WatchGraph_EnforcesGlobalDeviceIdentity_AndCascadesWithUser()
    {
        await using var application = await TestApplication.CreateAsync();
        await application.CreateUserAsync("user-a");
        await application.CreateUserAsync("user-b");

        var now = DateTime.UtcNow;
        var deviceId = Guid.NewGuid();
        await using (var scope = application.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            var device = CreateDevice(deviceId, "user-a", "stable-installation", now);
            db.WatchDevices.Add(device);
            db.WatchPairingCodes.Add(new WatchPairingCode
            {
                Id = Guid.NewGuid(),
                UserId = "user-a",
                CodeHash = "pairing-code-hash",
                CreatedAtUtc = now,
                ExpiresAtUtc = now.AddMinutes(10)
            });
            db.WatchSyncOperations.Add(new WatchSyncOperation
            {
                OperationId = Guid.NewGuid(),
                WatchDevice = device,
                OperationType = "CompleteSet",
                EntityId = 123,
                ReceivedAtUtc = now,
                ExpiresAtUtc = now.Add(WatchSyncOperation.RetentionPeriod),
                ResultJson = "{}"
            });
            await db.SaveChangesAsync();
        }

        await using (var duplicateScope = application.CreateScope())
        {
            var db = duplicateScope.ServiceProvider
                .GetRequiredService<WorkoutDbContext>();
            db.WatchDevices.Add(CreateDevice(
                Guid.NewGuid(),
                "user-b",
                "stable-installation",
                now));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using (var deletionScope = application.CreateScope())
        {
            var db = deletionScope.ServiceProvider
                .GetRequiredService<WorkoutDbContext>();
            var user = await db.Users.SingleAsync(x => x.Id == "user-a");
            db.Users.Remove(user);
            await db.SaveChangesAsync();
        }

        await using var verificationScope = application.CreateScope();
        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<WorkoutDbContext>();
        Assert.False(await verificationDb.WatchDevices.AnyAsync(x =>
            x.UserId == "user-a"));
        Assert.False(await verificationDb.WatchPairingCodes.AnyAsync(x =>
            x.UserId == "user-a"));
        Assert.False(await verificationDb.WatchSyncOperations.AnyAsync(x =>
            x.WatchDeviceId == deviceId));
        Assert.True(await verificationDb.Users.AnyAsync(x => x.Id == "user-b"));
    }

    [Fact]
    public async Task WatchPersistence_RejectsInvalidLifetimesAndDuplicateOperationIds()
    {
        await using var application = await TestApplication.CreateAsync();
        await application.CreateUserAsync("user-a");
        var now = DateTime.UtcNow;
        var device = CreateDevice(Guid.NewGuid(), "user-a", "device-a", now);

        await using (var scope = application.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            db.WatchDevices.Add(device);
            db.WatchPairingCodes.Add(new WatchPairingCode
            {
                Id = Guid.NewGuid(),
                UserId = "user-a",
                CodeHash = "invalid-expiry",
                CreatedAtUtc = now,
                ExpiresAtUtc = now,
                AttemptCount = 0
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        var operationId = Guid.NewGuid();
        await using (var scope = application.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
            var storedDevice = await db.WatchDevices.SingleOrDefaultAsync(x =>
                x.Id == device.Id);
            if (storedDevice is null)
            {
                db.WatchDevices.Add(device);
                await db.SaveChangesAsync();
            }

            db.WatchSyncOperations.Add(CreateOperation(operationId, device.Id, now));
            await db.SaveChangesAsync();
        }

        await using (var duplicateScope = application.CreateScope())
        {
            var db = duplicateScope.ServiceProvider
                .GetRequiredService<WorkoutDbContext>();
            db.WatchSyncOperations.Add(CreateOperation(operationId, device.Id, now));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task ExerciseTemplateSetVersion_DetectsConcurrentUpdates()
    {
        await using var application = await TestApplication.CreateAsync();
        await application.CreateUserAsync("user-a");

        int setId;
        await using (var setupScope = application.CreateScope())
        {
            var db = setupScope.ServiceProvider
                .GetRequiredService<WorkoutDbContext>();
            var plan = new TrainingPlan
            {
                UserId = "user-a",
                WorkoutName = "Concurrency",
                Date = DateTime.Today,
                Exercises =
                [
                    new Exercise
                    {
                        UserId = "user-a",
                        Name = "Exercise",
                        WorkoutName = "Concurrency",
                        Sets =
                        [
                            new ExerciseTemplateSet
                            {
                                SetNumber = 1,
                                Weight = 50,
                                Repetitions = 8
                            }
                        ]
                    }
                ]
            };
            db.TrainingPlans.Add(plan);
            await db.SaveChangesAsync();
            setId = plan.Exercises[0].Sets[0].Id;
        }

        await using var firstScope = application.CreateScope();
        await using var secondScope = application.CreateScope();
        var firstDb = firstScope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
        var secondDb = secondScope.ServiceProvider.GetRequiredService<WorkoutDbContext>();
        var first = await firstDb.ExerciseTemplateSets.SingleAsync(x => x.Id == setId);
        var second = await secondDb.ExerciseTemplateSets.SingleAsync(x => x.Id == setId);

        first.Completed = true;
        first.Version++;
        await firstDb.SaveChangesAsync();

        second.Weight = 55;
        second.Version++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            secondDb.SaveChangesAsync());
    }

    private static WatchDevice CreateDevice(
        Guid id,
        string userId,
        string stableDeviceId,
        DateTime now) =>
        new()
        {
            Id = id,
            UserId = userId,
            DeviceId = stableDeviceId,
            DisplayName = "Test watch",
            Platform = "WearOS",
            CreatedAtUtc = now,
            RefreshTokenHash = "refresh-token-hash",
            RefreshTokenExpiresAtUtc = now.AddDays(30)
        };

    private static WatchSyncOperation CreateOperation(
        Guid operationId,
        Guid deviceId,
        DateTime now) =>
        new()
        {
            OperationId = operationId,
            WatchDeviceId = deviceId,
            OperationType = "CompleteSet",
            EntityId = 1,
            ReceivedAtUtc = now,
            ExpiresAtUtc = now.Add(WatchSyncOperation.RetentionPeriod),
            ResultJson = "{}"
        };
}
