namespace SPRMS.Common;

public interface ICurrentUser
{
    long? UserID { get; }
    string Username { get; }
    string Role { get; }
    string IPAddress { get; }
    string? RequestID { get; }
    string? UserAgent { get; }
    bool HasPermission(string code);
}

