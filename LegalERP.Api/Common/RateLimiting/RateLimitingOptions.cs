using System.Collections.Generic;

namespace LegalERP.Api.Common.RateLimiting;

public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public PolicyRateLimitOptions Auth { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };
    public PolicyRateLimitOptions General { get; set; } = new() { PermitLimit = 150, WindowSeconds = 60 };
    public bool BypassLoopback { get; set; } = true;
    public List<string> WhitelistedIps { get; set; } = new();
    public string? InternalBypassKey { get; set; }

    // Flat compatibility accessors
    public int AuthPermitLimit
    {
        get => Auth.PermitLimit;
        set => Auth.PermitLimit = value;
    }

    public int AuthWindowSeconds
    {
        get => Auth.WindowSeconds;
        set => Auth.WindowSeconds = value;
    }

    public int GeneralPermitLimit
    {
        get => General.PermitLimit;
        set => General.PermitLimit = value;
    }

    public int GeneralWindowSeconds
    {
        get => General.WindowSeconds;
        set => General.WindowSeconds = value;
    }
}

public class PolicyRateLimitOptions
{
    public int PermitLimit { get; set; } = 10;
    public int WindowSeconds { get; set; } = 60;
}
