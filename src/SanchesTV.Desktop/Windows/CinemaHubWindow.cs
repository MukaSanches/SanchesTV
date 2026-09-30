using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SanchesTV.Core.Discovery;
using SanchesTV.Core.Models;

namespace SanchesTV.Desktop.Windows;

/// <summary>A keyboard-accessible discovery surface backed by the user's local library.</summary>
public sealed class CinemaHubWindow : Window
{
    private readonly DiscoverySnapshot _snapshot;
    private readonly Func<Channel, Task> _play;
    private readonly Action? _openFavorites;
    private readonly Action? _openEpg;
    private readonly Action? _importPlaylist;
    private readonly Action? _openMediaLab;
    private readonly bool _isDemonstration;
    private readonly List<Button> _playButtons = [];
    private readonly TextBlock _status = new();
    private readonly Border _statusPanel = new();
    private bool _startingPlayback;
    private bool _closed;

    private static readonly Brush Ink = Brush("#F4F2FF");
    private static readonly Brush Muted = Brush("#BCB8D0");
    private static readonly Brush Cyan = Brush("#67E8F9");
    private static readonly Brush Violet = Brush("#B49AFF");

    public CinemaHubWindow(
        DiscoverySnapshot snapshot,
        Func<Channel, Task> play,
        Action? openFavorites = null,
        Action? openEpg = null,
        Action? importPlaylist = null,
        Action? openMediaLab = null,
        bool demonstration = false)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(play);
        _snapshot = snapshot;
        _play = play;
        _openFavorites = openFavorites;
        _openEpg = openEpg;
        _importPlaylist = importPlaylist;
        _openMediaLab = openMediaLab;
        _isDemonstration = demonstration;

