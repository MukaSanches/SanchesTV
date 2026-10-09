using SanchesTV.Core.Parsing;
using Xunit;

namespace SanchesTV.Tests;

public sealed class M3uParserTests
{
    [Fact]
    public void Parses_ExtInf_And_StreamUrl()
    {
        const string m3u = "#EXTM3U\n#EXTINF:-1 tvg-id=\"tvbrasil\" tvg-name=\"TV Brasil\" group-title=\"TV aberta\",TV Brasil\nhttps://example.org/live.m3u8";
        var channels = M3uParser.Parse(m3u);
        var channel = Assert.Single(channels);
        Assert.Equal("TV Brasil", channel.Name);
        Assert.Equal("tvbrasil", channel.EpgId);
        Assert.Equal("TV aberta", channel.Category);
        Assert.Equal("https://example.org/live.m3u8", channel.Sources[0].Url.ToString());
    }

    [Fact]
    public void Normalizer_Removes_Accents_And_Case()
    {
        Assert.Equal("tv cultura sao paulo", TextNormalizer.Normalize("TV Cultura São Paulo"));
    }
    [Fact]
    public void Duplicated_Metadata_And_Commas_Are_Supported()
    {
        const string m3u = "#EXTINF:-1 tvg-id=\"old\" tvg-id=\"new\" group-title=\"Filmes, Séries\",Filmes, Séries e TV\nhttps://example.org/live";
        var channel = Assert.Single(M3uParser.Parse(m3u));
        Assert.Equal("new", channel.EpgId);
        Assert.Equal("Filmes, Séries", channel.Category);
        Assert.Equal("Filmes, Séries e TV", channel.Name);
    }

    [Fact]
    public void Broken_Entries_Do_Not_Attach_To_Next_Stream()
    {
        const string m3u = "#EXTINF:-1,Invalido\nnot-a-url\nhttps://example.org/ignored\n#EXTINF:-1,Valido\nhttps://example.org/ok";
        Assert.Equal("Valido", Assert.Single(M3uParser.Parse(m3u)).Name);
    }

    [Fact]
    public void Rejects_Local_File_Urls_And_Handles_Bom()
    {
        const string m3u = "\uFEFF#EXTM3U\n#EXTINF:-1,Arquivo\nfile:///C:/Windows/win.ini\n#EXTINF:-1,\nhttps://example.org/live";
        Assert.Equal("Canal", Assert.Single(M3uParser.Parse(m3u)).Name);
    }

    [Fact]
    public void Header_Options_Are_Isolated_Per_Channel()
    {
        const string m3u = "#EXTINF:-1,Um\n#EXTVLCOPT:http-user-agent=Test/1\nhttps://example.org/a\n#EXTINF:-1,Dois\nhttps://example.org/b";
        var channels = M3uParser.Parse(m3u);
        Assert.Equal("Test/1", channels[0].Sources[0].UserAgent);
        Assert.Null(channels[1].Sources[0].UserAgent);
    }

}
