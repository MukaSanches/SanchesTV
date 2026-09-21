using MonoTorrent;
using MonoTorrent.Client;
using SanchesTV.Core.P2P;

namespace SanchesTV.Desktop.P2P;

public sealed class P2pSettings
{
    public int MaxCacheGb { get; set; } = 10;
    public int MaxUploadKibPerSecond { get; set; } = 512;
    public bool KeepDownloadedData { get; set; }
    public bool AllowPortForwarding { get; set; }
    public bool PrebufferBeforePlay { get; set; } = true;
}

public sealed class P2pFileItem
{
    internal ITorrentManagerFile File { get; }

    public string Name => System.IO.Path.GetFileName(File.Path);
    public string Path => File.Path;
    public long Length => File.Length;
    public string SizeText => P2pMediaPolicy.FormatBytes(File.Length);
    public bool IsVideo => P2pMediaPolicy.IsLikelyVideo(File.Path);
    public bool IsSubtitle => P2pMediaPolicy.IsLikelySubtitle(File.Path);
    public string TypeText => IsVideo ? "Vídeo" : IsSubtitle ? "Legenda" : "Arquivo";
    public double Progress => File.BitField.PercentComplete;
    public string ProgressText => Progress.ToString("0.0") + "%";

    internal P2pFileItem(ITorrentManagerFile file) => File = file;
}

public sealed record P2pSessionStats(
    string State,
    string TorrentName,
    int Peers,
    long DownloadRate,
    long UploadRate,
    long Downloaded,
    long Uploaded,
    double SelectedProgress,
    long CacheBytes)
{
    public string DownloadRateText => P2pMediaPolicy.FormatBytes(DownloadRate) + "/s";
    public string UploadRateText => P2pMediaPolicy.FormatBytes(UploadRate) + "/s";
    public string DownloadedText => P2pMediaPolicy.FormatBytes(Downloaded);
    public string UploadedText => P2pMediaPolicy.FormatBytes(Uploaded);
    public string CacheText => P2pMediaPolicy.FormatBytes(CacheBytes);
}
