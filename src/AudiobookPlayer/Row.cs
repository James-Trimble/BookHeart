using System.ComponentModel;

namespace AudiobookPlayer;

/// <summary>One line in a list: what's shown, and what a screen reader says for it.</summary>
public sealed class Row(string title, string detail, string spoken, object? tag = null) : INotifyPropertyChanged
{
    private bool _isCurrent;

    public string Title { get; private set; } = title;
    public string Detail { get; private set; } = detail;
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
            Changed(nameof(IsCurrent), nameof(Spoken));
        }
    }

    /// <summary>Changes the text in place, so a list being read by a screen reader keeps its place.</summary>
    public void Update(string newTitle, string newDetail, string newSpoken)
    {
        if (Title == newTitle && Detail == newDetail && spoken == newSpoken) return;
        Title = newTitle;
        Detail = newDetail;
        spoken = newSpoken;
        Changed(nameof(Title), nameof(Detail), nameof(HasDetail), nameof(Spoken));
    }

    private void Changed(params string[] names)
    {
        foreach (var n in names) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
