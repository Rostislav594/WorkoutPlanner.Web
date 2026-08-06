using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using WorkoutPlanner.Web.Api.Contracts;
using WorkoutPlanner.Web.Api.Security;
using WorkoutPlanner.Web.Application.Abstractions;
using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Api;

public static class GymPlannerApiEndpoints
{
    public static RouteGroupBuilder MapGymPlannerApi(
        this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1")
            .WithTags("GymPlanner API");

        var authentication = api.MapGroup("/auth")
            .WithTags("Authentication");

        authentication.MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .Produces<RegistrationResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        authentication.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        authentication.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .Produces<AccessTokenResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        authentication.MapPost("/logout", LogoutAsync)
            .RequireAuthorization(MobileApiAuthorization.PolicyName)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        api.MapGet("/profile", GetProfileAsync)
            .WithTags("Profile")
            .RequireAuthorization(MobileApiAuthorization.PolicyName)
            .Produces<ProfileResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        api.MapPut("/profile", UpdateProfileAsync)
            .WithTags("Profile")
            .RequireAuthorization(MobileApiAuthorization.PolicyName)
            .Produces<ProfileResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var account = api.MapGroup("/account")
            .WithTags("Account")
            .RequireAuthorization(MobileApiAuthorization.PolicyName);

        account.MapPost("/change-password", ChangePasswordAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        account.MapPost("/revoke-access", RevokeAccessAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        account.MapDelete("", DeleteAccountAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        api.MapWorkoutApiEndpoints();
        api.MapWorkoutLifecycleApiEndpoints();
        api.MapProgressApiEndpoints();
        api.MapOnboardingApiEndpoints();
        api.MapExercisePhotoApiEndpoints();

        return api;
    }

    private static async Task<IResult> RegisterAsync(
        MobileRegisterRequest request,
        UserManager<IdentityUser> userManager,
        IStarterPlanService starterPlanService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var errors = ValidateCredentials(request.Email, request.Password);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var email = request.Email.Trim();
        var user = new IdentityUser
        {
            UserName = email,
            Email = email
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return Results.ValidationProblem(ToValidationErrors(result));

        try
        {
            await starterPlanService.CreateForUserAsync(
                user.Id,
                cancellationToken);
        }
        catch (Exception exception)
        {
            var logger = loggerFactory.CreateLogger("ApiRegistration");
            logger.LogError(
                exception,
                "Failed to provision starter plans for API user {UserId}.",
                user.Id);

            var cleanupResult = await userManager.DeleteAsync(user);
            if (!cleanupResult.Succeeded)
            {
                logger.LogCritical(
                    "Failed to roll back API user {UserId} after starter plan provisioning failed.",
                    user.Id);
            }

            return Results.Problem(
                title: "Registration could not be completed.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.Created(
            "/api/v1/profile",
            new RegistrationResponse(email));
    }

    private static async Task<IResult> LoginAsync(
        MobileLoginRequest request,
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        MobileSessionService mobileSessions,
        IOptionsMonitor<BearerTokenOptions> bearerTokenOptions,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var errors = ValidateCredentials(request.Email, request.Password);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return Results.Problem(
                title: "Invalid email or password.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var result = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return Results.Problem(
                title: "Invalid email or password.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var refreshLifetime = bearerTokenOptions
            .Get(IdentityConstants.BearerScheme)
            .RefreshTokenExpiration;
        var expiresAtUtc = timeProvider.GetUtcNow()
            .Add(refreshLifetime)
            .UtcDateTime;
        var session = await mobileSessions.CreateAsync(
            user.Id,
            request.DeviceName,
            expiresAtUtc,
            cancellationToken);
        var principal = await signInManager.CreateUserPrincipalAsync(user);
        MobileSessionService.AddSessionClaim(principal, session.Id);
        return Results.SignIn(
            principal,
            authenticationScheme: IdentityConstants.BearerScheme);
    }

    private static async Task<IResult> RefreshAsync(
        MobileRefreshRequest request,
        SignInManager<IdentityUser> signInManager,
        MobileSessionService mobileSessions,
        IOptionsMonitor<BearerTokenOptions> bearerTokenOptions,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.RefreshToken)] = ["Refresh token is required."]
            });
        }

        cancellationToken.ThrowIfCancellationRequested();
        var protector = bearerTokenOptions
            .Get(IdentityConstants.BearerScheme)
            .RefreshTokenProtector;
        var ticket = protector.Unprotect(request.RefreshToken);

        if (ticket?.Properties?.ExpiresUtc is not { } expiresUtc ||
            timeProvider.GetUtcNow() >= expiresUtc ||
            await signInManager.ValidateSecurityStampAsync(ticket.Principal)
                is not IdentityUser user)
        {
            return Results.Unauthorized();
        }

        if (!await mobileSessions.RefreshAsync(
                ticket.Principal,
                user.Id,
                cancellationToken) ||
            !MobileSessionService.TryGetSessionId(
                ticket.Principal,
                out var sessionId))
        {
            return Results.Unauthorized();
        }

        var principal = await signInManager.CreateUserPrincipalAsync(user);
        MobileSessionService.AddSessionClaim(principal, sessionId);
        return Results.SignIn(
            principal,
            authenticationScheme: IdentityConstants.BearerScheme);
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        MobileSessionService mobileSessions,
        CancellationToken cancellationToken)
    {
        await mobileSessions.RevokeCurrentAsync(
            context.User,
            GetRequiredUserId(context.User),
            cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordApiRequest request,
        HttpContext context,
        UserManager<IdentityUser> userManager,
        MobileSessionService mobileSessions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.CurrentPassword)] = ["Current and new passwords are required."]
            });
        }

        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.GetUserAsync(context.User);
        if (user is null)
            return Results.Unauthorized();

        var result = await userManager.ChangePasswordAsync(
            user,
            request.CurrentPassword,
            request.NewPassword);
        if (!result.Succeeded)
            return Results.ValidationProblem(ToValidationErrors(result));

        await mobileSessions.RevokeAllAsync(user.Id, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RevokeAccessAsync(
        HttpContext context,
        MobileSessionService mobileSessions,
        CancellationToken cancellationToken)
    {
        await mobileSessions.RevokeAllAsync(
            GetRequiredUserId(context.User),
            cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAccountAsync(
        IAccountService accountService,
        CancellationToken cancellationToken)
    {
        var result = await accountService.DeleteCurrentAccountAsync(cancellationToken);
        return result.Succeeded
            ? Results.NoContent()
            : Results.Problem(
                title: "Account deletion could not be completed.",
                statusCode: StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> GetProfileAsync(
        HttpContext context,
        IProfileService profileService,
        CancellationToken cancellationToken)
    {
        var profile = await profileService.GetCurrentProfileAsync(cancellationToken);
        return Results.Ok(ToResponse(
            profile,
            context.User.Identity?.Name ?? string.Empty));
    }

    private static async Task<IResult> UpdateProfileAsync(
        UpdateProfileRequest request,
        HttpContext context,
        IProfileService profileService,
        CancellationToken cancellationToken)
    {
        var errors = ValidateProfile(request);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var update = new ProfileUpdateRequest(
            request.FirstName,
            request.LastName,
            request.BirthDate,
            request.Gender);
        var hasProfile = await profileService.CurrentUserHasProfileAsync(cancellationToken);
        var succeeded = hasProfile
            ? await profileService.UpdateCurrentProfileAsync(update, cancellationToken)
            : await profileService.CreateCurrentProfileAsync(update, cancellationToken);

        if (!succeeded)
        {
            return Results.Problem(
                title: "The profile changed concurrently. Retry the request.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var profile = await profileService.GetCurrentProfileAsync(cancellationToken);
        return Results.Ok(ToResponse(
            profile,
            context.User.Identity?.Name ?? string.Empty));
    }

    private static ProfileResponse ToResponse(
        UserProfile? profile,
        string fallbackEmail) =>
        new(
            profile?.Email ?? fallbackEmail,
            profile?.FirstName ?? string.Empty,
            profile?.LastName ?? string.Empty,
            profile?.BirthDate,
            profile?.Gender ?? string.Empty,
            profile is not null);

    private static Dictionary<string, string[]> ValidateCredentials(
        string? email,
        string? password)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(email) ||
            !new EmailAddressAttribute().IsValid(email))
        {
            errors[nameof(email)] = ["A valid email address is required."];
        }

        if (string.IsNullOrWhiteSpace(password))
            errors[nameof(password)] = ["Password is required."];

        return errors;
    }

    private static Dictionary<string, string[]> ValidateProfile(
        UpdateProfileRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.FirstName) || request.FirstName.Length > 80)
            errors[nameof(request.FirstName)] = ["First name is required and must not exceed 80 characters."];
        if (string.IsNullOrWhiteSpace(request.LastName) || request.LastName.Length > 80)
            errors[nameof(request.LastName)] = ["Last name is required and must not exceed 80 characters."];
        if (request.BirthDate is null)
            errors[nameof(request.BirthDate)] = ["Birth date is required."];
        if (string.IsNullOrWhiteSpace(request.Gender))
            errors[nameof(request.Gender)] = ["Gender is required."];
        return errors;
    }

    private static Dictionary<string, string[]> ToValidationErrors(
        IdentityResult result) =>
        result.Errors
            .GroupBy(x => x.Code, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x.Select(error => error.Description).Distinct().ToArray(),
                StringComparer.Ordinal);

    private static string GetRequiredUserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException(
            "Authenticated mobile principal has no user identifier.");
}
