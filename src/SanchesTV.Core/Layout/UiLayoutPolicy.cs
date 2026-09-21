namespace SanchesTV.Core.Layout;

public enum UiLayoutMode
{
    Tiny,
    Compact,
    Medium,
    Wide,
    UltraWide
}

public sealed record UiLayoutMetrics(
    UiLayoutMode Mode,
    double NavigationWidth,
    double ChannelWidth,
    double SearchWidth,
    double ContentMargin,
    bool ShowNavigation,
    bool SinglePane,
    bool ShowTitleStatus,
    bool ShowBrowseSubtitle);

public static class UiLayoutPolicy
{
    public static UiLayoutMetrics Resolve(double width, double height)
    {
        if (width < 760)
            return new(
                UiLayoutMode.Tiny,
                0,
                0,
                Math.Clamp(width * 0.30, 130, 180),
                8,
                false,
                true,
                false,
                height >= 520);

        if (width < 1050)
            return new(
                UiLayoutMode.Compact,
                0,
                Math.Clamp(width * 0.34, 250, 320),
                Math.Clamp(width * 0.25, 170, 240),
                10,
                false,
                false,
                false,
                height >= 560);

        if (width < 1400)
            return new(
                UiLayoutMode.Medium,
                176,
                Math.Clamp(width * 0.27, 290, 350),
                Math.Clamp(width * 0.23, 220, 300),
                14,
                true,
                false,
                true,
                height >= 600);

        if (width < 1800)
            return new(
                UiLayoutMode.Wide,
                208,
                Math.Clamp(width * 0.25, 360, 420),
                Math.Clamp(width * 0.22, 300, 370),
                18,
                true,
                false,
                true,
                true);

        return new(
            UiLayoutMode.UltraWide,
            228,
            440,
            400,
            22,
            true,
            false,
            true,
            true);
    }
}
