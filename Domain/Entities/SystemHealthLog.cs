namespace SPRMS.API.Domain.Entities;

public class SystemHealthLog
{
    public long     HealthLogID    { get; set; }
    public string   CheckName      { get; set; } = "";
    public string   Status         { get; set; } = "";
    public int?     ResponseTimeMs { get; set; }
    public string?  Details        { get; set; }
    public string?  MachineName    { get; set; }
    public string?  AppVersion     { get; set; }
    public DateTime CheckedOn      { get; set; } = DateTime.UtcNow;
    public string   CreatedBy      { get; set; } = "system";
    public DateTime CreatedOn      { get; set; } = DateTime.UtcNow;
}

