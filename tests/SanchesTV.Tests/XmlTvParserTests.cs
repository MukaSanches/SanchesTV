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
}
