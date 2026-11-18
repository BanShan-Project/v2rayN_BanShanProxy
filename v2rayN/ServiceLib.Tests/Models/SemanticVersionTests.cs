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

    /// <summary>
    /// Releases alternate between three part (v7.25.7) and four part (v7.25.6.1) tags.
    /// UpdateService.ParseDownloadUrl reports "already latest" when current &gt;= release, so the
    /// table below is the whole contract: every newer tag has to win, every equal or older one
    /// must stay silent.
    /// </summary>
    [Test]
    [Arguments("7.25.6", "v7.25.7", true)]          // 三段 -> 三段
    [Arguments("7.25.6", "v7.25.6.1", true)]        // 三段 -> 四段
    [Arguments("7.25.6.1", "v7.25.7", true)]        // 四段 -> 三段
    [Arguments("7.25.6.1", "v7.25.6.2", true)]      // 四段 -> 四段
    [Arguments("7.25.6.1", "v7.25.6.10", true)]     // 第四段按数字比较，不是字符串
    [Arguments("7.25.6.1", "v7.26.0", true)]        // 四段 -> 高一位的三段
    [Arguments("7.25.7", "v7.25.6.1", false)]       // 服务器版本更旧，不提示
    [Arguments("7.25.6.1", "v7.25.6.1", false)]     // 完全相同
    [Arguments("7.25.6", "v7.25.6.0", false)]       // 第四段为 0 等价于三段
    [Arguments("7.25.6.1", "v7.25.6.1-beta", false)] // 预发布版不覆盖已装的正式版
    public async Task MixedReleaseTags_ShouldOfferUpdateOnlyWhenNewer(
        string currentText, string releaseText, bool shouldUpdate)
    {
        var current = new SemanticVersion(currentText);
        var release = new SemanticVersion(releaseText);

        // UpdateService.ParseDownloadUrl 的逻辑：curVersion >= version 时判定"已是最新"
        var updateOffered = !(current >= release);

        await updateOffered.Should().BeEqualTo(shouldUpdate);
    }
}
