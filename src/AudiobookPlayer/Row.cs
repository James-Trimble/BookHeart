using System.ComponentModel;

namespace AudiobookPlayer;

/// <summary>One line in a list: what's shown, and what a screen reader says for it.</summary>
public sealed class Row(string title, string detail, string spoken, object? tag = null) : INotifyPropertyChanged
{
    private bool _isCurrent;

    public string Title { get; } = title;
    public string Detail { get; } = detail;
    public string Spoken => _isCurrent ? spoken + ", playing now" : spoken;
    public object? Tag { get; } = tag;
    public bool HasDetail => Detail.Length > 0;

    /// <summary>Marks the chapter that is playing now.</summary>
    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            if (_isCurrent == value) return;
            _isCurrent = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCurrent)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Spoken)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
