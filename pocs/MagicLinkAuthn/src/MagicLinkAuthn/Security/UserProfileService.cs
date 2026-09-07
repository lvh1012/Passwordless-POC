using MagicLinkAuthn.Data;
using Microsoft.EntityFrameworkCore;

namespace MagicLinkAuthn.Security;

public sealed class UserProfileService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public UserProfileService(ApplicationDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<UserProfileCompletionResult> CompleteAsync(
        string? userId,
        string? suppliedFullName,
        string? suppliedPhoneNumber,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        var fullName = suppliedFullName?.Length > 200
            ? null
            : NormalizeFullName(suppliedFullName);
        if (fullName is null || fullName.Length is < 2 or > 100)
        {
            errors[nameof(ApplicationUser.FullName)] = "Full name must contain between 2 and 100 characters.";
        }

        var phoneNumber = suppliedPhoneNumber?.Trim();
        if (string.IsNullOrEmpty(phoneNumber))
        {
            phoneNumber = null;
        }
        else if (!IsE164PhoneNumber(phoneNumber))
        {
            errors[nameof(ApplicationUser.PhoneNumber)] =
                "Phone number must use E.164 format, for example +84901234567.";
        }

        if (errors.Count > 0)
        {
            return new UserProfileCompletionResult(UserProfileCompletionStatus.Invalid, errors);
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return new UserProfileCompletionResult(UserProfileCompletionStatus.UserNotFound, errors);
        }

        var completedAt = _timeProvider.GetUtcNow();
        var updated = await _dbContext.Users
            .Where(user => user.Id == userId && user.ProfileCompletedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(user => user.FullName, fullName)
                    .SetProperty(user => user.PhoneNumber, phoneNumber)
                    .SetProperty(user => user.PhoneNumberConfirmed, false)
                    .SetProperty(user => user.ProfileCompletedAt, completedAt),
                cancellationToken);

        if (updated == 1)
        {
            return new UserProfileCompletionResult(UserProfileCompletionStatus.Completed, errors);
        }

        var exists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId, cancellationToken);
        return new UserProfileCompletionResult(
            exists ? UserProfileCompletionStatus.AlreadyCompleted : UserProfileCompletionStatus.UserNotFound,
            errors);
    }

    public Task<bool> IsCompleteAsync(string userId, CancellationToken cancellationToken) =>
        _dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                user => user.Id == userId && user.ProfileCompletedAt != null,
                cancellationToken);

    public static string GetSafeReturnUrl(string? suppliedReturnUrl)
    {
        string? returnUrl;
        try
        {
            returnUrl = MagicLinkService.ValidateReturnUrl(suppliedReturnUrl);
        }
        catch (InvalidReturnUrlException)
        {
            return "/dashboard";
        }

        if (returnUrl is null)
        {
            return "/dashboard";
        }

        var path = returnUrl.Split('?', '#')[0];
        return string.Equals(path, "/onboarding", StringComparison.OrdinalIgnoreCase)
            ? "/dashboard"
            : returnUrl;
    }

    private static string? NormalizeFullName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return string.Join(
            ' ',
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static bool IsE164PhoneNumber(string value)
    {
        if (value.Length is < 9 or > 16 || value[0] != '+' || value[1] is < '1' or > '9')
        {
            return false;
        }

        for (var index = 2; index < value.Length; index++)
        {
            if (value[index] is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }
}

public sealed record UserProfileCompletionResult(
    UserProfileCompletionStatus Status,
    IReadOnlyDictionary<string, string> Errors);

public enum UserProfileCompletionStatus
{
    Completed,
    AlreadyCompleted,
    Invalid,
    UserNotFound
}
