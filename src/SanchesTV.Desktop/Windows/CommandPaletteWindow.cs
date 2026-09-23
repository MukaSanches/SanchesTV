using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SanchesTV.Desktop.Windows;

public sealed record CommandPaletteAction(string Title, string Detail, Func<Task> Execute);

public sealed class CommandPaletteWindow : Window
{
    private readonly IReadOnlyList<CommandPaletteAction> _actions;
    private readonly TextBox _search = new();
    private readonly ListBox _list = new();

    public CommandPaletteWindow(IEnumerable<CommandPaletteAction> actions)
    {
        _actions = actions.ToArray();
        Title = "SanchesTV 8 Command Center";
        Width = 680;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Background = new SolidColorBrush(Color.FromRgb(12, 14, 19));
        Foreground = Brushes.White;

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());

        var title = new TextBlock
        {
            Text = "SANCHES TV 8 • COMMAND CENTER",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 14)
        };
        Grid.SetRow(title, 0);
        root.Children.Add(title);

        _search.FontSize = 16;
        _search.Padding = new Thickness(12, 9, 12, 9);
        _search.Margin = new Thickness(0, 0, 0, 12);
        _search.TextChanged += (_, _) => Refresh();
        Grid.SetRow(_search, 1);
        root.Children.Add(_search);

        _list.Background = Brushes.Transparent;
        _list.Foreground = Brushes.White;
        _list.BorderThickness = new Thickness(0);
        _list.MouseDoubleClick += async (_, _) => await RunSelectedAsync();
        _list.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await RunSelectedAsync();
            }
        };
        Grid.SetRow(_list, 2);
        root.Children.Add(_list);

        Content = root;
        Loaded += (_, _) => { Refresh(); _search.Focus(); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
    }

    private void Refresh()
    {
        var query = _search.Text.Trim();
        _list.Items.Clear();

        foreach (var action in _actions.Where(a =>
                     query.Length == 0 ||
                     a.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                     a.Detail.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
        {
            _list.Items.Add(new ListBoxItem
            {
                Tag = action,
                Padding = new Thickness(10),
                Content = $"{action.Title}   —   {action.Detail}"
            });
        }

        if (_list.Items.Count > 0)
            _list.SelectedIndex = 0;
    }

    private async Task RunSelectedAsync()
    {
        if (_list.SelectedItem is not ListBoxItem { Tag: CommandPaletteAction action })
            return;

        await action.Execute();
        Close();
    }
}
