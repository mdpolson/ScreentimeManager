namespace Jellyfin.Plugin.ScreentimeManager.Configuration;

/// <summary>
/// The reset period for screen time limits.
/// </summary>
public enum ResetPeriod
{
    /// <summary>
    /// Resets every day at midnight.
    /// </summary>
    Daily,

    /// <summary>
    /// Resets every week.
    /// </summary>
    Weekly
}
