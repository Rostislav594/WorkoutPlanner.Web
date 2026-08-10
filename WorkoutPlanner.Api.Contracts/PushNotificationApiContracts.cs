namespace WorkoutPlanner.Api.Contracts;

public sealed record RegisterPushDeviceRequest(
    string InstallationId,
    string Platform,
    string PushToken);

public sealed record PushDeviceRegistrationResponse(
    string InstallationId,
    string Platform,
    DateTimeOffset UpdatedAt);
