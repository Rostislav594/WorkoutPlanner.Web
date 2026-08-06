using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using WorkoutPlanner.Web.Api.Contracts;
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
            .WithTags("Authentication")
            .AllowAnonymous();

        authentication.MapPost("/register", RegisterAsync)
            .Produces<RegistrationResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        authentication.MapPost("/login", LoginAsync)
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        authentication.MapPost("/refresh", RefreshAsync)
            .Produces<AccessTokenResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        var bearerOnly = new AuthorizeAttribute
        {
            AuthenticationSchemes = IdentityConstants.BearerScheme
        };

        api.MapGet("/profile", GetProfileAsync)
            .WithTags("Profile")
            .RequireAuthorization(bearerOnly)
            .Produces<ProfileResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        api.MapPut("/profile", UpdateProfileAsync)
            .WithTags("Profile")
            .RequireAuthorization(bearerOnly)
            .Produces<ProfileResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

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
        CancellationToken cancellationToken)
    {
        var errors = ValidateCredentials(request.Email, request.Password);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        cancellationToken.ThrowIfCancellationRequested();
        signInManager.AuthenticationScheme = IdentityConstants.BearerScheme;
        var result = await signInManager.PasswordSignInAsync(
            request.Email.Trim(),
            request.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        return result.Succeeded
            ? Results.Empty
            : Results.Problem(
                title: "Invalid email or password.",
                statusCode: StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> RefreshAsync(
        MobileRefreshRequest request,
        SignInManager<IdentityUser> signInManager,
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

        var principal = await signInManager.CreateUserPrincipalAsync(user);
        return Results.SignIn(
            principal,
            authenticationScheme: IdentityConstants.BearerScheme);
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
}
