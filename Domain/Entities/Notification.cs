namespace SPRMS.API.Domain.Entities;

public class Notification : AuditEntity
{
    public long      NotificationID   { get; set; }
    public long      RecipientID      { get; set; }
    public string    Channel          { get; set; } = "InApp";
    public string    NotificationType { get; set; } = "";
    public string    Subject          { get; set; } = "";
    public string    Body             { get; set; } = "";
    public bool      IsRead           { get; set; }
    public DateTime? ScheduledAt      { get; set; }
    public DateTime? SentAt           { get; set; }
    public string?   FailureReason    { get; set; }
    public User      Recipient        { get; set; } = null!;
}