        Title = "SanchesTV — Cinema Hub";
        Width = Math.Min(1160, SystemParameters.WorkArea.Width);
        Height = Math.Min(850, SystemParameters.WorkArea.Height);
        MinWidth = Math.Min(900, SystemParameters.WorkArea.Width);
        MinHeight = Math.Min(650, SystemParameters.WorkArea.Height);
        MaxHeight = SystemParameters.WorkArea.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("#0B0912");
        Foreground = Ink;
        FontFamily = new FontFamily("Segoe UI");
        Resources.Add(typeof(Button), BuildButtonStyle());
        Content = BuildUi();

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            Close();
            e.Handled = true;
        };
        Closed += (_, _) => _closed = true;
        Loaded += (_, _) => MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
    }

    private UIElement BuildUi()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var body = new StackPanel { Margin = new Thickness(28, 24, 28, 24) };
        if (_isDemonstration)
            body.Children.Add(new Border
            {
                Background = Brush("#222039"),
                BorderBrush = Cyan,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 18),
                Child = Text("Captura WPF • catálogo demonstrativo • sem reprodução", 13, Cyan, FontWeights.SemiBold)
            });
        body.Children.Add(BuildHeader());
        body.Children.Add(BuildHero());
        body.Children.Add(BuildMetrics());
        body.Children.Add(BuildRail("01", "Seus favoritos", "Os canais que você escolheu ter por perto.",
            _snapshot.Favorites, "Marque a estrela de um canal na biblioteca para criar sua seleção.",
            _openFavorites is null ? null : MakeActionButton("Abrir favoritos", _openFavorites)));
        body.Children.Add(BuildRail("02", "Volte aos seus canais", "Seu histórico local, do mais recente para o mais antigo.",
            _snapshot.Recent, "Depois de assistir a um canal, ele aparecerá aqui."));
        body.Children.Add(BuildRail("03", "Descubra sua próxima escolha", "Seleção local baseada em afinidade e disponibilidade das fontes.",
            _snapshot.Recommendations, "Adicione canais à biblioteca para receber sugestões.",
            _importPlaylist is null ? null : MakeActionButton("Importar playlist", _importPlaylist)));
        body.Children.Add(BuildSchedule());
        body.Children.Add(Text("Sua biblioteca. Seu ritmo. SanchesTV.", 13, Muted,
            margin: new Thickness(0, 28, 0, 0)));

        var scroll = new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            CanContentScroll = false,
            Focusable = false
        };
        root.Children.Add(scroll);

        _status.TextWrapping = TextWrapping.Wrap;
        _status.Foreground = Ink;
        _status.FontSize = 13;
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        _statusPanel.Background = Brush("#231A34");
        _statusPanel.BorderBrush = Violet;
        _statusPanel.BorderThickness = new Thickness(0, 1, 0, 0);
        _statusPanel.Padding = new Thickness(28, 13, 28, 13);
        _statusPanel.Child = _status;
        _statusPanel.Visibility = Visibility.Collapsed;
        Grid.SetRow(_statusPanel, 1);
        root.Children.Add(_statusPanel);
        return root;
    }

    private UIElement BuildHeader()
    {
        var header = new WrapPanel { Margin = new Thickness(0, 0, 0, 22) };
        var brand = new StackPanel { Width = 310, Margin = new Thickness(0, 0, 20, 10) };
        brand.Children.Add(Text("SANCHESTV", 21, Ink, FontWeights.Bold));
        brand.Children.Add(Text("C I N E M A  H U B", 10, Violet, FontWeights.SemiBold,
            new Thickness(1, 5, 0, 0)));
        header.Children.Add(brand);

        if (_openEpg is not null) header.Children.Add(MakeActionButton("Guia de programação", _openEpg));
        if (_openMediaLab is not null) header.Children.Add(MakeActionButton("Media Lab", _openMediaLab));
        if (_importPlaylist is not null) header.Children.Add(MakeActionButton("Adicionar canais", _importPlaylist));
        var close = MakeButton("Fechar", "Fechar Cinema Hub. Atalho Escape.");
        close.Click += (_, _) => Close();
        header.Children.Add(close);
        return header;
    }

    private UIElement BuildHero()
    {
        var hero = new Border
        {
            CornerRadius = new CornerRadius(24),
            BorderBrush = Brush("#534568"),
            BorderThickness = new Thickness(1),
            Background = new LinearGradientBrush(
                Color.FromRgb(46, 25, 74), Color.FromRgb(12, 35, 47), 20),
            Margin = new Thickness(0, 0, 0, 18)
        };
        var layers = new Grid();
        if (TryLoadHeroArt() is { } image)
        {
            var artwork = new Border
            {
                CornerRadius = hero.CornerRadius,
                Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill, AlignmentX = AlignmentX.Right }
            };
            layers.Children.Add(artwork);
        }
        layers.Children.Add(new Border
        {
            CornerRadius = hero.CornerRadius,
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, .5),
                EndPoint = new Point(1, .5),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(245, 14, 10, 26), 0),
                    new GradientStop(Color.FromArgb(212, 20, 12, 35), .5),
                    new GradientStop(Color.FromArgb(100, 13, 14, 28), 1)
                }
            }
        });

        var content = new StackPanel
        {
            Margin = new Thickness(30, 30, 30, 30),
            MaxWidth = 570,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        content.Children.Add(Text("A PRÓXIMA CENA COMEÇA AQUI", 11, Cyan, FontWeights.SemiBold));
        content.Children.Add(Text("Sua televisão,\nem outra dimensão.", 40, Ink, FontWeights.Bold,
            new Thickness(0, 14, 0, 14)));
        content.Children.Add(Text("Explore sua coleção, reencontre seus favoritos e acompanhe o que está passando agora.",
            15, Brush("#D3CCDF")));

        var featured = _snapshot.Recommendations.FirstOrDefault()
            ?? _snapshot.Recent.FirstOrDefault()
            ?? _snapshot.Favorites.FirstOrDefault();
        var actions = new WrapPanel { Margin = new Thickness(0, 22, 0, 0) };
        if (featured is not null)
        {
            _snapshot.ScheduleByChannel.TryGetValue(featured.Id, out var schedule);
            content.Children.Add(Text(featured.Name, 20, Ink, FontWeights.SemiBold,
                new Thickness(0, 22, 0, 0)));
            content.Children.Add(Text(schedule?.Now is { } now
                ? $"Agora · {now.Title}"
                : ChannelDescription(featured), 13, Muted, margin: new Thickness(0, 6, 0, 0)));
            actions.Children.Add(MakePlayButton(featured, "▶  Assistir " + featured.Name, primary: true));
        }
        else
        {
            content.Children.Add(Text("Sua coleção começa com uma playlist.", 19, Ink, FontWeights.SemiBold,
                new Thickness(0, 22, 0, 0)));
            if (_importPlaylist is not null)
                actions.Children.Add(MakeActionButton("Importar minha playlist", _importPlaylist, primary: true));
        }
        if (_openEpg is not null) actions.Children.Add(MakeActionButton("Explorar o guia", _openEpg));
        content.Children.Add(actions);
        layers.Children.Add(content);
        hero.Child = layers;
        return hero;
    }

    private UIElement BuildMetrics()
    {
        var section = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        var metrics = new WrapPanel();
        metrics.Children.Add(Metric(_snapshot.Metrics.ChannelCount, "canais na biblioteca", Violet));
        metrics.Children.Add(Metric(_snapshot.Metrics.FavoriteCount, "favoritos escolhidos", Cyan));
        metrics.Children.Add(Metric(_snapshot.Metrics.EpgChannelCount, "canais com programação", Violet));
        metrics.Children.Add(Metric(_snapshot.Metrics.OnlineSourceCount, "fontes com status online", Cyan));
        section.Children.Add(metrics);
        section.Children.Add(Text(
            $"Biblioteca local · {_snapshot.Metrics.SourceCount:N0} fontes · {_snapshot.Metrics.UntestedSourceCount:N0} ainda sem teste · seleção de {_snapshot.GeneratedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}",
            11, Muted, margin: new Thickness(0, 4, 0, 0)));
        return section;
    }

    private static Border Metric(int number, string label, Brush accent)
    {
        var panel = new StackPanel();
        panel.Children.Add(Text(number.ToString("N0"), 28, accent, FontWeights.Bold));
        panel.Children.Add(Text(label, 12, Muted, margin: new Thickness(0, 5, 0, 0)));
        return new Border
        {
            Width = 184,
            Background = Brush("#14101F"),
            BorderBrush = Brush("#2B2439"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(16, 14, 16, 14),
            Margin = new Thickness(0, 0, 12, 10),
            Child = panel
        };
    }

    private UIElement BuildRail(string index, string title, string subtitle,
        IReadOnlyList<Channel> channels, string emptyMessage, Button? action = null)
    {
        var section = new StackPanel { Margin = new Thickness(0, 22, 0, 0) };
        section.Children.Add(SectionHeader(index, title, subtitle, action));
        if (channels.Count == 0)
        {
            section.Children.Add(EmptyState(emptyMessage));
            return section;
        }
        var cards = new WrapPanel();
        foreach (var channel in channels) cards.Children.Add(BuildChannelCard(channel));
        section.Children.Add(cards);
        return section;
    }

    private UIElement BuildChannelCard(Channel channel)
    {
        var card = new StackPanel();
        var artwork = new Grid { Height = 86, Margin = new Thickness(0, 0, 0, 14) };
        var colors = channel.IsFavorite ? ("#38234F", "#132F3C") : ("#241F46", "#16313B");
        var gradient = new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString(colors.Item1),
            (Color)ColorConverter.ConvertFromString(colors.Item2), 30);
        artwork.Children.Add(new Border { Background = gradient, CornerRadius = new CornerRadius(10) });
        artwork.Children.Add(Text(Initials(channel.Name), 30, Ink, FontWeights.Bold,
            new Thickness(15, 17, 15, 0)));
        var chip = Text(channel.IsFavorite ? "★  FAVORITO" : "SUA BIBLIOTECA", 9, Cyan, FontWeights.SemiBold,
            new Thickness(15, 0, 15, 12));
        chip.VerticalAlignment = VerticalAlignment.Bottom;
        artwork.Children.Add(chip);
        card.Children.Add(artwork);
        card.Children.Add(Text(channel.Name, 17, Ink, FontWeights.SemiBold));
        card.Children.Add(Text(ChannelDescription(channel), 11, Muted, margin: new Thickness(0, 6, 0, 0)));
        _snapshot.ScheduleByChannel.TryGetValue(channel.Id, out var schedule);
        card.Children.Add(Text(schedule?.Now is { } now ? "Agora · " + now.Title : "Programação não disponível nesta seleção",
            12, Muted, margin: new Thickness(0, 10, 0, 0)));
        var button = MakePlayButton(channel, "▶  Assistir");
        button.Margin = new Thickness(0, 14, 0, 0);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        card.Children.Add(button);

        return new Border
        {
            Width = 244,
            Margin = new Thickness(0, 0, 14, 14),
            Padding = new Thickness(16),
            Background = Brush("#171221"),
            BorderBrush = Brush("#342B43"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Child = card
        };
    }

    private UIElement BuildSchedule()
    {
        var section = new StackPanel { Margin = new Thickness(0, 22, 0, 0) };
        section.Children.Add(SectionHeader("04", "Agora e na sequência", "Programação XMLTV dos canais desta seleção · horários locais.",
            _openEpg is null ? null : MakeActionButton("Abrir guia", _openEpg)));
        var channels = _snapshot.Favorites.Concat(_snapshot.Recent).Concat(_snapshot.Recommendations)
            .DistinctBy(c => c.Id)
            .Where(c => _snapshot.ScheduleByChannel.TryGetValue(c.Id, out var schedule)
                && (schedule.Now is not null || schedule.Next is not null))
            .Take(8).ToArray();
        if (channels.Length == 0)
        {
            section.Children.Add(EmptyState("A programação aparece aqui quando seus canais possuem um guia XMLTV associado."));
            return section;
        }
        foreach (var channel in channels)
        {
            var schedule = _snapshot.ScheduleByChannel[channel.Id];
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var content = new StackPanel { Margin = new Thickness(0, 0, 18, 0) };
            content.Children.Add(Text(channel.Name, 17, Ink, FontWeights.SemiBold));
            content.Children.Add(Text(schedule.Now is { } now
                ? $"AGORA  {now.Start.ToLocalTime():HH:mm}–{now.End.ToLocalTime():HH:mm}  ·  {now.Title}"
                : "AGORA  ·  Sem informação no guia", 13, Cyan, margin: new Thickness(0, 8, 0, 0)));
            if (schedule.Now is not null)
            {
                var progress = new ProgressBar
                {
                    Minimum = 0,
                    Maximum = 1,
                    Value = Math.Clamp(schedule.Progress, 0, 1),
                    Height = 4,
                    Foreground = Cyan,
                    Background = Brush("#342B43"),
                    BorderThickness = new Thickness(0),
                    Margin = new Thickness(0, 10, 0, 9)
                };
                AutomationProperties.SetName(progress, $"Progresso do programa em {channel.Name}");
                content.Children.Add(progress);
            }
            content.Children.Add(Text(schedule.Next is { } next
                ? $"A SEGUIR  {next.Start.ToLocalTime():HH:mm}  ·  {next.Title}"
                : "A SEGUIR  ·  Sem informação no guia", 12, Muted, margin: new Thickness(0, 5, 0, 0)));
            row.Children.Add(content);
            var play = MakePlayButton(channel, "▶  Assistir");
            play.Margin = new Thickness(0);
            play.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(play, 1);
            row.Children.Add(play);
            section.Children.Add(new Border
            {
                Padding = new Thickness(18),
                Margin = new Thickness(0, 0, 0, 10),
                CornerRadius = new CornerRadius(14),
                Background = Brush("#171221"),
                BorderBrush = Brush("#342B43"),
                BorderThickness = new Thickness(1),
                Child = row
            });
        }
        return section;
    }

    private static UIElement SectionHeader(string index, string title, string subtitle, Button? action)
    {
        var header = new WrapPanel { Margin = new Thickness(0, 0, 0, 15) };
        var content = new StackPanel { Width = 550, MaxWidth = 550, Margin = new Thickness(0, 0, 16, 10) };
        content.Children.Add(Text(index + "  /  " + title, 24, Ink, FontWeights.SemiBold));
        content.Children.Add(Text(subtitle, 13, Muted, margin: new Thickness(0, 7, 0, 0)));
        header.Children.Add(content);
        if (action is not null) header.Children.Add(action);
        return header;
    }

    private static Border EmptyState(string message) => new()
    {
        Padding = new Thickness(22),
        CornerRadius = new CornerRadius(14),
        BorderBrush = Brush("#342B43"),
        BorderThickness = new Thickness(1),
        Background = Brush("#14101F"),
        Child = Text(message, 14, Muted)
    };

    private Button MakePlayButton(Channel channel, string label, bool primary = false)
    {
        var button = MakeButton(label, "Assistir ao canal " + channel.Name, primary);
        button.IsEnabled = channel.Sources.Count > 0;
        button.ToolTip = channel.Sources.Count > 0 ? "Reproduzir " + channel.Name : "Este canal não possui uma fonte de reprodução.";
        button.Tag = channel;
        button.Click += async (_, _) => await PlayAsync(channel);
        _playButtons.Add(button);
        return button;
    }

    private Button MakeActionButton(string label, Action action, bool primary = false)
    {
        var button = MakeButton(label, label, primary);
        button.Click += (_, _) =>
        {
            var owner = Owner;
            Close();
            try { action(); }
            catch (Exception ex)
            {
                var message = "Não foi possível abrir esta ação: " + ex.Message;
                if (owner is { IsVisible: true })
                    MessageBox.Show(owner, message, "Cinema Hub", MessageBoxButton.OK, MessageBoxImage.Warning);
                else
                    MessageBox.Show(message, "Cinema Hub", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };
        return button;
    }

    private async Task PlayAsync(Channel channel)
    {
        if (_startingPlayback) return;
        _startingPlayback = true;
        foreach (var button in _playButtons) button.IsEnabled = false;
        ShowStatus("Iniciando " + channel.Name + "…");
        try
        {
            await _play(channel);
            if (!_closed) Close();
        }
        catch (Exception ex)
        {
            if (!_closed) ShowStatus("Não foi possível iniciar " + channel.Name + ". " + ex.Message);
        }
        finally
        {
            _startingPlayback = false;
            if (!_closed)
                foreach (var button in _playButtons)
                    button.IsEnabled = button.Tag is Channel item && item.Sources.Count > 0;
        }
    }

    private void ShowStatus(string message)
    {
        _status.Text = message;
        _statusPanel.Visibility = Visibility.Visible;
        AutomationProperties.SetName(_statusPanel, message);
    }

    private static Button MakeButton(string label, string accessibleName, bool primary = false)
    {
        var button = new Button
        {
            Content = new TextBlock
            {
                Text = label,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                MaxWidth = 380
            },
            Foreground = primary ? Brush("#120B24") : Ink,
            Background = primary ? Brush("#C0A8FF") : Brush("#241B32"),
            BorderBrush = primary ? Brush("#E0D2FF") : Brush("#554566"),
            Padding = new Thickness(15, 10, 15, 10),
            Margin = new Thickness(0, 0, 10, 10),
            MinHeight = 42,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand
        };
        AutomationProperties.SetName(button, accessibleName);
        return button;
    }

    private static Style BuildButtonStyle()
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border), "ButtonBorder");
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        content.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        border.AppendChild(content);
        template.VisualTree = border;
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BorderBrushProperty, Violet, "ButtonBorder"));
        template.Triggers.Add(hover);
        var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new Setter(Border.BorderBrushProperty, Cyan, "ButtonBorder"));
        focus.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(2), "ButtonBorder"));
        template.Triggers.Add(focus);
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, .45, "ButtonBorder"));
        template.Triggers.Add(disabled);
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        return style;
    }

    private static TextBlock Text(string text, double size, Brush foreground,
        FontWeight? weight = null, Thickness? margin = null) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = foreground,
        FontWeight = weight ?? FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap,
        Margin = margin ?? new Thickness(0)
    };

    private static string ChannelDescription(Channel channel)
    {
        var tags = new[] { channel.Category, channel.Country, channel.Language }
            .Where(x => !string.IsNullOrWhiteSpace(x));
        var description = string.Join(" · ", tags);
        return (description.Length > 0 ? description + " · " : string.Empty)
            + $"{channel.Sources.Count:N0} fonte(s)";
    }

    private static string Initials(string name)
    {
        var initials = string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2).Select(word => word[0]));
        return initials.Length == 0 ? "TV" : initials.ToUpperInvariant();
    }

    private static ImageSource? TryLoadHeroArt()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/cinema-horizon.png", UriKind.Absolute);
            var resource = Application.GetResourceStream(uri);
            if (resource is null) return null;
            using var stream = resource.Stream;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is System.IO.IOException or System.IO.FileFormatException
                                   or UriFormatException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    private static Brush Brush(string color)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
        brush.Freeze();
        return brush;
    }
}
