using System;

namespace Jellyfin.Plugin.ScreentimeManager.Configuration;

/// <summary>
/// Screen time rules for a specific user.
/// </summary>
public class UserScreenTimeRule
{
    /// <summary>
    /// Gets or sets the UserId.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the reset period for all rules for this user.
    /// </summary>
    public ResetPeriod ResetInterval { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a global time limit is enabled.
    /// </summary>
    public bool IsGlobalLimitEnabled { get; set; }

    /// <summary>
    /// Gets or sets the global time limit in minutes.
    /// </summary>
    public int GlobalTimeLimitMinutes { get; set; }

    /// <summary>
    /// Gets or sets specific library rules.
    /// </summary>
#pragma warning disable CA1819 // Properties should not return arrays
    public LibraryScreenTimeRule[] LibraryRules { get; set; } = Array.Empty<LibraryScreenTimeRule>();
#pragma warning restore CA1819 // Properties should not return arrays
}
