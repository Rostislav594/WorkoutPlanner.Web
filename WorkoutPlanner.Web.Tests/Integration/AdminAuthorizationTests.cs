using Microsoft.AspNetCore.Authorization;
using WorkoutPlanner.Web.Components.Pages;
using WorkoutPlanner.Web.Components.Pages.Admin;

namespace WorkoutPlanner.Web.Tests.Integration;

public sealed class AdminAuthorizationTests
{
    [Theory]
    [InlineData(typeof(Home))]
    [InlineData(typeof(AdminHome))]
    [InlineData(typeof(AdminPublications))]
    [InlineData(typeof(AdminUsers))]
    [InlineData(typeof(AdminUserDetail))]
    [InlineData(typeof(AdminSupport))]
    [InlineData(typeof(AdminSupportTicket))]
    [InlineData(typeof(AdminSystem))]
    public void AdministrativePages_RequireAdminRole(Type pageType)
    {
        var attributes = pageType
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .ToArray();

        Assert.Contains(attributes, attribute =>
            string.Equals(attribute.Roles, "Admin", StringComparison.Ordinal));
    }
}
