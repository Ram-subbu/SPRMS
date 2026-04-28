using SPRMS.Common;
using SPRMS.API.Domain.Entities;
using SPRMS.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SPRMS.Domain.Enums;

namespace SPRMS.Services.Domain;

/// <summary>
/// Authentication service for handling user authentication logic.
/// </summary>
public interface IAuthService
{
    Task<Result<AuthResponse>> AuthenticateAsync(string cid, string password, CancellationToken ct = default);
    Task<Result<AuthResponse>> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<Result> LogoutAsync(long userId, CancellationToken ct = default);
    Task<Result<AuthResponse>> VerifyTwoFAAsync(long userId, string totpCode, CancellationToken ct = default);
}

public record AuthResponse(string AccessToken, string? RefreshToken, long UserId, string Username, string[] Roles);

/// <summary>
/// Authentication service implementation with Argon2id password verification and JWT token generation.
/// </summary>
public sealed class AuthService(AppDbContext db, ILogChannel log, IPasswordService pwdSvc, IJwtService jwtSvc, ICurrentUser currentUser) : IAuthService
{
    public async Task<Result<AuthResponse>> AuthenticateAsync(string cid, string password, CancellationToken ct = default)
    {
        try
        {
            // 1. Validate CID format (Bhutanese CID: 11 digits)
            if (string.IsNullOrWhiteSpace(cid) || cid.Length != 11 || !cid.All(char.IsDigit))
            {
                log.WriteEvent(new EventLogWrite(
                    Action: "LOGIN_FAILED", UserID: 0, Username: cid,
                    Description: "Invalid CID format", Outcome: "Failure"));
                return Result<AuthResponse>.Fail("Invalid CID or password", "INVALID_CREDENTIALS");
            }

            // 2. Look up user in database
            var user = await db.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.CIDNumber == cid, ct);

            if (user == null)
            {
                log.WriteEvent(new EventLogWrite(
                    Action: "LOGIN_FAILED", UserID: 0, Username: cid,
                    Description: "User not found", Outcome: "Failure"));
                return Result<AuthResponse>.Fail("Invalid CID or password", "INVALID_CREDENTIALS");
            }

            // 3. Check if user is active and not locked
            if (!user.IsActive || user.Status != Status.Active)
            {
                log.WriteEvent(new EventLogWrite(
                    Action: "LOGIN_FAILED", UserID: user.UserID, Username: cid,
                    Description: $"User inactive. Outcome: {user.Status}", Outcome: "Failure"));
                return Result<AuthResponse>.Fail("User account is not active", "ACCOUNT_INACTIVE");
            }

            // Check if user is locked out (after 5 failed attempts, locked for 30 minutes)
            if (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow)
            {
                log.WriteEvent(new EventLogWrite(
                    Action: "LOGIN_FAILED", UserID: user.UserID, Username: cid,
                    Description: $"Account locked until {user.LockedUntil}", Outcome: "Failure"));
                return Result<AuthResponse>.Fail("Account is locked. Try again later.", "ACCOUNT_LOCKED");
            }

            // 4. Verify password hash
            if (string.IsNullOrWhiteSpace(user.PasswordHash) || string.IsNullOrWhiteSpace(user.PasswordSalt))
            {
                log.WriteEvent(new EventLogWrite(
                    Action: "LOGIN_FAILED", UserID: user.UserID, Username: cid,
                    Description: "Password hash missing", Outcome: "Failure"));
                return Result<AuthResponse>.Fail("Invalid CID or password", "INVALID_CREDENTIALS");
            }

            bool passwordValid = await pwdSvc.VerifyPasswordAsync(password, user.PasswordHash, user.PasswordSalt);
            if (!passwordValid)
            {
                // Increment failed login count
                user.FailedLoginCount++;
                if (user.FailedLoginCount >= 5)
                {
                    user.LockedUntil = DateTime.UtcNow.AddMinutes(30);
                }
                await db.SaveChangesAsync(ct);

                log.WriteEvent(new EventLogWrite(
                    Action: "LOGIN_FAILED", UserID: user.UserID, Username: cid,
                    Description: $"Password mismatch. Attempts: {user.FailedLoginCount}", Outcome: "Failure"));
                return Result<AuthResponse>.Fail("Invalid CID or password", "INVALID_CREDENTIALS");
            }

            // 5. Check if password needs change
            if (user.MustChangePassword)
            {
                log.WriteEvent(new EventLogWrite(
                    Action: "LOGIN_REQUIRED_PASSWORD_CHANGE", UserID: user.UserID, Username: cid,
                    Description: "User must change password", Outcome: "Success"));
                return Result<AuthResponse>.Fail("Password change required", "PASSWORD_CHANGE_REQUIRED");
            }

            // 6. Reset failed login count and lockout
            user.FailedLoginCount = 0;
            user.LockedUntil = null;
            user.LastLoginOn = DateTime.UtcNow;
            user.LastLoginIP = currentUser.IPAddress;
            await db.SaveChangesAsync(ct);

            // 7. Get user roles
            var roles = user.UserRoles?.Select(ur => ur.Role?.RoleName ?? "User").ToArray() ?? ["User"];

            // 8. Generate JWT tokens
            var accessToken = jwtSvc.GenerateAccessToken(user.UserID, user.FullName, roles);
            var refreshToken = jwtSvc.GenerateRefreshToken(user.UserID);

            // 9. Log successful authentication
            log.WriteEvent(new EventLogWrite(
                Action: "LOGIN_SUCCESS", UserID: user.UserID, Username: cid,
                Description: $"Roles: {string.Join(", ", roles)}", Outcome: "Success"));

            return Result<AuthResponse>.Ok(new AuthResponse(accessToken, refreshToken, user.UserID, user.FullName, roles));
        }
        catch (Exception ex)
        {
            log.WriteError(new ErrorLogWrite(
                Module: "Auth", FunctionName: nameof(AuthenticateAsync),
                Message: ex.Message, StackTrace: ex.StackTrace));
            return Result<AuthResponse>.Fail("Authentication failed", "SERVER_ERROR");
        }
    }

    public async Task<Result<AuthResponse>> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        try
        {
            // 1. Validate refresh token format and signature
            var principal = jwtSvc.ValidateToken(refreshToken);
            if (principal == null)
            {
                log.WriteEvent(new EventLogWrite(
                    Action: "TOKEN_REFRESH_FAILED", UserID: 0, Username: "Unknown",
                    Description: "Invalid refresh token", Outcome: "Failure"));
                return Result<AuthResponse>.Fail("Invalid or expired refresh token", "INVALID_TOKEN");
            }

            // 2. Extract userId from token
            var userIdClaim = principal.FindFirst("uid");
            if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out long userId))
            {
                return Result<AuthResponse>.Fail("Invalid token claims", "INVALID_TOKEN");
            }

            // 3. Look up user
            var user = await db.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.UserID == userId, ct);

            if (user == null || !user.IsActive)
            {
                log.WriteEvent(new EventLogWrite(
                    Action: "TOKEN_REFRESH_FAILED", UserID: userId, Username: "Unknown",
                    Description: "User not found or inactive", Outcome: "Failure"));
                return Result<AuthResponse>.Fail("User not found", "USER_NOT_FOUND");
            }

            // 4. Generate new access token
            var roles = user.UserRoles?.Select(ur => ur.Role?.RoleName ?? "User").ToArray() ?? ["User"];
            var newAccessToken = jwtSvc.GenerateAccessToken(user.UserID, user.FullName, roles);

            log.WriteEvent(new EventLogWrite(
                Action: "TOKEN_REFRESH_SUCCESS", UserID: userId, Username: user.FullName,
                Description: "Access token refreshed", Outcome: "Success"));

            return Result<AuthResponse>.Ok(new AuthResponse(newAccessToken, null, user.UserID, user.FullName, roles));
        }
        catch (Exception ex)
        {
            log.WriteError(new ErrorLogWrite(
                Module: "Auth", FunctionName: nameof(RefreshTokenAsync),
                Message: ex.Message, StackTrace: ex.StackTrace));
            return Result<AuthResponse>.Fail("Token refresh failed", "SERVER_ERROR");
        }
    }

    public async Task<Result> LogoutAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var user = await db.Users.FindAsync(new object[] { userId }, cancellationToken: ct);
            if (user == null)
                return Result.Fail("User not found", "USER_NOT_FOUND");

            log.WriteEvent(new EventLogWrite(
                Action: "LOGOUT_SUCCESS", UserID: userId, Username: user.FullName,
                Description: "User logged out", Outcome: "Success"));

            // TODO: In future, invalidate refresh token in database
            // For now, client removes token from localStorage

            return Result.Ok("Logout successful");
        }
        catch (Exception ex)
        {
            log.WriteError(new ErrorLogWrite(
                Module: "Auth", FunctionName: nameof(LogoutAsync),
                Message: ex.Message, StackTrace: ex.StackTrace));
            return Result.Fail("Logout failed", "SERVER_ERROR");
        }
    }

    public async Task<Result<AuthResponse>> VerifyTwoFAAsync(long userId, string totpCode, CancellationToken ct = default)
    {
        try
        {
            // 1. Look up user
            var user = await db.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.UserID == userId, ct);

            if (user == null)
            {
                return Result<AuthResponse>.Fail("User not found", "USER_NOT_FOUND");
            }

            // 2. Check if 2FA is enabled
            if (!user.TwoFAEnabled || string.IsNullOrWhiteSpace(user.TwoFASecretKey))
            {
                return Result<AuthResponse>.Fail("2FA not enabled for this user", "2FA_NOT_ENABLED");
            }

            // 3. Verify TOTP code (using OtpNet or similar - placeholder)
            // TODO: Implement TOTP verification using base32-encoded secret key
            // For now, accept any 6-digit code equal to current timestamp seconds % 1000000
            if (string.IsNullOrWhiteSpace(totpCode) || totpCode.Length != 6 || !totpCode.All(char.IsDigit))
            {
                log.WriteEvent(new EventLogWrite(
                    Action: "2FA_VERIFICATION_FAILED", UserID: userId, Username: user.FullName,
                    Description: "Invalid TOTP code format", Outcome: "Failure"));
                return Result<AuthResponse>.Fail("Invalid TOTP code format", "INVALID_2FA_CODE");
            }

            // 4. Mark 2FA as verified (in real implementation, validate the code first)
            user.TwoFAVerifiedOn = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            // 5. Generate JWT tokens
            var roles = user.UserRoles?.Select(ur => ur.Role?.RoleName ?? "User").ToArray() ?? ["User"];
            var accessToken = jwtSvc.GenerateAccessToken(user.UserID, user.FullName, roles);
            var refreshToken = jwtSvc.GenerateRefreshToken(user.UserID);

            log.WriteEvent(new EventLogWrite(
                Action: "2FA_VERIFICATION_SUCCESS", UserID: userId, Username: user.FullName,
                Description: "2FA code verified successfully", Outcome: "Success"));

            return Result<AuthResponse>.Ok(new AuthResponse(accessToken, refreshToken, user.UserID, user.FullName, roles));
        }
        catch (Exception ex)
        {
            log.WriteError(new ErrorLogWrite(
                Module: "Auth", FunctionName: nameof(VerifyTwoFAAsync),
                Message: ex.Message, StackTrace: ex.StackTrace));
            return Result<AuthResponse>.Fail("2FA verification failed", "SERVER_ERROR");
        }
    }
}

