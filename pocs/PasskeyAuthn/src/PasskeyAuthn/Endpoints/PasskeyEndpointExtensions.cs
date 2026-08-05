using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PasskeyAuthn.Configuration;
using PasskeyAuthn.Data;
using PasskeyAuthn.Models;
using PasskeyAuthn.Security;

namespace PasskeyAuthn.Endpoints;

/// <summary>
/// Maps the server-side Passkey registration and authentication ceremony endpoints.
/// </summary>
public static class PasskeyEndpointExtensions
{
    /// <summary>
    /// Gets the fixed-window policy name shared by all public Passkey operations.
    /// </summary>
    public const string RateLimitPolicy = "passkeys";

    private const string RegistrationCookieName = "passkey-registration";
    private static readonly ApiError InvalidEmail = new("invalid_email", "A valid email address is required.");
    private static readonly ApiError AuthenticationFailed = new("authentication_failed", "Authentication failed.");
    private static readonly ApiError RegistrationConflict = new("registration_conflict", "Registration cannot be started.");
    private static readonly ApiError RegistrationFailed = new("registration_failed", "Registration could not be started.");
    private static readonly ApiError InvalidRegistrationState = new("invalid_registration_state", "Registration could not be completed.");

    /// <summary>
    /// Maps exactly the four JSON endpoints that begin and complete Passkey registration and login.
    /// </summary>
    /// <param name="endpoints">The application's endpoint route builder.</param>
    /// <returns>The supplied route builder for further endpoint composition.</returns>
    public static IEndpointRouteBuilder MapPasskeyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var passkeys = endpoints.MapGroup("/api/passkeys")
            .WithMetadata(new RequireAntiforgeryTokenAttribute(required: true))
            .RequireRateLimiting(RateLimitPolicy);

