using MagicLinkAuthn.Configuration;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace MagicLinkAuthn.Security;

public sealed class MagicLinkOutboxProtector
{
    private const byte FormatVersion = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public MagicLinkOutboxProtector(IOptions<MagicLinkSettings> settings, IHostEnvironment environment)
    {
        if (ProductionConfigurationValidator.TryDecodeEncryptionKey(
                settings.Value.OutboxEncryptionKey,
                out var configuredKey))
        {
            _key = configuredKey;
            return;
        }

        if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            throw new InvalidOperationException(
                "MagicLink:OutboxEncryptionKey must be a Base64-encoded 256-bit key.");
        }

        _key = RandomNumberGenerator.GetBytes(32);
    }

    public byte[] Protect(Guid requestId, string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var plaintext = Encoding.UTF8.GetBytes(token);
        var protectedToken = new byte[1 + NonceSize + TagSize + plaintext.Length];
        protectedToken[0] = FormatVersion;
        var nonce = protectedToken.AsSpan(1, NonceSize);
        var tag = protectedToken.AsSpan(1 + NonceSize, TagSize);
        var ciphertext = protectedToken.AsSpan(1 + NonceSize + TagSize);
        RandomNumberGenerator.Fill(nonce);

        try
        {
            using var aes = new AesGcm(_key, TagSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, requestId.ToByteArray());
            return protectedToken;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public string Unprotect(Guid requestId, byte[] protectedToken)
    {
        ArgumentNullException.ThrowIfNull(protectedToken);
        if (protectedToken.Length <= 1 + NonceSize + TagSize || protectedToken[0] != FormatVersion)
        {
            throw new CryptographicException("The Magic Link outbox payload is invalid.");
        }

        var nonce = protectedToken.AsSpan(1, NonceSize);
        var tag = protectedToken.AsSpan(1 + NonceSize, TagSize);
        var ciphertext = protectedToken.AsSpan(1 + NonceSize + TagSize);
        var plaintext = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, requestId.ToByteArray());
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }
}
