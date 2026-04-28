using Microsoft.EntityFrameworkCore.Diagnostics;
using SPRMS.Common;

namespace SPRMS.API.Infrastructure.Persistence;

public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly ILogChannel _logChannel;

    public AuditInterceptor(ILogChannel logChannel)
    {
        _logChannel = logChannel;
    }

    // Override methods as needed for auditing
}