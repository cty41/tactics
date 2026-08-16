using Godot;

namespace Tactics.Godot.Adapter.Runtime;

internal interface ITextClipboard
{
    void SetText(string text);
}

internal sealed class GodotTextClipboard : ITextClipboard
{
    public void SetText(string text) => DisplayServer.ClipboardSet(text);
}
