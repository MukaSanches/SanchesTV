using System.Windows;
using System.Windows.Controls;
using SanchesTV.Desktop.Audio;
using SanchesTV.Desktop.Playback;

namespace SanchesTV.Desktop.Windows;

public sealed class AudioVideoSettingsWindow : Window
{
    private readonly MpvPlaybackEngine _mpv;
    private readonly ComboBox _profile = new();
    private readonly ComboBox _deinterlace = new();
    private readonly ComboBox _toneMapping = new();
    private readonly ComboBox _audioEnhancement = new();
    private readonly CheckBox _interpolation = new();
    private readonly CheckBox _exclusive = new();
    private readonly TextBox _audioLanguages = new();
    private readonly TextBox _subtitleLanguages = new();
    private readonly TextBox _audioDelay = new();
    private readonly TextBox _subtitleDelay = new();
    private readonly TextBlock _audioInfo = new();

    public AudioVideoSettingsWindow(MpvPlaybackEngine mpv, WindowsAudioService audio)
    {
        _mpv = mpv;

        Title = "SanchesTV 7 — Áudio, Vídeo e Legendas";
        Width = 720;
        Height = 720;
        MinWidth = 620;
        MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.Black;
        Foreground = System.Windows.Media.Brushes.White;

        _profile.ItemsSource = Enum.GetValues<PlaybackQualityProfile>();
        _profile.SelectedItem = mpv.Profile;
        _deinterlace.ItemsSource = Enum.GetValues<DeinterlaceMode>();
        _deinterlace.SelectedItem = mpv.Deinterlace;
        _toneMapping.ItemsSource = Enum.GetValues<ToneMappingMode>();
        _toneMapping.SelectedItem = mpv.ToneMapping;
        _audioEnhancement.ItemsSource = Enum.GetValues<AudioEnhancementMode>();
        _audioEnhancement.SelectedItem = mpv.AudioEnhancement;

        _interpolation.Content = "Interpolação temporal / frame pacing suave";
        _interpolation.IsChecked = mpv.FrameInterpolation;
        _exclusive.Content = "WASAPI Exclusive quando suportado";
        _exclusive.IsChecked = mpv.ExclusiveAudio;

        _audioLanguages.Text = mpv.PreferredAudioLanguages;
        _subtitleLanguages.Text = mpv.PreferredSubtitleLanguages;
        _audioDelay.Text = "0";
        _subtitleDelay.Text = "0";

        _audioInfo.Text = audio.GetSummary();
        _audioInfo.TextWrapping = TextWrapping.Wrap;
        _audioInfo.Foreground = Brush("#AAB4C4");

        var root = new StackPanel { Margin = new Thickness(24) };
        root.Children.Add(new TextBlock
        {
            Text = "Cinema Engine 7",
            FontSize = 28,
            FontWeight = FontWeights.Bold
        });
        root.Children.Add(new TextBlock
        {
            Text = "libmpv + gpu-next/libplacebo + FFmpeg + libass + dav1d + WASAPI. Ajustes aplicados em tempo real quando o libmpv está ativo.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 20),
            Foreground = Brush("#AAB4C4")
        });

        root.Children.Add(Section("Imagem"));
        root.Children.Add(Row("Perfil geral", _profile));
        root.Children.Add(Row("Deinterlace", _deinterlace));
        root.Children.Add(Row("Tone mapping HDR", _toneMapping));
        root.Children.Add(_interpolation);

        root.Children.Add(Section("Áudio"));
        root.Children.Add(Row("Tratamento", _audioEnhancement));
        root.Children.Add(_exclusive);
        root.Children.Add(Row("Idiomas preferidos", _audioLanguages));
        root.Children.Add(Row("Delay de áudio (s)", _audioDelay));

        root.Children.Add(Section("Legendas"));
        root.Children.Add(Row("Idiomas preferidos", _subtitleLanguages));
        root.Children.Add(Row("Delay da legenda (s)", _subtitleDelay));

        var trackActions = new WrapPanel { Margin = new Thickness(0, 8, 0, 14) };
        trackActions.Children.Add(Button("Próximo áudio", async (_, _) => await RunLiveAsync(_mpv.CycleAudioTrackAsync)));
        trackActions.Children.Add(Button("Próxima legenda", async (_, _) => await RunLiveAsync(_mpv.CycleSubtitleTrackAsync)));
        trackActions.Children.Add(Button("Mostrar/ocultar legenda", async (_, _) => await RunLiveAsync(_mpv.ToggleSubtitlesAsync)));
        trackActions.Children.Add(Button("Capturar frame", Screenshot_Click));
        root.Children.Add(trackActions);

        root.Children.Add(Section("Saída do Windows"));
        root.Children.Add(_audioInfo);

        root.Children.Add(new TextBlock
        {
            Text = "Normalizar usa dynaudnorm. Night usa compressão dinâmica. Dialogue aplica ganho seletivo nas frequências da fala. Interpolação usa o frame mixer do gpu-next/libplacebo.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 18, 0, 14),
            Foreground = Brush("#8F9BAD")
        });

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var apply = Button("Aplicar", Apply_Click);
        apply.MinWidth = 120;
        var close = Button("Fechar", (_, _) => Close());
        close.MinWidth = 90;
        actions.Children.Add(apply);
        actions.Children.Add(close);
        root.Children.Add(actions);

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = root
        };
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_profile.SelectedItem is PlaybackQualityProfile profile)
                await _mpv.SetProfileAsync(profile);

            await _mpv.SetExclusiveAudioAsync(_exclusive.IsChecked == true);

            await _mpv.SetAdvancedMediaAsync(
                _deinterlace.SelectedItem is DeinterlaceMode d ? d : DeinterlaceMode.Auto,
                _toneMapping.SelectedItem is ToneMappingMode t ? t : ToneMappingMode.Bt2390,
                _audioEnhancement.SelectedItem is AudioEnhancementMode a ? a : AudioEnhancementMode.Flat,
                _interpolation.IsChecked == true,
                _audioLanguages.Text,
                _subtitleLanguages.Text);

            if (_mpv.IsAvailable)
            {
                if (double.TryParse(_audioDelay.Text, out var ad))
                    await _mpv.SetAudioDelayAsync(Math.Clamp(ad, -20, 20));
                if (double.TryParse(_subtitleDelay.Text, out var sd))
                    await _mpv.SetSubtitleDelayAsync(Math.Clamp(sd, -60, 60));
            }

            MessageBox.Show(this, "Configuração aplicada.", "SanchesTV 7",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Áudio/Vídeo",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void Screenshot_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await _mpv.TakeScreenshotAsync();
            MessageBox.Show(this, "Captura salva em:\n" + path, "SanchesTV 7",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Captura",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task RunLiveAsync(Func<CancellationToken, Task> action)
    {
        try
        {
            if (!_mpv.IsAvailable)
                throw new InvalidOperationException("Abra um vídeo no libmpv primeiro.");
            await action(CancellationToken.None);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "SanchesTV 7",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static TextBlock Section(string text) => new()
    {
        Text = text,
        FontSize = 18,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 18, 0, 8)
    };

    private static Grid Row(string label, Control control)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush("#B8C2D2")
        });
        control.MinHeight = 32;
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        return grid;
    }

    private static Button Button(string text, RoutedEventHandler click)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(4)
        };
        button.Click += click;
        return button;
    }

    private static System.Windows.Media.Brush Brush(string hex) =>
        (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!;
}
