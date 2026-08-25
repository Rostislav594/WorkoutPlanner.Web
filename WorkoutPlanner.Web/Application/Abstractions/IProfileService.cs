using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Application.Abstractions;

public interface IProfileService
{
    Task<UserProfile?> GetCurrentProfileAsync(
        CancellationToken cancellationToken = default);
    Task<bool> CurrentUserHasProfileAsync(
        CancellationToken cancellationToken = default);
    Task<bool> CreateCurrentProfileAsync(
        ProfileUpdateRequest request,
        CancellationToken cancellationToken = default);
    Task<bool> UpdateCurrentProfileAsync(
        ProfileUpdateRequest request,
        CancellationToken cancellationToken = default);
    Task<bool> UpdateRestTimerSettingsAsync(
        RestTimerSettingsUpdateRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAccountService
{
    Task<OperationResult> ChangePasswordAsync(
        PasswordChangeRequest request,
        CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteCurrentAccountAsync(
        CancellationToken cancellationToken = default);
    Task SignOutAsync(CancellationToken cancellationToken = default);
}

public interface IStarterPlanService
{
    Task CreateForUserAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
