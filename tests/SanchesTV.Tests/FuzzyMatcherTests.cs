using SanchesTV.Core.Search;

namespace SanchesTV.Tests;

public sealed class FuzzyMatcherTests
{
    [Theory]
    [InlineData("glbo", "Globo", true)]
    [InlineData("record", "Record News", true)]
    [InlineData("sport", "SporTV", true)]
    [InlineData("portugl", "Portugal", true)]
    [InlineData("xyzabc", "Globo", false)]
    public void MatchesTyposAndPrefixes(string query, string candidate, bool expected)
    {
        Assert.Equal(expected, FuzzyMatcher.IsMatch(query, candidate));
    }

    [Fact]
    public void DistanceSupportsTransposition()
    {
        Assert.Equal(1, FuzzyMatcher.Distance("glbo", "globo"));
        Assert.Equal(1, FuzzyMatcher.Distance("gloob", "globo"));
    }
}
