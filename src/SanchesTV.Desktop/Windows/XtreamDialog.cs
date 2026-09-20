using System.Windows;
using System.Windows.Controls;

namespace SanchesTV.Desktop.Windows;

public sealed class XtreamDialog : Window
{
    private readonly TextBox _server = new();
    private readonly TextBox _user = new();
    private readonly PasswordBox _password = new();

    public string Server => _server.Text.Trim();
    public string Username => _user.Text.Trim();
    public string Password => _password.Password;

    public XtreamDialog()
    {
        Title = "Adicionar Xtream";
        Width = 520;
        Height = 310;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;
        Foreground = System.Windows.Media.Brushes.Black;

        var panel = new StackPanel { Margin = new Thickness(18) };
        AddField(panel, "Servidor (https://...)", _server);
        AddField(panel, "Usuário", _user);
        panel.Children.Add(new TextBlock { Text = "Senha", Margin = new Thickness(0, 8, 0, 4) });
        _password.Height = 30;
        panel.Children.Add(_password);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancel = new Button { Content = "Cancelar", Width = 90 };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        var ok = new Button { Content = "Importar", Width = 90, IsDefault = true };
        ok.Click += (_, _) => { DialogResult = true; Close(); };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        panel.Children.Add(buttons);
        Content = panel;
    }

    private static void AddField(Panel panel, string label, TextBox box)
    {
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 4) });
        box.Height = 30;
        panel.Children.Add(box);
    }
}
