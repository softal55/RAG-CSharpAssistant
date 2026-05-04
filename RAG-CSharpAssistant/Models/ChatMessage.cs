using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RAG_CSharpAssistant.Models;

public sealed class ChatMessage : INotifyPropertyChanged
{
    private static readonly Color UserBubble = Color.FromArgb("#448AFF");
    private static readonly Color BotBubble = Color.FromArgb("#2A2A2A");

    private string _text;

    public ChatMessage(string text, bool isUser)
    {
        _text = text;
        IsUser = isUser;
    }

    public bool IsUser { get; }

    /// <summary>Used by chat UI templates to toggle visibility without a value converter.</summary>
    public bool IsBot => !IsUser;

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            OnPropertyChanged();
        }
    }

    public LayoutOptions BubbleAlignment => IsUser ? LayoutOptions.End : LayoutOptions.Start;

    public Color BubbleColor => IsUser ? UserBubble : BotBubble;

    public Color TextColor => Colors.White;

    // MAUI CornerRadius order: TopLeft, TopRight, BottomLeft, BottomRight.
    // User bubble has its tail at the bottom-right; bot bubble has it at the bottom-left.
    public CornerRadius Corners => IsUser
        ? new CornerRadius(16, 16, 16, 0)
        : new CornerRadius(16, 16, 0, 16);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
