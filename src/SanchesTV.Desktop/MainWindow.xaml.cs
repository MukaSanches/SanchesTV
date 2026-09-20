using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using LibVLCSharp.Shared;
using SanchesTV.Core.Catalog;
using SanchesTV.Core.Health;
using SanchesTV.Core.Import;
using SanchesTV.Core.Models;
using SanchesTV.Core.Parsing;
using SanchesTV.Core.Storage;
using SanchesTV.Desktop.Playback;
using SanchesTV.Desktop.Remote;
using SanchesTV.Desktop.Windows;

namespace SanchesTV.Desktop;

public partial class MainWindow : Window
{
    private readonly AppDatabase _db = new();
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly VlcPlaybackEngine _player = new();
    private RemoteControlServer? _remote;
    private IReadOnlyList<Channel> _allChannels = Array.Empty<Channel>();
    private List<ChannelListItem> _visibleItems = new();
    private Channel? _currentChannel;
    private string _mode = "all";
    private bool _isFullscreen;
    private bool _isRecording;

    public MainWindow()
    {
        InitializeComponent();
        VideoView.Loaded += (_, _) => VideoView.MediaPlayer = _player.MediaPlayer;
        _player.PlaybackError += (_, message) => Dispatcher.Invoke(() => StatusText.Text = message);
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true, "Inicializando SanchesTV...");
            await _db.InitializeAsync();

            var existing = await _db.GetChannelsAsync();
            if (existing.Count == 0)
                await _db.UpsertChannelsAsync(BuiltInCatalog.Create());

            await RefreshChannelsAsync();
            StatusText.Text = "Pronto";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Falha ao iniciar", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Erro na inicialização";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RefreshChannelsAsync()
    {
        _allChannels = await _db.GetChannelsAsync();
        await ApplyFilterAsync();
    }

    private async Task ApplyFilterAsync()
    {
        IEnumerable<Channel> query = _allChannels;

        if (_mode == "favorites")
            query = query.Where(c => c.IsFavorite);
        else if (_mode == "mytv")
            query = query.Where(c => c.MyTvPosition is not null).OrderBy(c => c.MyTvPosition);
        else if (_mode == "recent")
        {
            var ids = await _db.GetRecentChannelIdsAsync();
            var order = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
            query = query.Where(c => order.ContainsKey(c.Id)).OrderBy(c => order[c.Id]);
        }

        var normalized = TextNormalizer.Normalize(SearchBox.Text ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            query = query.Where(c =>
                c.NormalizedName.Contains(normalized, StringComparison.Ordinal) ||
                TextNormalizer.Normalize(c.Category ?? string.Empty).Contains(normalized, StringComparison.Ordinal) ||
                TextNormalizer.Normalize(c.State ?? string.Empty).Contains(normalized, StringComparison.Ordinal) ||
                TextNormalizer.Normalize(c.Region ?? string.Empty).Contains(normalized, StringComparison.Ordinal));
        }

        _visibleItems = query
            .Select(c => new ChannelListItem(c))
            .ToList();

        ChannelList.ItemsSource = _visibleItems;
        CountText.Text = $"{_visibleItems.Count:N0} canais";
    }

    private Channel? SelectedChannel =>
        ChannelList.SelectedItem is ChannelListItem item ? item.Channel : null;

