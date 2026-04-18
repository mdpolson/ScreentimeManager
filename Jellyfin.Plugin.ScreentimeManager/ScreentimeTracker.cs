using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ScreentimeManager.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ScreentimeManager;

/// <summary>
/// Service responsible for tracking user playback duration and enforcing screen time limits.
/// </summary>
public class ScreentimeTracker : IHostedService, IDisposable
{
    private readonly ISessionManager _sessionManager;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<ScreentimeTracker> _logger;
    private readonly string _stateFilePath;

    /// <summary>
    /// Tracks usage: UserId -> (LibraryId -> Minutes). Global time is tracked with LibraryId = "Global".
    /// </summary>
    private ConcurrentDictionary<string, ConcurrentDictionary<string, double>> _usageState;

    /// <summary>
    /// Tracks when a session started playing.
    /// </summary>
    private ConcurrentDictionary<string, DateTime> _sessionStartTimes;

    /// <summary>
    /// Tracks when the usage state was last reset.
    /// </summary>
    private ConcurrentDictionary<string, DateTime> _lastResetTimes;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScreentimeTracker"/> class.
    /// </summary>
    /// <param name="sessionManager">The session manager.</param>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="logger">The logger.</param>
    public ScreentimeTracker(ISessionManager sessionManager, ILibraryManager libraryManager, ILogger<ScreentimeTracker> logger)
    {
        _sessionManager = sessionManager;
        _libraryManager = libraryManager;
        _logger = logger;
        _usageState = new ConcurrentDictionary<string, ConcurrentDictionary<string, double>>();
        _sessionStartTimes = new ConcurrentDictionary<string, DateTime>();

        _stateFilePath = Path.Combine(Plugin.Instance!.DataFolderPath, "ScreentimeState.json");
        LoadState();

        _lastResetTimes = new ConcurrentDictionary<string, DateTime>();
        Instance = this;
    }

