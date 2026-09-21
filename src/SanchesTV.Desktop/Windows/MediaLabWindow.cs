using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SanchesTV.Core.Models;
using SanchesTV.Desktop.Tools;
using SanchesTV.Desktop.Diagnostics;
using SanchesTV.Desktop.AI;
using SanchesTV.Desktop.Update;

namespace SanchesTV.Desktop.Windows;

public sealed class MediaLabWindow : Window
{
    private readonly MediaToolsService _tools;
    private readonly MediaRouterService _router;
    private readonly Func<ChannelSource?> _sourceProvider;
    private readonly HardwareMonitorService _hardware;
    private readonly DlnaCastService _dlna = new();
    private readonly AdvancedMediaProcessor _processor;
    private readonly HyperionService _hyperion;
    private readonly OnnxModelInspector _onnx = new();
    private readonly MediaMetadataService _metadata = new();
    private readonly AppUpdateService _updates = new();

    private readonly ListBox _renderers = new();
    private IReadOnlyList<DlnaRenderer> _knownRenderers = Array.Empty<DlnaRenderer>();

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
        Func<ChannelSource?> sourceProvider,
        HardwareMonitorService hardware)
    {
        _tools = tools;
        _router = router;
        _sourceProvider = sourceProvider;
        _hardware = hardware;
        _processor = new AdvancedMediaProcessor(tools);
        _hyperion = new HyperionService(tools);

        base.Title = "SanchesTV 7 — Media Lab";
        Width = 900;
        Height = 760;
        MinWidth = 720;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.Black;
        Foreground = System.Windows.Media.Brushes.White;

        Content = BuildUi();
        Loaded += async (_, _) => await RefreshStatusAsync();
        Closed += async (_, _) => await _hyperion.DisposeAsync();
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
        tabs.Items.Add(BuildCastTab());
        tabs.Items.Add(BuildAnalysisTab());
        tabs.Items.Add(BuildProcessingTab());
        tabs.Items.Add(BuildSubtitleTab());
        tabs.Items.Add(BuildAmbientTab());
        tabs.Items.Add(BuildAiTab());
        tabs.Items.Add(BuildUpdateTab());
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
        actions.Children.Add(Button("Copiar SRT", CopySrt_Click));
        actions.Children.Add(Button("Copiar RTMP", CopyRtmp_Click));
        actions.Children.Add(Button("Copiar Media-over-QUIC", CopyMoq_Click));
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
            return $"RTSP: {s.RtspUrl}\nHLS: {s.HlsUrl}\nWebRTC: {s.WebRtcUrl}\nSRT: {s.SrtUrl}\nRTMP: {s.RtmpUrl}\nMedia-over-QUIC: {s.MoqUrl}\nLAN: {(s.LanEnabled ? "sim" : "não")}";
        }
    }

    private TabItem BuildCastTab()
    {
        var panel = Panel();
        panel.Children.Add(Title("DLNA / UPnP"));
        panel.Children.Add(Description(
            "Descobre televisores e Media Renderers na rede local. O SanchesTV cria um HLS acessível na LAN e envia a URL ao dispositivo selecionado."));

        var actions = new WrapPanel();
        actions.Children.Add(Button("Procurar TVs", DiscoverDlna_Click));
        actions.Children.Add(Button("Transmitir canal atual", CastDlna_Click));
        actions.Children.Add(Button("Parar na TV", StopDlna_Click));
        panel.Children.Add(actions);

        _renderers.MinHeight = 220;
        _renderers.Margin = new Thickness(0, 12, 0, 0);
        _renderers.DisplayMemberPath = nameof(DlnaRenderer.Name);
        panel.Children.Add(_renderers);

        return new TabItem { Header = "DLNA", Content = Scroll(panel) };
    }

    private async void DiscoverDlna_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy("Procurando Media Renderers via SSDP...");
            _knownRenderers = await _dlna.DiscoverAsync(TimeSpan.FromSeconds(4));
            _renderers.ItemsSource = _knownRenderers;
            if (_knownRenderers.Count > 0)
                _renderers.SelectedIndex = 0;
            _status.Text = _knownRenderers.Count == 0
                ? "Nenhum renderer DLNA respondeu."
                : $"{_knownRenderers.Count} dispositivo(s) encontrados.";
        }
        catch (Exception ex)
        {
            _status.Text = "Falha DLNA: " + ex.Message;
        }
    }

    private async void CastDlna_Click(object sender, RoutedEventArgs e)
    {
        if (_renderers.SelectedItem is not DlnaRenderer renderer)
        {
            _status.Text = "Selecione uma TV/renderer.";
            return;
        }

        var source = _sourceProvider();
        if (source is null)
        {
            _status.Text = "Abra um canal primeiro.";
            return;
        }

        try
        {
            SetBusy("Preparando HLS para a TV...");
            var session = _router.Current is { LanEnabled: true } current
                ? current
                : await _router.StartAsync(source, enableLan: true);

            await _dlna.CastAsync(renderer, session.HlsUrl, "SanchesTV");
            _status.Text = "Transmitindo para " + renderer.Name;
            RefreshBindings();
        }
        catch (Exception ex)
        {
            _status.Text = "Cast falhou: " + ex.Message;
        }
    }

    private async void StopDlna_Click(object sender, RoutedEventArgs e)
    {
        if (_renderers.SelectedItem is not DlnaRenderer renderer)
            return;
        try
        {
            await _dlna.StopAsync(renderer);
            _status.Text = "Reprodução DLNA parada.";
        }
        catch (Exception ex)
        {
            _status.Text = "Falha ao parar: " + ex.Message;
        }
    }

    private TabItem BuildProcessingTab()
    {
        var panel = Panel();
        panel.Children.Add(Title("Processamento avançado"));
        panel.Children.Add(Description(
            "Usa os filtros realmente presentes no FFmpeg empacotado: EBU R128/loudnorm, SoXR, zscale/zimg, BWDIF e VMAF quando disponíveis."));

        var actions = new WrapPanel();
        actions.Children.Add(Button("Ver capacidades", ProcessingCaps_Click));
        actions.Children.Add(Button("Analisar loudness", Loudness_Click));
        actions.Children.Add(Button("Normalizar EBU R128", Normalize_Click));
        actions.Children.Add(Button("Processar vídeo Cinema", Enhance_Click));
        actions.Children.Add(Button("Upscale IA 2×", Upscale_Click));
        actions.Children.Add(Button("Interpolar RIFE 2× FPS", Rife_Click));
        actions.Children.Add(Button("Comparar VMAF", Vmaf_Click));
        panel.Children.Add(actions);

        panel.Children.Add(new TextBlock
        {
            Text = "Arquivos processados são gravados em Vídeos\\SanchesTV\\Processados. Operações pesadas não bloqueiam o player principal.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("#8998AD"),
            Margin = new Thickness(0, 14, 0, 0)
        });

        return new TabItem { Header = "Processar", Content = Scroll(panel) };
    }

    private async void ProcessingCaps_Click(object sender, RoutedEventArgs e)
    {
        await RunToOutputAsync("Detectando filtros e encoders do FFmpeg...",
            async ct => (await _processor.GetCapabilitiesAsync(ct)).Summary);
    }

    private async void Loudness_Click(object sender, RoutedEventArgs e)
    {
        var path = PickMediaFile();
        if (path is null) return;
        await RunToOutputAsync("Medindo loudness EBU R128...", ct => _processor.AnalyzeLoudnessAsync(path, ct));
    }

    private async void Normalize_Click(object sender, RoutedEventArgs e)
    {
        var path = PickMediaFile();
        if (path is null) return;
        await RunToOutputAsync("Normalizando áudio...", ct => _processor.NormalizeAudioAsync(path, ct));
    }

    private async void Enhance_Click(object sender, RoutedEventArgs e)
    {
        var path = PickMediaFile();
        if (path is null) return;
        await RunToOutputAsync("Processando vídeo...", ct => _processor.EnhanceVideoAsync(path, ct));
    }

    private async void UpscaleVideo_Click(object sender, RoutedEventArgs e)
    {
        var path = PickMediaFile();
        if (path is null) return;
        await RunToOutputAsync(
            "Real-ESRGAN: extraindo frames e fazendo upscale 2x. Pode usar bastante GPU e disco...",
            ct => _processor.UpscaleVideo2xAsync(path, anime: false, ct));
    }

    private async void RifeVideo_Click(object sender, RoutedEventArgs e)
    {
        var path = PickMediaFile();
        if (path is null) return;
        await RunToOutputAsync(
            "RIFE: interpolando para 2x FPS. O processamento é offline e pode demorar...",
            ct => _processor.InterpolateVideo2xAsync(path, ct));
    }

    private async void Upscale_Click(object sender, RoutedEventArgs e)
    {
        var path = PickMediaFile();
        if (path is null) return;

        var anime = MessageBox.Show(
            this,
            "Usar o modelo otimizado para anime/animação?",
            "Real-ESRGAN",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;

        await RunToOutputAsync(
            "Real-ESRGAN Vulkan fazendo upscale 2×. Esta operação pode ser demorada...",
            ct => _processor.UpscaleVideo2xAsync(path, anime, ct));
    }

    private async void Rife_Click(object sender, RoutedEventArgs e)
    {
        var path = PickMediaFile();
        if (path is null) return;

        await RunToOutputAsync(
            "RIFE Vulkan interpolando quadros para 2× FPS. Esta operação pode ser demorada...",
            ct => _processor.InterpolateVideo2xAsync(path, ct));
    }

    private async void Vmaf_Click(object sender, RoutedEventArgs e)
    {
        var reference = PickMediaFile();
        if (reference is null) return;
        var distorted = PickMediaFile();
        if (distorted is null) return;
        await RunToOutputAsync("Calculando VMAF...", ct => _processor.CompareVmafAsync(reference, distorted, ct));
    }

    private TabItem BuildAmbientTab()
    {
        var panel = Panel();
        panel.Children.Add(Title("Ambilight / Hyperion.NG"));
        panel.Children.Add(Description(
            "Inicia o Hyperion.NG empacotado com a SanchesTV 7. O hardware de LEDs, WLED e captura deve ser configurado no painel local do Hyperion."));

        var actions = new WrapPanel();
        actions.Children.Add(Button("▶ Iniciar Hyperion", StartHyperion_Click));
        actions.Children.Add(Button("Abrir painel local", OpenHyperion_Click));
        actions.Children.Add(Button("■ Parar Hyperion", StopHyperion_Click));
        panel.Children.Add(actions);

        panel.Children.Add(new TextBlock
        {
            Text = "O Hyperion roda em processo separado. Se não houver controlador LED configurado, o restante da SanchesTV continua funcionando normalmente.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("#8998AD"),
            Margin = new Thickness(0, 14, 0, 0)
        });

        return new TabItem { Header = "Ambilight", Content = Scroll(panel) };
    }

    private TabItem BuildAiTab()
    {
        var panel = Panel();
        panel.Children.Add(Title("IA local / ONNX"));
        panel.Children.Add(Description(
            "ONNX Runtime permite carregar modelos locais sem enviar conteúdo para a nuvem. Esta tela valida modelos e mostra entradas/saídas antes de eles serem usados em módulos especializados."));

        panel.Children.Add(Button("Inspecionar modelo .onnx", InspectOnnx_Click));
        panel.Children.Add(new TextBlock
        {
            Text = "ONNX Runtime: " + _onnx.RuntimeVersion + "\nWhisper local está disponível na guia Legendas.",
            Margin = new Thickness(0, 14, 0, 0),
            Foreground = Brush("#AAB4C4")
        });

        return new TabItem { Header = "IA local", Content = Scroll(panel) };
    }

    private void InspectOnnx_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar modelo ONNX",
            Filter = "Modelo ONNX|*.onnx|Todos os arquivos|*.*"
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            _output.Text = _onnx.Inspect(dialog.FileName);
            _status.Text = "Modelo ONNX validado.";
        }
        catch (Exception ex)
        {
            _output.Text = ex.ToString();
            _status.Text = "Modelo ONNX inválido ou incompatível.";
        }
    }

    private TabItem BuildUpdateTab()
    {
        var panel = Panel();
        panel.Children.Add(Title("Atualizações"));
        panel.Children.Add(Description(
            "Velopack oferece atualização incremental quando o aplicativo foi instalado pelo instalador Velopack. A release tradicional continua disponível como recuperação."));

        var actions = new WrapPanel();
        actions.Children.Add(Button("Procurar atualização", CheckUpdate_Click));
        panel.Children.Add(actions);

        panel.Children.Add(new TextBlock
        {
            Text = $"Versão atual: {_updates.CurrentVersion}\nGerenciado pelo Velopack: {(_updates.IsVelopackInstalled ? "sim" : "não")}",
            Margin = new Thickness(0, 14, 0, 0),
            Foreground = Brush("#AAB4C4")
        });

        return new TabItem { Header = "Atualizações", Content = Scroll(panel) };
    }

    private async void StartHyperion_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _status.Text = "Iniciando Hyperion.NG...";
            await _hyperion.StartAsync();
            _status.Text = "Hyperion ativo.";
        }
        catch (Exception ex)
        {
            _status.Text = "Hyperion: " + ex.Message;
        }
    }

    private void OpenHyperion_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _hyperion.OpenWebUi();
            _status.Text = "Painel Hyperion aberto no navegador.";
        }
        catch (Exception ex)
        {
            _status.Text = "Hyperion: " + ex.Message;
        }
    }

    private async void StopHyperion_Click(object sender, RoutedEventArgs e)
    {
        await _hyperion.StopAsync();
        _status.Text = "Hyperion parado.";
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_updates.IsVelopackInstalled)
            {
                _status.Text = "Esta instalação não foi criada pelo Velopack. Instale a edição V7 Velopack para updates delta.";
                return;
            }

            SetBusy("Consultando GitHub Releases...");
            var update = await _updates.CheckAsync();
            if (update is null)
            {
                _status.Text = "Nenhuma atualização disponível.";
                return;
            }

            var answer = MessageBox.Show(
                this,
                "Nova versão disponível: " + update.TargetFullRelease.Version + "\n\nBaixar e reiniciar agora?",
                "Atualização SanchesTV",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (answer != MessageBoxResult.Yes)
            {
                _status.Text = "Atualização adiada.";
                return;
            }

            await _updates.DownloadAsync(update, progress =>
                Dispatcher.Invoke(() => _status.Text = $"Baixando atualização: {progress}%"));
            _updates.ApplyAndRestart(update);
        }
        catch (Exception ex)
        {
            _status.Text = "Atualização falhou: " + ex.Message;
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
        actions.Children.Add(Button("Metadata TagLib", Metadata_Click));
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
            RuntimeStatus = await _tools.GetVersionSummaryAsync() +
                "\n\nHARDWARE\n" + _hardware.GetSummary();
            _status.Text = "Runtimes e hardware verificados.";
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
                $"HLS:\n{session.HlsUrl}\n\nWebRTC:\n{session.WebRtcUrl}\n\nRTSP:\n{session.RtspUrl}\n\nSRT:\n{session.SrtUrl}\n\nRTMP:\n{session.RtmpUrl}\n\nMedia-over-QUIC:\n{session.MoqUrl}",
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

    private void CopySrt_Click(object sender, RoutedEventArgs e)
    {
        if (_router.Current is { } session)
            Clipboard.SetText(session.SrtUrl.ToString());
    }

    private void CopyRtmp_Click(object sender, RoutedEventArgs e)
    {
        if (_router.Current is { } session)
            Clipboard.SetText(session.RtmpUrl.ToString());
    }

    private void CopyMoq_Click(object sender, RoutedEventArgs e)
    {
        if (_router.Current is { } session)
            Clipboard.SetText(session.MoqUrl.ToString());
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

    private void Metadata_Click(object sender, RoutedEventArgs e)
    {
        var path = PickMediaFile();
        if (path is null)
            return;

        try
        {
            _output.Text = _metadata.Read(path);
            _status.Text = "Metadata local lida com TagLib#.";
        }
        catch (Exception ex)
        {
            _output.Text = ex.ToString();
            _status.Text = "Falha ao ler metadata.";
        }
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
