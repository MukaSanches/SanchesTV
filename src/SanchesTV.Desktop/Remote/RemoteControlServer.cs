using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SanchesTV.Desktop.Remote;

public sealed class RemoteControlServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public int Port { get; }
    public string Token { get; } = Convert.ToHexString(Guid.NewGuid().ToByteArray())[..10].ToLowerInvariant();

    public event Action? NextRequested;
    public event Action? PreviousRequested;
    public event Action? PlayPauseRequested;
    public event Action? MuteRequested;
    public event Action<int>? VolumeRequested;

    public RemoteControlServer(int port = 8765)
    {
        Port = port;
        _listener = new TcpListener(IPAddress.Any, port);
    }

    public string GetRemoteUrl()
    {
        var ip = Dns.GetHostEntry(Dns.GetHostName())
            .AddressList
            .FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(x));
        return $"http://{ip ?? IPAddress.Loopback}:{Port}/?token={Token}";
    }

    public void Start()
    {
        if (_loop is not null)
            return;
        _listener.Start();
        _loop = Task.Run(ListenLoopAsync);
    }

    private async Task ListenLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                await HandleClientAsync(client, _cts.Token);
            }
            catch (OperationCanceledException) { break; }
            catch { }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);

        var requestLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(requestLine))
            return;

        var parts = requestLine.Split(' ');
        if (parts.Length < 2)
            return;

        var uri = new Uri("http://localhost" + parts[1]);
        var suppliedToken = ParseQueryValue(uri.Query, "token");
        if (!string.Equals(suppliedToken, Token, StringComparison.Ordinal))
        {
            await WriteResponseAsync(stream, "403 Forbidden", "text/plain; charset=utf-8", "Token inválido.", cancellationToken);
            return;
        }

        switch (uri.AbsolutePath)
        {
            case "/api/next": NextRequested?.Invoke(); break;
            case "/api/prev": PreviousRequested?.Invoke(); break;
            case "/api/play": PlayPauseRequested?.Invoke(); break;
            case "/api/mute": MuteRequested?.Invoke(); break;
            case "/api/volup": VolumeRequested?.Invoke(+5); break;
            case "/api/voldown": VolumeRequested?.Invoke(-5); break;
        }

        if (uri.AbsolutePath.StartsWith("/api/", StringComparison.Ordinal))
        {
            await WriteResponseAsync(stream, "200 OK", "application/json; charset=utf-8", "{\"ok\":true}", cancellationToken);
            return;
        }

        await WriteResponseAsync(stream, "200 OK", "text/html; charset=utf-8", BuildPage(Token), cancellationToken);
    }

    private static string? ParseQueryValue(string query, string key)
    {
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && string.Equals(Uri.UnescapeDataString(kv[0]), key, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(kv[1]);
        }
        return null;
    }

    private static async Task WriteResponseAsync(NetworkStream stream, string status, string contentType, string body, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status}\r\nContent-Type: {contentType}\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(bytes, cancellationToken);
    }

    private static string BuildPage(string token) => $$"""
    <!doctype html>
    <html lang="pt-BR">
    <head>
      <meta name="viewport" content="width=device-width,initial-scale=1">
      <title>SanchesTV Controle</title>
      <style>
        body{font-family:Segoe UI,Arial;background:#0d0f13;color:#fff;margin:0;padding:24px;text-align:center}
        h1{margin:0 0 8px}.sub{color:#9aa3b2;margin-bottom:28px}
        .grid{display:grid;grid-template-columns:1fr 1fr;gap:12px;max-width:520px;margin:auto}
        button{font-size:22px;padding:22px;border:0;border-radius:14px;background:#222833;color:#fff}
        button:active{transform:scale(.98);background:#394355}.wide{grid-column:1/3}
      </style>
    </head>
    <body>
      <h1>SanchesTV</h1><div class="sub">Controle remoto local</div>
      <div class="grid">
        <button onclick="go('prev')">⏮ Canal</button><button onclick="go('next')">Canal ⏭</button>
        <button class="wide" onclick="go('play')">⏯ Play / Pause</button>
        <button onclick="go('voldown')">🔉 −</button><button onclick="go('volup')">🔊 +</button>
        <button class="wide" onclick="go('mute')">🔇 Mudo</button>
      </div>
      <script>
        const token='{{token}}';
        function go(a){fetch('/api/'+a+'?token='+token,{cache:'no-store'});}
      </script>
    </body>
    </html>
    """;

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _listener.Stop();
        if (_loop is not null)
        {
            try { await _loop; } catch { }
        }
        _cts.Dispose();
    }
}
