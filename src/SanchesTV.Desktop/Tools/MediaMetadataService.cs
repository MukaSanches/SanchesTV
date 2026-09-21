using System.IO;

namespace SanchesTV.Desktop.Tools;

public sealed class MediaMetadataService
{
    public string Read(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Arquivo não encontrado.", path);

        using var file = TagLib.File.Create(path);
        var p = file.Properties;
        var t = file.Tag;

        var codecs = p.Codecs
            .Select(x => x.Description)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return string.Join(Environment.NewLine,
            $"Arquivo: {Path.GetFileName(path)}",
            $"Título: {Value(t.Title)}",
            $"Artista: {Value(t.FirstPerformer)}",
            $"Álbum: {Value(t.Album)}",
            $"Ano: {(t.Year == 0 ? "?" : t.Year)}",
            $"Duração: {p.Duration:hh\:mm\:ss}",
            $"Vídeo: {p.VideoWidth}×{p.VideoHeight}",
            $"Áudio: {p.AudioChannels} canais • {p.AudioSampleRate} Hz • {p.AudioBitrate} kb/s",
            $"Codecs: {(codecs.Length == 0 ? "?" : string.Join(", ", codecs))}");
    }

    private static string Value(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "?" : value;
}
