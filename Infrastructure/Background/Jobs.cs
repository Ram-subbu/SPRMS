using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SPRMS.API.Domain.Entities;
using SPRMS.API.Infrastructure.Persistence;
using SPRMS.Domain.Enums;

namespace SPRMS.Services.Background;

public sealed class StipendReminderJob(AppDbContext db)
{
    public async Task RunAsync()
    {
        var cfg   = await db.SystemConfigurations.Where(c => c.ConfigKey == "StipendReminderDays").Select(c => c.ConfigValue).FirstOrDefaultAsync();
        var days  = int.TryParse(cfg, out var d) ? d : 7;
        var cutoff= DateTime.UtcNow.AddDays(days);
        var today = DateTime.UtcNow.Date;
        var due   = await db.ProgressReports
            .Include(r => r.Scholarship).ThenInclude(s => s.StudentProfile).ThenInclude(sp => sp.User)
            .Where(r => r.Status == Status.Pending && r.DueDate >= today && r.DueDate <= cutoff)
            .ToListAsync();
        foreach (var rpt in due)
        {
            var u = rpt.Scholarship.StudentProfile.User;
            var n = new SPRMS.API.Domain.Entities.Notification {
                RecipientID = u.UserID, Channel = "Email", NotificationType = "StipendReminder",
                Subject = $"Progress Report Due — {rpt.ReportingPeriod}",
                Body = $"Dear {u.FullName},\n\nYour report for {rpt.ReportingPeriod} is due on {rpt.DueDate:dd MMM yyyy}. Please submit to avoid stipend delay.\n\nRCMS",
                CreatedBy = "system", CreatedOn = DateTime.UtcNow
            };
            db.Notifications.Add(n);
        }
        if (due.Any()) await db.SaveChangesAsync();
    }
}

public sealed class RefundAlertJob(AppDbContext db)
{
    public async Task RunAsync()
    {
        var before = DateTime.UtcNow.AddDays(90).Date;
        var after  = DateTime.UtcNow.AddDays(-90).Date;
        var ledgers= await db.RefundLedgers
            .Include(r => r.Termination).ThenInclude(t => t.Scholarship)
                .ThenInclude(s => s.StudentProfile).ThenInclude(sp => sp.User)
            .Where(r => r.Status != Status.PaidInFull && (r.DueDate.Date == before || r.DueDate.Date == after))
            .ToListAsync();
        foreach (var l in ledgers)
        {
            var u   = l.Termination.Scholarship.StudentProfile.User;
            var pre = l.DueDate.Date == before;
            db.Notifications.Add(new SPRMS.API.Domain.Entities.Notification {
                RecipientID = u.UserID, Channel = "Email",
                NotificationType = pre ? "RefundDue" : "RefundOverdue",
                Subject = pre ? "Refund Due in 90 days" : "URGENT: Refund Overdue",
                Body = $"Dear {u.FullName},\n\nRefund of BTN {l.TotalOwed:N2} {(pre?"due on":"was due on")} {l.DueDate:dd MMM yyyy}. Outstanding: BTN {l.OutstandingBalance:N2}.",
                CreatedBy = "system", CreatedOn = DateTime.UtcNow
            });
        }
        if (ledgers.Any()) await db.SaveChangesAsync();
    }
}

public sealed class CourseEndNotifyJob(AppDbContext db)
{
    public async Task RunAsync()
    {
        var today  = DateTime.UtcNow.Date;
        var cutoff = DateTime.UtcNow.AddDays(30).Date;
        var nearing= await db.Scholarships
            .Include(s => s.StudentProfile).ThenInclude(sp => sp.User)
            .Where(s => s.Status == Status.Active && s.CurrentEndDate.Date >= today && s.CurrentEndDate.Date <= cutoff)
            .ToListAsync();
        foreach (var sch in nearing)
            db.Notifications.Add(new SPRMS.API.Domain.Entities.Notification {
                RecipientID = sch.StudentProfile.UserID, Channel = "Email",
                NotificationType = "CourseEndApproaching",
                Subject = $"Scholarship ends {sch.CurrentEndDate:dd MMM yyyy}",
                Body = $"Dear {sch.StudentProfile.User.FullName},\n\nYour scholarship ends on {sch.CurrentEndDate:dd MMM yyyy}. Ensure all reports are submitted.",
                CreatedBy = "system", CreatedOn = DateTime.UtcNow
            });
        var complete = await db.Scholarships
            .Where(s => s.Status == Status.Active && s.CurrentEndDate.Date == today)
            .ToListAsync();
        foreach (var sch in complete)
        {
            sch.Status = Status.Completed;
            if (!sch.LegacyFlag)
                sch.ObligationYears = Math.Round((decimal)(sch.CurrentEndDate - sch.StartDate).TotalDays / 365m, 1);
        }
        if (nearing.Any() || complete.Any()) await db.SaveChangesAsync();
    }
}

public sealed class HealthCheckLogJob(HealthCheckService health, IConfiguration cfg)
{
    public async Task RunAsync()
    {
        var report = await health.CheckHealthAsync();
        await using var conn = new SqlConnection(cfg.GetConnectionString("DefaultConnection"));
        await conn.OpenAsync();
        foreach (var (name, entry) in report.Entries)
        {
            await using var cmd = new SqlCommand(@"INSERT INTO dbo.SystemHealthLogs(CheckName,Status,ResponseTimeMs,Details,MachineName,AppVersion,CheckedOn,CreatedBy,CreatedOn) VALUES(@cn,@st,@ms,@det,@mn,@av,GETDATE(),'system',GETDATE())", conn);
            cmd.Parameters.AddRange([
                new("@cn",name), new("@st",entry.Status.ToString()),
                new("@ms",(int)entry.Duration.TotalMilliseconds),
                new("@det",(object?)(entry.Description??string.Join(",",entry.Data.Select(d=>$"{d.Key}={d.Value}")))??DBNull.Value),
                new("@mn",Environment.MachineName),
                new("@av",typeof(HealthCheckLogJob).Assembly.GetName().Version?.ToString()??"1.0"),
            ]);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
