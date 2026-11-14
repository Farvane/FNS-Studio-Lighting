using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Keen.VRage.Library.Diagnostics;

namespace Farvane.StudioLighting;

/// <summary>
/// Reversible local-client studio profile for the known FNS skybox.
/// Settings and Pulsar profile provide explicit opt-in; assets stay in Workshop.
/// </summary>
internal sealed class LightingController : IDisposable
{
    private const string Label = "[FNS Studio Lighting] ";
    internal const string OwnerId = "farvane.fns-studio-lighting";
    private const float DirectionalLightIntensity = 10f;
    private const float IndirectLightDiffuseFactor = 2.0f;
    private const ulong AmbientLightColor = 0xFFC0C0C0UL;
    private static readonly Guid FnsSkybox = new("121672c3-c988-4357-8883-4ec2983d9001");
    private const BindingFlags All = BindingFlags.Instance |
        BindingFlags.Public | BindingFlags.NonPublic;
    private Harmony? _harmony;
    private object? _service;
    private object? _before;
    private object? _profile;
    private bool _applied;

    private bool _enabled;
    private bool _supported;
    private bool _conflict;
    private object? _lastPublisher;
    private object[]? _lastArgs;
    public string Status { get; private set; } = "Starting";
    public bool IsSupported => _supported;

