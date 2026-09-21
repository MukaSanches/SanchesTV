using SanchesTV.Core.P2P;

namespace SanchesTV.Tests;

public sealed class P2pMediaPolicyTests
{
    [Theory]
    [InlineData("filme.mkv")]
    [InlineData("VIDEO.MP4")]
    [InlineData("pasta/episodio.webm")]
    [InlineData("arquivo.m2ts")]
    public void DetectsCommonVideoFiles(string path)
    {
        Assert.True(P2pMediaPolicy.IsLikelyVideo(path));
    }

    [Theory]
    [InlineData("legenda.srt")]
    [InlineData("subtitle.ASS")]
    [InlineData("captions.vtt")]
    public void DetectsSubtitleFiles(string path)
    {
        Assert.True(P2pMediaPolicy.IsLikelySubtitle(path));
    }

    [Fact]
    public void RejectsNonVideoFile()
    {
        Assert.False(P2pMediaPolicy.IsLikelyVideo("documento.pdf"));
    }

    [Theory]
    [InlineData(0, 1L * 1024 * 1024 * 1024)]
    [InlineData(10, 10L * 1024 * 1024 * 1024)]
    [InlineData(999, 500L * 1024 * 1024 * 1024)]
    public void ClampsCacheLimit(int gigabytes, long expected)
    {
        Assert.Equal(expected, P2pMediaPolicy.CacheLimitBytes(gigabytes));
    }

    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1048576, "1 MB")]
    public void FormatsBytes(long bytes, string expected)
    {
        Assert.Equal(expected, P2pMediaPolicy.FormatBytes(bytes));
    }
}
