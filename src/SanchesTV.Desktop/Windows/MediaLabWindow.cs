using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SanchesTV.Core.Models;
using SanchesTV.Desktop.Tools;

namespace SanchesTV.Desktop.Windows;

public sealed class MediaLabWindow : Window
{
    private readonly MediaToolsService _tools;
    private readonly MediaRouterService _router;
    private readonly Func<ChannelSource?> _sourceProvider;

    private readonly TextBox _output = new()
    {
        AcceptsReturn = true,
        IsReadOnly = true,
        TextWrapping = TextWrapping.NoWrap,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        MinHeight = 240
    };
    private readonly TextBlock _status = new();
    private readonly CheckBox _lan = new();

    public MediaLabWindow(
        MediaToolsService tools,
        MediaRouterService router,
        Func<ChannelSource?> sourceProvider)
    {
        _tools = tools;
        _router = router;
        _sourceProvider = sourceProvider;

        Title = "SanchesTV 7 — Media Lab";
        Width = 900;
        Height = 760;
        MinWidth = 720;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.Black;
        Foreground = System.Windows.Media.Brushes.White;

        Content = BuildUi();
        Loaded += async (_, _) => await RefreshStatusAsync();
    }

    private UIElement BuildUi()
    {
        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        header.Children.Add(new TextBlock
        {
            Text = "Media Lab 7",
            FontSize = 28,
            FontWeight = FontWeights.Bold
        });
        header.Children.Add(new TextBlock
        {
            Text = "Roteamento HLS/WebRTC/RTSP, análise broadcast, legendas e transcrição local. Os processos ficam isolados do player principal.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("#AAB4C4"),
            Margin = new Thickness(0, 5, 0, 0)
        });
        root.Children.Add(header);

        var tabs = new TabControl();
        Grid.SetRow(tabs, 1);
        root.Children.Add(tabs);

        tabs.Items.Add(BuildRouterTab());
        tabs.Items.Add(BuildAnalysisTab());
        tabs.Items.Add(BuildSubtitleTab());
        tabs.Items.Add(BuildRuntimeTab());

        _status.Text = "Inicializando Media Lab...";
        _status.Foreground = Brush("#8F9BAD");
        _status.Margin = new Thickness(0, 10, 0, 0);
        Grid.SetRow(_status, 2);
        root.Children.Add(_status);

        return root;
    }

