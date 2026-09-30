using SanchesTV.Core.Diagnostics;
using Xunit;

namespace SanchesTV.Tests;

public sealed class DiagnosticRedactorTests
{
    [Theory]
    [InlineData("https://iptv.example/live/my-user/my-password/42.ts")]
    [InlineData("https://my-user:my-password@iptv.example/player_api.php?username=my-user&password=my-password")]
    [InlineData("https://iptv.example/get.php?username=my-user&password=my-password&type=m3u_plus#private-fragment")]
    public void Redact_AddressesKeepOnlySchemeAndHost(string address)
    {
        var result = DiagnosticRedactor.Redact($"Falha em {address}");

        Assert.Equal("Falha em https://iptv.example/[redacted]", result);
        Assert.DoesNotContain("my-user", result);
        Assert.DoesNotContain("my-password", result);
        Assert.DoesNotContain("private-fragment", result);
    }

    [Fact]
    public void Redact_PreservesHostPortAndRemovesUserInfo()
    {
        var result = DiagnosticRedactor.Redact("http://name:secret@127.0.0.1:8080/live/name/secret/1.ts");

        Assert.Equal("http://127.0.0.1:8080/[redacted]", result);
    }

    [Fact]
    public void Redact_RemovesWholeMagnetIncludingHashNameAndTracker()
    {
        var result = DiagnosticRedactor.Redact(
            "Abrindo magnet:?xt=urn:btih:PRIVATEHASH&dn=private-name&tr=https://tracker.example/secret");

        Assert.Equal("Abrindo [redacted-magnet]", result);
    }

    [Theory]
    [InlineData("username=my-user password=my-password token=private-token")]
    [InlineData("user: my-user; pwd: my-password; access_token: private-token")]
    [InlineData("{\"username\":\"my-user\",\"password\":\"my-password\",\"token\":\"private-token\"}")]
    [InlineData("USER_NAME='my-user' PASS='my-password' API_KEY='private-token'")]
    public void Redact_RemovesCommonCredentialAssignments(string message)
    {
        var result = DiagnosticRedactor.Redact(message);

        Assert.DoesNotContain("my-user", result);
        Assert.DoesNotContain("my-password", result);
        Assert.DoesNotContain("private-token", result);
        Assert.Contains("[redacted]", result);
    }

    [Fact]
    public void Redact_RemovesCredentialsAcrossMultilineExceptionMessages()
    {
        var message = "Falha de rede\nhttps://user:pass@iptv.example/live/user/pass/1.ts\n" +
            "password = 'a private phrase'\nBearer eyJprivate.jwt.signature\n" +
            "magnet:?xt=urn:btih:PRIVATEHASH&dn=PRIVATEFILE\nTente novamente.";

        var result = DiagnosticRedactor.Redact(message);

        Assert.Contains("Falha de rede\nhttps://iptv.example/[redacted]\n", result);
        Assert.Contains("Tente novamente.", result);
        Assert.DoesNotContain("user:pass", result);
        Assert.DoesNotContain("a private phrase", result);
        Assert.DoesNotContain("eyJprivate", result);
        Assert.DoesNotContain("PRIVATE", result);
    }

    [Fact]
    public void Redact_RemovesBearerTokenAfterAuthorizationHeader()
    {
        var result = DiagnosticRedactor.Redact("Authorization: Bearer private-token");

        Assert.DoesNotContain("private-token", result);
    }

    [Theory]
    [InlineData("Authorization: Basic dXNlcjpwYXNz", "Authorization: [redacted]")]
    [InlineData("Proxy-Authorization: Basic dXNlcjpwYXNz", "Proxy-Authorization: [redacted]")]
    [InlineData("authorization: Bearer private-token", "authorization: [redacted]")]
    [InlineData("Authorization: Digest username=\"private-user\", realm=\"private-realm\", response=\"private-response\"", "Authorization: [redacted]")]
    public void Redact_RemovesAuthorizationSchemeAndCompleteValue(string message, string expected)
    {
        Assert.Equal(expected, DiagnosticRedactor.Redact(message));
    }

    [Fact]
    public void Redact_AuthorizationHeaderDoesNotConsumeNextDiagnosticLine()
    {
        const string message = "Falha HTTP\r\nAuthorization: Basic dXNlcjpwYXNz\r\n" +
            "Proxy-Authorization: Basic cHJveHk6c2VjcmV0\nTentativa=2; status=401";

        var result = DiagnosticRedactor.Redact(message);

        Assert.Equal("Falha HTTP\r\nAuthorization: [redacted]\r\n" +
            "Proxy-Authorization: [redacted]\nTentativa=2; status=401", result);
        Assert.DoesNotContain("dXNlcjpwYXNz", result);
        Assert.DoesNotContain("cHJveHk6c2VjcmV0", result);
    }

    [Fact]
    public void Redact_RemovesUrlAssignedToCredentialWithoutChangingOrdinaryHostNames()
    {
        var result = DiagnosticRedactor.Redact(
            "token=https://private.example/value\nhttp://user:8080/path");

        Assert.Equal("token=[redacted]\nhttp://user:8080/[redacted]", result);
    }

    [Fact]
    public void Redact_DoesNotTruncateBeforeRemovingCredentials()
    {
        var result = DiagnosticRedactor.Redact("password=private-password-with-a-long-value", 12);

        Assert.Equal("password=[re", result);
        Assert.DoesNotContain("private", result);
    }

    [Fact]
    public void Redact_PreservesOrdinaryDiagnosticText()
    {
        const string message = "Playback started: engine=LibVLC, provider=Demo, attempt=2.";

        Assert.Equal(message, DiagnosticRedactor.Redact(message));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n")]
    public void Redact_EmptyInputProducesEmptyText(string? value)
    {
        Assert.Equal(string.Empty, DiagnosticRedactor.Redact(value));
    }
}
