using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Keen.VRage.UI.Screens;

namespace Farvane.StudioLighting;

internal sealed class LightingScreenModel : ScreenViewModel
{
    internal Plugin Plugin { get; }
    public LightingScreenModel(Plugin plugin)
    {
        Plugin = plugin;
        KeepsOtherScreensVisible = false;
        AllowsInputBelowUI = false;
        AllowsInputFromLowerScreens = false;
        InitializeInputContext();
    }
}

// Uses the same ScreenView/CreateScreen extension points as the SE2 plugin template.
internal sealed class LightingScreen : ScreenView
{
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _feedback = new() { TextWrapping = TextWrapping.Wrap };
    private readonly CheckBox _enabled = new() { Content = "Enable studio lighting in FNS Design Range" };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public LightingScreen()
    {
        var apply = new Button { Content = "Apply", HorizontalAlignment = HorizontalAlignment.Left };
        apply.Click += (_, _) =>
        {
            if (DataContext is not LightingScreenModel model) return;
            try
            {
                model.Plugin.Apply(_enabled.IsChecked == true);
                _feedback.Text = "Saved.";
            }
            catch (Exception error)
            {
                _feedback.Text = error is InvalidOperationException ? error.Message : "Could not save settings. Check available disk space and permissions.";
                _enabled.IsChecked = model.Plugin.Enabled;
            }
            Refresh();
        };
        var close = new Button { Content = "Close", HorizontalAlignment = HorizontalAlignment.Left };
        close.Click += (_, _) => Dispose();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        buttons.Children.Add(apply);
        buttons.Children.Add(close);
        var panel = new StackPanel { Margin = new Thickness(30), Spacing = 22 };
        panel.Children.Add(new TextBlock { Text = "[FNS] GRIDWORX - Studio Lighting", FontSize = 25, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = "v1.0.0 • SE2 4.0.0.3169 • local-client rendering", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(_status);
        panel.Children.Add(_enabled);
        panel.Children.Add(new TextBlock
        {
            Text = "Requires the FNS Design Range skybox. Normal lighting is preserved elsewhere. Disable the old FNSStudioLightingPOC plugin before using this companion. If status says restore failed, restart SE2.",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(buttons);
        panel.Children.Add(_feedback);
        var border = new Border
        {
            Width = 720,
            MaxHeight = 720,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.FromRgb(23, 31, 39)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(66, 82, 96)),
            BorderThickness = new Thickness(1),
            Child = new ScrollViewer { Content = panel }
        };
        var overlay = new Grid { Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)) };
        overlay.Children.Add(border);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Background = Brushes.Transparent;
        Content = overlay;
        _timer.Tick += (_, _) => Refresh();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is LightingScreenModel model) _enabled.IsChecked = model.Plugin.Enabled;
        Refresh();
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    private void Refresh()
    {
        if (DataContext is LightingScreenModel model) _status.Text = "Status: " + model.Plugin.Status;
    }
}
