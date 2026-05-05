namespace SPRMS.API.Application.DTOs;

public record UserCreateRequest(
    string Username,
    string Email,
    string Password,
    List<long> RoleIds
);
