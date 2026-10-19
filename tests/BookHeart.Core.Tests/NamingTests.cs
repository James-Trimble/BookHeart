using BookHeart.Core;

namespace BookHeart.Core.Tests;

public class NamingTests
{
    [Fact]
    public void NaturalSortReadsNumbersAsNumbers()
    {
        var sorted = NaturalComparer.Sort(["b/Part 10.mp3", "b/Part 2.mp3", "b/Part 1.mp3"]);
        Assert.Equal(["Part 1.mp3", "Part 2.mp3", "Part 10.mp3"], sorted.Select(Path.GetFileName));
    }

    [Fact]
    public void NaturalSortIgnoresCase()
    {
        Assert.True(NaturalComparer.Instance.Compare("apple 2", "Apple 10") < 0);
        Assert.Equal(0, NaturalComparer.Instance.Compare("Track 01", "track 1"));
    }

    [Theory]
    [InlineData(new[] { "The Hobbit - Chapter 01.mp3", "The Hobbit - Chapter 02.mp3" }, "The Hobbit")]
    [InlineData(new[] { "Dune Part 1.mp3", "Dune Part 2.mp3", "Dune Part 10.mp3" }, "Dune")]
    [InlineData(new[] { "Book One.mp3", "Book Two.mp3" }, "2 audio files")]
    [InlineData(new[] { "01.mp3", "02.mp3", "03.mp3" }, "3 audio files")]
    public void SetTitle(string[] files, string expected) => Assert.Equal(expected, BookNaming.SetTitle(files));

    [Fact]
    public void TrackTitleDropsBookTitle()
    {
        Assert.Equal("Chapter 01", BookNaming.TrackTitle("The Hobbit - Chapter 01.mp3", "The Hobbit"));
        Assert.Equal("Intro", BookNaming.TrackTitle("Intro.mp3", "The Hobbit"));
    }

    [Fact]
    public void RecognisesAudioFiles()
    {
        Assert.True(BookNaming.IsAudioFile("book.M4B"));
        Assert.False(BookNaming.IsAudioFile("cover.jpg"));
    }
}
