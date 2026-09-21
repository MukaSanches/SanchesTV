using System.Windows;
using System.Windows.Controls;
using SanchesTV.Desktop.Audio;
using SanchesTV.Desktop.Playback;

namespace SanchesTV.Desktop.Windows;

public sealed class AudioVideoSettingsWindow : Window
{
    private readonly MpvPlaybackEngine _mpv;
    private readonly ComboBox _profile = new();
    private readonly CheckBox _exclusive = new();
    private readonly TextBlock _audioInfo = new();

    public AudioVideoSettingsWindow(MpvPlaybackEngine mpv, WindowsAudioService audio)
    {
        _mpv = mpv;

        Title = "SanchesTV — Áudio e Vídeo";
        Width = 560;
        Height = 470;
        MinWidth = 500;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.Black;
        Foreground = System.Windows.Media.Brushes.White;

        _profile.ItemsSource = Enum.GetValues<PlaybackQualityProfile>();
        _profile.SelectedItem = mpv.Profile;
        _exclusive.Content = "WASAPI Exclusive (quando suportado pelo dispositivo)";
        _exclusive.IsChecked = mpv.ExclusiveAudio;
        _exclusive.Margin = new Thickness(0, 8, 0, 12);

        _audioInfo.Text = audio.GetSummary();
        _audioInfo.TextWrapping = TextWrapping.Wrap;
        _audioInfo.Foreground = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(170, 180, 196));

        var root = new StackPanel { Margin = new Thickness(24) };
        root.Children.Add(new TextBlock
        {
            Text = "Qualidade de reprodução",
            FontSize = 24,
            FontWeight = FontWeights.Bold
        });
        root.Children.Add(new TextBlock
        {
            Text = "O libmpv usa gpu-next/libplacebo, FFmpeg, libass e dav1d. O LibVLC permanece como fallback.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 18),
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(170, 180, 196))
        });

        root.Children.Add(new TextBlock { Text = "Perfil:" });
        _profile.Margin = new Thickness(0, 6, 0, 8);
        root.Children.Add(_profile);
        root.Children.Add(_exclusive);

        root.Children.Add(new TextBlock
        {
            Text = "Áudio Windows",
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 10, 0, 6)
        });
        root.Children.Add(_audioInfo);

        root.Children.Add(new TextBlock
        {
            Text = "Máxima qualidade: scaling e tone mapping mais pesados. Baixa latência: buffer menor. Baixo consumo: filtros reduzidos.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 18, 0, 18),
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(145, 155, 172))
        });

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var apply = new Button { Content = "Aplicar", MinWidth = 110, Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(4) };
        var close = new Button { Content = "Fechar", MinWidth = 90, Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(4) };
        apply.Click += Apply_Click;
        close.Click += (_, _) => Close();
        actions.Children.Add(apply);
        actions.Children.Add(close);
        root.Children.Add(actions);

        Content = root;
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_profile.SelectedItem is PlaybackQualityProfile profile)
            await _mpv.SetProfileAsync(profile);

        await _mpv.SetExclusiveAudioAsync(_exclusive.IsChecked == true);
        MessageBox.Show(this, "Configuração aplicada ao libmpv.", "SanchesTV",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
