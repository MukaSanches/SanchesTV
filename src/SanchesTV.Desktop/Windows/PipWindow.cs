using System.Windows;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;

namespace SanchesTV.Desktop.Windows;

public sealed class PipWindow : Window
{
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _player;
    private readonly VideoView _view;

    public PipWindow(Uri source)
    {
        Title = "SanchesTV — Picture in Picture";
        Width = 480;
        Height = 300;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = System.Windows.Media.Brushes.Black;

        Core.Initialize();
        _libVlc = new LibVLC("--no-video-title-show", "--quiet", "--avcodec-hw=any");
        _player = new MediaPlayer(_libVlc) { Volume = 70 };
        _view = new VideoView { MediaPlayer = _player };
        Content = _view;

        Loaded += (_, _) =>
        {
            using var media = new Media(_libVlc, source, ":network-caching=1200", ":http-reconnect=true");
            _player.Play(media);
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        try { _player.Stop(); } catch { }
        _view.Dispose();
        _player.Dispose();
        _libVlc.Dispose();
        base.OnClosed(e);
    }
}
