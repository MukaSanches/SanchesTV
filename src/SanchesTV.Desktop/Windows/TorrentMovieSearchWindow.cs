using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using SanchesTV.Core.Search;
using SanchesTV.Desktop.P2P;

namespace SanchesTV.Desktop.Windows;

/// <summary>Separate discovery surface; leaves the existing P2P player and home UI intact.</summary>
public sealed class TorrentMovieSearchWindow : Window
{
    private readonly TorrentMovieSearchService _search = new();
    private readonly Func<string, Task> _loadMagnet;
    private readonly Func<string, Task> _loadTorrentFile;

    private readonly TextBox _query = new();
    private readonly ListView _results = new();
    private readonly TextBlock _status = new();
    private readonly TextBlock _warning = new();
    private readonly Button _searchButton = new();
    private readonly Button _openButton = new();
    private CancellationTokenSource? _searchCts;
    private bool _opening;

    public TorrentMovieSearchWindow(
        Func<string, Task> loadMagnet,
        Func<string, Task> loadTorrentFile)
    {
        _loadMagnet = loadMagnet;
        _loadTorrentFile = loadTorrentFile;

        Title = "SanchesTV — Buscar filmes por torrent";
        Width = 1030;
        Height = 680;
        MinWidth = 780;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("#0C1018");
        Foreground = Brushes.White;
        Content = BuildUi();

        Loaded += (_, _) => _query.Focus();
        Closed += (_, _) =>
        {
            _searchCts?.Cancel();
            _search.Dispose();
        };
    }

