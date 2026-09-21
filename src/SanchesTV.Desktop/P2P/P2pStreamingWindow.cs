using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace SanchesTV.Desktop.P2P;

public sealed class P2pStreamingWindow : Window
{
    private readonly P2pStreamingService _service;
    private readonly TextBox _magnet = new() { MinHeight = 80, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
    private readonly ListBox _files = new();
    private readonly TextBlock _status = new();
    private P2pSession? _session;

    public event EventHandler<Uri>? PlayRequested;

    public P2pStreamingWindow(P2pStreamingService service)
    {
        _service = service;
        Title = "SanchesTV — Streaming P2P";
        Width = 760;
        Height = 620;
        MinWidth = 560;
        MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.Black;
        Foreground = System.Windows.Media.Brushes.White;

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel();
        header.Children.Add(new TextBlock { Text = "Streaming P2P", FontSize = 26, FontWeight = FontWeights.Bold });
        header.Children.Add(new TextBlock
        {
            Text = "Cole um magnet ou abra um .torrent de conteúdo que você tenha direito de acessar. O vídeo começa antes do download terminar.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = System.Windows.Media.Brushes.LightGray,
            Margin = new Thickness(0, 6, 0, 14)
        });
        header.Children.Add(_magnet);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 10) };
        actions.Children.Add(MakeButton("Adicionar magnet", AddMagnet_Click));
        actions.Children.Add(MakeButton("Abrir .torrent", OpenTorrent_Click));
        actions.Children.Add(MakeButton("Atualizar", Refresh_Click));
        Grid.SetRow(actions, 1);

        _files.DisplayMemberPath = "Label";
        _files.MouseDoubleClick += (_, _) => PlaySelected();

        var filePanel = new Grid();
        filePanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        filePanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        filePanel.Children.Add(new TextBlock { Text = "Arquivos de vídeo", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        Grid.SetRow(_files, 1);
        filePanel.Children.Add(_files);
        Grid.SetRow(filePanel, 2);

        var footer = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _status.Text = "Motor P2P parado. Ele inicia somente quando necessário.";
        _status.TextWrapping = TextWrapping.Wrap;
        _status.Foreground = System.Windows.Media.Brushes.LightGray;
        footer.Children.Add(_status);
        var play = MakeButton("▶ Assistir agora", (_, _) => PlaySelected());
        play.MinWidth = 150;
        Grid.SetColumn(play, 1);
        footer.Children.Add(play);
        Grid.SetRow(footer, 3);

        root.Children.Add(header);
        root.Children.Add(actions);
        root.Children.Add(filePanel);
        root.Children.Add(footer);
        Content = root;
    }

    private static Button MakeButton(string text, RoutedEventHandler click)
    {
        var b = new Button { Content = text, Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0) };
        b.Click += click;
        return b;
    }

    private async void AddMagnet_Click(object sender, RoutedEventArgs e)
    {
        var value = _magnet.Text.Trim();
        if (string.IsNullOrWhiteSpace(value))
            return;

        try
        {
            SetBusy("Conectando ao swarm e obtendo metadados...");
            _session = await _service.AddMagnetAsync(value);
            RenderFiles();
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            MessageBox.Show(this, ex.Message, "Streaming P2P", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OpenTorrent_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Torrent BitTorrent|*.torrent" };
        if (dialog.ShowDialog(this) != true)
            return;

        // O servidor aceita o metainfo em hexadecimal. Isso mantém o arquivo local:
        // nenhum indexador ou serviço de busca externo é consultado pelo SanchesTV.
        try
        {
            SetBusy("Abrindo .torrent local...");
            var bytes = await File.ReadAllBytesAsync(dialog.FileName);
            await _service.EnsureStartedAsync();

            using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:11470/"), Timeout = TimeSpan.FromSeconds(45) };
            using var response = await http.PostAsJsonAsync("create", new
            {
                torrent = Convert.ToHexString(bytes).ToLowerInvariant(),
                guessFileIdx = true,
                fileMustInclude = Array.Empty<string>()
            });
            response.EnsureSuccessStatusCode();
            using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("error", out var err))
                throw new InvalidOperationException(err.GetString());

            var hash = doc.RootElement.GetProperty("infoHash").GetString()
                ?? throw new InvalidOperationException("Torrent sem info hash.");
            _session = await _service.RefreshAsync(hash);
            RenderFiles();
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null)
            return;
        try
        {
            _session = await _service.RefreshAsync(_session.InfoHash);
            RenderFiles();
        }
        catch (Exception ex) { _status.Text = ex.Message; }
    }

    private void RenderFiles()
    {
        if (_session is null)
            return;

        var videos = _session.Files.Where(P2pStreamingService.IsVideo)
            .OrderByDescending(f => f.Length)
            .Select(f => new FileItem(f, $"{f.Name}  •  {f.DisplaySize}  •  {f.Progress:P0}"))
            .ToList();

        _files.ItemsSource = videos;
        if (videos.Count > 0)
            _files.SelectedIndex = 0;

        _status.Text = $"{_session.Name} • {_session.Peers} peers • {_session.DownloadSpeed / 1_048_576d:N1} MB/s • {videos.Count} vídeo(s)";
    }

    private void PlaySelected()
    {
        if (_session is null || _files.SelectedItem is not FileItem item)
            return;

        PlayRequested?.Invoke(this, _service.GetStreamUri(_session.InfoHash, item.File.Index));
        _status.Text = $"Transmitindo: {item.File.Name}";
    }

    private void SetBusy(string text)
    {
        _status.Text = text;
        _files.ItemsSource = null;
    }

    private sealed record FileItem(P2pFile File, string Label);
}
