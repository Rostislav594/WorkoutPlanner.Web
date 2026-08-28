namespace WorkoutPlanner.Web.Application.Contracts;

public sealed record PushDeviceRegistrationResult(
    string InstallationId,
    string Platform,
    DateTimeOffset UpdatedAt);

public sealed record PushDeviceTarget(Guid Id, string InstallationId, string Platform, string PushToken);
