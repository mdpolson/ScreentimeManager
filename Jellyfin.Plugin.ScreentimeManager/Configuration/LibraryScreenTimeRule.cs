namespace Jellyfin.Plugin.ScreentimeManager.Configuration;

/// <summary>
/// A screen time limit rule for a specific library.
/// </summary>
public class LibraryScreenTimeRule
{
    /// <summary>
    /// Gets or sets the Jellyfin Library Id.
    /// </summary>
    public string LibraryId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this rule is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the time limit in minutes.
    /// </summary>
    public int TimeLimitMinutes { get; set; }
}
