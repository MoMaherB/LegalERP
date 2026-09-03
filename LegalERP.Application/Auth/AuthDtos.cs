namespace LegalERP.Application.Auth;

public record LoginDto(string Email, string Password, bool RememberMe = false);

public record ChangePasswordDto(string CurrentPassword, string NewPassword, string ConfirmNewPassword);

public record CreateUserDto(
    string FullName,
    string Email,
    string Password,
    string Role
);

public record UpdateUserDto(
    string FullName,
    string Email,
    string Role,
    bool IsActive
);

public record ResetUserPasswordDto(string NewPassword);

public record UserDto(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    bool IsActive,
    string? ProfilePicturePath,
    DateTime CreatedAt
);

public record CurrentUserDto(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    string? ProfilePicturePath
);
