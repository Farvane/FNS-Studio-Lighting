# FNS Design Range — Studio Lighting

**v0.1.0 preview** — optional Pulsar Modern client plugin for Space Engineers 2.

Applies neutral studio lighting while the FNS Design Range skybox is active:
sun disc off, reduced directional shadows, fixed exposure and bloom off.
The companion changes local rendering, not gameplay or saved world data.

Requires **Pulsar Modern**, **SE2 4.0.0.3169** and the separate **FNS Design
Range — Skybox** Workshop item. Other renderer versions are disabled until
validated. Windows is the release test platform; multiplayer and Linux are
not validated. Every player who wants these lighting changes needs the plugin.

## Install

1. Install Pulsar from https://github.com/SpaceGT/Pulsar/releases and run Modern.
2. Close SE2/Pulsar. Extract `FNSStudioLighting.dll` from the release ZIP into
   the existing `Modern/Local` directory under your Pulsar installation.
3. Disable the old experimental `FNSStudioLightingPOC` plugin if installed.
4. Enable **FNSStudioLighting** in your Pulsar profile and launch SE2.
5. Open the companion's settings from the Pulsar plugin menu, select **Enable
   studio lighting in FNS Design Range**, and click **Apply**. It starts disabled.
6. Enable the FNS skybox for the world and load it. Settings should report
   **Applied: FNS studio lighting**. Without the skybox it stays inactive.

The direct download does not depend on inclusion in the shared plugin list.
Do not install both a local copy and a later hub copy at the same time.

## Settings and recovery

The settings screen shows disabled, waiting, active, unavailable or conflicting
frame status. Apply takes effect immediately for an already-observed FNS
environment, and disabling restores the original frame while still owned.
If the engine or another plugin replaces that frame, this companion releases
ownership and pauses rather than overwriting it. Toggle off/on to retry.

Settings are stored in `settings.json` inside the plugin configuration directory
provided by Pulsar. Missing settings start disabled. Invalid settings leave the
plugin inactive; close the game and rename the settings file to reset it.
If restoration fails, restart SE2 before continuing.

Uninstall with SE2/Pulsar closed: disable the plugin in its profile and remove
`FNSStudioLighting.dll` from `Modern/Local`. The Workshop skybox remains usable
with normal game lighting.

## Development

Requires the installed game assemblies and .NET 10 SDK:

```powershell
.\build.ps1 -Game2Path 'path-to-your-SE2-Game2-directory'
```

Builds a Release DLL and documentation package under `artifacts/`.
No automatic deployment or publishing. Source is MIT licensed; the skybox
artwork retains its separate license. See `THIRD-PARTY-NOTICES.md`.

This is a community plugin, not an official Keen Software House product.
