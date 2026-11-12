using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace Oracle.Services;

internal sealed class LogMimicDtrEntry : IDisposable
{
    private readonly IDtrBarEntry _entry;
    private readonly LogMimicService _replay;
    private readonly Action _openWindow;
    private string _lastText = string.Empty;
    private string _lastTooltip = string.Empty;
    private bool _disposed;

    public LogMimicDtrEntry(IDtrBar bar, LogMimicService replay, Action openWindow)
    {
        _replay = replay;
        _openWindow = openWindow;
        _entry = bar.Get("LogMimic");
        _entry.OnClick = OnClick;
        Update();
    }

    public void Update()
    {
        if (_disposed) return;
        _entry.Shown = PluginServices.ClientState.IsLoggedIn;
        if (!_entry.Shown) return;

        var text = _replay.IsReplayEnabled ? "LM: ON" : "LM: OFF";
        if (text != _lastText)
        {
            _lastText = text;
            _entry.Text = new SeString(new TextPayload(text));
        }
        var tooltip = I18n.Get("dtr.help") + "\n\n" + _replay.State;
        if (tooltip != _lastTooltip)
        {
            _lastTooltip = tooltip;
            _entry.Tooltip = new SeString(new TextPayload(tooltip));
        }
    }

    private void OnClick(DtrInteractionEvent click)
    {
        if (_disposed || click.ScrollDirection != MouseScrollDirection.None) return;
        if (click.ClickType == MouseClickType.Right)
        {
            _openWindow();
            return;
        }
        if (click.ClickType != MouseClickType.Left) return;
        // Use the same controller as /lm and the Replay page; failed starts stay OFF.
        _replay.Command([_replay.IsReplayEnabled ? "off" : "on"]);
        Update();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _entry.OnClick = null;
        _entry.Remove();
    }
}
