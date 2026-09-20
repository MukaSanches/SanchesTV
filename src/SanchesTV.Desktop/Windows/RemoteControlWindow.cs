using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using QRCoder;

namespace SanchesTV.Desktop.Windows;

public sealed class RemoteControlWindow : Window
{
    public RemoteControlWindow(string url)
    {
        Title = "SanchesTV — Controle pelo celular";
        Width = 430;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        Foreground = System.Windows.Media.Brushes.Black;

        var panel = new StackPanel { Margin = new Thickness(22), HorizontalAlignment = HorizontalAlignment.Center };
        panel.Children.Add(new TextBlock
        {
            Text = "Controle pelo celular",
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Conecte o celular à mesma rede Wi-Fi e leia o QR Code.",
            Margin = new Thickness(0, 8, 0, 18),
            TextWrapping = TextWrapping.Wrap,
            Width = 340,
            TextAlignment = TextAlignment.Center
        });

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(10);

        var bitmap = new BitmapImage();
        using (var ms = new MemoryStream(png))
        {
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();
        }

        panel.Children.Add(new Image { Source = bitmap, Width = 260, Height = 260 });
        panel.Children.Add(new TextBox
        {
            Text = url,
            IsReadOnly = true,
            Width = 350,
            Margin = new Thickness(0, 18, 0, 0)
        });

        Content = panel;
    }
}
