using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkoutPlanner.Web.Data;
using WorkoutPlanner.Web.Services;
using WorkoutPlanner.Web.Tests.Infrastructure;

namespace WorkoutPlanner.Web.Tests.Integration;

public sealed class StarterPlanServiceTests
{
    [Fact]
    public async Task CreateForUserAsync_CreatesFourPersonalIdempotentPlans()
    {
        await using var application = await TestApplication.CreateAsync();
        await application.CreateUserAsync("user-a");
        await application.CreateUserAsync("user-b");

        await using (var scope = application.CreateScope())
        {
            var service = scope.ServiceProvider
                .GetRequiredService<StarterPlanService>();

            await service.CreateForUserAsync("user-a");
            await service.CreateForUserAsync("user-a");
            await service.CreateForUserAsync("user-b");
        }

        await using var verificationScope = application.CreateScope();
        var db = verificationScope.ServiceProvider
            .GetRequiredService<WorkoutDbContext>();
        var userAPlans = await db.TrainingPlans
            .Where(x => x.UserId == "user-a")
            .ToListAsync();
        var userBPlans = await db.TrainingPlans
            .Where(x => x.UserId == "user-b")
            .ToListAsync();

        Assert.Equal(4, userAPlans.Count);
        Assert.Equal(4, userBPlans.Count);
        Assert.All(userAPlans, x => Assert.Equal("user-a", x.UserId));
        Assert.All(userBPlans, x => Assert.Equal("user-b", x.UserId));
        Assert.Empty(await db.TrainingPlans
            .Where(x => x.UserId == null)
            .ToListAsync());
    }
}
