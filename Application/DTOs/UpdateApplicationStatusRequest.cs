namespace SPRMS.API.Application.DTOs;

public class UpdateApplicationStatusRequest
{
    public long ApplicationID { get; set; }
    public string Status { get; set; } = "";
    // Add other properties
}