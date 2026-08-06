namespace WorkoutPlanner.Api.Contracts;

public sealed record MobileRegisterRequest(string Email, string Password);
public sealed record MobileLoginRequest(string Email, string Password, string? DeviceName = null);
public sealed record MobileRefreshRequest(string RefreshToken);
public sealed record RegistrationResponse(string Email);
public sealed record ProfileResponse(string Email, string FirstName, string LastName, DateTime? BirthDate, string Gender, bool HasProfile);
public sealed record UpdateProfileRequest(string FirstName, string LastName, DateTime? BirthDate, string Gender);
public sealed record ChangePasswordApiRequest(string CurrentPassword, string NewPassword);
