using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;

namespace MagicLinkAuthn.Security;

public sealed class MagicLinkTokenService
{
    public const int TokenByteLength = 32;
    public const int TokenHashByteLength = 32;
    public const int EncodedTokenLength = 43;

    public GeneratedMagicLinkToken Generate()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(TokenByteLength);
        try
        {
            return new GeneratedMagicLinkToken(
                WebEncoders.Base64UrlEncode(tokenBytes),
                SHA256.HashData(tokenBytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(tokenBytes);
        }
    }

    public bool TryHash(string? encodedToken, out byte[] hash)
    {
        hash = [];
        if (encodedToken is null || encodedToken.Length != EncodedTokenLength)
        {
            return false;
        }

        byte[] tokenBytes;
        try
        {
            tokenBytes = WebEncoders.Base64UrlDecode(encodedToken);
        }
        catch (FormatException)
        {
            return false;
        }

        try
        {
            if (tokenBytes.Length != TokenByteLength ||
                !string.Equals(WebEncoders.Base64UrlEncode(tokenBytes), encodedToken, StringComparison.Ordinal))
            {
                return false;
            }

            hash = SHA256.HashData(tokenBytes);
            return true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(tokenBytes);
        }
    }
}

public sealed record GeneratedMagicLinkToken(string EncodedToken, byte[] Hash);
