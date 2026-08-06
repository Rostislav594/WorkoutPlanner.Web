using Microsoft.AspNetCore.Identity;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;

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
            return OperationResult.Failure("Пользователь не найден.");
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
        "PasswordMismatch" => "Текущий пароль указан неверно.",
        "PasswordTooShort" => "Пароль слишком короткий.",
        "PasswordRequiresDigit" => "Пароль должен содержать хотя бы одну цифру.",
        "PasswordRequiresLower" => "Пароль должен содержать хотя бы одну строчную латинскую букву.",
        "PasswordRequiresUpper" => "Пароль должен содержать хотя бы одну заглавную латинскую букву.",
        "PasswordRequiresNonAlphanumeric" => "Пароль должен содержать хотя бы один специальный символ.",
        _ => error.Description
    };
}
