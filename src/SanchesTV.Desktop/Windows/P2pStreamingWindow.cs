using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using SanchesTV.Desktop.P2P;

namespace SanchesTV.Desktop.Windows;

public sealed class P2pStreamingWindow : Window
{
    private readonly P2pStreamingService _service;
    private readonly Func<Uri, string, Task> _playStream;
    private readonly Func<Task> _stopPlayback;

    private readonly TextBox _magnet = new();
    private readonly ListView _files = new();
    private readonly TextBlock _status = new();
    private readonly TextBlock _stats = new();
    private readonly TextBlock _cache = new();
    private readonly Button _play = new();
    private readonly CheckBox _keepCache = new();
    private readonly CheckBox _portForwarding = new();
    private readonly CheckBox _prebuffer = new();
    private readonly ComboBox _cacheLimit = new();
    private readonly ComboBox _bufferSize = new();
    private readonly ComboBox _stallRecovery = new();
    private readonly TextBox _uploadLimit = new();
    private readonly CheckBox _autoRecovery = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool _maintenanceRunning;

    public P2pStreamingWindow(
        P2pStreamingService service,
        Func<Uri, string, Task> playStream,
        Func<Task> stopPlayback)
    {
        _service = service;
        _playStream = playStream;
        _stopPlayback = stopPlayback;

        Title = "SanchesTV — P2P Streaming";
        Width = 920;
        Height = 720;
        MinWidth = 720;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.Black;
        Foreground = System.Windows.Media.Brushes.White;
        AllowDrop = true;

        Content = BuildUi();
        ApplySettingsToUi();

        Loaded += (_, _) =>
        {
            RefreshFiles();
            RefreshStats();
            _timer.Start();
        };

        Closed += (_, _) => _timer.Stop();
        DragOver += OnDragOver;
        Drop += OnDrop;
        _timer.Tick += MaintenanceTick;
    }

