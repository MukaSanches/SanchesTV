using SanchesTV.Core.Parsing;
using Xunit;

namespace SanchesTV.Tests;

public sealed class XmlTvParserTests
{
    [Fact]
    public void Parses_Programme_With_Timezone()
    {
        const string xml = """
        <tv>
          <programme start="20260920200000 -0300" stop="20260920210000 -0300" channel="TVCamara.br">
            <title>Programa teste</title>
            <desc>Descrição</desc>
          </programme>
        </tv>
        """;

        var programs = XmlTvParser.Parse(xml);
        var program = Assert.Single(programs);
        Assert.Equal("TVCamara.br", program.ChannelEpgId);
        Assert.Equal("Programa teste", program.Title);
    }
    [Fact]
    public void Prefers_Portuguese_Programme_Metadata_And_Rejects_Reversed_Intervals()
    {
        const string xml = """
        <tv>
          <programme channel="bad" start="20260920210000 +0000" stop="20260920200000 +0000"><title>Bad</title></programme>
          <programme channel="good" start="20260920200000 Z" stop="20260920210000 Z">
            <title lang="en">Movie</title><title lang="pt-BR">Filme</title>
          </programme>
        </tv>
        """;
        var programme = Assert.Single(XmlTvParser.Parse(xml));
        Assert.Equal("Filme", programme.Title);
        Assert.Equal(TimeSpan.Zero, programme.Start.Offset);
    }

    [Fact]
    public void Dtd_And_External_Entities_Are_Prohibited()
    {
        const string xml = "<!DOCTYPE tv [<!ENTITY x SYSTEM \"file:///C:/secret\">]><tv><programme channel=\"x\" start=\"20260920200000\" stop=\"20260920210000\"><title>&x;</title></programme></tv>";
        Assert.Throws<System.Xml.XmlException>(() => XmlTvParser.Parse(xml));
    }

    [Fact]
    public void Handles_Thousands_Of_Programme_Elements()
    {
        var xml = new System.Text.StringBuilder("<tv>");
        for (var i = 0; i < 3000; i++)
            xml.Append("<programme channel=\"tv\" start=\"20260920200000 +0000\" stop=\"20260920210000 +0000\"><title>Teste</title></programme>");
        xml.Append("</tv>");
        Assert.Equal(3000, XmlTvParser.Parse(xml.ToString()).Count);
    }


    [Fact]
    public void Stream_Parser_Uses_Xml_Encoding_Declaration()
    {
        const string xml = "<?xml version=\"1.0\" encoding=\"iso-8859-1\"?><tv><programme channel=\"tv\" start=\"20260920200000 +0000\" stop=\"20260920210000 +0000\"><title>Informação</title></programme></tv>";
        var encoding = System.Text.Encoding.Latin1;
        using var stream = new MemoryStream(encoding.GetBytes(xml));
        Assert.Equal("Informação", Assert.Single(XmlTvParser.Parse(stream)).Title);
    }

    [Fact]
    public void Stream_Parser_Supports_Gzip_Without_Materializing_Xml_String()
    {
        const string xml = "<tv><programme channel=\"tv\" start=\"20260920200000 +0000\" stop=\"20260920210000 +0000\"><title>Noticiário</title></programme></tv>";
        using var compressed = new MemoryStream();
        using (var compressor = new System.IO.Compression.GZipStream(
            compressed, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
        {
            var data = System.Text.Encoding.UTF8.GetBytes(xml);
            compressor.Write(data);
        }
        compressed.Position = 0;
        using var unpack = new System.IO.Compression.GZipStream(
            compressed, System.IO.Compression.CompressionMode.Decompress);
        Assert.Equal("Noticiário", Assert.Single(XmlTvParser.Parse(unpack)).Title);
    }

    [Fact]
    public void Stream_Parser_Prohibits_Dtd()
    {
        const string xml = "<!DOCTYPE tv [<!ENTITY x SYSTEM \"file:///etc/passwd\">]><tv/>";
        using var source = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        Assert.Throws<System.Xml.XmlException>(() => XmlTvParser.Parse(source));
    }

}
