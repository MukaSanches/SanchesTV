using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Xml.Linq;

namespace SanchesTV.Desktop.Tools;

public sealed record DlnaRenderer(
    string Name,
    Uri DescriptionUrl,
    Uri ControlUrl,
    string ServiceType);

public sealed class DlnaCastService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public async Task<IReadOnlyList<DlnaRenderer>> DiscoverAsync(
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default)
    {
        var wait = duration ?? TimeSpan.FromSeconds(3);
        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Client.ReceiveTimeout = Math.Max(500, (int)wait.TotalMilliseconds);

        var request = string.Join("\r\n",
            "M-SEARCH * HTTP/1.1",
            "HOST: 239.255.255.250:1900",
            "MAN: \"ssdp:discover\"",
            "MX: 2",
            "ST: urn:schemas-upnp-org:device:MediaRenderer:1",
            "", "");
        var bytes = Encoding.ASCII.GetBytes(request);
        await udp.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900));

        var locations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var until = DateTimeOffset.UtcNow + wait;

        while (DateTimeOffset.UtcNow < until)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = until - DateTimeOffset.UtcNow;
            try
            {
                var receiveTask = udp.ReceiveAsync(cancellationToken).AsTask();
                var completed = await Task.WhenAny(receiveTask, Task.Delay(remaining, cancellationToken));
                if (completed != receiveTask)
                    break;

                var text = Encoding.ASCII.GetString(receiveTask.Result.Buffer);
                var location = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(x => x.StartsWith("LOCATION:", StringComparison.OrdinalIgnoreCase));
                if (location is not null)
                {
                    var value = location[(location.IndexOf(':') + 1)..].Trim();
                    if (Uri.TryCreate(value, UriKind.Absolute, out _))
                        locations.Add(value);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                break;
            }
        }

        var renderers = new List<DlnaRenderer>();
        foreach (var location in locations)
        {
            try
            {
                var renderer = await ParseRendererAsync(new Uri(location), cancellationToken);
                if (renderer is not null)
                    renderers.Add(renderer);
            }
            catch
            {
            }
        }

        return renderers
            .GroupBy(x => x.ControlUrl.ToString(), StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x.Name)
            .ToArray();
    }

    public async Task CastAsync(
        DlnaRenderer renderer,
        Uri mediaUri,
        string title,
        CancellationToken cancellationToken = default)
    {
        await SoapAsync(renderer, "SetAVTransportURI", $"""
<u:SetAVTransportURI xmlns:u="{renderer.ServiceType}">
  <InstanceID>0</InstanceID>
  <CurrentURI>{Escape(mediaUri.ToString())}</CurrentURI>
  <CurrentURIMetaData>{Escape(BuildDidl(title, mediaUri))}</CurrentURIMetaData>
</u:SetAVTransportURI>
""", cancellationToken);

        await SoapAsync(renderer, "Play", $"""
<u:Play xmlns:u="{renderer.ServiceType}">
  <InstanceID>0</InstanceID>
  <Speed>1</Speed>
</u:Play>
""", cancellationToken);
    }

    public async Task StopAsync(DlnaRenderer renderer, CancellationToken cancellationToken = default)
    {
        await SoapAsync(renderer, "Stop", $"""
<u:Stop xmlns:u="{renderer.ServiceType}">
  <InstanceID>0</InstanceID>
</u:Stop>
""", cancellationToken);
    }

    private async Task<DlnaRenderer?> ParseRendererAsync(Uri description, CancellationToken cancellationToken)
    {
        var xml = await _http.GetStringAsync(description, cancellationToken);
        var doc = XDocument.Parse(xml);
        XNamespace deviceNs = doc.Root?.Name.Namespace ?? XNamespace.None;

        var name = doc.Descendants(deviceNs + "friendlyName").FirstOrDefault()?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = description.Host;

        foreach (var service in doc.Descendants(deviceNs + "service"))
        {
            var type = service.Element(deviceNs + "serviceType")?.Value;
            if (type is null || !type.Contains(":AVTransport:", StringComparison.OrdinalIgnoreCase))
                continue;

            var control = service.Element(deviceNs + "controlURL")?.Value;
            if (string.IsNullOrWhiteSpace(control))
                continue;

            var url = new Uri(description, control);
            return new DlnaRenderer(name, description, url, type);
        }

        return null;
    }

    private async Task SoapAsync(
        DlnaRenderer renderer,
        string action,
        string body,
        CancellationToken cancellationToken)
    {
        var envelope = $"""
<?xml version="1.0" encoding="utf-8"?>
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/" s:encodingStyle="http://schemas.xmlsoap.org/soap/encoding/">
  <s:Body>{body}</s:Body>
</s:Envelope>
""";

        using var request = new HttpRequestMessage(HttpMethod.Post, renderer.ControlUrl);
        request.Headers.TryAddWithoutValidation("SOAPACTION", $"\"{renderer.ServiceType}#{action}\"");
        request.Content = new StringContent(envelope, Encoding.UTF8, "text/xml");
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"DLNA {action}: HTTP {(int)response.StatusCode} {detail}");
        }
    }

    private static string BuildDidl(string title, Uri mediaUri) => $"""
<DIDL-Lite xmlns="urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:upnp="urn:schemas-upnp-org:metadata-1-0/upnp/">
  <item id="0" parentID="0" restricted="1">
    <dc:title>{Escape(title)}</dc:title>
    <upnp:class>object.item.videoItem</upnp:class>
    <res protocolInfo="http-get:*:application/vnd.apple.mpegurl:*">{Escape(mediaUri.ToString())}</res>
  </item>
</DIDL-Lite>
""";

    private static string Escape(string value) => SecurityElement.Escape(value) ?? string.Empty;
}
