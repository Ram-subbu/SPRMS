namespace SPRMS.Common;

public interface IFileService
{
    Task<(string Path, long Size, string Mime)> SaveAsync(IFormFile file, string folder, CancellationToken ct = default);
    bool IsAllowed(string name);
    bool WithinLimit(IFormFile file, int maxMb = 10);
}

public interface IEmailService
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

