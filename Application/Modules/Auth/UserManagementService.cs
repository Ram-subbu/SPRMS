using SPRMS.API.Application.Interfaces;
using SPRMS.API.Domain.Entities;
using SPRMS.API.Infrastructure.Persistence;
using SPRMS.Services.Domain;
using UserCreateRequest = SPRMS.API.Application.DTOs.UserCreateRequest;

namespace SPRMS.API.Application.Modules.Auth;

public sealed class UserManagementService(AppDbContext db, IPasswordService pwdSvc) : IUserManagementService
{
    public async Task<long> CreateUserAsync(UserCreateRequest request, CancellationToken ct = default)
    {
        var (hash, salt) = await pwdSvc.HashPasswordAsync(request.Password);

        var user = new User
        {
            FullName     = request.Username,
            Email        = request.Email,
            PasswordHash = hash,
            PasswordSalt = salt,
            IsActive     = true,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        if (request.RoleIds.Count > 0)
        {
            var userRoles = request.RoleIds.Select(roleId => new UserRole
            {
                UserID     = user.UserID,
                RoleID     = roleId,
                AssignedBy = user.UserID,
                AssignedOn = DateTime.UtcNow,
            });

            db.UserRoles.AddRange(userRoles);
            await db.SaveChangesAsync(ct);
        }

        return user.UserID;
    }
}
