using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;

namespace SPRMS;

public class HangfireAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // Try to get HttpContext - handle API variations
        var httpContextProperty = context.GetType().GetProperty("HttpContext", System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (httpContextProperty?.GetValue(context) is HttpContext ctx)
            return ctx.User.IsInRole("SystemAdmin");
        
        return false; // Default deny if unable to verify
    }
}