    private async Task PlayChannelAsync(Channel channel)
    {
        if (channel.Sources.Count == 0)
            return;

        try
        {
            SetBusy(true, $"Abrindo {channel.Name}...");
            StatusText.Text = "Testando fontes e iniciando...";
            var ordered = channel.Sources
                .OrderBy(s => s.Status == StreamStatus.Online ? 0 : s.Status == StreamStatus.NotTested ? 1 : 2)
                .ThenBy(s => s.Priority)
                .Select(s => s.Url)
                .ToArray();

            var active = await _player.OpenWithFallbackAsync(ordered);
            _currentChannel = channel;
            await _db.RecordPlayedAsync(channel.Id);

            NowPlayingText.Text = channel.Name;
            StatusText.Text = $"Reproduzindo via {active.Host}";
            await UpdateEpgAsync(channel);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Nenhuma fonte iniciou";
            MessageBox.Show(this, ex.Message, "Falha de reprodução", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task UpdateEpgAsync(Channel channel)
    {
        var (now, next) = await _db.GetNowNextAsync(channel.EpgId);
        NowEpgText.Text = now is null ? "Programação atual não disponível" : $"Agora: {now.Title}";
        NextEpgText.Text = next is null ? string.Empty : $"A seguir: {next.Title} • {next.Start.ToLocalTime():HH:mm}";
    }

    private void SetBusy(bool busy, string? text = null)
    {
        LoadingOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (text is not null) LoadingText.Text = text;
    }

    private async void ImportFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Importar lista M3U",
            Filter = "Playlists M3U|*.m3u;*.m3u8|Todos os arquivos|*.*"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            SetBusy(true, "Importando playlist...");
            var text = await File.ReadAllTextAsync(dialog.FileName);
            var channels = M3uParser.Parse(text, Path.GetFileName(dialog.FileName));
            var count = await _db.UpsertChannelsAsync(channels);
            await RefreshChannelsAsync();
            StatusText.Text = $"{count} entradas processadas";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Erro ao importar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { SetBusy(false); }
    }

    private async void ImportUrl_Click(object sender, RoutedEventArgs e)
    {
        var input = new InputDialog("Adicionar M3U", "URL da playlist M3U/M3U8:") { Owner = this };
        if (input.ShowDialog() != true || !Uri.TryCreate(input.Value, UriKind.Absolute, out var uri)) return;

        try
        {
            SetBusy(true, "Baixando playlist...");
            var text = await _http.GetStringAsync(uri);
            var channels = M3uParser.Parse(text, uri.Host);
            var count = await _db.UpsertChannelsAsync(channels);
            await RefreshChannelsAsync();
            StatusText.Text = $"{count} entradas processadas";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Erro ao importar URL", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { SetBusy(false); }
    }

    private async void Xtream_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new XtreamDialog { Owner = this };
        if (dialog.ShowDialog() != true) return;
        if (!Uri.TryCreate(dialog.Server, UriKind.Absolute, out _))
        {
            MessageBox.Show(this, "Informe um servidor HTTP/HTTPS válido.", "Xtream", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            SetBusy(true, "Importando canais Xtream...");
            var importer = new XtreamImporter(_http);
            var channels = await importer.ImportLiveAsync(dialog.Server, dialog.Username, dialog.Password);
            var count = await _db.UpsertChannelsAsync(channels);
            await RefreshChannelsAsync();
            StatusText.Text = $"{count} canais Xtream processados";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Falha no Xtream", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ImportEpg_Click(object sender, RoutedEventArgs e)
    {
        var choose = MessageBox.Show(this,
            "Clique em Sim para carregar um arquivo XMLTV local. Clique em Não para informar uma URL XMLTV.",
            "Adicionar EPG",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (choose == MessageBoxResult.Cancel) return;

        try
        {
            SetBusy(true, "Processando EPG...");
            string xml;
            if (choose == MessageBoxResult.Yes)
            {
                var dialog = new OpenFileDialog { Filter = "XMLTV|*.xml;*.xmltv|Todos os arquivos|*.*" };
                if (dialog.ShowDialog(this) != true) return;
                xml = await File.ReadAllTextAsync(dialog.FileName);
            }
            else
            {
                var input = new InputDialog("EPG XMLTV", "URL XMLTV:") { Owner = this };
                if (input.ShowDialog() != true || !Uri.TryCreate(input.Value, UriKind.Absolute, out var uri)) return;
                xml = await _http.GetStringAsync(uri);
            }

            var programs = XmlTvParser.Parse(xml);
            await _db.ReplaceEpgAsync(programs);
            StatusText.Text = $"{programs.Count:N0} programas de EPG importados";
            if (_currentChannel is not null) await UpdateEpgAsync(_currentChannel);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Erro no EPG", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { SetBusy(false); }
    }

    private async void BuiltIn_Click(object sender, RoutedEventArgs e)
    {
        var count = await _db.UpsertChannelsAsync(BuiltInCatalog.Create());
        await RefreshChannelsAsync();
        StatusText.Text = $"Catálogo oficial atualizado ({count} entradas)";
    }

    private async void ToggleFavorite_Click(object sender, RoutedEventArgs e)
    {
        var channel = SelectedChannel;
        if (channel is null) return;
        await _db.SetFavoriteAsync(channel.Id, !channel.IsFavorite);
        await RefreshChannelsAsync();
    }

    private async void ToggleMyTv_Click(object sender, RoutedEventArgs e)
    {
        var channel = SelectedChannel;
        if (channel is null) return;

        int? position = null;
        if (channel.MyTvPosition is null)
            position = _allChannels.Where(c => c.MyTvPosition is not null).Select(c => c.MyTvPosition!.Value).DefaultIfEmpty(0).Max() + 1;

        await _db.SetMyTvPositionAsync(channel.Id, position);
        await RefreshChannelsAsync();
    }

    private async void HealthCheck_Click(object sender, RoutedEventArgs e)
    {
        var channel = SelectedChannel;
        if (channel is null) return;

        SetBusy(true, "Testando fontes...");
        try
        {
            using var healthHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            var checker = new StreamHealthChecker(healthHttp);
            var messages = new List<string>();
            foreach (var source in channel.Sources)
            {
                if (source.Url.Scheme is not ("http" or "https"))
                {
                    messages.Add($"{source.Provider}: protocolo {source.Url.Scheme} — teste pelo player");
                    continue;
                }
                var result = await checker.CheckAsync(source);
                messages.Add($"{source.Provider}: {result.Status} ({result.Latency?.TotalMilliseconds:N0} ms)");
            }
            MessageBox.Show(this, string.Join(Environment.NewLine, messages), $"Fontes — {channel.Name}", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally { SetBusy(false); }
    }

    private async void ChannelList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedChannel is { } channel)
            await PlayChannelAsync(channel);
    }

    private async void ChannelList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedChannel is { } channel)
        {
            var (now, next) = await _db.GetNowNextAsync(channel.EpgId);
            StatusText.Text = now is null ? channel.Name : $"{channel.Name} • {now.Title}";
            if (next is not null)
                ToolTip = $"A seguir: {next.Title}";
        }
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => await ApplyFilterAsync();

    private async void All_Click(object sender, RoutedEventArgs e) { _mode = "all"; await ApplyFilterAsync(); }
    private async void Favorites_Click(object sender, RoutedEventArgs e) { _mode = "favorites"; await ApplyFilterAsync(); }
    private async void Recent_Click(object sender, RoutedEventArgs e) { _mode = "recent"; await ApplyFilterAsync(); }
    private async void MyTv_Click(object sender, RoutedEventArgs e) { _mode = "mytv"; await ApplyFilterAsync(); }

    private async void Previous_Click(object sender, RoutedEventArgs e) => await StepChannelAsync(-1);
    private async void Next_Click(object sender, RoutedEventArgs e) => await StepChannelAsync(+1);

    private async Task StepChannelAsync(int delta)
    {
        if (_visibleItems.Count == 0) return;
        var current = _currentChannel is null ? -1 : _visibleItems.FindIndex(x => x.Channel.Id == _currentChannel.Id);
        var index = current < 0 ? 0 : (current + delta + _visibleItems.Count) % _visibleItems.Count;
        ChannelList.SelectedIndex = index;
        ChannelList.ScrollIntoView(ChannelList.SelectedItem);
        await PlayChannelAsync(_visibleItems[index].Channel);
    }

    private async void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_player.MediaPlayer.IsPlaying) await _player.PauseAsync();
        else await _player.PlayAsync();
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        await _player.StopAsync();
        StatusText.Text = "Parado";
    }

    private async void Mute_Click(object sender, RoutedEventArgs e)
    {
        var mute = !_player.MediaPlayer.Mute;
        await _player.SetMuteAsync(mute);
        MuteButton.Content = mute ? "🔇" : "🔊";
    }

    private async void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        await _player.SetVolumeAsync(e.NewValue);
    }

    private void Rewind_Click(object sender, RoutedEventArgs e)
    {
        if (!_player.MediaPlayer.IsSeekable)
        {
            StatusText.Text = "Esta fonte não oferece timeshift/seek.";
            return;
        }
        _player.MediaPlayer.Time = Math.Max(0, _player.MediaPlayer.Time - 30_000);
    }

    private void Live_Click(object sender, RoutedEventArgs e)
    {
        if (!_player.MediaPlayer.IsSeekable)
        {
            StatusText.Text = "Esta fonte não oferece retorno ao vivo por seek.";
            return;
        }
        _player.MediaPlayer.Position = 1f;
        _player.MediaPlayer.SetPause(false);
    }

    private void Record_Click(object sender, RoutedEventArgs e)
    {
        if (_currentChannel is null)
        {
            StatusText.Text = "Selecione um canal antes de gravar.";
            return;
        }

        if (!_isRecording)
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "SanchesTV");
            if (_player.StartRecording(directory))
            {
                _isRecording = true;
                RecordButton.Content = "■ Parar gravação";
                StatusText.Text = $"Gravando em {directory}";
            }
            else
            {
                StatusText.Text = "Não foi possível iniciar a gravação.";
            }
        }
        else
        {
            _player.StopRecording();
            _isRecording = false;
            RecordButton.Content = "● Gravar";
            StatusText.Text = _player.RecordingPath is null ? "Gravação encerrada" : $"Gravação salva: {_player.RecordingPath}";
        }
    }

    private void Pip_Click(object sender, RoutedEventArgs e)
    {
        if (_player.CurrentSource is null) return;
        var window = new PipWindow(_player.CurrentSource) { Owner = this };
        window.Show();
    }

    private void Multiview_Click(object sender, RoutedEventArgs e)
    {
        var candidates = _visibleItems
            .Select(x => x.Channel)
            .Where(c => c.Sources.Count > 0)
            .OrderByDescending(c => _currentChannel is not null && c.Id == _currentChannel.Id)
            .Take(4)
            .Select(c => c.Sources.OrderBy(s => s.Priority).First().Url)
            .ToList();

        if (candidates.Count < 2)
        {
            StatusText.Text = "São necessários pelo menos dois canais com fonte.";
            return;
        }

        new MultiviewWindow(candidates) { Owner = this }.Show();
    }

    private void Remote_Click(object sender, RoutedEventArgs e)
    {
        if (_remote is null)
        {
            _remote = new RemoteControlServer();
            _remote.NextRequested += () => Dispatcher.Invoke(() => Next_Click(this, new RoutedEventArgs()));
            _remote.PreviousRequested += () => Dispatcher.Invoke(() => Previous_Click(this, new RoutedEventArgs()));
            _remote.PlayPauseRequested += () => Dispatcher.Invoke(() => PlayPause_Click(this, new RoutedEventArgs()));
            _remote.MuteRequested += () => Dispatcher.Invoke(() => Mute_Click(this, new RoutedEventArgs()));
            _remote.VolumeRequested += delta => Dispatcher.Invoke(() =>
                VolumeSlider.Value = Math.Clamp(VolumeSlider.Value + delta, 0, 100));
            _remote.Start();
        }

        new RemoteControlWindow(_remote.GetRemoteUrl()) { Owner = this }.ShowDialog();
    }

    private void Diagnostics_Click(object sender, RoutedEventArgs e)
    {
        var mp = _player.MediaPlayer;
        using var media = mp.Media;
        var source = media?.Mrl ?? "(nenhuma)";
        if (source.Contains('@'))
        {
            try
            {
                var u = new Uri(source);
                source = $"{u.Scheme}://***@{u.Host}{u.AbsolutePath}";
            }
            catch { source = "(fonte protegida)"; }
        }

        var text =
            $"Engine: {_player.Name}\n" +
            $"Estado: {mp.State}\n" +
            $"Fonte: {source}\n" +
            $"FPS: {mp.Fps:N2}\n" +
            $"Volume: {mp.Volume}%\n" +
            $"Tempo: {TimeSpan.FromMilliseconds(Math.Max(0, mp.Time)):hh\\:mm\\:ss}\n" +
            $"Duração: {(mp.Length > 0 ? TimeSpan.FromMilliseconds(mp.Length).ToString(@"hh\:mm\:ss") : "ao vivo/indefinida")}\n" +
            $"Seek/timeshift: {(mp.IsSeekable ? "sim" : "não")}\n" +
            $"Pausa: {(mp.CanPause ? "sim" : "não")}\n" +
            $"Banco: {_db.DatabasePath}";

        MessageBox.Show(this, text, "Diagnóstico SanchesTV", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Fullscreen_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

    private void ToggleFullscreen()
    {
        _isFullscreen = !_isFullscreen;
        HeaderPanel.Visibility = _isFullscreen ? Visibility.Collapsed : Visibility.Visible;
        FooterPanel.Visibility = _isFullscreen ? Visibility.Collapsed : Visibility.Visible;
        LibraryPanel.Visibility = _isFullscreen ? Visibility.Collapsed : Visibility.Visible;
        LibraryColumn.Width = _isFullscreen ? new GridLength(0) : new GridLength(390);
        WindowStyle = _isFullscreen ? WindowStyle.None : WindowStyle.SingleBorderWindow;
        WindowState = _isFullscreen ? WindowState.Maximized : WindowState.Normal;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _isFullscreen)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
        else if (e.Key == Key.Space)
        {
            PlayPause_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Up && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            Previous_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Down && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            Next_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_remote is not null) await _remote.DisposeAsync();
        await _player.DisposeAsync();
        _http.Dispose();
    }

    private sealed class ChannelListItem
    {
        public Channel Channel { get; }
        public string Name => Channel.Name;
        public string? Category => Channel.Category;
        public string VirtualNumber => Channel.VirtualNumber?.ToString("000") ?? string.Empty;
        public string FavoriteMark => Channel.IsFavorite ? "★" : string.Empty;

        public ChannelListItem(Channel channel) => Channel = channel;
    }
}
