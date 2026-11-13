using System;
using System.IO;
using HarmonyLib;
using Keen.Game2.Client.UI.Library;
using Keen.VRage.Core;
using Keen.VRage.Core.Plugins;
using Keen.VRage.Library.Diagnostics;
using Keen.VRage.Library.Utils;

namespace Farvane.StudioLighting;

public sealed class Plugin : IPlugin, IDisposable
{
    public const string Name = "FNSStudioLighting";
    // Pulsar injects this delegate before constructing the plugin.
#pragma warning disable CS0649
    private static Func<string, string?, string>? GetConfigPath;
#pragma warning restore CS0649
    private readonly string? _configFile;
    private readonly Settings _settings = new();
    private readonly LightingController? _controller;
    private readonly string? _startupError;

    public Plugin()
    {
        try
        {
            var directory = GetConfigPath?.Invoke(Name, null)
                ?? throw new InvalidOperationException("Pulsar configuration directory is unavailable");
            _configFile = Path.Combine(directory, "settings.json");
            _settings = Settings.Load(_configFile);
            _controller = new LightingController(_settings.Enabled);
        }
        catch (Exception error)
        {
            _startupError = "Unavailable: settings could not be loaded (" + error.GetType().Name + ")";
            Log.Default.WriteLine("[FNS Studio Lighting] " + _startupError);
        }
    }

    public void OpenConfigDialog()
    {
        try
        {
            var engine = Singleton<VRageCore>.Instance?.Engine;
            var appType = AccessTools.TypeByName("Keen.Game2.GameAppComponent");
            if (engine is null || appType is null) return;
            var get = AccessTools.FirstMethod(engine.GetType(), method => method.Name == "Get" &&
                method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 1 &&
                method.GetParameters().Length == 1)
                ?? throw new InvalidOperationException("Engine component lookup is unavailable");
            var app = get.MakeGenericMethod(appType).Invoke(engine, new object[] { default(StringId) });
            var ui = AccessTools.Method(appType, "GetSharedUI")?.Invoke(app, null) as SharedUIComponent;
            if (ui is null) return;
            ui.CreateScreen<LightingScreen>(new LightingScreenModel(this), showCursor: true);
        }
        catch (Exception error)
        {
            Log.Default.WriteLine("[FNS Studio Lighting] Settings screen unavailable: " + error.GetType().Name);
        }
    }

    internal bool Enabled => _settings.Enabled;
    internal string Status => _controller?.Status ?? _startupError ?? "Unavailable";

    internal void Apply(bool enabled)
    {
        if (_controller is null || _configFile is null)
            throw new InvalidOperationException("Restart SE2 after fixing the settings file");
        if (enabled && !_controller.IsSupported)
            throw new InvalidOperationException("This runtime is unavailable; see the status above");
        // Save first: a disk failure leaves both the previous option and runtime unchanged.
        new Settings { Enabled = enabled }.Save(_configFile);
        _settings.Enabled = enabled;
        _controller.SetEnabled(enabled);
    }

    public void Dispose() => _controller?.Dispose();
}
