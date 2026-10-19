using System.Globalization;

namespace AudiobookPlayer.Core;

/// <summary>Compares names with runs of digits read as numbers, so "Part 2" sorts before "Part 10".</summary>
public sealed class NaturalComparer : IComparer<string?>
{
    public static readonly NaturalComparer Instance = new();

    private static readonly CompareInfo Culture = CultureInfo.CurrentCulture.CompareInfo;
    private const CompareOptions TextOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreWidth | CompareOptions.IgnoreKanaType;

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsAsciiDigit(x[i]) && char.IsAsciiDigit(y[j]))
            {
                int si = i, sj = j;
                while (i < x.Length && char.IsAsciiDigit(x[i])) i++;
                while (j < y.Length && char.IsAsciiDigit(y[j])) j++;
                var a = x.AsSpan(si, i - si).TrimStart('0');
                var b = y.AsSpan(sj, j - sj).TrimStart('0');
                if (a.Length != b.Length) return a.Length.CompareTo(b.Length);
                var c = a.SequenceCompareTo(b);
                if (c != 0) return Math.Sign(c);
            }
            else
            {
                int si = i, sj = j;
                while (i < x.Length && !char.IsAsciiDigit(x[i])) i++;
                while (j < y.Length && !char.IsAsciiDigit(y[j])) j++;
                var c = Culture.Compare(x, si, i - si, y, sj, j - sj, TextOptions);
                if (c != 0) return Math.Sign(c);
            }
        }
        return (x.Length - i).CompareTo(y.Length - j);
    }

    /// <summary>Sorts by full path, so files in "Disc 1" come before "Disc 2", then by name within each folder.</summary>
    public static List<string> Sort(IEnumerable<string> paths) => paths.OrderBy(p => p, Instance).ToList();
}
