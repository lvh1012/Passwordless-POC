using MagicLinkAuthn.Configuration;
using MagicLinkAuthn.Data;
using MagicLinkAuthn.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Mail;

namespace MagicLinkAuthn.Security;

public sealed class MagicLinkService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILookupNormalizer _normalizer;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMagicLinkEmailSender _emailSender;
    private readonly MagicLinkTokenService _tokenService;
    private readonly MagicLinkSettings _settings;
    private readonly TimeProvider _timeProvider;

    public MagicLinkService(
        ApplicationDbContext dbContext,
        ILookupNormalizer normalizer,
        UserManager<ApplicationUser> userManager,
        IMagicLinkEmailSender emailSender,
        MagicLinkTokenService tokenService,
        IOptions<MagicLinkSettings> settings,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _normalizer = normalizer;
        _userManager = userManager;
        _emailSender = emailSender;
        _tokenService = tokenService;
        _settings = settings.Value;
        _timeProvider = timeProvider;
    }

    public async Task<MagicLinkIssueResult> IssueAsync(
        string? suppliedEmail,
        string? suppliedReturnUrl,
        CancellationToken cancellationToken)
    {
        var email = ValidateEmail(suppliedEmail);
        var normalizedEmail = _normalizer.NormalizeEmail(email)
            ?? throw new InvalidEmailException();
        var returnUrl = ValidateReturnUrl(suppliedReturnUrl);
        var now = _timeProvider.GetUtcNow();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Serialize issuance per normalized email. Sending inside this short transaction lets a provider failure
        // roll back the new token without revoking the user's previous valid link.
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({normalizedEmail}, 0))",
            cancellationToken);

        var cooldownStart = now.AddSeconds(-_settings.EmailCooldownSeconds);
        var isCoolingDown = await _dbContext.MagicLinkRequests
            .AnyAsync(request => request.NormalizedEmail == normalizedEmail && request.CreatedAt >= cooldownStart, cancellationToken);
        if (isCoolingDown)
        {
            await transaction.RollbackAsync(cancellationToken);
            return MagicLinkIssueResult.Throttled;
        }

        var token = _tokenService.Generate();
        var request = new MagicLinkRequest
        {
            Id = Guid.NewGuid(),
            Email = email,
            NormalizedEmail = normalizedEmail,
            TokenHash = token.Hash,
            ReturnUrl = returnUrl,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(_settings.LifetimeMinutes)
        };

        _dbContext.MagicLinkRequests.Add(request);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var baseUri = new Uri(_settings.PublicBaseUrl, UriKind.Absolute);
        var callback = new Uri(baseUri, $"/magic-link/callback#token={token.EncodedToken}");
        var providerMessageId = await _emailSender.SendAsync(email, callback, request.Id, cancellationToken);

        await _dbContext.MagicLinkRequests
            .Where(candidate =>
                candidate.NormalizedEmail == normalizedEmail &&
                candidate.Id != request.Id &&
                candidate.SentAt != null &&
                candidate.ConsumedAt == null &&
                candidate.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(candidate => candidate.RevokedAt, now),
                cancellationToken);

        request.SentAt = now;
        request.ProviderMessageId = providerMessageId;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return MagicLinkIssueResult.Sent;
    }

    public async Task<MagicLinkRedemption?> RedeemAsync(string? encodedToken, CancellationToken cancellationToken)
    {
        if (!_tokenService.TryHash(encodedToken, out var tokenHash))
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow();
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var request = await _dbContext.MagicLinkRequests
            .SingleOrDefaultAsync(candidate => candidate.TokenHash.SequenceEqual(tokenHash), cancellationToken);
        if (request is null ||
            request.SentAt is null ||
            request.ConsumedAt is not null ||
            request.RevokedAt is not null ||
            request.ExpiresAt <= now)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var consumed = await _dbContext.MagicLinkRequests
            .Where(candidate =>
                candidate.Id == request.Id &&
                candidate.SentAt != null &&
                candidate.ConsumedAt == null &&
                candidate.RevokedAt == null &&
                candidate.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(candidate => candidate.ConsumedAt, now),
                cancellationToken);
        if (consumed != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                EmailConfirmed = true
            };
            var creation = await _userManager.CreateAsync(user);
            if (!creation.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to create the Identity user: {string.Join(", ", creation.Errors.Select(error => error.Code))}");
            }
        }
        else if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            var update = await _userManager.UpdateAsync(user);
            if (!update.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to confirm the Identity email: {string.Join(", ", update.Errors.Select(error => error.Code))}");
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new MagicLinkRedemption(user, request.ReturnUrl ?? "/dashboard");
    }

    public static string? ValidateReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return null;
        }

        if (returnUrl.Length > 2048 ||
            !returnUrl.StartsWith('/', StringComparison.Ordinal) ||
            returnUrl.StartsWith("//", StringComparison.Ordinal) ||
            returnUrl.StartsWith("/\\", StringComparison.Ordinal) ||
            returnUrl.Contains('\r') ||
            returnUrl.Contains('\n'))
        {
            throw new InvalidReturnUrlException();
        }

        return returnUrl;
    }

    private static string ValidateEmail(string? suppliedEmail)
    {
        var email = suppliedEmail?.Trim();
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254)
        {
            throw new InvalidEmailException();
        }

        try
        {
            var address = new MailAddress(email);
            if (!string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidEmailException();
            }
        }
        catch (FormatException)
        {
            throw new InvalidEmailException();
        }

        return email;
    }
}

public enum MagicLinkIssueResult
{
    Sent,
    Throttled
}

public sealed record MagicLinkRedemption(ApplicationUser User, string ReturnUrl);

public sealed class InvalidEmailException : Exception
{
}

public sealed class InvalidReturnUrlException : Exception
{
}
