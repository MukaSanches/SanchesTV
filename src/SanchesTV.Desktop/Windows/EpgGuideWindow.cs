using System.Windows;
using System.Windows.Controls;
using SanchesTV.Core.Models;
using SanchesTV.Core.Storage;
using SanchesTV.Desktop.Recording;

namespace SanchesTV.Desktop.Windows;

public sealed class EpgGuideWindow : Window
{
    private readonly AppDatabase _db;
    private readonly RecordingSchedulerService _scheduler;
    private readonly Channel _channel;
    private readonly ListView _programs = new();
    private readonly ListView _recordings = new();
    private readonly TextBlock _status = new();
    private IReadOnlyList<EpgProgram> _loadedPrograms = Array.Empty<EpgProgram>();

    public EpgGuideWindow(
        AppDatabase db,
        RecordingSchedulerService scheduler,
        Channel channel)
    {
        _db = db;
        _scheduler = scheduler;
        _channel = channel;

        Title = "SanchesTV 7 — Guia e Gravações";
        Width = 980;
        Height = 720;
        MinWidth = 760;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.Black;
        Foreground = System.Windows.Media.Brushes.White;

        Content = BuildUi();
        Loaded += async (_, _) => await RefreshAsync();
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
            Text = "Guia de programação — " + _channel.Name,
            FontSize = 26,
            FontWeight = FontWeights.Bold
        });
        header.Children.Add(new TextBlock
        {
            Text = "Programação carregada do XMLTV local. É possível agendar um programa ou todos os episódios com o mesmo título disponíveis no guia.",
            Foreground = Brush("#AAB4C4"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        });
        root.Children.Add(header);

        var tabs = new TabControl();
        Grid.SetRow(tabs, 1);
        root.Children.Add(tabs);

        var guidePanel = new Grid { Margin = new Thickness(12) };
        guidePanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        guidePanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        actions.Children.Add(Button("Gravar programa", Schedule_Click));
        actions.Children.Add(Button("Gravar série/título", ScheduleSeries_Click));
        actions.Children.Add(Button("Atualizar", async (_, _) => await RefreshAsync()));
        guidePanel.Children.Add(actions);

        var guideView = new GridView();
        guideView.Columns.Add(new GridViewColumn { Header = "Data", Width = 100, DisplayMemberBinding = Bind(nameof(ProgramRow.Date)) });
        guideView.Columns.Add(new GridViewColumn { Header = "Início", Width = 70, DisplayMemberBinding = Bind(nameof(ProgramRow.Start)) });
        guideView.Columns.Add(new GridViewColumn { Header = "Fim", Width = 70, DisplayMemberBinding = Bind(nameof(ProgramRow.End)) });
        guideView.Columns.Add(new GridViewColumn { Header = "Programa", Width = 330, DisplayMemberBinding = Bind(nameof(ProgramRow.Title)) });
        guideView.Columns.Add(new GridViewColumn { Header = "Categoria", Width = 140, DisplayMemberBinding = Bind(nameof(ProgramRow.Category)) });
        guideView.Columns.Add(new GridViewColumn { Header = "Descrição", Width = 430, DisplayMemberBinding = Bind(nameof(ProgramRow.Description)) });
        _programs.View = guideView;
        _programs.MouseDoubleClick += async (_, _) => await ScheduleSelectedAsync();
        Grid.SetRow(_programs, 1);
        guidePanel.Children.Add(_programs);

        tabs.Items.Add(new TabItem { Header = "Próximos 14 dias", Content = guidePanel });

        var recordingsPanel = new Grid { Margin = new Thickness(12) };
        recordingsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        recordingsPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var recordingActions = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        recordingActions.Children.Add(Button("Cancelar/remover", DeleteRecording_Click));
        recordingActions.Children.Add(Button("Atualizar", async (_, _) => await RefreshRecordingsAsync()));
        recordingsPanel.Children.Add(recordingActions);

        var recordingView = new GridView();
        recordingView.Columns.Add(new GridViewColumn { Header = "Data", Width = 100, DisplayMemberBinding = Bind(nameof(RecordingRow.Date)) });
        recordingView.Columns.Add(new GridViewColumn { Header = "Início", Width = 70, DisplayMemberBinding = Bind(nameof(RecordingRow.Start)) });
        recordingView.Columns.Add(new GridViewColumn { Header = "Fim", Width = 70, DisplayMemberBinding = Bind(nameof(RecordingRow.End)) });
        recordingView.Columns.Add(new GridViewColumn { Header = "Programa", Width = 330, DisplayMemberBinding = Bind(nameof(RecordingRow.Title)) });
        recordingView.Columns.Add(new GridViewColumn { Header = "Estado", Width = 120, DisplayMemberBinding = Bind(nameof(RecordingRow.Status)) });
        recordingView.Columns.Add(new GridViewColumn { Header = "Arquivo/erro", Width = 420, DisplayMemberBinding = Bind(nameof(RecordingRow.Detail)) });
        _recordings.View = recordingView;
        Grid.SetRow(_recordings, 1);
        recordingsPanel.Children.Add(_recordings);

        tabs.Items.Add(new TabItem { Header = "Gravações", Content = recordingsPanel });

        _status.Foreground = Brush("#8F9BAD");
        _status.Margin = new Thickness(0, 10, 0, 0);
        Grid.SetRow(_status, 2);
        root.Children.Add(_status);

        return root;
    }

    private async Task RefreshAsync()
    {
        if (string.IsNullOrWhiteSpace(_channel.EpgId))
        {
            _status.Text = "Este canal não possui tvg-id/EPG associado.";
            _programs.ItemsSource = null;
            return;
        }

        var from = DateTimeOffset.UtcNow.AddHours(-2);
        var to = DateTimeOffset.UtcNow.AddDays(14);
        _loadedPrograms = await _db.GetProgramsAsync(_channel.EpgId, from, to);

        _programs.ItemsSource = _loadedPrograms
            .Select(x => new ProgramRow(x))
            .ToArray();

        _status.Text = $"{_loadedPrograms.Count:N0} programa(s) no guia até {to.ToLocalTime():dd/MM}.";
        await RefreshRecordingsAsync();
    }

    private async Task RefreshRecordingsAsync()
    {
        var rows = (await _db.GetScheduledRecordingsAsync(includeFinished: true))
            .Where(x => x.ChannelId == _channel.Id)
            .OrderByDescending(x => x.Start)
            .Select(x => new RecordingRow(x))
            .ToArray();
        _recordings.ItemsSource = rows;
    }

    private async void Schedule_Click(object sender, RoutedEventArgs e) =>
        await ScheduleSelectedAsync();

    private async Task ScheduleSelectedAsync()
    {
        if (_programs.SelectedItem is not ProgramRow row)
        {
            _status.Text = "Selecione um programa.";
            return;
        }

        if (row.Program.End <= DateTimeOffset.UtcNow)
        {
            _status.Text = "Esse programa já terminou.";
            return;
        }

        await _scheduler.ScheduleAsync(_channel, row.Program);
        _status.Text = "Agendado: " + row.Program.Title;
        await RefreshRecordingsAsync();
    }

    private async void ScheduleSeries_Click(object sender, RoutedEventArgs e)
    {
        if (_programs.SelectedItem is not ProgramRow row)
        {
            _status.Text = "Selecione um programa.";
            return;
        }

        var count = await _scheduler.ScheduleSeriesAsync(
            _channel,
            _loadedPrograms,
            row.Program.Title);

        _status.Text = count == 0
            ? "Nenhum novo episódio encontrado no guia."
            : $"{count} ocorrência(s) de “{row.Program.Title}” agendadas.";
        await RefreshRecordingsAsync();
    }

    private async void DeleteRecording_Click(object sender, RoutedEventArgs e)
    {
        if (_recordings.SelectedItem is not RecordingRow row)
            return;

        if (row.Recording.Status == ScheduledRecordingStatus.Recording)
        {
            _status.Text = "Uma gravação em andamento não é removida por esta tela.";
            return;
        }

        await _db.DeleteScheduledRecordingAsync(row.Recording.Id);
        _status.Text = "Agendamento removido.";
        await RefreshRecordingsAsync();
    }

    private static System.Windows.Data.Binding Bind(string path) => new(path);

    private static Button Button(string text, RoutedEventHandler handler)
    {
        var b = new Button
        {
            Content = text,
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(4)
        };
        b.Click += handler;
        return b;
    }

    private static System.Windows.Media.Brush Brush(string hex) =>
        (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!;

    private sealed class ProgramRow
    {
        public EpgProgram Program { get; }
        public string Date => Program.Start.ToLocalTime().ToString("dd/MM");
        public string Start => Program.Start.ToLocalTime().ToString("HH:mm");
        public string End => Program.End.ToLocalTime().ToString("HH:mm");
        public string Title => Program.Title;
        public string Category => Program.Category ?? "";
        public string Description => Program.Description ?? "";

        public ProgramRow(EpgProgram program) => Program = program;
    }

    private sealed class RecordingRow
    {
        public ScheduledRecording Recording { get; }
        public string Date => Recording.Start.ToLocalTime().ToString("dd/MM");
        public string Start => Recording.Start.ToLocalTime().ToString("HH:mm");
        public string End => Recording.End.ToLocalTime().ToString("HH:mm");
        public string Title => Recording.Title;
        public string Status => Recording.Status switch
        {
            ScheduledRecordingStatus.Scheduled => "Agendada",
            ScheduledRecordingStatus.Recording => "Gravando",
            ScheduledRecordingStatus.Completed => "Concluída",
            ScheduledRecordingStatus.Failed => "Falhou",
            ScheduledRecordingStatus.Cancelled => "Cancelada",
            _ => Recording.Status.ToString()
        };
        public string Detail => Recording.OutputPath ?? Recording.LastError ?? "";

        public RecordingRow(ScheduledRecording recording) => Recording = recording;
    }
}
