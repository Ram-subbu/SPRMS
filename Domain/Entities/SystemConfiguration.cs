namespace SPRMS.API.Domain.Entities;

public class SystemConfiguration : AuditEntity
{
    public long    ConfigID    { get; set; }
    public string  ConfigKey   { get; set; } = "";
    public string  ConfigValue { get; set; } = "";
    public string  DataType    { get; set; } = "String";
    public string? Description { get; set; }
}

