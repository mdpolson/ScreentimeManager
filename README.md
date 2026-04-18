# Jellyfin Screentime Manager Plugin

The **Screentime Manager** is a Jellyfin plugin that allows administrators to define maximum viewing durations either globally per user or on a per-library level. It seamlessly integrates into the Jellyfin session manager to enforce playback limits dynamically, helping administrators or parents ensure their users maintain healthy viewing habits.

## Features

- **Global User Limits**: Set a maximum viewing time limit per user across all Jellyfin content.
- **Per-Library Granularity**: Define strict, separate limits per user for specific libraries (e.g. limit "TV Shows" to 1 hour, but allow unlimited access to "Documentaries").
- **Custom Reset Intervals**: Choose to have user limits reset exactly at Midnight (Daily) or at midnight Sunday (Weekly).
- **Execution Enforcer**: The plugin quietly hooks into Jellyfin's runtime and continuously tracks elapsed media time. Upon a limit being reached, the current video playback is aggressively halted and an error context is safely dispatched to the viewing client.
- **Native Configuration UI**: Configuration binds directly into standard Jellyfin Web dashboard views with a dynamic, user-selected component mapping interface.
- **Resilient Cache**: Screen time durations are flushed concurrently and natively via `ScreentimeState.json` meaning state reliably survives restarts.

## Installation

### From Source

Ensure you have the [.NET 9.0 SDK](https://dotnet.microsoft.com/download) installed.

To compile this plugin from source:
```sh
dotnet build Jellyfin.Plugin.ScreentimeManager.sln
```

Place the compiled DLL from `Jellyfin.Plugin.ScreentimeManager\bin\Debug\net9.0\Jellyfin.Plugin.ScreentimeManager.dll` directly into the plugin directory of your active Jellyfin server.

### Adding to Jellyfin

Once compiled, navigate to:
*Linux/Docker*: `/config/plugins/`
*Windows*: `%localappdata%\jellyfin\plugins\`
Create a `ScreentimeManager` folder here and drop the resulting DLL files inside.

Restart your Jellyfin server. Provide the plugin configuration access through `Dashboard -> Plugins -> ScreentimeManager`.

## Usage & Configuration

Once loaded, traverse to your Jellyfin Dashboard -> Plugins -> Catalog -> **ScreentimeManager** and configure.

1. Highlight your preferred user from the main dropdown menu.
2. Select whether their viewing period should renew on a **Daily** or **Weekly** cycle.
3. Toggle their **Global Rule**, restricting their total aggregated media watch time.
4. Scroll to configure rules granularly over specific libraries.
5. Save the configuration.

Users who exceed their allocated limits will be abruptly stopped upon starting a video or continuously when a time boundary is crossed.

## Requirements

- **Jellyfin**: v10.9.x or greater recommended.
- Built utilizing `.NET 9.0`.

## Licensing

Licensed under the GPLv3 to align with Jellyfin plugin specifications. All proprietary plugin rules enforce GPLv3 distribution compliance naturally natively when packaged.