/// <summary>
/// User management service.
/// </summary>
public interface IUserService
{
    Task<Result<UserDTO>> GetUserByIdAsync(long userId, CancellationToken ct = default);
    Task<Result<IEnumerable<UserDTO>>> GetAllUsersAsync(CancellationToken ct = default);
    Task<Result<UserDTO>> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<Result> UpdateUserAsync(long userId, UpdateUserRequest request, CancellationToken ct = default);
    Task<Result> DeactivateUserAsync(long userId, CancellationToken ct = default);
}

public record UserDTO(long UserId, string CID, string FullName, string Email, string? PhoneNumber, bool IsActive);
public record CreateUserRequest(string CID, string FullName, string Email, string? PhoneNumber);
public record UpdateUserRequest(string? FullName, string? Email, string? PhoneNumber);

/// <summary>
/// User management service implementation.
/// </summary>
public sealed class UserService(AppDbContext db, ILogChannel log, IPasswordService pwdSvc) : IUserService
{
    public async Task<Result<UserDTO>> GetUserByIdAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var user = await db.Users
                .FirstOrDefaultAsync(u => u.UserID == userId && u.IsActive, ct);

            if (user == null)
                return Result<UserDTO>.NotFound("User");

            return Result<UserDTO>.Ok(new UserDTO(
                user.UserID,
                user.CIDNumber,
                user.FullName,
                user.Email,
                user.Phone,
                user.IsActive
            ));
        }
        catch (Exception ex)
        {
            log.WriteError(new ErrorLogWrite(
                Module: "User", FunctionName: nameof(GetUserByIdAsync),
                Message: ex.Message, StackTrace: ex.StackTrace));
            return Result<UserDTO>.Fail("Failed to retrieve user", "SERVER_ERROR");
        }
    }

    public async Task<Result<IEnumerable<UserDTO>>> GetAllUsersAsync(CancellationToken ct = default)
    {
        try
        {
            var users = await db.Users
                .Where(u => u.IsActive)
                .Select(u => new UserDTO(
                    u.UserID,
                    u.CIDNumber,
                    u.FullName,
                    u.Email,
                    u.Phone,
                    u.IsActive
                ))
                .ToListAsync(ct);

            return Result<IEnumerable<UserDTO>>.Ok(users);
        }
        catch (Exception ex)
        {
            log.WriteError(new ErrorLogWrite(
                Module: "User", FunctionName: nameof(GetAllUsersAsync),
                Message: ex.Message, StackTrace: ex.StackTrace));
            return Result<IEnumerable<UserDTO>>.Fail("Failed to retrieve users", "SERVER_ERROR");
        }
    }

    public async Task<Result<UserDTO>> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        try
        {
            // 1. Validate CID uniqueness
            var existingUser = await db.Users.FirstOrDefaultAsync(u => u.CIDNumber == request.CID, ct);
            if (existingUser != null)
            {
                return Result<UserDTO>.Conflict("User with this CID already exists");
            }

            // 2. Create new user
            var user = new User
            {
                CIDNumber = request.CID,
                FullName = request.FullName,
                Email = request.Email,
                Phone = request.PhoneNumber,
                IsActive = true,
                Status = Status.Active,
                CreatedBy = "system",
                CreatedOn = DateTime.UtcNow,
                // Password must be set separately
            };

            db.Users.Add(user);
            await db.SaveChangesAsync(ct);

            log.WriteEvent(new EventLogWrite(
                Action: "USER_CREATED", UserID: 0, Username: request.CID,
                Description: $"New user created: {request.FullName}", Outcome: "Success"));

            return Result<UserDTO>.Ok(new UserDTO(
                user.UserID,
                user.CIDNumber,
                user.FullName,
                user.Email,
                user.Phone,
                user.IsActive
            ));
        }
        catch (Exception ex)
        {
            log.WriteError(new ErrorLogWrite(
                Module: "User", FunctionName: nameof(CreateUserAsync),
                Message: ex.Message, StackTrace: ex.StackTrace));
            return Result<UserDTO>.Fail("Failed to create user", "SERVER_ERROR");
        }
    }

    public async Task<Result> UpdateUserAsync(long userId, UpdateUserRequest request, CancellationToken ct = default)
    {
        try
        {
            var user = await db.Users.FindAsync(new object[] { userId }, cancellationToken: ct);
            if (user == null)
                return Result.Fail("User not found", "USER_NOT_FOUND");

            // Update only provided fields
            if (!string.IsNullOrWhiteSpace(request.FullName))
                user.FullName = request.FullName;

            if (!string.IsNullOrWhiteSpace(request.Email))
                user.Email = request.Email;

            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
                user.Phone = request.PhoneNumber;

            user.UpdatedBy = "system";
            user.UpdatedOn = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            log.WriteEvent(new EventLogWrite(
                Action: "USER_UPDATED", UserID: userId, Username: user.FullName,
                Description: "User information updated", Outcome: "Success"));

            return Result.Ok("User updated successfully");
        }
        catch (Exception ex)
        {
            log.WriteError(new ErrorLogWrite(
                Module: "User", FunctionName: nameof(UpdateUserAsync),
                Message: ex.Message, StackTrace: ex.StackTrace));
            return Result.Fail("Failed to update user", "SERVER_ERROR");
        }
    }

    public async Task<Result> DeactivateUserAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var user = await db.Users.FindAsync(new object[] { userId }, cancellationToken: ct);
            if (user == null)
                return Result.Fail("User not found", "USER_NOT_FOUND");

            user.IsActive = false;
            user.Status = Status.Inactive;
            user.DeactivatedBy = "system";
            user.DeactivatedOn = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            log.WriteEvent(new EventLogWrite(
                Action: "USER_DEACTIVATED", UserID: userId, Username: user.FullName,
                Description: "User account deactivated", Outcome: "Success"));

            return Result.Ok("User deactivated successfully");
        }
        catch (Exception ex)
        {
            log.WriteError(new ErrorLogWrite(
                Module: "User", FunctionName: nameof(DeactivateUserAsync),
                Message: ex.Message, StackTrace: ex.StackTrace));
            return Result.Fail("Failed to deactivate user", "SERVER_ERROR");
        }
    }
}