    /// <summary>
    /// Gets the current instance of the tracker.
    /// </summary>
    public static ScreentimeTracker? Instance { get; private set; }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart += OnPlaybackStart;
        _sessionManager.PlaybackProgress += OnPlaybackProgress;
        _sessionManager.PlaybackStopped += OnPlaybackStopped;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart -= OnPlaybackStart;
        _sessionManager.PlaybackProgress -= OnPlaybackProgress;
        _sessionManager.PlaybackStopped -= OnPlaybackStopped;
        SaveState();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the usage for a specific user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>A dictionary of library IDs and their usage in minutes.</returns>
    public Dictionary<string, double> GetUserUsage(string userId)
    {
        CheckAndResetLimits(userId);
        if (_usageState.TryGetValue(userId, out var userUsage))
        {
            return new Dictionary<string, double>(userUsage);
        }

        return new Dictionary<string, double>();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="ScreentimeTracker"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sessionManager.PlaybackStart -= OnPlaybackStart;
            _sessionManager.PlaybackProgress -= OnPlaybackProgress;
            _sessionManager.PlaybackStopped -= OnPlaybackStopped;
        }
    }

    private string? GetLibraryId(BaseItem? item)
    {
        if (item == null)
        {
            return null;
        }

        var current = item;
        while (current != null && current.ParentId != Guid.Empty)
        {
            if (current.ParentId == _libraryManager.RootFolder.Id)
            {
                return current.Id.ToString("N");
            }

            current = _libraryManager.GetItemById(current.ParentId);
        }

        return current?.Id.ToString("N");
    }

    private void OnPlaybackStart(object? sender, PlaybackProgressEventArgs e)
    {
        var session = e.Session;
        if (session.UserId.Equals(Guid.Empty))
        {
            return;
        }

        string userId = session.UserId.ToString("N");
        string sessionId = session.Id;

        CheckAndResetLimits(userId);

        if (IsLimitExceeded(userId, GetLibraryId(e.Item)))
        {
            StopPlayback(session, "Screen time limit exceeded.");
            return;
        }

        _sessionStartTimes[sessionId] = DateTime.UtcNow;
    }

    private void OnPlaybackProgress(object? sender, PlaybackProgressEventArgs e)
    {
        var session = e.Session;
        if (session.UserId.Equals(Guid.Empty))
        {
            return;
        }

        string sessionId = session.Id;
        string userId = session.UserId.ToString("N");

        CheckAndResetLimits(userId);

        if (_sessionStartTimes.TryGetValue(sessionId, out var startTime))
        {
            var now = DateTime.UtcNow;
            var delta = now - startTime;
            _sessionStartTimes[sessionId] = now;

            string? libraryId = GetLibraryId(e.Item);
            AddUsage(userId, libraryId, delta.TotalMinutes);

            if (IsLimitExceeded(userId, libraryId))
            {
                StopPlayback(session, "Screen time limit exceeded.");
            }
        }
        else
        {
            _sessionStartTimes[sessionId] = DateTime.UtcNow;
        }
    }

    private void OnPlaybackStopped(object? sender, PlaybackStopEventArgs e)
    {
        var session = e.Session;
        if (session.UserId.Equals(Guid.Empty))
        {
            return;
        }

        string sessionId = session.Id;
        string userId = session.UserId.ToString("N");

        if (_sessionStartTimes.TryRemove(sessionId, out var startTime))
        {
            var now = DateTime.UtcNow;
            var delta = now - startTime;
            string? libraryId = GetLibraryId(e.Item);
            AddUsage(userId, libraryId, delta.TotalMinutes);
        }

        SaveState();
    }

    private void AddUsage(string userId, string? libraryId, double minutes)
    {
        var userUsage = _usageState.GetOrAdd(userId, _ => new ConcurrentDictionary<string, double>());
        userUsage.AddOrUpdate("Global", minutes, (_, existing) => existing + minutes);

        if (!string.IsNullOrEmpty(libraryId))
        {
            userUsage.AddOrUpdate(libraryId, minutes, (_, existing) => existing + minutes);
        }
    }

    private bool IsLimitExceeded(string userId, string? libraryId)
    {
        var config = Plugin.Instance?.Configuration.UserRules?.FirstOrDefault(r => string.Equals(r.UserId, userId, StringComparison.OrdinalIgnoreCase));
        if (config == null)
        {
            return false;
        }

        _usageState.TryGetValue(userId, out var userUsage);
        double globalUsage = userUsage?.TryGetValue("Global", out var g) == true ? g : 0;

        if (config.IsGlobalLimitEnabled && config.GlobalTimeLimitMinutes > 0 && globalUsage >= config.GlobalTimeLimitMinutes)
        {
            return true;
        }

        if (!string.IsNullOrEmpty(libraryId))
        {
            var libConfig = config.LibraryRules?.FirstOrDefault(l => string.Equals(l.LibraryId, libraryId, StringComparison.OrdinalIgnoreCase));
            if (libConfig != null && libConfig.IsEnabled && libConfig.TimeLimitMinutes > 0)
            {
                double libUsage = userUsage?.TryGetValue(libraryId, out var l) == true ? l : 0;
                if (libUsage >= libConfig.TimeLimitMinutes)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void StopPlayback(SessionInfo session, string reason)
    {
        _logger.LogWarning("Stopping playback for user {UserId}. Reason: {Reason}", session.UserId, reason);
        var msg = new MessageCommand
        {
            Header = "Screentime Manager",
            Text = reason,
            TimeoutMs = 10000
        };

        _sessionManager.SendMessageCommand(session.Id, session.Id, msg, CancellationToken.None);

        var req = new PlaystateRequest
        {
            Command = PlaystateCommand.Stop
        };

        _sessionManager.SendPlaystateCommand(session.Id, session.Id, req, CancellationToken.None);
    }

    private void CheckAndResetLimits(string userId)
    {
        var config = Plugin.Instance?.Configuration.UserRules?.FirstOrDefault(r => string.Equals(r.UserId, userId, StringComparison.OrdinalIgnoreCase));
        if (config == null)
        {
            return;
        }

        var now = DateTime.Now;
        if (!_lastResetTimes.TryGetValue(userId, out var lastReset))
        {
            lastReset = now;
            _lastResetTimes[userId] = lastReset;
        }

        bool shouldReset = false;

        if (config.ResetInterval == ResetPeriod.Daily)
        {
            if (now.Date > lastReset.Date)
            {
                shouldReset = true;
            }
        }
        else if (config.ResetInterval == ResetPeriod.Weekly)
        {
            if (now.Date > lastReset.Date && (now - lastReset).TotalDays >= 1)
            {
                if (now.DayOfWeek == DayOfWeek.Sunday || (now - lastReset).TotalDays >= 7)
                {
                    shouldReset = true;
                }
            }
        }

        if (shouldReset)
        {
            if (_usageState.TryGetValue(userId, out var dict))
            {
                dict.Clear();
            }

            _lastResetTimes[userId] = now;
            SaveState();
        }
    }

    private void LoadState()
    {
        try
        {
            if (File.Exists(_stateFilePath))
            {
                var json = File.ReadAllText(_stateFilePath);
                var state = JsonSerializer.Deserialize<ScreentimeStateModel>(json);
                if (state != null)
                {
                    if (state.UsageState != null)
                    {
                        foreach (var kvp in state.UsageState)
                        {
                            _usageState[kvp.Key] = new ConcurrentDictionary<string, double>(kvp.Value);
                        }
                    }

                    if (state.LastResetTimes != null)
                    {
                        foreach (var kvp in state.LastResetTimes)
                        {
                            _lastResetTimes[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading screentime state.");
        }
    }

    private void SaveState()
    {
        try
        {
            var dictUsage = new Dictionary<string, Dictionary<string, double>>();
            foreach (var kvp in _usageState)
            {
                dictUsage[kvp.Key] = kvp.Value.ToDictionary(k => k.Key, v => v.Value);
            }

            var state = new ScreentimeStateModel
            {
                UsageState = dictUsage,
                LastResetTimes = _lastResetTimes.ToDictionary(k => k.Key, v => v.Value)
            };

            var json = JsonSerializer.Serialize(state);
            Directory.CreateDirectory(Plugin.Instance!.DataFolderPath);
            File.WriteAllText(_stateFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving screentime state.");
        }
    }

    /// <summary>
    /// Model for saving state.
    /// </summary>
    private sealed class ScreentimeStateModel
    {
        public Dictionary<string, Dictionary<string, double>> UsageState { get; set; } = new();

        public Dictionary<string, DateTime> LastResetTimes { get; set; } = new();
    }
}
