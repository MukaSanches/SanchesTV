using SanchesTV.Core.Layout;
using Xunit;

namespace SanchesTV.Tests;

public sealed class UiLayoutPolicyTests
{
    [Theory]
    [InlineData(480, 320, UiLayoutMode.Tiny)]
    [InlineData(520, 360, UiLayoutMode.Tiny)]
    [InlineData(720, 480, UiLayoutMode.Tiny)]
    [InlineData(800, 600, UiLayoutMode.Compact)]
    [InlineData(1024, 768, UiLayoutMode.Compact)]
    [InlineData(1280, 720, UiLayoutMode.Medium)]
    [InlineData(1366, 768, UiLayoutMode.Medium)]
    [InlineData(1536, 864, UiLayoutMode.Wide)]
    [InlineData(1920, 1080, UiLayoutMode.UltraWide)]
    public void Resolve_Covers_Common_Window_Sizes(double width, double height, UiLayoutMode expected)
    {
        var layout = UiLayoutPolicy.Resolve(width, height);
        Assert.Equal(expected, layout.Mode);
    }

    [Fact]
    public void Tiny_Mode_Uses_Single_Pane_And_Hides_Fixed_Navigation()
    {
        var layout = UiLayoutPolicy.Resolve(640, 420);

        Assert.True(layout.SinglePane);
        Assert.False(layout.ShowNavigation);
        Assert.False(layout.ShowTitleStatus);
    }

    [Theory]
    [InlineData(520, 360)]
    [InlineData(800, 600)]
    [InlineData(1366, 768)]
    [InlineData(1536, 864)]
    [InlineData(1920, 1080)]
    public void Resolve_Never_Returns_Negative_Dimensions(double width, double height)
    {
        var layout = UiLayoutPolicy.Resolve(width, height);

        Assert.True(layout.NavigationWidth >= 0);
        Assert.True(layout.ChannelWidth >= 0);
        Assert.True(layout.SearchWidth > 0);
        Assert.True(layout.ContentMargin >= 0);
    }
}
