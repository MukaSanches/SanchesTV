using System.Windows;
using System.Windows.Controls;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;

namespace SanchesTV.Desktop.Windows;

public sealed class MultiviewWindow : Window
{
    private readonly LibVLC _libVlc;
    private readonly List<MediaPlayer> _players = new();
    private readonly List<VideoView> _views = new();

    public MultiviewWindow(IReadOnlyList<Uri> sources)
    {
        Title = "SanchesTV — Multiview";
        Width = 1100;
        Height = 700;
        Background = System.Windows.Media.Brushes.Black;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        LibVLCSharp.Shared.Core.Initialize();
        _libVlc = new LibVLC("--no-video-title-show", "--quiet", "--avcodec-hw=any");

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        for (var i = 0; i < Math.Min(4, sources.Count); i++)
        {
            var player = new MediaPlayer(_libVlc) { Volume = i == 0 ? 60 : 0 };
            var view = new VideoView { MediaPlayer = player, Margin = new Thickness(2) };
            Grid.SetRow(view, i / 2);
            Grid.SetColumn(view, i % 2);
            grid.Children.Add(view);
            _players.Add(player);
            _views.Add(view);

            using var media = new Media(_libVlc, sources[i], ":network-caching=1500", ":http-reconnect=true");
            player.Play(media);
        }

        Content = grid;
    }

    protected override void OnClosed(EventArgs e)
    {
        foreach (var p in _players)
        {
            try { p.Stop(); } catch { }
            p.Dispose();
        }
        foreach (var v in _views) v.Dispose();
        _libVlc.Dispose();
        base.OnClosed(e);
    }
}
