using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace WorkoutPlanner.Web.Tests.Infrastructure;

internal sealed class TestWebHostEnvironment : IWebHostEnvironment
{
    public required string ApplicationName { get; set; }

    public required IFileProvider WebRootFileProvider { get; set; }

    public required string WebRootPath { get; set; }

    public required string EnvironmentName { get; set; }

    public required string ContentRootPath { get; set; }

    public required IFileProvider ContentRootFileProvider { get; set; }
}
