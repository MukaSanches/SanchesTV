using System.Windows;
using System.Windows.Controls;

namespace SanchesTV.Desktop.Windows;

public sealed class InputDialog : Window
{
    private readonly TextBox _textBox;
    public string Value => _textBox.Text.Trim();

    public InputDialog(string title, string label, string initial = "")
    {
        Title = title;
        Width = 520;
        Height = 180;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;
        Foreground = System.Windows.Media.Brushes.Black;

        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 8) });
        _textBox = new TextBox { Text = initial, Height = 32 };
        panel.Children.Add(_textBox);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var cancel = new Button { Content = "Cancelar", Width = 90 };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        var ok = new Button { Content = "OK", Width = 90, IsDefault = true };
        ok.Click += (_, _) => { DialogResult = true; Close(); };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        panel.Children.Add(buttons);
        Content = panel;

        Loaded += (_, _) => { _textBox.Focus(); _textBox.SelectAll(); };
    }
}