        passkeys.MapPost("/register/options", CreateRegistrationOptionsAsync);
        passkeys.MapPost("/register/complete", CompleteRegistrationAsync);
        passkeys.MapPost("/login/options", CreateLoginOptionsAsync);
        passkeys.MapPost("/login/complete", CompleteLoginAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateRegistrationOptionsAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RegistrationStateProtector stateProtector,
        IOptions<PasskeySettings> settingsOptions)
    {
        var settings = settingsOptions.Value;
        var request = await ReadRequestAsync<EmailRequest>(context.Request);
        var email = NormalizeAndValidateEmail(request?.Email, settings.MaxDisplayNameLength);
        if (email is null)
        {
            return Error(StatusCodes.Status400BadRequest, InvalidEmail);
        }

        var normalizedEmail = userManager.NormalizeEmail(email);
        var user = await userManager.Users.SingleOrDefaultAsync(candidate => candidate.NormalizedEmail == normalizedEmail);
        if (user is not null)
        {
            var passkeys = await userManager.GetPasskeysAsync(user);
            if (passkeys.Count >= settings.MaxPasskeysPerUser)
            {
                return Error(StatusCodes.Status409Conflict, RegistrationConflict);
            }

            if (passkeys.Count > 0)
            {
                return Error(StatusCodes.Status409Conflict, RegistrationConflict);
            }
        }
        else
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
            };
            if (!(await userManager.CreateAsync(user)).Succeeded)
            {
                return Error(StatusCodes.Status400BadRequest, RegistrationFailed);
            }
        }

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        user.PasskeyRegistrationExpiresAt = expiresAt;
        if (!(await userManager.UpdateAsync(user)).Succeeded)
        {
            return Error(StatusCodes.Status400BadRequest, RegistrationFailed);
        }

        var optionsJson = await signInManager.MakePasskeyCreationOptionsAsync(new PasskeyUserEntity
        {
            Id = user.Id,
            Name = email,
            DisplayName = email,
        });

        // The browser receives only an authenticated ciphertext; the user ID never becomes client-readable state.
        context.Response.Cookies.Append(
            RegistrationCookieName,
            stateProtector.Protect(user.Id, expiresAt),
            RegistrationCookieOptions(expiresAt));

        return Results.Content(optionsJson, "application/json");
    }

    private static async Task<IResult> CompleteRegistrationAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RegistrationStateProtector stateProtector)
    {
        var token = context.Request.Cookies[RegistrationCookieName];
        var state = token is null ? null : stateProtector.Unprotect(token);
        if (state is null)
        {
            return Error(StatusCodes.Status400BadRequest, InvalidRegistrationState);
        }

        var user = await userManager.FindByIdAsync(state.UserId);
        if (user?.PasskeyRegistrationExpiresAt is not { } pendingExpiry || pendingExpiry <= DateTimeOffset.UtcNow)
        {
            return Error(StatusCodes.Status400BadRequest, InvalidRegistrationState);
        }

        var request = await ReadRequestAsync<CredentialRequest>(context.Request);
        if (request is null || request.Credential.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return Error(StatusCodes.Status400BadRequest, InvalidRegistrationState);
        }

        PasskeyAttestationResult attestation;
        try
        {
            attestation = await signInManager.PerformPasskeyAttestationAsync(request.Credential.GetRawText());
        }
        catch (Exception exception) when (IsInvalidCeremonyInput(exception))
        {
            return Error(StatusCodes.Status400BadRequest, InvalidRegistrationState);
        }

        if (!attestation.Succeeded ||
            !string.Equals(attestation.UserEntity.Id, user.Id, StringComparison.Ordinal))
        {
            return Error(StatusCodes.Status400BadRequest, InvalidRegistrationState);
        }

        if (!(await userManager.AddOrUpdatePasskeyAsync(user, attestation.Passkey)).Succeeded)
        {
            return Error(StatusCodes.Status400BadRequest, InvalidRegistrationState);
        }

        user.PasskeyRegistrationExpiresAt = null;
        if (!(await userManager.UpdateAsync(user)).Succeeded)
        {
            return Error(StatusCodes.Status400BadRequest, InvalidRegistrationState);
        }

        context.Response.Cookies.Delete(RegistrationCookieName, RegistrationCookieOptions());
        await signInManager.SignInAsync(user, isPersistent: false);
        return Results.Ok();
    }

    private static async Task<IResult> CreateLoginOptionsAsync(
        HttpRequest httpRequest,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOptions<PasskeySettings> settingsOptions)
    {
        var request = await ReadRequestAsync<EmailRequest>(httpRequest);
        var email = NormalizeAndValidateEmail(request?.Email, settingsOptions.Value.MaxDisplayNameLength);
        if (email is null)
        {
            return Error(StatusCodes.Status400BadRequest, InvalidEmail);
        }

        var normalizedEmail = userManager.NormalizeEmail(email);
        var user = await userManager.Users.SingleOrDefaultAsync(candidate => candidate.NormalizedEmail == normalizedEmail);
        if (user is null || (await userManager.GetPasskeysAsync(user)).Count == 0)
        {
            return Error(StatusCodes.Status401Unauthorized, AuthenticationFailed);
        }

        var optionsJson = await signInManager.MakePasskeyRequestOptionsAsync(user);
        return Results.Content(optionsJson, "application/json");
    }

    private static async Task<IResult> CompleteLoginAsync(
        HttpRequest httpRequest,
        SignInManager<ApplicationUser> signInManager)
    {
        var request = await ReadRequestAsync<CredentialRequest>(httpRequest);
        if (request is null || request.Credential.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return Error(StatusCodes.Status400BadRequest, AuthenticationFailed);
        }

        SignInResult signInResult;
        try
        {
            signInResult = await signInManager.PasskeySignInAsync(request.Credential.GetRawText());
        }
        catch (Exception exception) when (IsInvalidCeremonyInput(exception))
        {
            return Error(StatusCodes.Status400BadRequest, AuthenticationFailed);
        }

        return signInResult.Succeeded
            ? Results.Ok()
            : Error(StatusCodes.Status401Unauthorized, AuthenticationFailed);
    }

    private static string? NormalizeAndValidateEmail(string? email, int maxDisplayNameLength)
    {
        var normalized = email?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ||
               normalized.Length > maxDisplayNameLength ||
               !new EmailAddressAttribute().IsValid(normalized)
            ? null
            : normalized;
    }

    private static bool IsInvalidCeremonyInput(Exception exception) =>
        exception is ArgumentException or InvalidOperationException or JsonException or FormatException;

    private static async ValueTask<T?> ReadRequestAsync<T>(HttpRequest request)
        where T : class
    {
        if (request.ContentLength == 0)
        {
            return null;
        }

        try
        {
            // Manual boundary parsing keeps malformed bodies inside the stable API error contract.
            return await request.ReadFromJsonAsync<T>(request.HttpContext.RequestAborted);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static CookieOptions RegistrationCookieOptions(DateTimeOffset? expiresAt = null) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
        Path = "/",
        Expires = expiresAt,
    };

    private static IResult Error(int statusCode, ApiError error) => Results.Json(error, statusCode: statusCode);
}