    private UIElement BuildUi()
    {
        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var title = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        title.Children.Add(new TextBlock
        {
            Text = "P2P Streaming",
            FontSize = 28,
            FontWeight = FontWeights.Bold
        });
        title.Children.Add(new TextBlock
        {
            Text = "Transmita magnet links e arquivos .torrent fornecidos por você sem esperar o download completo.",
            Foreground = Brush("#A9B4C6"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        });
        title.Children.Add(new TextBlock
        {
            Text = "Use apenas conteúdo que você tem direito de acessar ou distribuir. O SanchesTV não pesquisa indexadores de torrents.",
            Foreground = Brush("#E9BD74"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        });
        Grid.SetRow(title, 0);
        root.Children.Add(title);

        var sourcePanel = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        sourcePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sourcePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        sourcePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _magnet.MinHeight = 66;
        _magnet.AcceptsReturn = true;
        _magnet.TextWrapping = TextWrapping.Wrap;
        _magnet.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _magnet.ToolTip = "Cole um magnet link aqui";
        _magnet.Background = Brush("#141922");
        _magnet.Foreground = System.Windows.Media.Brushes.White;
        _magnet.BorderBrush = Brush("#374257");
        _magnet.Padding = new Thickness(10);
        Grid.SetColumn(_magnet, 0);
        sourcePanel.Children.Add(_magnet);

        var loadMagnet = MakeButton("Carregar magnet", LoadMagnet_Click);
        loadMagnet.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(loadMagnet, 1);
        sourcePanel.Children.Add(loadMagnet);

        var openTorrent = MakeButton("Abrir .torrent", OpenTorrent_Click);
        openTorrent.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(openTorrent, 2);
        sourcePanel.Children.Add(openTorrent);

        Grid.SetRow(sourcePanel, 1);
        root.Children.Add(sourcePanel);

        var settings = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };

        _keepCache.Content = "Manter cache para continuar ao abrir o mesmo torrent";
        _keepCache.Margin = new Thickness(0, 0, 14, 8);
        settings.Children.Add(_keepCache);

        _prebuffer.Content = "Pré-buffer antes de reproduzir";
        _prebuffer.Margin = new Thickness(0, 0, 14, 8);
        settings.Children.Add(_prebuffer);

        _portForwarding.Content = "UPnP/NAT-PMP para melhorar conectividade";
        _portForwarding.Margin = new Thickness(0, 0, 14, 8);
        settings.Children.Add(_portForwarding);

        settings.Children.Add(MakeLabel("Cache máximo:"));
        _cacheLimit.ItemsSource = new[] { 5, 10, 25, 50, 100 };
        _cacheLimit.Width = 72;
        _cacheLimit.Margin = new Thickness(5, 0, 10, 8);
        settings.Children.Add(_cacheLimit);
        settings.Children.Add(MakeLabel("GB"));

        settings.Children.Add(MakeLabel("Buffer inicial:"));
        _bufferSize.ItemsSource = new[] { 8, 16, 24, 32, 64, 96 };
        _bufferSize.Width = 72;
        _bufferSize.Margin = new Thickness(5, 0, 5, 8);
        settings.Children.Add(_bufferSize);
        settings.Children.Add(MakeLabel("MB"));

        settings.Children.Add(MakeLabel("Recuperar após:"));
        _stallRecovery.ItemsSource = new[] { 8, 12, 20, 30, 45 };
        _stallRecovery.Width = 66;
        _stallRecovery.Margin = new Thickness(5, 0, 5, 8);
        settings.Children.Add(_stallRecovery);
        settings.Children.Add(MakeLabel("s"));

        _autoRecovery.Content = "Recuperação automática";
        _autoRecovery.Margin = new Thickness(10, 0, 14, 8);
        settings.Children.Add(_autoRecovery);

        settings.Children.Add(MakeLabel("Upload:"));
        _uploadLimit.Width = 76;
        _uploadLimit.Margin = new Thickness(5, 0, 5, 8);
        settings.Children.Add(_uploadLimit);
        settings.Children.Add(MakeLabel("KiB/s (0 = sem limite)"));

        var applySettings = MakeButton("Aplicar", ApplySettings_Click);
        applySettings.Margin = new Thickness(10, 0, 0, 8);
        settings.Children.Add(applySettings);

        Grid.SetRow(settings, 2);
        root.Children.Add(settings);

        _files.Background = Brush("#10141C");
        _files.Foreground = System.Windows.Media.Brushes.White;
        _files.BorderBrush = Brush("#2A3447");
        _files.SelectionMode = SelectionMode.Single;
        _files.MouseDoubleClick += async (_, _) => await PlaySelectedAsync();

        var view = new GridView();
        view.Columns.Add(new GridViewColumn
        {
            Header = "Arquivo",
            Width = 470,
            DisplayMemberBinding = new Binding(nameof(P2pFileItem.Path))
        });
        view.Columns.Add(new GridViewColumn
        {
            Header = "Tipo",
            Width = 80,
            DisplayMemberBinding = new Binding(nameof(P2pFileItem.TypeText))
        });
        view.Columns.Add(new GridViewColumn
        {
            Header = "Tamanho",
            Width = 105,
            DisplayMemberBinding = new Binding(nameof(P2pFileItem.SizeText))
        });
        view.Columns.Add(new GridViewColumn
        {
            Header = "Pronto",
            Width = 80,
            DisplayMemberBinding = new Binding(nameof(P2pFileItem.ProgressText))
        });
        _files.View = view;

        Grid.SetRow(_files, 3);
        root.Children.Add(_files);

        var actions = new DockPanel { Margin = new Thickness(0, 12, 0, 8) };

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        _play.Content = "▶ Assistir agora";
        _play.MinWidth = 140;
        _play.Padding = new Thickness(14, 9, 14, 9);
        _play.Click += async (_, _) => await PlaySelectedAsync();
        right.Children.Add(_play);

        var stop = MakeButton("■ Parar P2P", Stop_Click);
        stop.Margin = new Thickness(8, 0, 0, 0);
        right.Children.Add(stop);

        var recover = MakeButton("↻ Recuperar swarm", Recover_Click);
        recover.Margin = new Thickness(8, 0, 0, 0);
        right.Children.Add(recover);

        var clear = MakeButton("Limpar cache antigo", ClearCache_Click);
        clear.Margin = new Thickness(8, 0, 0, 0);
        right.Children.Add(clear);

        DockPanel.SetDock(right, Dock.Right);
        actions.Children.Add(right);

        var left = new StackPanel();
        _status.Text = "Cole um magnet ou abra um .torrent.";
        _status.Foreground = Brush("#AEB8C8");
        left.Children.Add(_status);

        _cache.Foreground = Brush("#7E8DA4");
        _cache.Margin = new Thickness(0, 3, 0, 0);
        left.Children.Add(_cache);
        actions.Children.Add(left);

        Grid.SetRow(actions, 4);
        root.Children.Add(actions);

        _stats.Foreground = Brush("#8998AD");
        _stats.TextWrapping = TextWrapping.Wrap;
        Grid.SetRow(_stats, 5);
        root.Children.Add(_stats);

        return root;
    }

    private async void LoadMagnet_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_magnet.Text))
        {
            _status.Text = "Cole um magnet link primeiro.";
            return;
        }

        await LoadAsync(() => _service.LoadMagnetAsync(_magnet.Text));
    }

    private async void OpenTorrent_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Abrir arquivo .torrent",
            Filter = "Torrent|*.torrent|Todos os arquivos|*.*"
        };

        if (dialog.ShowDialog(this) == true)
            await LoadAsync(() => _service.LoadTorrentFileAsync(dialog.FileName));
    }

    private async Task LoadAsync(Func<Task<IReadOnlyList<P2pFileItem>>> load)
    {
        try
        {
            SetBusy(true, "Obtendo metadados e procurando peers...");
            await load();
            RefreshFiles();

            var preferred = _service.Files.FirstOrDefault(x => x.IsVideo)
                ?? _service.Files.FirstOrDefault();

            if (preferred is not null)
                _files.SelectedItem = preferred;

            _status.Text = _service.Files.Count.ToString("N0") +
                " arquivo(s) encontrados • selecione um vídeo para assistir.";
        }
        catch (Exception ex)
        {
            _status.Text = "Falha: " + ex.Message;
            MessageBox.Show(this, ex.Message, "P2P Streaming", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task PlaySelectedAsync()
    {
        if (_files.SelectedItem is not P2pFileItem file)
        {
            _status.Text = "Selecione um arquivo.";
            return;
        }

        try
        {
            SetBusy(true, "Preparando buffer inicial...");
            var uri = await _service.StartStreamAsync(file);
            await _playStream(uri, file.Name);
            _status.Text = "Reproduzindo por P2P: " + file.Name;
        }
        catch (Exception ex)
        {
            _status.Text = "Não foi possível iniciar: " + ex.Message;
            MessageBox.Show(this, ex.Message, "P2P Streaming", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true, "Encerrando sessão P2P...");
            await _stopPlayback();
            await _service.StopSessionAsync();
            RefreshFiles();
            _status.Text = "Sessão P2P encerrada.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true, "Limpando sessões antigas...");
            await _service.ClearInactiveCacheAsync();
            _status.Text = "Cache inativo limpo.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void ApplySettings_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(_uploadLimit.Text, out var upload))
            upload = 512;

        var settings = new P2pSettings
        {
            KeepDownloadedData = _keepCache.IsChecked == true,
            PrebufferBeforePlay = _prebuffer.IsChecked != false,
            AllowPortForwarding = _portForwarding.IsChecked == true,
            MaxCacheGb = _cacheLimit.SelectedItem is int gb ? gb : 10,
            InitialBufferMb = _bufferSize.SelectedItem is int buffer ? buffer : 24,
            StallRecoverySeconds = _stallRecovery.SelectedItem is int stall ? stall : 12,
            AutoRecovery = _autoRecovery.IsChecked != false,
            MetadataTimeoutSeconds = 60,
            StartBufferTimeoutSeconds = 120,
            MaxUploadKibPerSecond = Math.Clamp(upload, 0, 1024 * 1024)
        };

        try
        {
            await _service.ApplySettingsAsync(settings);
            _status.Text = "Configurações P2P salvas.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Configurações P2P", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ApplySettingsToUi()
    {
        _keepCache.IsChecked = _service.Settings.KeepDownloadedData;
        _prebuffer.IsChecked = _service.Settings.PrebufferBeforePlay;
        _portForwarding.IsChecked = _service.Settings.AllowPortForwarding;

        var options = new[] { 5, 10, 25, 50, 100 };
        _cacheLimit.SelectedItem = options.Contains(_service.Settings.MaxCacheGb)
            ? _service.Settings.MaxCacheGb
            : 10;

        var bufferOptions = new[] { 8, 16, 24, 32, 64, 96 };
        _bufferSize.SelectedItem = bufferOptions.Contains(_service.Settings.InitialBufferMb)
            ? _service.Settings.InitialBufferMb
            : 24;

        var stallOptions = new[] { 8, 12, 20, 30, 45 };
        _stallRecovery.SelectedItem = stallOptions.Contains(_service.Settings.StallRecoverySeconds)
            ? _service.Settings.StallRecoverySeconds
            : 12;

        _autoRecovery.IsChecked = _service.Settings.AutoRecovery;
        _uploadLimit.Text = _service.Settings.MaxUploadKibPerSecond.ToString();
    }

    private void RefreshFiles()
    {
        _files.ItemsSource = null;
        _files.ItemsSource = _service.Files;
        _play.IsEnabled = _service.Files.Count > 0;
    }

    private void RefreshStats()
    {
        var s = _service.GetStats();
        _stats.Text = _service.HasActiveSession
            ? "Saúde: " + s.Health +
              " • Estado: " + s.State +
              " • Peers: " + s.Peers +
              " • ↓ " + s.DownloadRateText +
              " • ↑ " + s.UploadRateText +
              " • Recebido: " + s.DownloadedText +
              " • arquivo: " + s.SelectedProgress.ToString("0.0") + "%" +
              " • recuperações: " + s.RecoveryCount +
              (s.LastRecovery == "nenhuma" ? "" : " • última: " + s.LastRecovery)
            : "P2P parado.";

        _cache.Text = "Cache: " + s.CacheText +
            " / limite " + _service.Settings.MaxCacheGb + " GB • " + _service.CacheRoot;
    }

    private async void MaintenanceTick(object? sender, EventArgs e)
    {
        if (_maintenanceRunning)
            return;

        _maintenanceRunning = true;
        try
        {
            await _service.MaintainAsync();
            RefreshStats();
        }
        catch
        {
            // O watchdog nunca deve derrubar a interface.
        }
        finally
        {
            _maintenanceRunning = false;
        }
    }

    private async void Recover_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true, "Forçando nova descoberta de peers...");
            await _service.RecoverNowAsync();
            RefreshStats();
            _status.Text = "Recuperação solicitada: tracker + DHT + prioridade do vídeo.";
        }
        catch (Exception ex)
        {
            _status.Text = "Recuperação falhou: " + ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy, string? text = null)
    {
        if (text is not null)
            _status.Text = text;
        Cursor = busy ? Cursors.Wait : Cursors.Arrow;
        _play.IsEnabled = !busy && _service.Files.Count > 0;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
            return;

        var torrent = files.FirstOrDefault(x =>
            string.Equals(System.IO.Path.GetExtension(x), ".torrent", StringComparison.OrdinalIgnoreCase));

        if (torrent is not null)
            await LoadAsync(() => _service.LoadTorrentFileAsync(torrent));
    }

    private static Button MakeButton(string text, RoutedEventHandler handler)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 108,
            Padding = new Thickness(12, 8, 12, 8)
        };
        button.Click += handler;
        return button;
    }

    private static TextBlock MakeLabel(string text) => new()
    {
        Text = text,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(6, 0, 0, 8),
        Foreground = Brush("#AEB8C8")
    };

    private static System.Windows.Media.Brush Brush(string hex) =>
        (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!;
}
