using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SPRMS.Common;

namespace SPRMS.API.Infrastructure.Persistence;

public class AppDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;

    public AppDbContextFactory(DbContextOptions<AppDbContext> options)
    {
        _options = options;
    }

    public AppDbContext CreateDbContext()
    {
        return new AppDbContext(
            _options,
            new DesignTimeCurrentUser(),
            new DesignTimeAuditInterceptor());
    }
}

public class DesignTimeCurrentUser : ICurrentUser
{
    public long? UserID => null;
    public string Username => "";
    public string Role => "";
    public string IPAddress => "";
    public string? RequestID => null;
    public string? UserAgent => null;
    public bool HasPermission(string code) => false;
}

public class DesignTimeAuditInterceptor : AuditInterceptor
{
    public DesignTimeAuditInterceptor() : base(new DesignTimeLogChannel()) { }
}

public class DesignTimeLogChannel : ILogChannel
{
    public void WriteEvent(EventLogWrite e) { }
    public void WriteLogin(LoginLogWrite e) { }
    public void WriteAudit(AuditLogWrite e) { }
    public void WriteError(ErrorLogWrite e) { }
    public void WriteInt(IntLogWrite e) { }
    public ChannelReader<LogItem> Reader => throw new NotImplementedException();
}