using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using LibVLCSharp.Shared;
using SanchesTV.Core.Catalog;
using SanchesTV.Core.Health;
using SanchesTV.Core.Import;
using SanchesTV.Core.Layout;
using SanchesTV.Core.Models;
using SanchesTV.Core.Parsing;
using SanchesTV.Core.Storage;
using SanchesTV.Core.Search;
using SanchesTV.Desktop.Playback;
using SanchesTV.Desktop.Audio;
using SanchesTV.Desktop.P2P;
using SanchesTV.Desktop.Remote;
using SanchesTV.Desktop.Windows;
using SanchesTV.Desktop.Tools;
using SanchesTV.Desktop.Recording;
using SanchesTV.Desktop.Diagnostics;

namespace SanchesTV.Desktop;

public partial class MainWindow : Window
{
    private static readonly HashSet<string> LusophoneCountryCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BR", "PT", "AO", "MZ", "CV", "GW", "ST", "TL"
    };

    private readonly AppDatabase _db = new();
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(45) };
    private readonly VlcPlaybackEngine _vlc = new();
    private readonly MpvPlaybackEngine _mpv = new();
    private readonly FfmpegRecorder _recorder = new();
    private readonly TimeshiftService _timeshift = new();
    private readonly WindowsAudioService _audio = new();
    private readonly P2pStreamingService _p2p = new();
    private readonly MediaToolsService _mediaTools = new();
    private readonly HardwareMonitorService _hardware = new();
    private readonly MediaRouterService _mediaRouter;
    private readonly RecordingSchedulerService _recordingScheduler;
    private readonly TaskCompletionSource<bool> _mpvReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private RemoteControlServer? _remote;
    private P2pStreamingWindow? _p2pWindow;
    private IReadOnlyList<Channel> _allChannels = Array.Empty<Channel>();
    private List<ChannelListItem> _visibleItems = new();
    private Channel? _currentChannel;
    private string _mode = "all";
    private bool _isFullscreen;
    private bool _isRecording;
    private bool _catalogSyncRunning;
    private bool _showCompactChannels = true;
    private bool _muted;
    private PlaybackBackend _activeBackend = PlaybackBackend.Vlc;
    private ChannelSource? _activeSource;
    private ChannelSource? _timeshiftOriginSource;
    private WindowState _windowStateBeforeFullscreen = WindowState.Normal;

    public MainWindow()
    {
        InitializeComponent();

        _mediaRouter = new MediaRouterService(_mediaTools);
        _recordingScheduler = new RecordingSchedulerService(_db);
        _recordingScheduler.StatusChanged += (_, message) => Dispatcher.Invoke(() =>
        {
            StatusText.Text = message;
            TitleStatusText.Text = "Gravação programada";
        });
        _recordingScheduler.Start();

        VideoView.Loaded += (_, _) => VideoView.MediaPlayer = _vlc.MediaPlayer;

        MpvSurface.HostReady += (_, hwnd) =>
        {
            try
            {
                _mpv.Initialize(hwnd);
                _mpvReady.TrySetResult(true);
            }
            catch (Exception ex)
            {
                _mpvReady.TrySetResult(false);
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = $"libmpv indisponível; VLC ativo ({ex.Message})";
                    SetVideoBackend(PlaybackBackend.Vlc);
                });
            }
        };

        _vlc.PlaybackError += (_, message) => Dispatcher.Invoke(() =>
        {
            if (_activeBackend == PlaybackBackend.Vlc)
            {
                StatusText.Text = message;
                TitleStatusText.Text = "Falha de reprodução";
            }
        });

        _mpv.PlaybackError += (_, message) => Dispatcher.Invoke(() =>
        {
            if (_activeBackend == PlaybackBackend.Mpv)
            {
                StatusText.Text = message;
                TitleStatusText.Text = "Falha no libmpv";
            }
        });

        Loaded += MainWindow_Loaded;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) => Windows11Backdrop.Apply(this);

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_isFullscreen)
            ApplyResponsiveLayout();
    }

    private void ApplyResponsiveLayout()
    {
        if (!IsLoaded && ActualWidth <= 0)
            return;

        var layout = UiLayoutPolicy.Resolve(
            Math.Max(ActualWidth, 520),
            Math.Max(ActualHeight, 360));

        CompactMenuButton.Visibility = layout.ShowNavigation
            ? Visibility.Collapsed
            : Visibility.Visible;

        TitleStatusContainer.Visibility = layout.ShowTitleStatus
            ? Visibility.Visible
            : Visibility.Collapsed;

        BrowseSubtitleText.Visibility = layout.ShowBrowseSubtitle
            ? Visibility.Visible
            : Visibility.Collapsed;

        BrowseView.Margin = new Thickness(layout.ContentMargin);

        NavigationPanel.Visibility = layout.ShowNavigation
            ? Visibility.Visible
            : Visibility.Collapsed;
        NavigationColumn.Width = new GridLength(layout.NavigationWidth);

        SearchColumn.Width = new GridLength(layout.SearchWidth);

        CompactChannelButton.Visibility = layout.SinglePane
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (layout.SinglePane)
        {
            BrowseGapColumn.Width = new GridLength(0);

            if (_showCompactChannels)
            {
                ChannelBrowserPanel.Visibility = Visibility.Visible;
                PlayerPanel.Visibility = Visibility.Collapsed;
                ChannelBrowserColumn.Width = new GridLength(1, GridUnitType.Star);
                PlayerColumn.Width = new GridLength(0);
                CompactChannelButton.Content = "▶ Player";
                CompactChannelButton.ToolTip = "Mostrar o player";
            }
            else
            {
                ChannelBrowserPanel.Visibility = Visibility.Collapsed;
                PlayerPanel.Visibility = Visibility.Visible;
                ChannelBrowserColumn.Width = new GridLength(0);
                PlayerColumn.Width = new GridLength(1, GridUnitType.Star);
                CompactChannelButton.Content = "☰ Canais";
                CompactChannelButton.ToolTip = "Mostrar a lista de canais";
            }
        }
        else
        {
            ChannelBrowserPanel.Visibility = Visibility.Visible;
            PlayerPanel.Visibility = Visibility.Visible;
            ChannelBrowserColumn.Width = new GridLength(layout.ChannelWidth);
            BrowseGapColumn.Width = new GridLength(layout.ContentMargin <= 10 ? 8 : 12);
            PlayerColumn.Width = new GridLength(1, GridUnitType.Star);
        }

        if (layout.Mode is UiLayoutMode.Tiny or UiLayoutMode.Compact)
        {
            PlayerPrimaryCommands.HorizontalAlignment = HorizontalAlignment.Stretch;
            PlayerSecondaryCommands.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        else
        {
            PlayerPrimaryCommands.HorizontalAlignment = HorizontalAlignment.Left;
            PlayerSecondaryCommands.HorizontalAlignment = HorizontalAlignment.Left;
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true, "Inicializando SanchesTV 7...");
            await _db.InitializeAsync();

            var existing = await _db.GetChannelsAsync();
            if (existing.Count == 0)
                await _db.UpsertChannelsAsync(BuiltInCatalog.Create());

            await RefreshChannelsAsync();
            ApplyResponsiveLayout();
            await ShowHomeAsync();
            StatusText.Text = "Pronto";
            TitleStatusText.Text = "Central de TV em Português";
            SetBusy(false);

            await AutoSyncCatalogAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Falha ao iniciar", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Erro na inicialização";
            TitleStatusText.Text = "Erro";
            SetBusy(false);
        }
    }

    private async Task AutoSyncCatalogAsync()
    {
        try
        {
            var raw = await _db.GetSettingAsync("catalog.v5.last_sync_utc");
            if (DateTimeOffset.TryParse(raw, out var last) &&
                DateTimeOffset.UtcNow - last < TimeSpan.FromHours(12))
                return;

            await SyncCatalogAsync(false);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Catálogo: atualização automática indisponível ({ex.Message})";
            SetBusy(false);
        }
    }

    private async Task SyncCatalogAsync(bool showResult)
    {
        if (_catalogSyncRunning)
            return;

        _catalogSyncRunning = true;
        var before = _allChannels.Count;

        try
        {
            SetBusy(true, "Atualizando catálogo e miniaturas...");
            StatusText.Text = $"Sincronizando {PortugueseCatalogRegistry.Sources.Count} fontes automáticas...";
            TitleStatusText.Text = "Atualizando catálogo";

            var service = new PortugueseCatalogSyncService(_http);
            var result = await service.DownloadAsync();

            if (result.DownloadedSources == 0)
                throw new InvalidOperationException("Nenhuma fonte do catálogo pôde ser baixada.");

            await _db.UpsertChannelsAsync(result.Channels);
            await _db.SetSettingAsync("catalog.v5.last_sync_utc", DateTimeOffset.UtcNow.ToString("O"));
            await RefreshChannelsAsync();
            await RefreshHomeListsAsync();

            var added = Math.Max(0, _allChannels.Count - before);
            StatusText.Text =
                $"{result.CandidateChannels:N0} entradas • {added:N0} novos • {_allChannels.Count:N0} canais";
            TitleStatusText.Text = $"{_allChannels.Count:N0} canais • {_allChannels.Sum(c => c.Sources.Count):N0} fontes";

            if (showResult)
            {
                var sourceText = $"{result.DownloadedSources} fontes atualizadas";
                if (result.FailedSources > 0)
                    sourceText += $" • {result.FailedSources} com falha";

                var errors = result.Errors.Count == 0
                    ? string.Empty
                    : "\n\nFalhas:\n" + string.Join("\n", result.Errors);

                MessageBox.Show(
                    this,
                    $"{sourceText}\nEntradas processadas: {result.CandidateChannels:N0}\nNovos canais após deduplicação: {added:N0}\nTotal local: {_allChannels.Count:N0}{errors}",
                    "Catálogo SanchesTV 7",
                    MessageBoxButton.OK,
                    result.FailedSources == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Falha ao atualizar catálogo: {ex.Message}";
            TitleStatusText.Text = "Atualização incompleta";
            if (showResult)
                MessageBox.Show(this, ex.Message, "Catálogo", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _catalogSyncRunning = false;
            SetBusy(false);
        }
    }

    private async Task RefreshChannelsAsync()
    {
        _allChannels = await _db.GetChannelsAsync();
        await ApplyFilterAsync();
    }

    private async Task RefreshHomeListsAsync()
    {
        var recentIds = await _db.GetRecentChannelIdsAsync(14);
        var order = recentIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);

        RecentHomeList.ItemsSource = _allChannels
            .Where(c => order.ContainsKey(c.Id))
            .OrderBy(c => order[c.Id])
            .Take(12)
            .Select(c => new ChannelListItem(c))
            .ToArray();

        BrazilHomeList.ItemsSource = _allChannels
            .Where(IsBrazil)
            .OrderByDescending(c => c.IsFavorite)
            .ThenBy(c => c.Name)
            .Take(12)
            .Select(c => new ChannelListItem(c))
            .ToArray();

        MoviesHomeList.ItemsSource = _allChannels
            .Where(IsMovie)
            .OrderByDescending(c => c.IsFavorite)
            .ThenBy(c => c.Name)
            .Take(12)
            .Select(c => new ChannelListItem(c))
            .ToArray();

        HomeCountText.Text =
            $"{_allChannels.Count:N0} canais • {_allChannels.Sum(c => c.Sources.Count):N0} fontes";
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
        else if (_mode == "br")
            query = query.Where(IsBrazil);
        else if (_mode == "portuguese")
            query = query.Where(IsPortuguese);
        else if (_mode == "movies")
            query = query.Where(IsMovie);

        var normalized = TextNormalizer.Normalize(SearchBox.Text ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            query = query.Where(c =>
                FuzzyMatcher.IsMatch(
                    normalized,
                    c.Name,
                    c.NormalizedName,
                    c.Category,
                    c.Country,
                    c.Language,
                    c.State,
                    c.Region,
                    c.EpgId,
                    string.Join(" ", c.Sources.Select(s => s.Provider))));
        }

        _visibleItems = query.Select(c => new ChannelListItem(c)).ToList();
        ChannelList.ItemsSource = _visibleItems;
        CountText.Text = $"{_visibleItems.Count:N0} canais • {_visibleItems.Sum(x => x.Channel.Sources.Count):N0} fontes";
    }

    private static bool IsBrazil(Channel c) =>
        string.Equals(c.Country, "BR", StringComparison.OrdinalIgnoreCase) ||
        (c.EpgId?.Contains(".br", StringComparison.OrdinalIgnoreCase) ?? false) ||
        c.Sources.Any(s => s.Provider.Contains("Brasil", StringComparison.OrdinalIgnoreCase));

    private static bool IsPortuguese(Channel c)
    {
        if (!string.IsNullOrWhiteSpace(c.Country) && LusophoneCountryCodes.Contains(c.Country))
            return true;

        var language = TextNormalizer.Normalize(c.Language ?? string.Empty);
        return language.Contains("portugu", StringComparison.Ordinal) ||
               c.Sources.Any(s => s.Provider.Contains("Português", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMovie(Channel c)
    {
        var category = TextNormalizer.Normalize(c.Category ?? string.Empty);
        return category.Contains("movie", StringComparison.Ordinal) ||
               category.Contains("filme", StringComparison.Ordinal) ||
               category.Contains("cinema", StringComparison.Ordinal) ||
               c.Sources.Any(s => s.Provider.Contains("Filmes", StringComparison.OrdinalIgnoreCase));
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
            TitleStatusText.Text = $"Abrindo {channel.Name}";

            BrowseView.Visibility = Visibility.Visible;
            HomeView.Visibility = Visibility.Collapsed;

            var ordered = channel.Sources
                .OrderBy(s => s.Status == StreamStatus.Online ? 0 : s.Status == StreamStatus.NotTested ? 1 : 2)
                .ThenBy(s => s.Priority)
                .ToArray();

            ChannelSource active;
            Exception? mpvFailure = null;

            if (await EnsureMpvReadyAsync())
            {
                try
                {
                    await _vlc.StopAsync();
                    SetVideoBackend(PlaybackBackend.Mpv);
                    active = await _mpv.OpenWithFallbackAsync(ordered);
                    _activeBackend = PlaybackBackend.Mpv;
                }
                catch (Exception ex)
                {
                    mpvFailure = ex;
                    await _mpv.StopAsync();
                    SetVideoBackend(PlaybackBackend.Vlc);
                    active = await _vlc.OpenWithFallbackAsync(ordered);
                    _activeBackend = PlaybackBackend.Vlc;
                }
            }
            else
            {
                SetVideoBackend(PlaybackBackend.Vlc);
                active = await _vlc.OpenWithFallbackAsync(ordered);
                _activeBackend = PlaybackBackend.Vlc;
            }

            _activeSource = active;
            _currentChannel = channel;
            await _db.RecordPlayedAsync(channel.Id);

            if (UiLayoutPolicy.Resolve(Math.Max(ActualWidth, 520), Math.Max(ActualHeight, 360)).SinglePane)
            {
                _showCompactChannels = false;
                ApplyResponsiveLayout();
            }

            NowPlayingText.Text = channel.Name;
            PlayerInitialText.Text = GetInitial(channel.Name);
            SourceBadgeText.Text = active.Provider.Length > 34 ? active.Provider[..34] + "…" : active.Provider;
            SetPlayerLogo(channel.Logo);

            var engineLabel = _activeBackend == PlaybackBackend.Mpv ? "libmpv" : "LibVLC";
            StatusText.Text = $"Reproduzindo • {engineLabel} • {active.Url.Host}";
            AppTelemetry.PlaybackStarted(engineLabel, active.Provider, active.Url.Host);
            TitleStatusText.Text = mpvFailure is null
                ? $"{channel.Name} • {engineLabel}"
                : $"{channel.Name} • VLC fallback";
            await UpdateEpgAsync(channel);
            await RefreshHomeListsAsync();
        }
        catch (Exception ex)
        {
            AppTelemetry.PlaybackFailed(_activeBackend.ToString(), ex.Message);
            StatusText.Text = "Nenhuma fonte iniciou";
            TitleStatusText.Text = "Fonte indisponível";
            MessageBox.Show(this, ex.Message, "Falha de reprodução", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<bool> EnsureMpvReadyAsync()
    {
        if (_mpv.IsAvailable)
            return true;

        if (MpvSurface.NativeHandle != IntPtr.Zero)
        {
            try
            {
                _mpv.Initialize(MpvSurface.NativeHandle);
                _mpvReady.TrySetResult(true);
                return true;
            }
            catch
            {
                _mpvReady.TrySetResult(false);
                return false;
            }
        }

        var completed = await Task.WhenAny(_mpvReady.Task, Task.Delay(1800));
        return completed == _mpvReady.Task && await _mpvReady.Task;
    }

    private void SetVideoBackend(PlaybackBackend backend)
    {
        _activeBackend = backend;
        MpvSurface.Visibility = backend == PlaybackBackend.Mpv ? Visibility.Visible : Visibility.Collapsed;
        VideoView.Visibility = backend == PlaybackBackend.Vlc ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetPlayerLogo(string? logo)
    {
        PlayerLogo.Source = null;
        if (string.IsNullOrWhiteSpace(logo))
            return;

        try
        {
            if (Uri.TryCreate(logo, UriKind.Absolute, out var uri))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = uri;
                bitmap.CacheOption = BitmapCacheOption.OnDemand;
                bitmap.CreateOptions = BitmapCreateOptions.DelayCreation;
                bitmap.EndInit();
                PlayerLogo.Source = bitmap;
            }
        }
        catch
        {
            PlayerLogo.Source = null;
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
        if (text is not null)
            LoadingText.Text = text;
    }

    private async Task ShowHomeAsync()
    {
        HomeView.Visibility = Visibility.Visible;
        BrowseView.Visibility = Visibility.Collapsed;
        await RefreshHomeListsAsync();
        TitleStatusText.Text = "Central de TV em Português";
    }

    private async Task ShowBrowseAsync(string mode, string title, string subtitle)
    {
        _mode = mode;
        _showCompactChannels = true;
        HomeView.Visibility = Visibility.Collapsed;
        BrowseView.Visibility = Visibility.Visible;
        BrowseTitleText.Text = title;
        BrowseSubtitleText.Text = subtitle;
        await ApplyFilterAsync();
        TitleStatusText.Text = $"{title} • {_visibleItems.Count:N0}";
    }

    private void CompactMenu_Click(object sender, RoutedEventArgs e)
    {
        if (CompactMenuButton.ContextMenu is not { } menu)
            return;

        menu.PlacementTarget = CompactMenuButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void ToggleCompactPane_Click(object sender, RoutedEventArgs e)
    {
        _showCompactChannels = !_showCompactChannels;
        ApplyResponsiveLayout();
    }

    private async void Home_Click(object sender, RoutedEventArgs e) => await ShowHomeAsync();

    private async void All_Click(object sender, RoutedEventArgs e) =>
        await ShowBrowseAsync("all", "Ao vivo", "Todos os canais disponíveis no catálogo local.");

    private async void Movies_Click(object sender, RoutedEventArgs e) =>
        await ShowBrowseAsync("movies", "Filmes", "Playlist Movies do IPTV-org e canais classificados como cinema/filmes.");

    private async void Brazil_Click(object sender, RoutedEventArgs e) =>
        await ShowBrowseAsync("br", "Brasil", "Canais brasileiros agregados das fontes públicas configuradas.");

    private async void Lusophone_Click(object sender, RoutedEventArgs e) =>
        await ShowBrowseAsync("portuguese", "Português", "Conteúdo classificado em português e países lusófonos.");

    private async void Favorites_Click(object sender, RoutedEventArgs e) =>
        await ShowBrowseAsync("favorites", "Favoritos", "Seus canais marcados como favoritos.");

    private async void Recent_Click(object sender, RoutedEventArgs e) =>
        await ShowBrowseAsync("recent", "Recentes", "Canais assistidos recentemente.");

    private async void MyTv_Click(object sender, RoutedEventArgs e) =>
        await ShowBrowseAsync("mytv", "Minha TV", "Sua seleção pessoal de canais.");

    private void Premium_Click(object sender, RoutedEventArgs e)
    {
        new PremiumHubWindow { Owner = this }.Show();
    }

    private async void SyncPortugueseCatalog_Click(object sender, RoutedEventArgs e) =>
        await SyncCatalogAsync(true);

    private void MediaLab_Click(object sender, RoutedEventArgs e)
    {
        new MediaLabWindow(_mediaTools, _mediaRouter, () => _activeSource, _hardware)
        {
            Owner = this
        }.Show();
    }

    private void EpgGuide_Click(object sender, RoutedEventArgs e)
    {
        var channel = SelectedChannel ?? _currentChannel;
        if (channel is null)
        {
            MessageBox.Show(this, "Selecione ou reproduza um canal primeiro.",
                "Guia de programação", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        new EpgGuideWindow(_db, _recordingScheduler, channel)
        {
            Owner = this
        }.Show();
    }

    private void P2p_Click(object sender, RoutedEventArgs e)
    {
        if (_p2pWindow is { IsVisible: true })
        {
            _p2pWindow.Activate();
            return;
        }

        _p2pWindow = new P2pStreamingWindow(
            _p2p,
            PlayP2pStreamAsync,
            StopP2pPlaybackAsync)
        {
            Owner = this
        };
        _p2pWindow.Closed += (_, _) => _p2pWindow = null;
        _p2pWindow.Show();
    }

    private async Task PlayP2pStreamAsync(Uri uri, string displayName)
    {
        try
        {
            SetBusy(true, "Preparando buffer P2P...");

            var source = new ChannelSource(
                Guid.NewGuid(),
                "P2P",
                uri,
                0);

            ChannelSource active;
            Exception? mpvFailure = null;

            if (await EnsureMpvReadyAsync())
            {
                try
                {
                    await _vlc.StopAsync();
                    SetVideoBackend(PlaybackBackend.Mpv);
                    active = await _mpv.OpenWithFallbackAsync([source]);
                    _activeBackend = PlaybackBackend.Mpv;
                }
                catch (Exception ex)
                {
                    mpvFailure = ex;
                    await _mpv.StopAsync();
                    SetVideoBackend(PlaybackBackend.Vlc);
                    active = await _vlc.OpenWithFallbackAsync([source]);
                    _activeBackend = PlaybackBackend.Vlc;
                }
            }
            else
            {
                SetVideoBackend(PlaybackBackend.Vlc);
                active = await _vlc.OpenWithFallbackAsync([source]);
                _activeBackend = PlaybackBackend.Vlc;
            }

            _activeSource = active;
            _currentChannel = null;
            _showCompactChannels = false;

            HomeView.Visibility = Visibility.Collapsed;
            BrowseView.Visibility = Visibility.Visible;
            BrowseTitleText.Text = "Streaming P2P";
            BrowseSubtitleText.Text = "BitTorrent progressivo com buffer e seek inteligente.";
            PlayerPanel.Visibility = Visibility.Visible;
            ApplyResponsiveLayout();

            NowPlayingText.Text = displayName;
            PlayerInitialText.Text = "P2P";
            SourceBadgeText.Text = "P2P";
            SetPlayerLogo(null);
            NowEpgText.Text = "Reprodução progressiva • cache local • seek por pieces";
            NextEpgText.Text = "Fonte fornecida pelo usuário • HTTP somente em 127.0.0.1";

            var engine = _activeBackend == PlaybackBackend.Mpv ? "libmpv" : "LibVLC";
            StatusText.Text = mpvFailure is null
                ? $"P2P • {engine}"
                : "P2P • LibVLC fallback";
            AppTelemetry.PlaybackStarted(engine, "P2P", "127.0.0.1");
            TitleStatusText.Text = $"{displayName} • P2P";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Streaming P2P", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task StopP2pPlaybackAsync()
    {
        if (!string.Equals(_activeSource?.Provider, "P2P", StringComparison.OrdinalIgnoreCase))
            return;

        if (_activeBackend == PlaybackBackend.Mpv && _mpv.IsAvailable)
            await _mpv.StopAsync();
        else
            await _vlc.StopAsync();

        _activeSource = null;
        StatusText.Text = "P2P parado";
        TitleStatusText.Text = "Streaming P2P encerrado";
    }

    private async void HomeList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox list && list.SelectedItem is ChannelListItem item)
            await PlayChannelAsync(item.Channel);
    }

    private async void ImportFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Importar lista M3U",
            Filter = "Playlists M3U|*.m3u;*.m3u8|Todos os arquivos|*.*"
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            SetBusy(true, "Importando playlist...");
            var text = await File.ReadAllTextAsync(dialog.FileName);
            var channels = M3uParser.Parse(text, Path.GetFileName(dialog.FileName));
            var count = await _db.UpsertChannelsAsync(channels);
            await RefreshChannelsAsync();
            await RefreshHomeListsAsync();
            StatusText.Text = $"{count:N0} entradas processadas";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Erro ao importar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void ImportUrl_Click(object sender, RoutedEventArgs e)
    {
        var input = new InputDialog("Adicionar M3U", "URL da playlist M3U/M3U8:") { Owner = this };
        if (input.ShowDialog() != true || !Uri.TryCreate(input.Value, UriKind.Absolute, out var uri))
            return;

        try
        {
            SetBusy(true, "Baixando playlist...");
            var text = await _http.GetStringAsync(uri);
            var channels = M3uParser.Parse(text, uri.Host);
            var count = await _db.UpsertChannelsAsync(channels);
            await RefreshChannelsAsync();
            await RefreshHomeListsAsync();
            StatusText.Text = $"{count:N0} entradas processadas";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Erro ao importar URL", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void Xtream_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new XtreamDialog { Owner = this };
        if (dialog.ShowDialog() != true)
            return;

        if (!Uri.TryCreate(dialog.Server, UriKind.Absolute, out _))
        {
            MessageBox.Show(this, "Informe um servidor HTTP/HTTPS válido.", "Xtream",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            SetBusy(true, "Importando canais Xtream...");
            var importer = new XtreamImporter(_http);
            var channels = await importer.ImportLiveAsync(dialog.Server, dialog.Username, dialog.Password);
            var count = await _db.UpsertChannelsAsync(channels);
            await RefreshChannelsAsync();
            await RefreshHomeListsAsync();
            StatusText.Text = $"{count:N0} canais Xtream processados";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Falha no Xtream", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void ImportEpg_Click(object sender, RoutedEventArgs e)
    {
        var choose = MessageBox.Show(
            this,
            "Sim: arquivo XMLTV local.\nNão: informar uma URL XMLTV.",
            "Adicionar EPG",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (choose == MessageBoxResult.Cancel)
            return;

        try
        {
            SetBusy(true, "Processando EPG...");
            string xml;

            if (choose == MessageBoxResult.Yes)
            {
                var dialog = new OpenFileDialog { Filter = "XMLTV|*.xml;*.xmltv|Todos os arquivos|*.*" };
                if (dialog.ShowDialog(this) != true)
                    return;
                xml = await File.ReadAllTextAsync(dialog.FileName);
            }
            else
            {
                var input = new InputDialog("EPG XMLTV", "URL XMLTV:") { Owner = this };
                if (input.ShowDialog() != true || !Uri.TryCreate(input.Value, UriKind.Absolute, out var uri))
                    return;
                xml = await _http.GetStringAsync(uri);
            }

            var programs = XmlTvParser.Parse(xml);
            await _db.ReplaceEpgAsync(programs);
            StatusText.Text = $"{programs.Count:N0} programas de EPG importados";

            if (_currentChannel is not null)
                await UpdateEpgAsync(_currentChannel);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Erro no EPG", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void ToggleFavorite_Click(object sender, RoutedEventArgs e)
    {
        var channel = SelectedChannel ?? _currentChannel;
        if (channel is null)
            return;

        await _db.SetFavoriteAsync(channel.Id, !channel.IsFavorite);
        await RefreshChannelsAsync();
        await RefreshHomeListsAsync();
    }

    private async void ToggleMyTv_Click(object sender, RoutedEventArgs e)
    {
        var channel = SelectedChannel ?? _currentChannel;
        if (channel is null)
            return;

        int? position = null;
        if (channel.MyTvPosition is null)
        {
            position = _allChannels
                .Where(c => c.MyTvPosition is not null)
                .Select(c => c.MyTvPosition!.Value)
                .DefaultIfEmpty(0)
                .Max() + 1;
        }

        await _db.SetMyTvPositionAsync(channel.Id, position);
        await RefreshChannelsAsync();
        await RefreshHomeListsAsync();
    }

    private async void HealthCheck_Click(object sender, RoutedEventArgs e)
    {
        var channel = SelectedChannel ?? _currentChannel;
        if (channel is null)
            return;

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

            MessageBox.Show(this, string.Join(Environment.NewLine, messages),
                $"Fontes — {channel.Name}", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void ChannelList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedChannel is { } channel)
            await PlayChannelAsync(channel);
    }

    private async void ChannelList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedChannel is not { } channel)
            return;

        var (now, next) = await _db.GetNowNextAsync(channel.EpgId);
        StatusText.Text = now is null
            ? $"{channel.Name} • {channel.Sources.Count} fonte(s)"
            : $"{channel.Name} • {now.Title}";

        if (next is not null)
            ToolTip = $"A seguir: {next.Title}";
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        await ApplyFilterAsync();
        TitleStatusText.Text = $"{BrowseTitleText.Text} • {_visibleItems.Count:N0}";
    }

    private async void Previous_Click(object sender, RoutedEventArgs e) => await StepChannelAsync(-1);
    private async void Next_Click(object sender, RoutedEventArgs e) => await StepChannelAsync(+1);

    private async Task StepChannelAsync(int delta)
    {
        if (_visibleItems.Count == 0)
            return;

        var current = _currentChannel is null
            ? -1
            : _visibleItems.FindIndex(x => x.Channel.Id == _currentChannel.Id);

        var index = current < 0
            ? 0
            : (current + delta + _visibleItems.Count) % _visibleItems.Count;

        ChannelList.SelectedIndex = index;
        ChannelList.ScrollIntoView(ChannelList.SelectedItem);
        await PlayChannelAsync(_visibleItems[index].Channel);
    }

    private async void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_activeBackend == PlaybackBackend.Mpv && _mpv.IsAvailable)
        {
            if (_mpv.IsPlaying && !_mpv.IsPaused)
                await _mpv.PauseAsync();
            else
                await _mpv.PlayAsync();
            return;
        }

        if (_vlc.MediaPlayer.IsPlaying)
            await _vlc.PauseAsync();
        else
            await _vlc.PlayAsync();
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (_activeBackend == PlaybackBackend.Mpv && _mpv.IsAvailable)
            await _mpv.StopAsync();
        else
            await _vlc.StopAsync();

        StatusText.Text = "Parado";
        TitleStatusText.Text = "Reprodução parada";
    }

    private async void Mute_Click(object sender, RoutedEventArgs e)
    {
        _muted = !_muted;
        if (_mpv.IsAvailable)
            await _mpv.SetMuteAsync(_muted);
        await _vlc.SetMuteAsync(_muted);
        MuteButton.Content = _muted ? "🔇" : "🔊";
    }

    private async void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        if (_mpv.IsAvailable)
            await _mpv.SetVolumeAsync(e.NewValue);
        await _vlc.SetVolumeAsync(e.NewValue);
    }

    private async void Timeshift_Click(object sender, RoutedEventArgs e)
    {
        if (_timeshift.IsRunning)
        {
            try
            {
                SetBusy(true, "Voltando à fonte ao vivo...");
                await _timeshift.StopAsync();

                if (_timeshiftOriginSource is { } origin)
                {
                    _activeSource = origin;
                    if (await EnsureMpvReadyAsync())
                    {
                        await _vlc.StopAsync();
                        SetVideoBackend(PlaybackBackend.Mpv);
                        _activeSource = await _mpv.OpenWithFallbackAsync([origin]);
                        _activeBackend = PlaybackBackend.Mpv;
                    }
                    else
                    {
                        SetVideoBackend(PlaybackBackend.Vlc);
                        _activeSource = await _vlc.OpenWithFallbackAsync([origin]);
                        _activeBackend = PlaybackBackend.Vlc;
                    }
                }

                _timeshiftOriginSource = null;
                StatusText.Text = "Timeshift encerrado • fonte ao vivo";
                TimeshiftButton.Content = "⏱ Timeshift";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Timeshift",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                SetBusy(false);
            }
            return;
        }

        if (_activeSource is null)
        {
            StatusText.Text = "Abra um canal antes de iniciar o timeshift.";
            return;
        }

        if (string.Equals(_activeSource.Provider, "P2P", StringComparison.OrdinalIgnoreCase))
        {
            StatusText.Text = "P2P já possui seek próprio; timeshift adicional não é necessário.";
            return;
        }

        try
        {
            SetBusy(true, "Criando buffer de timeshift...");
            _timeshiftOriginSource = _activeSource;
            var uri = await _timeshift.StartAsync(_activeSource);
            var local = new ChannelSource(
                Guid.NewGuid(),
                "Timeshift",
                uri,
                0);

            if (await EnsureMpvReadyAsync())
            {
                await _vlc.StopAsync();
                SetVideoBackend(PlaybackBackend.Mpv);
                _activeSource = await _mpv.OpenWithFallbackAsync([local]);
                _activeBackend = PlaybackBackend.Mpv;
            }
            else
            {
                SetVideoBackend(PlaybackBackend.Vlc);
                _activeSource = await _vlc.OpenWithFallbackAsync([local]);
                _activeBackend = PlaybackBackend.Vlc;
            }

            StatusText.Text = "Timeshift ativo • pause, volte e use LIVE para retornar ao ponto mais recente";
            TimeshiftButton.Content = "⏱ Sair do timeshift";
        }
        catch (Exception ex)
        {
            _timeshiftOriginSource = null;
            await _timeshift.StopAsync();
            MessageBox.Show(this, ex.Message, "Timeshift",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void Rewind_Click(object sender, RoutedEventArgs e)
    {
        if (_activeBackend == PlaybackBackend.Mpv && _mpv.IsAvailable)
        {
            if (!_mpv.IsSeekable)
            {
                StatusText.Text = "Esta fonte não oferece timeshift/seek.";
                return;
            }

            await _mpv.SeekRelativeAsync(-30);
            return;
        }

        if (!_vlc.MediaPlayer.IsSeekable)
        {
            StatusText.Text = "Esta fonte não oferece timeshift/seek.";
            return;
        }

        _vlc.MediaPlayer.Time = Math.Max(0, _vlc.MediaPlayer.Time - 30_000);
    }

    private async void Live_Click(object sender, RoutedEventArgs e)
    {
        if (_activeBackend == PlaybackBackend.Mpv && _mpv.IsAvailable)
        {
            if (!_mpv.IsSeekable)
            {
                StatusText.Text = "Esta fonte não oferece retorno ao vivo por seek.";
                return;
            }

            await _mpv.GoLiveAsync();
            return;
        }

        if (!_vlc.MediaPlayer.IsSeekable)
        {
            StatusText.Text = "Esta fonte não oferece retorno ao vivo por seek.";
            return;
        }

        _vlc.MediaPlayer.Position = 1f;
        _vlc.MediaPlayer.SetPause(false);
    }

    private void Record_Click(object sender, RoutedEventArgs e)
    {
        if (_activeSource is null)
        {
            StatusText.Text = "Selecione um canal antes de gravar.";
            return;
        }

        if (!_isRecording)
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                "SanchesTV");

            if (_recorder.Start(_activeSource, directory))
            {
                _isRecording = true;
                RecordButton.Content = "■ STOP REC";
                StatusText.Text = $"FFmpeg gravando/remuxando em {directory}";
            }
            else
            {
                StatusText.Text = "FFmpeg não pôde iniciar a gravação.";
            }
        }
        else
        {
            _recorder.Stop();
            _isRecording = false;
            RecordButton.Content = "● REC";
            StatusText.Text = _recorder.OutputPath is null
                ? "Gravação encerrada"
                : $"Gravação salva: {_recorder.OutputPath}";
        }
    }

    private void Pip_Click(object sender, RoutedEventArgs e)
    {
        if (_activeSource is null)
            return;

        new PipWindow(_activeSource) { Owner = this }.Show();
    }

    private void Multiview_Click(object sender, RoutedEventArgs e)
    {
        var candidates = _visibleItems
            .Select(x => x.Channel)
            .Where(c => c.Sources.Count > 0)
            .OrderByDescending(c => _currentChannel is not null && c.Id == _currentChannel.Id)
            .Take(4)
            .Select(c => c.Sources.OrderBy(s => s.Priority).First())
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

    private void AudioVideo_Click(object sender, RoutedEventArgs e)
    {
        new AudioVideoSettingsWindow(_mpv, _audio, _hardware) { Owner = this }.ShowDialog();
    }

    private void Diagnostics_Click(object sender, RoutedEventArgs e)
    {
        var source = _activeSource?.Url.ToString() ?? "(nenhuma)";
        if (source.Contains('@'))
        {
            try
            {
                var u = new Uri(source);
                source = $"{u.Scheme}://***@{u.Host}{u.AbsolutePath}";
            }
            catch
            {
                source = "(fonte protegida)";
            }
        }

        string engineDetails;
        if (_activeBackend == PlaybackBackend.Mpv && _mpv.IsAvailable)
        {
            engineDetails = _mpv.GetDiagnostics();
        }
        else
        {
            var mp = _vlc.MediaPlayer;
            engineDetails =
                $"Engine: LibVLC fallback\n" +
                $"Estado: {mp.State}\n" +
                $"FPS: {mp.Fps:N2}\n" +
                $"Volume: {mp.Volume}%\n" +
                $"Seek: {(mp.IsSeekable ? "sim" : "não")}";
        }

        var ffmpeg = _recorder.IsAvailable ? "disponível" : "ausente";
        var text =
            $"SanchesTV: 7.0.0\n" +
            $"Pipeline: libmpv → LibVLC fallback\n" +
            $"Fonte: {source}\n" +
            $"Provider: {_activeSource?.Provider ?? "(nenhum)"}\n\n" +
            $"{engineDetails}\n\n" +
            $"{_audio.GetSummary()}\n" +
            $"FFmpeg recorder: {ffmpeg}\n" +
            $"P2P: {_p2p.GetStats().State} • peers {_p2p.GetStats().Peers} • cache {_p2p.GetStats().CacheText}\n" +
            $"Catálogo local: {_allChannels.Count:N0} canais\n" +
            $"Fontes automáticas: {PortugueseCatalogRegistry.Sources.Count}\n" +
            $"Banco: {_db.DatabasePath}";

        MessageBox.Show(this, text, "Diagnóstico SanchesTV 7", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Fullscreen_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

    private void VideoSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2)
            return;

        ToggleFullscreen();
        e.Handled = true;
    }

    private void ToggleFullscreen()
    {
        if (!_isFullscreen)
        {
            _isFullscreen = true;
            _windowStateBeforeFullscreen = WindowState;

            if (_currentChannel is not null)
            {
                HomeView.Visibility = Visibility.Collapsed;
                BrowseView.Visibility = Visibility.Visible;
            }

            TitleBar.Visibility = Visibility.Collapsed;
            TitleBarRow.Height = new GridLength(0);
            NavigationPanel.Visibility = Visibility.Collapsed;
            NavigationColumn.Width = new GridLength(0);

            BrowseHeader.Visibility = Visibility.Collapsed;
            ChannelBrowserPanel.Visibility = Visibility.Collapsed;
            ChannelBrowserColumn.Width = new GridLength(0);
            BrowseGapColumn.Width = new GridLength(0);
            PlayerPanel.Visibility = Visibility.Visible;
            PlayerPanel.BorderThickness = new Thickness(0);
            PlayerPanel.CornerRadius = new CornerRadius(0);
            PlayerColumn.Width = new GridLength(1, GridUnitType.Star);
            BrowseView.Margin = new Thickness(0);

            PlayerSecondaryCommands.Visibility = Visibility.Collapsed;
            FullscreenButton.Content = "↙  Sair da tela cheia  F11";
            FullscreenButton.ToolTip = "Sair da tela cheia — F11, Esc ou duplo clique";

            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            WindowState = WindowState.Maximized;
            TitleStatusText.Text = "Tela cheia • F11 / Esc / duplo clique para sair";
        }
        else
        {
            _isFullscreen = false;

            Topmost = false;
            ResizeMode = ResizeMode.CanResize;
            TitleBar.Visibility = Visibility.Visible;
            TitleBarRow.Height = new GridLength(52);
            NavigationPanel.Visibility = Visibility.Visible;

            BrowseHeader.Visibility = Visibility.Visible;
            ChannelBrowserPanel.Visibility = Visibility.Visible;
            PlayerPanel.BorderThickness = new Thickness(1);
            PlayerPanel.CornerRadius = new CornerRadius(16);
            PlayerSecondaryCommands.Visibility = Visibility.Visible;
            BrowseView.Margin = new Thickness(24, 20, 24, 20);

            FullscreenButton.Content = "⛶  Tela cheia  F11";
            FullscreenButton.ToolTip = "Tela cheia — F11 ou duplo clique no vídeo";

            WindowState = _windowStateBeforeFullscreen == WindowState.Minimized
                ? WindowState.Normal
                : _windowStateBeforeFullscreen;

            ApplyResponsiveLayout();
            TitleStatusText.Text = _currentChannel?.Name ?? "Central de TV em Português";
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _isFullscreen)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
        else if (e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
        else if (e.Key == Key.Space && BrowseView.Visibility == Visibility.Visible)
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
        if (_remote is not null)
            await _remote.DisposeAsync();

        _recorder.Dispose();
        await _timeshift.DisposeAsync();
        _hardware.Dispose();
        await _mediaRouter.DisposeAsync();
        await _recordingScheduler.DisposeAsync();
        await _p2p.DisposeAsync();
        await _mpv.DisposeAsync();
        await _vlc.DisposeAsync();
        _http.Dispose();
    }

    private static string GetInitial(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "TV";

        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2)
            return string.Concat(words[0][0], words[1][0]).ToUpperInvariant();

        return name.Length >= 2 ? name[..2].ToUpperInvariant() : name.ToUpperInvariant();
    }

    private enum PlaybackBackend
    {
        Mpv,
        Vlc
    }

    private sealed class ChannelListItem
    {
        public Channel Channel { get; }
        public string Name => Channel.Name;
        public string? Logo => Channel.Logo;
        public string Initial => GetInitial(Channel.Name);
        public string Details => string.Join(
            " • ",
            new[] { Channel.Country, Channel.Category }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
        public string SourceInfo =>
            $"{Channel.Sources.Count} fonte(s)" +
            (Channel.Sources.FirstOrDefault() is { } source ? $" • {source.Provider}" : string.Empty);
        public string FavoriteMark => Channel.IsFavorite ? "★" : string.Empty;

        public ChannelListItem(Channel channel) => Channel = channel;
    }
}
