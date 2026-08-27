using WorkoutPlanner.Web.Application.Abstractions;

namespace WorkoutPlanner.Web.Services.Admin;

public static class AdminOperationsServiceCollectionExtensions
{
    public static IServiceCollection AddGymPlannerAdminOperations(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<AdminAccessVerifier>();
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IAdminSupportService, AdminSupportService>();
        services.AddScoped<IAdminSystemHealthService, AdminSystemHealthService>();
        return services;
    }
}