    public LightingController(bool enabled = true)
    {
        _enabled = enabled;
        try
        {
            var frameType = AccessTools.TypeByName("Keen.VRage.Render.Data.FrameSettingsConfiguration")
                ?? throw new InvalidOperationException("Frame configuration type unavailable");
            var version = frameType.Assembly.GetName().Version?.ToString();
            if (version != "4.0.0.3169")
                throw new InvalidOperationException("Supported renderer build: 4.0.0.3169; found " + version);
            if (Harmony.HasAnyPatches("farvane.fns-studio-lighting-poc"))
                throw new InvalidOperationException("Disable the experimental FNSStudioLightingPOC plugin and restart");
            if (_instance is not null)
                throw new InvalidOperationException("Another Studio Lighting instance is already running");
            _harmony = new Harmony(OwnerId);
            var publisherType = AccessTools.TypeByName(
                "Keen.VRage.Render.Data.EnvironmentResourcesConfigurationPostProcessor")
                ?? throw new InvalidOperationException("Environment publisher not found");
            var publisher = publisherType.GetMethods(All)
                .Single(m => m.Name == "Publish" && m.GetParameters().Length == 1);
            var callback = AccessTools.Method(typeof(LightingController), nameof(OnEnvironmentPublished))
                ?? throw new InvalidOperationException("POC callback not found");
            _instance = this;
            _harmony.Patch(publisher, postfix: new HarmonyMethod(callback));
            var framePublisherType = AccessTools.TypeByName(
                "Keen.VRage.Render.Data.FrameSettingsPostProcessor")
                ?? throw new InvalidOperationException("Frame publisher not found");
            var framePublisher = framePublisherType.GetMethods(All)
                .Single(m => m.Name == "Publish" && m.GetParameters().Length == 1);
            _harmony.Patch(framePublisher, postfix: new HarmonyMethod(
                AccessTools.Method(typeof(LightingController), nameof(OnFramePublished))));
            // The 11:41 live session returned to the menu without an observed
            // empty environment publication. Restore before the observed game
            // disconnect path tears down scenes; retain publication cleanup too.
            var appType = AccessTools.TypeByName("Keen.Game2.GameAppComponent")
                ?? throw new InvalidOperationException("Game lifecycle component not found");
            foreach (var name in new[] { "Disconnect", "ExitGame" })
            {
                var method = appType.GetMethods(All)
                    .Single(m => m.Name == name && m.GetParameters().Length == 0);
                _harmony.Patch(method, prefix: new HarmonyMethod(
                    AccessTools.Method(typeof(LightingController), nameof(BeforeLeavingWorld))));
            }
            _supported = true;
            SetStatus(_enabled ? "Waiting for the FNS Design Range skybox" : "Disabled in settings");
            Log.Default.WriteLine(Label + "READY v1.0.0; renderer=4.0.0.3169; lifecycle=disconnect+exit");
        }
        catch (Exception ex)
        {
            Log.Default.WriteLine(Label + "startup FAILED: " + ex.GetType().Name +
                ": " + ex.Message);
            _harmony?.UnpatchAll(OwnerId);
            _harmony = null;
            if (ReferenceEquals(_instance, this)) _instance = null;
            SetStatus("Unavailable: " + ex.Message);
        }
    }

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        _conflict = false;
        if (!enabled)
        {
            if (Restore("disabled in settings")) SetStatus("Disabled in settings");
            return;
        }
        if (!_supported) return;
        SetStatus("Waiting for the FNS Design Range skybox");
        if (_lastPublisher is not null && _lastArgs is not null)
            HandlePublished(_lastPublisher, _lastArgs);
    }

    private void SetStatus(string status)
    {
        if (Status == status) return;
        Status = status;
        Log.Default.WriteLine(Label + status);
    }

    private static LightingController? _instance;

    private static void OnEnvironmentPublished(object __instance, object[] __args)
    {
        _instance?.HandlePublished(__instance, __args);
    }

    private static void BeforeLeavingWorld(MethodBase __originalMethod)
    {
        var controller = _instance;
        if (controller is null) return;
        controller._lastPublisher = null;
        controller._lastArgs = null;
        controller._conflict = false;
        controller.Restore("game " + __originalMethod.Name);
    }

    private static void OnFramePublished(object __instance)
    {
        // The engine may replace our profile during a definition-set transition.
        // Never restore the old baseline over that newer configuration.
        try
        {
            var plugin = _instance;
            if (plugin?._applied == true &&
                ReferenceEquals(GetService(__instance), plugin._service) &&
                !ReferenceEquals(Member(plugin._service, "_activeFrameConfig"), plugin._profile))
            {
                plugin.Forget("engine published a replacement frame configuration");
                plugin._conflict = true;
                plugin.SetStatus("Paused: another frame configuration replaced studio lighting. Toggle off/on to retry.");
            }
        }
        catch (Exception ex)
        {
            Log.Default.WriteLine(Label + "frame observer failed: " + ex.GetType().Name);
        }
    }

    private void HandlePublished(object publisher, object[] args)
    {
        try
        {
            _lastPublisher = publisher;
            _lastArgs = args;
            if (!_enabled || !_supported)
            {
                Restore("studio lighting disabled");
                return;
            }
            var input = args.Length == 0 ? null : args[0];
            var count = Member(input, "Length") as int? ?? 0;
            if (count == 0)
            {
                // A world/environment unload or reload invalidated the resource.
                _conflict = false;
                _lastPublisher = null;
                _lastArgs = null;
                if (Restore("environment no longer published"))
                    SetStatus("Waiting for the FNS Design Range skybox");
                return;
            }
            if (count != 1)
            {
                _conflict = false;
                if (Restore("ambiguous environment publication"))
                    SetStatus("Inactive: expected exactly one environment configuration");
                return;
            }

            var environment = input?.GetType().GetProperty("Item")
                ?.GetValue(input, new object[] { 0 });
            var skybox = Member(environment, "Skybox");
            var flare = Member(environment, "SunFlare");
            if (!IsFnsSkybox(skybox) || flare is not null)
            {
                _conflict = false;
                if (Restore("environment no longer matches FNS conditions"))
                    SetStatus("Inactive: FNS Design Range skybox is not active");
                return;
            }
            if (_conflict) return;
            var service = GetService(publisher);
            if (_applied)
            {
                if (ReferenceEquals(service, _service))
                {
                    if (ReferenceEquals(Member(service, "_activeFrameConfig"), _profile)) return;
                    Forget("active frame was replaced");
                    _conflict = true;
                    SetStatus("Paused: another frame configuration replaced studio lighting. Toggle off/on to retry.");
                    return;
                }
                if (!Restore("renderer service or active frame changed")) return;
            }
            if (service is null)
            {
                SetStatus("Inactive: renderer settings service is not available");
                return;
            }
            var active = Member(service, "_activeFrameConfig");
            if (active is null)
            {
                SetStatus("Inactive: renderer frame configuration is not available");
                return;
            }

            // Clone rather than modifying the engine's effective default object.
            var profile = ShallowClone(active);
            UpdateSection(profile, "Environment", section =>
            {
                Set(section, "SunDisc", false);
                Set(section, "SunDiscIntensity", 0);
                Set(section, "SkyboxBrightness", 0.85f);
            });
            UpdateSection(profile, "Light", section =>
            {
                // Keep a subtle key light for shape while neutral ambient fill
                // reduces orientation-dependent hull color changes.
                Set(section, "DirLightIntensity", DirectionalLightIntensity);
                Set(section, "IndirectLightDiffuseFactor", IndirectLightDiffuseFactor);
                UpdateSection(section, "AmbientLightColor", color =>
                    Set(color, "PackedValue", AmbientLightColor));
            });
            UpdateSection(profile, "Shadow", section =>
                Set(section, "EnableDirectionalLightShadows", false));
            UpdateSection(profile, "PostProcess", section =>
            {
                Set(section, "EyeAdaptation", false);
                Set(section, "Bloom", false);
            });

            var setter = FindFrameSetter(service);
            // Save originals only immediately before applying.
            _service = service;
            _before = active;
            _profile = profile;
            // Record ownership before invocation: SetSettings may assign the
            // profile and then throw while resetting downstream renderer state.
            _applied = true;
            setter.Invoke(service, new[] { profile });
            if (!ReferenceEquals(Member(service, "_activeFrameConfig"), profile))
                throw new InvalidOperationException("Active frame readback differs from POC profile");
            Log.Default.WriteLine(Label +
                "APPLIED in memory (SunDisc=false, DirLightIntensity=10, IndirectLightDiffuseFactor=2.0, AmbientLightColor=#C0C0C0, shadows=false, eye adaptation=false, bloom=false)");
            SetStatus("Applied: FNS studio lighting");
        }
        catch (Exception ex)
        {
            var restored = Restore("application failed");
            _supported = false;
            if (restored)
                SetStatus("Unavailable: renderer update failed (" + ex.GetType().Name + "). Restart after checking game compatibility.");
            Log.Default.WriteLine(Label + "NOT APPLIED: " +
                ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool IsFnsSkybox(object? skybox)
    {
        if (skybox is null) return false;
        // Installed ResourceHandle<CubemapTextureAsset> has a Guid-by-reference
        // constructor and value equality. Compare handles, never display strings.
        var constructor = skybox.GetType().GetConstructor(All, null,
            new[] { typeof(Guid).MakeByRefType() }, null)
            ?? throw new InvalidOperationException("Skybox GUID handle constructor unavailable");
        return skybox.Equals(constructor.Invoke(new object[] { FnsSkybox }));
    }

    private static MethodInfo FindFrameSetter(object service) =>
        service.GetType().GetMethods(All).Single(m =>
            m.Name == "SetSettings" &&
            m.GetParameters().Length == 1 &&
            m.GetParameters()[0].ParameterType.Name == "FrameSettingsConfiguration");

    private bool Restore(string reason)
    {
        try
        {
            if (!_applied || _service is null || _before is null) return true;
            if (!ReferenceEquals(Member(_service, "_activeFrameConfig"), _profile))
            {
                Forget("active frame was replaced; " + reason);
                return true;
            }
            FindFrameSetter(_service).Invoke(_service, new[] { _before });
            if (!ReferenceEquals(Member(_service, "_activeFrameConfig"), _before))
                throw new InvalidOperationException("Original frame readback failed");
            Log.Default.WriteLine(Label + "RESTORED original frame settings: " + reason);
            Clear();
            SetStatus(_enabled ? "Waiting for the FNS Design Range skybox" : "Disabled in settings");
            return true;
        }
        catch (Exception ex)
        {
            Log.Default.WriteLine(Label + "restore failed: " + ex.GetType().Name);
            SetStatus("Restore failed: exit and restart SE2 before continuing (" + ex.GetType().Name + ")");
            // Retain ownership and baseline so the next lifecycle event can retry.
            return false;
        }
    }

    private void Forget(string reason)
    {
        Log.Default.WriteLine(Label + "RELEASED saved frame settings: " + reason);
        Clear();
    }

    private void Clear()
    {
        _service = null;
        _before = null;
        _profile = null;
        _applied = false;
    }

    private static object? GetService(object publisher)
    {
        object? context = null;
        for (Type? t = publisher.GetType(); t != null; t = t.BaseType)
        {
            var contextProp = t.GetProperty(
                "Context", All | BindingFlags.DeclaredOnly);
            if (contextProp is null) continue;
            context = contextProp.GetValue(publisher);
            break;
        }
        var serviceType = AccessTools.TypeByName(
            "Keen.VRage.Render.Data.RenderSettingsManipulator");
        if (context is null || serviceType is null) return null;
        var method = context.GetType().GetMethods(All)
            .Single(m => m.Name == "TryGet" && m.IsGenericMethodDefinition
                && m.GetGenericArguments().Length == 1
                && m.GetParameters().Length == 0);
        return method.MakeGenericMethod(serviceType).Invoke(context, null);
    }

    private static object ShallowClone(object original) =>
        typeof(object).GetMethod("MemberwiseClone",
            BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(original, null)!;

    private static object? Member(object? source, string name)
    {
        if (source is null) return null;
        var t = source.GetType();
        return t.GetProperty(name, All)?.GetValue(source) ??
            t.GetField(name, All)?.GetValue(source);
    }

    // Change the boxed struct, or clone a nested reference, and assign it back.
    private static void UpdateSection(object parent, string key, Action<object> change)
    {
        var original = Member(parent, key) ??
            throw new InvalidOperationException("Section not present: " + key);
        var section = original.GetType().IsValueType ? original : ShallowClone(original);
        change(section);
        Set(parent, key, section);
    }

    private static void Set(object target, string key, object value)
    {
        var t = target.GetType();
        var property = t.GetProperty(key, All);
        var field = t.GetField(key, All);
        if (property?.CanWrite == true)
        {
            property.SetValue(target, ConvertValue(value, property.PropertyType));
            return;
        }
        if (field is { IsInitOnly: false })
        {
            field.SetValue(target, ConvertValue(value, field.FieldType));
            return;
        }
        throw new InvalidOperationException("Writable field/property unavailable: " +
            t.Name + "." + key);
    }

    private static object ConvertValue(object value, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (underlying.IsInstanceOfType(value)) return value;
        return Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
    }

    public void Dispose()
    {
        _enabled = false;
        _supported = false;
        _lastPublisher = null;
        _lastArgs = null;
        Restore("plugin unload");
        _harmony?.UnpatchAll(OwnerId);
        if (ReferenceEquals(_instance, this)) _instance = null;
    }
}
