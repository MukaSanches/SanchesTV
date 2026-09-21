using System.Diagnostics;
using System.IO;
using System.Net.Sockets;

namespace SanchesTV.Desktop.Tools;

public sealed class HyperionService : IAsyncDisposable
{
    private readonly MediaToolsService _tools;
    private Process? _process;

    public HyperionService(MediaToolsService tools) => _tools = tools;

    public bool IsRunning => _process is { HasExited: false };
    public Uri WebUi => new("http://127.0.0.1:8090/");

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            return;

        var exe = _tools.HyperionPath
            ?? throw new FileNotFoundException("Hyperion.NG não encontrado no runtime.");

        _process?.Dispose();
        _process = Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }) ?? throw new InvalidOperationException("Não foi possível iniciar Hyperion.NG.");

        var until = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTimeOffset.UtcNow < until)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_process.HasExited)
                throw new InvalidOperationException($"Hyperion encerrou com código {_process.ExitCode}.");

            using var tcp = new TcpClient();
            try
            {
                await tcp.ConnectAsync("127.0.0.1", 8090, cancellationToken);
                return;
            }
            catch
            {
                await Task.Delay(350, cancellationToken);
            }
        }

        // Algumas builds inicializam a UI antes do webserver; manter processo ativo
        // e permitir que o usuário tente abrir o painel posteriormente.
    }

    public void OpenWebUi()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = WebUi.ToString(),
            UseShellExecute = true
        });
    }

    public async Task StopAsync()
    {
        var p = _process;
        _process = null;
        if (p is null)
            return;

        try
        {
            if (!p.HasExited)
            {
                p.Kill(entireProcessTree: true);
                await p.WaitForExitAsync();
            }
        }
        catch
        {
        }
        p.Dispose();
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
