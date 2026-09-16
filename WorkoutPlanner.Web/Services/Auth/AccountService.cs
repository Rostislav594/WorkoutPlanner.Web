using Microsoft.AspNetCore.Identity;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;
using WorkoutPlanner.Web.Services.Localization;

namespace WorkoutPlanner.Web.Services.Auth;

public sealed class AccountService : IAccountService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CurrentUserService _currentUser;

    public AccountService(IServiceScopeFactory scopeFactory, CurrentUserService currentUser)
    {
        _scopeFactory = scopeFactory;
        _currentUser = currentUser;
    }

    public async Task<OperationResult> ChangePasswordAsync(
        PasswordChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var userId = await _currentUser.GetRequiredUserIdAsync();
        await using var scope = _scopeFactory.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<IdentityUser>>();
        var user = await users.FindByIdAsync(userId);
        if (user is null)
            return OperationResult.Failure(ServerTexts.Current["Server_Account_UserNotFound"]);
        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return new OperationResult(false, result.Errors.Select(TranslateIdentityError).ToList());
        await signIn.RefreshSignInAsync(user);
        return OperationResult.Success;
    }

    public async Task<OperationResult> DeleteCurrentAccountAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var deletion = scope.ServiceProvider.GetRequiredService<AccountDeletionService>();
        var result = await deletion.DeleteCurrentAccountAsync(cancellationToken);
        return result.Succeeded
            ? OperationResult.Success
            : new OperationResult(false, result.Errors.Select(x => x.Description).ToList());
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var scope = _scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SignInManager<IdentityUser>>().SignOutAsync();
    }

    private static string TranslateIdentityError(IdentityError error) => error.Code switch
    {
        "PasswordMismatch" => ServerTexts.Current["Password_Mismatch"],
        "PasswordTooShort" => ServerTexts.Current["Password_TooShort"],
        "PasswordRequiresDigit" => ServerTexts.Current["Password_RequiresDigit"],
        "PasswordRequiresLower" => ServerTexts.Current["Password_RequiresLower"],
        "PasswordRequiresUpper" => ServerTexts.Current["Password_RequiresUpper"],
        "PasswordRequiresNonAlphanumeric" => ServerTexts.Current["Password_RequiresNonAlphanumeric"],
        _ => error.Description
    };
}
