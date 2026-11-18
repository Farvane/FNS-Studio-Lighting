# \[FNS\] GRIDWORX - Studio Lighting

**v1.0.0** — optional Pulsar Modern client plugin for Space Engineers 2.

Applies neutral studio lighting while the FNS Design Range skybox is active:
sun disc off, a subdued directional key with neutral ambient fill, directional
shadows off, fixed exposure and bloom off.
The companion changes local rendering, not gameplay or saved world data.

Requires **Pulsar Modern**, **SE2 4.0.0.3169** and the separate **FNS Design
Range — Skybox** Workshop item. Other renderer versions are disabled until
validated. Windows is the release test platform; multiplayer and Linux are
not validated. Every player who wants these lighting changes needs the plugin.

## Other FNS plugins

`StudioLightingBridge` (public static) exposes `Enabled` and `Toggle()` so
FNS Build Tools' radial menu can switch this plugin on or off. Other plugins find
it by type name at runtime, with no assembly reference. Nothing else is exposed.

## Install

1. Install Pulsar from https://github.com/SpaceGT/Pulsar/releases and run Modern.
2. Close SE2/Pulsar. Extract `FNSStudioLighting.dll` and
   `FNSStudioLighting.xml` from the release ZIP into the existing
   `Modern/Local` directory under your Pulsar installation. The XML supplies
   the friendly name and description shown by Pulsar.
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

For repeatable in-game review, place white, black, neutral gray, OPC orange
(`#E96512`), FNS blue (`#3584C4`) and FNS green (`#42B77B`) armor on one test
assembly. Keep the camera, graphics options and exposure conditions fixed;
capture vanilla, then studio-lighting views at 0, 90, 180 and 270 degrees to
the sun. Compare orientation-to-orientation brightness, retained paint
saturation, white clipping, dark-surface detail and whether enough subtle
shading remains to read the geometry. Visual calibration still requires SE2;
the build cannot establish that the profile is perceptually correct.

This is a community plugin, not an official Keen Software House product.
