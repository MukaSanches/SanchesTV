using SanchesTV.Core.Models;

namespace SanchesTV.Desktop.Playback;

internal static class VlcSourceOptions
{
    public static string[] Build(ChannelSource source, params string[] extra)
    {
        var options = new List<string>
        {
            ":network-caching=1500",
            ":live-caching=1500",
            ":http-reconnect=true"
        };

        if (!string.IsNullOrWhiteSpace(source.UserAgent))
            options.Add($":http-user-agent={source.UserAgent}");
        if (!string.IsNullOrWhiteSpace(source.Referrer))
            options.Add($":http-referrer={source.Referrer}");
        if (!string.IsNullOrWhiteSpace(source.Origin))
            options.Add($":http-origin={source.Origin}");

        options.AddRange(extra.Where(x => !string.IsNullOrWhiteSpace(x)));
        return options.ToArray();
    }
}