    private UIElement BuildUi()
    {
        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        heading.Children.Add(new TextBlock
        {
            Text = "Descobrir filmes • BitTorrent",
            FontSize = 27,
            FontWeight = FontWeights.Bold
        });
        heading.Children.Add(new TextBlock
        {
            Text = "Pesquise por título. Fontes: Internet Archive e provedores Torznab configurados por você.",
            Foreground = Brush("#ACBDCF"),
            Margin = new Thickness(0, 6, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
        heading.Children.Add(new TextBlock
        {
            Text = "Somente obras de domínio público, licenciadas ou que você tenha autorização para acessar. A disponibilidade depende das fontes e dos peers.",
            Foreground = Brush("#DDBE82"),
            Margin = new Thickness(0, 6, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetRow(heading, 0);
        root.Children.Add(heading);

        var searchBar = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 14) };

        var configure = MakeButton("Configurar fontes", (_, _) => OpenConfig());
        configure.Margin = new Thickness(8, 0, 0, 0);
        DockPanel.SetDock(configure, Dock.Right);
        searchBar.Children.Add(configure);

        _searchButton.Content = "Pesquisar";
        _searchButton.Padding = new Thickness(22, 10, 22, 10);
        _searchButton.Background = Brush("#3364D5");
        _searchButton.Foreground = Brushes.White;
        _searchButton.BorderThickness = new Thickness(0);
        _searchButton.Click += async (_, _) => await SearchAsync();
        DockPanel.SetDock(_searchButton, Dock.Right);
        searchBar.Children.Add(_searchButton);

        _query.FontSize = 16;
        _query.MinHeight = 43;
        _query.Padding = new Thickness(11, 8, 11, 8);
        _query.ToolTip = "Ex.: Night of the Living Dead";
        _query.Background = Brush("#161E2D");
        _query.Foreground = Brushes.White;
        _query.BorderBrush = Brush("#39445A");
        _query.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await SearchAsync();
            }
        };
        searchBar.Children.Add(_query);
        Grid.SetRow(searchBar, 1);
        root.Children.Add(searchBar);

        _results.Background = Brush("#111823");
        _results.Foreground = Brushes.White;
        _results.BorderBrush = Brush("#303C50");
        _results.SelectionMode = SelectionMode.Single;
        _results.SelectionChanged += (_, _) => _openButton.IsEnabled =
            !_opening && _results.SelectedItem is TorrentSearchResult;
        _results.MouseDoubleClick += async (_, _) => await OpenSelectedAsync();

        var columns = new GridView();
        columns.Columns.Add(new GridViewColumn
        {
            Header = "Título",
            Width = 430,
            DisplayMemberBinding = new Binding(nameof(TorrentSearchResult.Title))
        });
        columns.Columns.Add(new GridViewColumn
        {
            Header = "Fonte",
            Width = 175,
            DisplayMemberBinding = new Binding(nameof(TorrentSearchResult.Provider))
        });
        columns.Columns.Add(new GridViewColumn
        {
            Header = "Ano",
            Width = 65,
            DisplayMemberBinding = new Binding(nameof(TorrentSearchResult.YearText))
        });
        columns.Columns.Add(new GridViewColumn
        {
            Header = "Seeders",
            Width = 85,
            DisplayMemberBinding = new Binding(nameof(TorrentSearchResult.SeedersText))
        });
        columns.Columns.Add(new GridViewColumn
        {
            Header = "Tamanho",
            Width = 105,
            DisplayMemberBinding = new Binding(nameof(TorrentSearchResult.SizeText))
        });
        _results.View = columns;
        Grid.SetRow(_results, 2);
        root.Children.Add(_results);

        var footer = new DockPanel { Margin = new Thickness(0, 15, 0, 0) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        _openButton.Content = "▶ Preparar no player P2P";
        _openButton.Padding = new Thickness(17, 9, 17, 9);
        _openButton.Background = Brush("#3364D5");
        _openButton.Foreground = Brushes.White;
        _openButton.BorderThickness = new Thickness(0);
        _openButton.IsEnabled = false;
        _openButton.Click += async (_, _) => await OpenSelectedAsync();
        actions.Children.Add(_openButton);
        DockPanel.SetDock(actions, Dock.Right);
        footer.Children.Add(actions);

        var information = new StackPanel();
        _status.Text = "Digite o nome do filme para pesquisar.";
        _status.Foreground = Brush("#B9C6D8");
        _status.TextWrapping = TextWrapping.Wrap;
        information.Children.Add(_status);
        _warning.Foreground = Brush("#D8B986");
        _warning.FontSize = 11;
        _warning.TextWrapping = TextWrapping.Wrap;
        _warning.MaxWidth = 710;
        _warning.Margin = new Thickness(0, 5, 0, 0);
        information.Children.Add(_warning);
        footer.Children.Add(information);

        Grid.SetRow(footer, 3);
        root.Children.Add(footer);
        return root;
    }

    private async Task SearchAsync()
    {
        if (!_searchButton.IsEnabled)
            return;

        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _searchButton.IsEnabled = false;
        _openButton.IsEnabled = false;
        _results.ItemsSource = null;
        _warning.Text = "";
        _status.Text = "Pesquisando catálogos e verificando descritores .torrent...";
        Cursor = Cursors.Wait;

        try
        {
            var response = await _search.SearchAsync(_query.Text, token);
            if (token.IsCancellationRequested) return;
            _results.ItemsSource = response.Results;
            _status.Text = response.Results.Count == 0
                ? "Nenhum torrent encontrado nas fontes configuradas para este título."
                : $"{response.Results.Count} fonte(s) encontradas. Selecione uma para preparar a reprodução.";
            _warning.Text = string.Join("  •  ", response.Warnings.Take(3));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _status.Text = "Falha na pesquisa: " + ex.Message;
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                _searchButton.IsEnabled = true;
                Cursor = Cursors.Arrow;
            }
        }
    }

    private async Task OpenSelectedAsync()
    {
        if (_opening || _results.SelectedItem is not TorrentSearchResult result)
            return;

        _opening = true;
        _openButton.IsEnabled = false;
        _status.Text = "Preparando fonte P2P. Nenhum filme completo será baixado previamente.";
        string? temporaryTorrent = null;

        try
        {
            if (result.Magnet is { Length: > 0 } magnet)
            {
                await _loadMagnet(magnet);
            }
            else if (result.TorrentFileUri is not null)
            {
                temporaryTorrent = await _search.DownloadTorrentDescriptorAsync(
                    result.TorrentFileUri, CancellationToken.None);
                await _loadTorrentFile(temporaryTorrent);
            }
            else
            {
                throw new InvalidOperationException("Este resultado não tem magnet ou .torrent válido.");
            }

            _status.Text = "Abra a janela P2P, selecione o vídeo e clique em 'Assistir agora'.";
        }
        catch (Exception ex)
        {
            _status.Text = "Não foi possível preparar o torrent: " + ex.Message;
            MessageBox.Show(this, ex.Message, "Busca BitTorrent", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            if (temporaryTorrent is not null)
            {
                try { File.Delete(temporaryTorrent); } catch { }
            }
            _opening = false;
            _openButton.IsEnabled = _results.SelectedItem is TorrentSearchResult;
        }
    }

    private void OpenConfig()
    {
        try
        {
            Process.Start(new ProcessStartInfo("notepad.exe", "\"" + _search.ConfigPath + "\"")
            {
                UseShellExecute = true
            });
            _status.Text = "Adicione fontes Torznab no sources.json, salve e pesquise novamente.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Configurar fontes", MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static Button MakeButton(string text, RoutedEventHandler handler)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(12, 9, 12, 9),
            Background = Brush("#27334A"),
            Foreground = Brushes.White,
            BorderBrush = Brush("#46536A")
        };
        button.Click += handler;
        return button;
    }

    private static Brush Brush(string hex) =>
        new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
}
