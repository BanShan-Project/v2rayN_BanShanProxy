namespace ServiceLib.Tests.Models;

public class SemanticVersionTests
{
    [Test]
    public async Task FourPartVersion_ShouldBeGreaterThanThreePartVersion()
    {
        var release = new SemanticVersion("v7.25.6.1");
        var current = new SemanticVersion("7.25.6");

        await (release > current).Should().BeTrue();
        await (current < release).Should().BeTrue();
        await (release <= current).Should().BeFalse();
        await release.Equals(current).Should().BeFalse();
    }

    [Test]
    public async Task FourPartVersion_ShouldCompareEqualWhenIdentical()
    {
        var left = new SemanticVersion("v7.25.6.1");
        var right = new SemanticVersion("7.25.6.1");

        await (left == right || left.CompareTo(right) == 0).Should().BeTrue();
        await left.Equals(right).Should().BeTrue();
        await (left.GetHashCode() == right.GetHashCode()).Should().BeTrue();
    }

    [Test]
    public async Task ThreePartVersions_ShouldKeepUpstreamBehaviour()
    {
        await (new SemanticVersion("7.25.7") > new SemanticVersion("7.25.6")).Should().BeTrue();
        await new SemanticVersion("v7.25.6").Equals(new SemanticVersion("7.25.6")).Should().BeTrue();
        await (new SemanticVersion("7.25.6-beta") < new SemanticVersion("7.25.6")).Should().BeTrue();
    }

    [Test]
    public async Task FourPartVersion_ShouldSortBetweenPatchReleases()
    {
        var hotfix = new SemanticVersion("7.25.6.1");

        await (hotfix > new SemanticVersion("7.25.5.9")).Should().BeTrue();
        await (hotfix < new SemanticVersion("7.25.7")).Should().BeTrue();
        await (new SemanticVersion("7.26.0.0") > hotfix).Should().BeTrue();
    }

    [Test]
    public async Task StandardVersionString_ShouldKeepTheFourthComponent()
    {
        var version = new SemanticVersion("v7.25.6.1");

        await version.ToStandardVersionString().Should().BeEqualTo("7.25.6.1");
        await version.ToStandardVersionString("v").Should().BeEqualTo("v7.25.6.1");
        await version.ToString().Should().BeEqualTo("v7.25.6.1");
    }

    [Test]
    public async Task StandardVersionString_ShouldDropZeroRevision()
    {
        var version = new SemanticVersion("7.25.6");

        await version.ToStandardVersionString("v").Should().BeEqualTo("v7.25.6");
    }
}
