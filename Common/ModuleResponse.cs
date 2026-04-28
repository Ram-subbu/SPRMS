namespace SPRMS.API.Common;

public class ModuleResponse
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string? Location { get; set; }
    public object? Data { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = [];
}