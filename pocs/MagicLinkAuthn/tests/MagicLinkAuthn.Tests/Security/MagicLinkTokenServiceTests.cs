using MagicLinkAuthn.Security;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;
using Xunit;

namespace MagicLinkAuthn.Tests.Security;

public sealed class MagicLinkTokenServiceTests
{
    private readonly MagicLinkTokenService _service = new();

    [Fact]
    public void Generate_UsesA256BitOpaqueTokenAndSha256Hash()
    {
        var generated = _service.Generate();

        Assert.Equal(MagicLinkTokenService.EncodedTokenLength, generated.EncodedToken.Length);
        Assert.Equal(MagicLinkTokenService.TokenHashByteLength, generated.Hash.Length);

        var decoded = WebEncoders.Base64UrlDecode(generated.EncodedToken);
        Assert.Equal(MagicLinkTokenService.TokenByteLength, decoded.Length);
        Assert.Equal(SHA256.HashData(decoded), generated.Hash);
    }

    [Fact]
    public void Generate_DoesNotRepeatTokensAcrossSample()
    {
        var tokens = Enumerable.Range(0, 1_000)
            .Select(_ => _service.Generate().EncodedToken)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(1_000, tokens.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64url")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA+")]
    public void TryHash_RejectsNonCanonicalOrWrongLengthTokens(string? token)
    {
        Assert.False(_service.TryHash(token, out var hash));
        Assert.Empty(hash);
    }

    [Fact]
    public void TryHash_AcceptsGeneratedToken()
    {
        var generated = _service.Generate();

        Assert.True(_service.TryHash(generated.EncodedToken, out var hash));
        Assert.Equal(generated.Hash, hash);
    }
}
