namespace SPRMS.Common;

public interface IGeoIPService
{
    Task<GeoInfo> LookupAsync(string ip, CancellationToken ct = default);
}

public interface IDeviceService
{
    DeviceInfo Parse(string? userAgent);
}

public record GeoInfo(
    string? Country,
    string? Region,
    string? City,
    decimal? Lat,
    decimal? Lng,
    string? ISP,
    string? ASN,
    bool IsThreat = false,
    string? ThreatDetail = null);

public record DeviceInfo(string? DeviceType, string? DeviceName, string? OSName, string? BrowserName);

