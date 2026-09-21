using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SanchesTV.Core.Catalog;

namespace SanchesTV.Desktop.Windows;

public sealed class PremiumHubWindow : Window
{
    private readonly ListBox _providerList = new();
    private readonly ListView _channelList = new();
    private readonly TextBox _search = new();
    private readonly TextBlock _providerTitle = new();
    private readonly TextBlock _providerCount = new();
    private readonly TextBlock _providerDescription = new();

    public PremiumHubWindow()
    {
        Title = "SanchesTV 5.0 — Hub Premium PT-BR";
        Width = 1180;
        Height = 760;
        MinWidth = 900;
        MinHeight = 600;
        Background = new SolidColorBrush(Color.FromRgb(14, 16, 20));
        Foreground = Brushes.White;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = BuildUi();

        _providerList.ItemsSource = PremiumCatalog.Providers;
        _providerList.DisplayMemberPath = nameof(PremiumProvider.Name);
        _providerList.SelectionChanged += ProviderList_SelectionChanged;
        _providerList.SelectedIndex = 0;

        _search.TextChanged += (_, _) => RefreshChannels();
        _channelList.MouseDoubleClick += ChannelList_MouseDoubleClick;
        RefreshChannels();
    }

    private UIElement BuildUi()
    {
        var root = new Grid { Margin = new Thickness(16) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new StackPanel();
        left.Children.Add(new TextBlock
        {
            Text = "Hub Premium",
            FontSize = 28,
            FontWeight = FontWeights.Bold
        });
        left.Children.Add(new TextBlock
        {
            Text = "Provedores oficiais PT-BR",
            Foreground = new SolidColorBrush(Color.FromRgb(127, 200, 255)),
            Margin = new Thickness(0, 2, 0, 14)
        });
        left.Children.Add(new TextBlock
        {
            Text = "Selecione um serviço:",
            Foreground = new SolidColorBrush(Color.FromRgb(170, 178, 192)),
            Margin = new Thickness(0, 0, 0, 6)
        });

        _providerList.Background = new SolidColorBrush(Color.FromRgb(24, 28, 35));
        _providerList.Foreground = Brushes.White;
        _providerList.BorderThickness = new Thickness(0);
        _providerList.MinHeight = 250;
        left.Children.Add(_providerList);

        var notice = new TextBlock
        {
            Text = "O SanchesTV não captura nem descriptografa streams premium. O login e a reprodução protegida acontecem no serviço oficial do assinante.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(154, 163, 178)),
            Margin = new Thickness(0, 18, 0, 0)
        };
        left.Children.Add(notice);

        Grid.SetColumn(left, 0);
        root.Children.Add(left);

        var right = new Grid();
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _providerTitle.FontSize = 25;
        _providerTitle.FontWeight = FontWeights.Bold;
        right.Children.Add(_providerTitle);

        _providerCount.Foreground = new SolidColorBrush(Color.FromRgb(127, 200, 255));
        _providerCount.Margin = new Thickness(0, 4, 0, 0);
        Grid.SetRow(_providerCount, 1);
        right.Children.Add(_providerCount);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 12, 0, 14)
        };
        var open = MakeButton("Abrir serviço oficial");
        open.Click += (_, _) => OpenSelectedProvider(false);
        var guide = MakeButton("Abrir canais/grade");
        guide.Click += (_, _) => OpenSelectedProvider(true);
        actions.Children.Add(open);
        actions.Children.Add(guide);
        Grid.SetRow(actions, 2);
        right.Children.Add(actions);

        var searchPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        _providerDescription.TextWrapping = TextWrapping.Wrap;
        _providerDescription.Foreground = new SolidColorBrush(Color.FromRgb(205, 210, 218));
        _providerDescription.Margin = new Thickness(0, 0, 0, 12);
        searchPanel.Children.Add(_providerDescription);

        _search.Height = 38;
        _search.ToolTip = "Pesquisar canal premium ou provedor";
        searchPanel.Children.Add(_search);

        Grid.SetRow(searchPanel, 3);
        right.Children.Add(searchPanel);

        _channelList.Background = new SolidColorBrush(Color.FromRgb(18, 21, 26));
        _channelList.Foreground = Brushes.White;
        _channelList.BorderThickness = new Thickness(0);
        _channelList.View = new GridView
        {
            Columns =
            {
                new GridViewColumn { Header = "Canal", DisplayMemberBinding = new System.Windows.Data.Binding(nameof(PremiumChannelEntry.Channel)), Width = 310 },
                new GridViewColumn { Header = "Onde assistir oficialmente", DisplayMemberBinding = new System.Windows.Data.Binding(nameof(PremiumChannelEntry.ProviderName)), Width = 260 }
            }
        };
        Grid.SetRow(_channelList, 4);
        right.Children.Add(_channelList);

        Grid.SetColumn(right, 2);
        root.Children.Add(right);
        return root;
    }

    private static Button MakeButton(string text) => new()
    {
        Content = text,
        Padding = new Thickness(12, 7, 12, 7),
        Margin = new Thickness(0, 0, 8, 0),
        MinWidth = 150
    };

    private void ProviderList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_providerList.SelectedItem is not PremiumProvider provider)
            return;

        _providerTitle.Text = provider.Name;
        _providerCount.Text = provider.ChannelCountLabel;
        _providerDescription.Text = provider.Description;
        RefreshChannels();
    }

    private void RefreshChannels()
    {
        var query = _search.Text;
        var selected = _providerList.SelectedItem as PremiumProvider;

        IEnumerable<PremiumChannelEntry> rows = PremiumCatalog.SearchChannels(query);
        if (selected is not null && string.IsNullOrWhiteSpace(query))
            rows = rows.Where(x => x.ProviderId == selected.Id);

        _channelList.ItemsSource = rows.ToArray();
    }

    private void ChannelList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_channelList.SelectedItem is PremiumChannelEntry row)
            OpenUrl(row.ProviderUrl);
    }

    private void OpenSelectedProvider(bool guide)
    {
        if (_providerList.SelectedItem is not PremiumProvider provider)
            return;

        OpenUrl(guide && !string.IsNullOrWhiteSpace(provider.GuideUrl)
            ? provider.GuideUrl!
            : provider.WebUrl);
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
