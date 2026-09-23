namespace WorkoutPlanner.Api.Contracts;

public sealed record AccountSessionResponse(
    Guid Id, string? DeviceName, DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc, DateTime? LastRefreshedAtUtc, bool IsCurrent);
