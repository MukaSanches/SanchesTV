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
                192,
                Math.Clamp(width * 0.245, 320, 336),
                Math.Clamp(width * 0.20, 250, 276),
                12,
                true,
                false,
                true,
                height >= 600);

        if (width < 1800)
            return new(
                UiLayoutMode.Wide,
                200,
                Math.Clamp(width * 0.235, 350, 380),
                Math.Clamp(width * 0.20, 280, 320),
                16,
                true,
                false,
                true,
                true);

        return new(
            UiLayoutMode.UltraWide,
            216,
            410,
            360,
            18,
            true,
            false,
            true,
            true);
    }
}
