namespace SPRMS.API.Domain.Entities;

public class IntegrationErrorLog
{
    public long     IntErrLogID     { get; set; }
    public string   ExternalSystem  { get; set; } = "";
    public string   OperationName   { get; set; } = "";
    public string?  RequestPayload  { get; set; }
    public string?  ResponsePayload { get; set; }
    public long?    HTTPStatusCode  { get; set; }
    public string   ErrorMessage    { get; set; } = "";
    public byte     RetryCount      { get; set; }
    public bool     IsRetryable     { get; set; }
    public DateTime? ResolvedOn     { get; set; }
    public string?  EntityType      { get; set; }
    public long?    EntityID        { get; set; }
    public string   CreatedBy       { get; set; } = "system";
    public DateTime CreatedOn       { get; set; } = DateTime.UtcNow;
}

