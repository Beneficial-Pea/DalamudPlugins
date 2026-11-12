using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace Oracle.Services;

/// <summary>Party/duty countdown system messages, including Korean clients.</summary>
internal sealed unsafe class CountdownSyncDetector : IDisposable
{
    private readonly CountdownSignals _signals = new();
    private readonly NativeCountdownSignals _nativeSignals = new();
    public bool JustStarted { get; private set; }
    public bool JustCancelled { get; private set; }
    public float StartedRemaining { get; private set; }
    public float? ClockRemaining { get; private set; }
    public void Subscribe() => PluginServices.ChatGui.ChatMessage += OnChatMessage;
    public void Dispose() => PluginServices.ChatGui.ChatMessage -= OnChatMessage;
    public void Update()
    {
        var now = Environment.TickCount64;
        var chat = _signals.Drain(now);
        var agent = PluginServices.ClientState.IsLoggedIn ? AgentCountDownSettingDialog.Instance() : null;
        var signal = agent == null ? chat : _nativeSignals.Observe(agent->Active, agent->TimeRemaining, now);
        // Explicit cancellation messages also cover cancellation in the last few frames.
        if (chat.Cancelled) signal = new(false, true, 0);
        JustStarted = signal.Started;
        JustCancelled = signal.Cancelled;
        StartedRemaining = signal.Remaining;
        ClockRemaining = signal.ClockRemaining;
    }
    private void OnChatMessage(IHandleableChatMessage message)
    {
        if (message.LogKind is not (XivChatType.SystemMessage or XivChatType.Urgent or XivChatType.Notice)) return;
        var text = message.Message.TextValue;
        if (!string.IsNullOrWhiteSpace(text)) _signals.Receive(text, Environment.TickCount64);
    }
}
