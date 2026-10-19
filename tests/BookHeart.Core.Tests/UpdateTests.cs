using BookHeart.Core;

namespace BookHeart.Core.Tests;

public class UpdateTests
{
    private static AppVersion V(string s) => AppVersion.TryParse(s)!;

    [Theory]
    [InlineData("0.2.0", "0.1.1")]
    [InlineData("0.10.0", "0.9.9")]
    [InlineData("1.0.0", "1.0.0-beta.3")]
    [InlineData("0.2.0-beta.2", "0.2.0-beta.1")]
    [InlineData("0.2.0-beta.10", "0.2.0-beta.9")]
    [InlineData("0.2.0-rc.1", "0.2.0-beta.5")]
    public void NewerVersionsCompareHigher(string newer, string older)
    {
        Assert.True(V(newer) > V(older));
        Assert.True(V(older) < V(newer));
    }

    [Fact]
    public void ParsesTagsAndIgnoresBuildMetadata()
    {
        Assert.Equal(V("0.2.0"), V("v0.2.0+abc123"));
        Assert.Equal("0.2.0-beta.1", V("v0.2.0-beta.1").ToString());
        Assert.Equal("1.2.0", V("1.2").ToString());
        Assert.Null(AppVersion.TryParse("latest"));
        Assert.Null(AppVersion.TryParse(""));
    }

    private const string SampleFeed = """
    [
      { "tag_name": "v0.3.0", "name": "BookHeart 0.3.0", "draft": true, "prerelease": false, "body": "", "html_url": "x",
        "assets": [ { "name": "BookHeart-0.3.0-Setup.exe", "browser_download_url": "https://e/3.exe", "size": 1 } ] },
      { "tag_name": "v0.2.1-beta.1", "name": "BookHeart 0.2.1-beta.1", "draft": false, "prerelease": true, "body": "Test", "html_url": "x",
        "assets": [ { "name": "BookHeart-0.2.1-beta.1-Setup.exe", "browser_download_url": "https://e/b.exe", "size": 5 } ] },
      { "tag_name": "v0.2.0", "name": "BookHeart 0.2.0", "draft": false, "prerelease": false, "html_url": "https://github.com/r/0.2.0",
        "body": "## What's changed\r\n\r\n- **Updates** arrive by themselves.\r\n- See the [manual](docs/manual.md) and `F1`.",
        "assets": [
          { "name": "BookHeart-0.2.0-Portable.zip", "browser_download_url": "https://e/p.zip", "size": 20, "digest": "sha256:AB12" },
          { "name": "BookHeart-0.2.0-Setup.exe", "browser_download_url": "https://e/s.exe", "size": 10, "digest": "sha256:CD34" } ] },
      { "tag_name": "v0.1.1", "name": "BookHeart 0.1.1", "draft": false, "prerelease": false, "body": "", "html_url": "x", "assets": [] }
    ]
    """;

    [Fact]
    public void ReadsTheReleaseList()
    {
        var releases = UpdateFeed.Parse(SampleFeed);
        // The draft and the release with no downloads are left out.
        Assert.Equal(["0.2.1-beta.1", "0.2.0"], releases.Select(r => r.Version.ToString()));
        var r = releases[1];
        Assert.Equal("https://e/s.exe", r.Installer!.Url);
        Assert.Equal("cd34", r.Installer.Sha256);
        Assert.Equal("ab12", r.Portable!.Sha256);
        Assert.Equal("What's changed\n\n• Updates arrive by themselves.\n• See the manual and F1.", r.Notes);
    }

    [Fact]
    public void OffersTheNewestStableReleaseOnly()
    {
        var releases = UpdateFeed.Parse(SampleFeed);
        Assert.Equal("0.2.0", UpdateFeed.FindUpdate(releases, V("0.1.1"), includePreReleases: false, skippedVersion: null)!.Version.ToString());
        Assert.Null(UpdateFeed.FindUpdate(releases, V("0.2.0"), includePreReleases: false, skippedVersion: null));
    }

    [Fact]
    public void TestersGetTestVersions()
    {
        var releases = UpdateFeed.Parse(SampleFeed);
        Assert.Equal("0.2.1-beta.1", UpdateFeed.FindUpdate(releases, V("0.2.0"), includePreReleases: true, skippedVersion: null)!.Version.ToString());
    }

    [Fact]
    public void SkippedVersionIsNotOfferedAgainUntilSomethingNewer()
    {
        var releases = UpdateFeed.Parse(SampleFeed);
        Assert.Null(UpdateFeed.FindUpdate(releases, V("0.1.1"), false, skippedVersion: "0.2.0"));
        Assert.NotNull(UpdateFeed.FindUpdate(releases, V("0.1.1"), false, skippedVersion: "0.2.0", ignoreSkipped: true));
        Assert.NotNull(UpdateFeed.FindUpdate(releases, V("0.1.1"), false, skippedVersion: "0.1.5"));
    }

    [Fact]
    public void GarbageFeedGivesNothing() => Assert.Empty(UpdateFeed.Parse("""{ "message": "rate limited" }"""));
}
