using Dalamud.Game.Chat;
using Dalamud.Game.Text;

namespace Oracle.Services;

/// <summary>Party/duty countdown system messages, including Korean clients.</summary>
internal sealed class CountdownSyncDetector : IDisposable
{
    private readonly CountdownSignals _signals = new();
    public bool JustStarted { get; private set; }
    public bool JustCancelled { get; private set; }
    public float StartedRemaining { get; private set; }
    public void Subscribe() => PluginServices.ChatGui.ChatMessage += OnChatMessage;
    public void Dispose() => PluginServices.ChatGui.ChatMessage -= OnChatMessage;
    public void Update()
    {
        var signal = _signals.Drain(Environment.TickCount64);
        JustStarted = signal.Started;
        JustCancelled = signal.Cancelled;
        StartedRemaining = signal.Remaining;
    }
    private void OnChatMessage(IHandleableChatMessage message)
    {
        if (message.LogKind is not (XivChatType.SystemMessage or XivChatType.Urgent or XivChatType.Notice)) return;
        var text = message.Message.TextValue;
        if (!string.IsNullOrWhiteSpace(text)) _signals.Receive(text, Environment.TickCount64);
    }
}
