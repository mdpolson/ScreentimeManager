using System;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ScreentimeManager.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
    }

    /// <summary>
    /// Gets or sets the configured screen time rules per user.
    /// </summary>
#pragma warning disable CA1819 // Properties should not return arrays
    public UserScreenTimeRule[] UserRules { get; set; } = Array.Empty<UserScreenTimeRule>();
#pragma warning restore CA1819 // Properties should not return arrays
}
