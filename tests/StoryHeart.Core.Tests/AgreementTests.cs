using StoryHeart.Core;

namespace StoryHeart.Core.Tests;

public class AgreementTests
{
    [Fact]
    public void FindsTheLastUpdatedDate()
    {
        Assert.Equal("23 October 2026", Agreement.LastUpdated("TITLE\r\n\r\nCopyright (c) 2026.\r\nLast updated: 23 October 2026\r\n\r\nText."));
        Assert.Null(Agreement.LastUpdated("An agreement without a date."));
    }

    [Fact]
    public void TheShippedAgreementHasADate()
    {
        var eula = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "legal", "EULA.txt");
        Assert.NotNull(Agreement.LastUpdated(File.ReadAllText(eula)));
    }
}