    private TabItem BuildRouterTab()
    {
        var panel = Panel();
        panel.Children.Add(Title("Transmitir para outros dispositivos"));
        panel.Children.Add(Description(
            "O canal atual é publicado localmente pelo FFmpeg no MediaMTX. MediaMTX disponibiliza RTSP, HLS e WebRTC. Por padrão fica restrito ao próprio PC."));

        _lan.Content = "Permitir leitura pela rede local";
        _lan.Margin = new Thickness(0, 4, 0, 12);
        panel.Children.Add(_lan);

        var actions = new WrapPanel();
        actions.Children.Add(Button("▶ Iniciar roteador", StartRouter_Click));
        actions.Children.Add(Button("■ Parar roteador", StopRouter_Click));
        actions.Children.Add(Button("Copiar HLS", CopyHls_Click));
        actions.Children.Add(Button("Copiar WebRTC", CopyWebRtc_Click));
        panel.Children.Add(actions);

        var endpoint = new TextBlock
        {
            Text = "Ao iniciar, os endereços aparecerão abaixo.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 14, 0, 0),
            Foreground = Brush("#AAB4C4")
        };
        endpoint.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(RouterDescription))
        {
            Source = this
        });
        panel.Children.Add(endpoint);

        return new TabItem { Header = "Transmitir", Content = Scroll(panel) };
    }

    public string RouterDescription
    {
        get
        {
            var s = _router.Current;
            if (s is null)
                return "Roteador parado.";
            return $"RTSP: {s.RtspUrl}\nHLS: {s.HlsUrl}\nWebRTC: {s.WebRtcUrl}\nLAN: {(s.LanEnabled ? "sim" : "não")}";
        }
    }

    private TabItem BuildAnalysisTab()
    {
        var panel = Panel();
        panel.Children.Add(Title("Diagnóstico profissional"));
        panel.Children.Add(Description(
            "FFprobe analisa a fonte atual ou arquivos locais. TSDuck faz inspeção profunda de MPEG Transport Stream."));

        var actions = new WrapPanel();
        actions.Children.Add(Button("Analisar fonte atual (FFprobe)", ProbeCurrent_Click));
        actions.Children.Add(Button("Analisar arquivo (FFprobe)", ProbeFile_Click));
        actions.Children.Add(Button("Analisar MPEG-TS (TSDuck)", AnalyzeTs_Click));
        panel.Children.Add(actions);

        _output.Margin = new Thickness(0, 14, 0, 0);
        panel.Children.Add(_output);

        return new TabItem { Header = "Diagnóstico", Content = Scroll(panel) };
    }

    private TabItem BuildSubtitleTab()
    {
        var panel = Panel();
        panel.Children.Add(Title("Legendas e fala"));
        panel.Children.Add(Description(
            "CCExtractor extrai closed captions existentes. whisper.cpp cria legenda SRT em PT-BR totalmente offline usando o modelo incluído."));

        var actions = new WrapPanel();
        actions.Children.Add(Button("Extrair Closed Captions", ExtractCaptions_Click));
        actions.Children.Add(Button("Transcrever PT-BR offline", Transcribe_Click));
        actions.Children.Add(Button("Abrir pasta de legendas", OpenSubtitlesFolder_Click));
        panel.Children.Add(actions);

        panel.Children.Add(new TextBlock
        {
            Text = "A transcrição usa o modelo Whisper Tiny multilíngue para manter o instalador em tamanho administrável. Ela prioriza velocidade e funcionamento local; modelos maiores podem ser acrescentados futuramente.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("#8998AD"),
            Margin = new Thickness(0, 14, 0, 0)
        });

        return new TabItem { Header = "Legendas", Content = Scroll(panel) };
    }

    private TabItem BuildRuntimeTab()
    {
        var panel = Panel();
        panel.Children.Add(Title("Runtimes e integridade"));
        panel.Children.Add(Description(
            "Mostra os componentes avançados empacotados com a SanchesTV 7. O pipeline verifica os arquivos antes de gerar o instalador."));

        panel.Children.Add(Button("Atualizar status", async (_, _) => await RefreshStatusAsync()));

        var statusBox = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            MinHeight = 300,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 14, 0, 0)
        };
        statusBox.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding(nameof(RuntimeStatus))
        {
            Source = this
        });
        panel.Children.Add(statusBox);

        return new TabItem { Header = "Runtimes", Content = Scroll(panel) };
    }

    public string RuntimeStatus { get; private set; } = "Carregando...";

    private async Task RefreshStatusAsync()
    {
        try
        {
            _status.Text = "Verificando runtimes...";
            RuntimeStatus = await _tools.GetVersionSummaryAsync();
            _status.Text = "Runtimes verificados.";
            RefreshBindings();
        }
        catch (Exception ex)
        {
            RuntimeStatus = _tools.GetStatus() + "\n\nFalha de versão: " + ex.Message;
            _status.Text = "Verificação parcial.";
            RefreshBindings();
        }
    }

    private async void StartRouter_Click(object sender, RoutedEventArgs e)
    {
        var source = _sourceProvider();
        if (source is null)
        {
            MessageBox.Show(this, "Abra um canal ou stream primeiro.", "Media Router",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            SetBusy("Iniciando MediaMTX e publicando o stream...");
            var session = await _router.StartAsync(source, _lan.IsChecked == true);
            _status.Text = "Roteador ativo.";
            RefreshBindings();
            MessageBox.Show(this,
                $"HLS:\n{session.HlsUrl}\n\nWebRTC:\n{session.WebRtcUrl}\n\nRTSP:\n{session.RtspUrl}",
                "SanchesTV Media Router",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _status.Text = "Falha no roteador.";
            MessageBox.Show(this, ex.Message, "Media Router",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void StopRouter_Click(object sender, RoutedEventArgs e)
    {
        await _router.StopAsync();
        _status.Text = "Roteador parado.";
        RefreshBindings();
    }

    private void CopyHls_Click(object sender, RoutedEventArgs e)
    {
        if (_router.Current is { } session)
            Clipboard.SetText(session.HlsUrl.ToString());
    }

    private void CopyWebRtc_Click(object sender, RoutedEventArgs e)
    {
        if (_router.Current is { } session)
            Clipboard.SetText(session.WebRtcUrl.ToString());
    }

    private async void ProbeCurrent_Click(object sender, RoutedEventArgs e)
    {
        var source = _sourceProvider();
        if (source is null)
        {
            _output.Text = "Nenhuma fonte ativa.";
            return;
        }

        await RunToOutputAsync("Analisando fonte atual...", ct => _tools.ProbeAsync(source, ct));
    }

    private async void ProbeFile_Click(object sender, RoutedEventArgs e)
    {
        var path = PickMediaFile();
        if (path is null)
            return;
        await RunToOutputAsync("Analisando arquivo...", ct => _tools.ProbeLocalFileAsync(path, ct));
    }

    private async void AnalyzeTs_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar MPEG Transport Stream",
            Filter = "Transport Stream|*.ts;*.m2ts;*.mts|Todos os arquivos|*.*"
        };
        if (dialog.ShowDialog(this) != true)
            return;

        await RunToOutputAsync("TSDuck analisando MPEG-TS...", ct =>
            _tools.AnalyzeTransportStreamAsync(dialog.FileName, ct));
    }

    private async void ExtractCaptions_Click(object sender, RoutedEventArgs e)
    {
        var path = PickMediaFile();
        if (path is null)
            return;

        await RunToOutputAsync("Extraindo closed captions...", ct =>
            _tools.ExtractCaptionsAsync(path, ct));
    }

    private async void Transcribe_Click(object sender, RoutedEventArgs e)
    {
        var path = PickMediaFile();
        if (path is null)
            return;

        await RunToOutputAsync("Transcrevendo offline em PT-BR. Isso pode levar alguns minutos...", ct =>
            _tools.TranscribePtBrAsync(path, ct));
    }

    private void OpenSubtitlesFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            "SanchesTV", "Legendas");
        Directory.CreateDirectory(path);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private async Task RunToOutputAsync(
        string status,
        Func<CancellationToken, Task<string>> operation)
    {
        try
        {
            SetBusy(status);
            using var cts = new CancellationTokenSource();
            _output.Text = await operation(cts.Token);
            _status.Text = "Concluído.";
        }
        catch (Exception ex)
        {
            _output.Text = ex.ToString();
            _status.Text = "Falha.";
        }
    }

    private void SetBusy(string text)
    {
        _status.Text = text;
        _output.Text = text;
    }

    private static string? PickMediaFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar mídia",
            Filter = "Mídia|*.mkv;*.mp4;*.ts;*.m2ts;*.mts;*.avi;*.mov;*.webm;*.mpg;*.mpeg;*.mp3;*.wav;*.m4a;*.flac|Todos os arquivos|*.*"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private void RefreshBindings()
    {
        GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
        foreach (var tab in FindVisualChildren<TabItem>(this))
        {
            if (tab.Content is DependencyObject dep)
            {
                foreach (var box in FindVisualChildren<TextBox>(dep))
                    box.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                foreach (var block in FindVisualChildren<TextBlock>(dep))
                    block.GetBindingExpression(TextBlock.TextProperty)?.UpdateTarget();
            }
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject dep) where T : DependencyObject
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(dep);
        for (var i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(dep, i);
            if (child is T typed)
                yield return typed;
            foreach (var nested in FindVisualChildren<T>(child))
                yield return nested;
        }
    }

    private static StackPanel Panel() => new() { Margin = new Thickness(18) };

    private static ScrollViewer Scroll(UIElement child) => new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Content = child
    };

    private static TextBlock Title(string text) => new()
    {
        Text = text,
        FontSize = 21,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 0, 0, 6)
    };

    private static TextBlock Description(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Foreground = Brush("#AAB4C4"),
        Margin = new Thickness(0, 0, 0, 14)
    };

    private static Button Button(string text, RoutedEventHandler handler)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(4)
        };
        button.Click += handler;
        return button;
    }

    private static System.Windows.Media.Brush Brush(string hex) =>
        (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!;
}
