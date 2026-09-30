using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SanchesTV.Core.Discovery;
using SanchesTV.Core.Models;
using SanchesTV.Core.Parsing;
using SanchesTV.Desktop.Windows;

namespace SanchesTV.Desktop.Diagnostics;

/// <summary>
/// Renders the real WPF discovery window using a deterministic, clearly labelled fixture.
/// It never constructs the main window, playback engines, database or network clients.
/// </summary>
internal static class CinemaHubCapture
{
    private const int CaptureWidth = 1160;
    private const int ViewportHeight = 850;
    private const int MaximumFullHeight = 3000;
    private static readonly DateTimeOffset FixtureNow = new(2026, 9, 30, 20, 0, 0, TimeSpan.Zero);

    public static async Task<int> RunAsync(string outputDirectory)
    {
        CinemaHubWindow? window = null;
        try
        {
            Directory.CreateDirectory(outputDirectory);
            var errorPath = Path.Combine(outputDirectory, "error.txt");
            if (File.Exists(errorPath)) File.Delete(errorPath);
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

            window = new CinemaHubWindow(CreateSnapshot(), _ => Task.CompletedTask,
                () => { }, () => { }, () => { }, () => { }, demonstration: true)
            {
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = 0,
                Top = 0,
                MinWidth = 0,
                MinHeight = 0,
                MaxWidth = double.PositiveInfinity,
                MaxHeight = double.PositiveInfinity,
                Width = CaptureWidth,
                Height = ViewportHeight,
                ShowActivated = false,
                UseLayoutRounding = true,
                SnapsToDevicePixels = true
            };
            window.Show();
            var content = (Grid)window.Content;
            // The window's background is outside the content subtree being captured.
            content.Background = window.Background;
            await PrepareLayoutAsync(window, content, ViewportHeight);
            SavePng(content, CaptureWidth, ViewportHeight,
                Path.Combine(outputDirectory, "cinema-hub-native.png"));

            var scroll = (ScrollViewer)content.Children[0];
            var desiredFullHeight = (int)Math.Ceiling(scroll.ExtentHeight);
            var fullHeight = Math.Clamp(desiredFullHeight, ViewportHeight, MaximumFullHeight);
            await PrepareLayoutAsync(window, content, fullHeight);
            scroll.ScrollToTop();
            await window.Dispatcher.InvokeAsync(() => content.UpdateLayout(), DispatcherPriority.ApplicationIdle);
            SavePng(content, CaptureWidth, fullHeight,
                Path.Combine(outputDirectory, "cinema-hub-native-full.png"));

            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "capture-info.json"),
                JsonSerializer.Serialize(new
                {
                    renderer = "WPF RenderTargetBitmap",
                    catalogue = "Deterministic demonstration fixture; no playback or network access",
                    fixtureNowUtc = FixtureNow,
                    channelCount = 8,
                    width = CaptureWidth,
                    viewportHeight = ViewportHeight,
                    fullHeight,
                    fullContentHeight = desiredFullHeight,
                    fullCaptureTruncated = desiredFullHeight > MaximumFullHeight,
                    images = new[] { "cinema-hub-native.png", "cinema-hub-native-full.png" }
                }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception ex)
        {
            try
            {
                Directory.CreateDirectory(outputDirectory);
                await File.WriteAllTextAsync(Path.Combine(outputDirectory, "error.txt"), ex.ToString());
            }
            catch
            {
                // An invalid or unwritable output path is still reported through the exit code.
            }
            return 1;
        }
        finally
        {
            window?.Close();
        }
    }

    private static async Task PrepareLayoutAsync(Window window, FrameworkElement content, int height)
    {
        window.Height = height;
        content.Width = CaptureWidth;
        content.Height = height;
        content.Measure(new Size(CaptureWidth, height));
        content.Arrange(new Rect(0, 0, CaptureWidth, height));
        window.UpdateLayout();
        await window.Dispatcher.InvokeAsync(() => content.UpdateLayout(), DispatcherPriority.ApplicationIdle);
    }

    private static void SavePng(Visual content, int width, int height, string path)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    private static DiscoverySnapshot CreateSnapshot()
    {
        var names = new[]
        {
            "Horizonte Cinema", "Brasil em Cena", "Natureza Viva", "Arena Esportes",
            "Som & Arte", "Mundo Ciência", "Notícias Agora", "Pequenos Exploradores"
        };
        var categories = new[] { "Cinema", "Cultura", "Natureza", "Esportes", "Música", "Ciência", "Notícias", "Infantil" };
        var titles = new[]
        {
            "Curtas do Horizonte", "Caminhos do Brasil", "Entre rios e florestas", "Histórias do esporte",
            "Palco aberto", "Além das estrelas", "O mundo em perspectiva", "Uma aventura no jardim"
        };
        var nextTitles = new[]
        {
            "Luzes da cidade", "Retratos da nossa terra", "Vida no oceano", "Movimento e superação",
            "Notas brasileiras", "Descobertas em laboratório", "Conexões do dia", "Pequenas grandes descobertas"
        };
        var statuses = new[]
        {
            StreamStatus.Online, StreamStatus.Online, StreamStatus.Online, StreamStatus.NotTested,
            StreamStatus.Slow, StreamStatus.Online, StreamStatus.NotTested, StreamStatus.Offline
        };
        var channels = Enumerable.Range(0, names.Length).Select(index => new Channel(
            Guid.Parse($"83000000-0000-0000-0000-{index + 1:000000000000}"),
            names[index], TextNormalizer.Normalize(names[index]), "Brasil", "Português", null, null,
            categories[index], null, $"demo-{index + 1}", index + 1,
            [new ChannelSource(
                Guid.Parse($"83000000-0000-0000-0001-{index + 1:000000000000}"),
                "Catálogo demonstrativo", new Uri($"https://example.invalid/demo/{index + 1}.m3u8"),
                0, statuses[index])],
            IsFavorite: index < 2)).ToArray();
        var programs = channels.SelectMany((channel, index) => new[]
        {
            new EpgProgram(channel.EpgId!, FixtureNow.AddMinutes(-20), FixtureNow.AddMinutes(40),
                titles[index], "Programação fictícia para captura da interface WPF.", categories[index]),
            new EpgProgram(channel.EpgId!, FixtureNow.AddMinutes(40), FixtureNow.AddMinutes(100),
                nextTitles[index], "Programação fictícia para captura da interface WPF.", categories[index])
        }).ToArray();
        return new DiscoveryEngine().Build(channels, [channels[4].Id, channels[2].Id, channels[0].Id], programs, FixtureNow);
    }
}
