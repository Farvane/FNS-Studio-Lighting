using System;

namespace Farvane.StudioLighting;

/// <summary>
/// Public switch for other FNS plugins (FNS Build Tools' radial menu). It is read by
/// type name through reflection, so the caller needs no assembly reference and works
/// when this plugin is not installed. Attached by <see cref="Plugin"/>; null when absent.
/// </summary>
public static class StudioLightingBridge
{
    private static Plugin? _plugin;

    internal static void Attach(Plugin plugin) => _plugin = plugin;

    internal static void Detach(Plugin plugin)
    {
        if (ReferenceEquals(_plugin, plugin)) _plugin = null;
    }

    /// <summary>True when studio lighting is currently enabled in settings.</summary>
    public static bool Enabled => _plugin?.Enabled ?? false;

    /// <summary>Turns studio lighting on or off and returns the resulting status text.</summary>
    public static string Toggle()
    {
        var plugin = _plugin ?? throw new InvalidOperationException("Studio lighting is not running.");
        plugin.Apply(!plugin.Enabled);
        return plugin.Status;
    }
}
