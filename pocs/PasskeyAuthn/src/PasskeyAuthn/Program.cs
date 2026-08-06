using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PasskeyAuthn.Configuration;
using PasskeyAuthn.Data;
using PasskeyAuthn.Endpoints;
using PasskeyAuthn.Security;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
    // Render terminates TLS on proxy addresses that are not stable enough to enumerate; only deploy this app behind Render's proxy.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var passkeySettings = builder.Configuration
    .GetSection(PasskeySettings.SectionName)
    .Get<PasskeySettings>() ?? new PasskeySettings();
var passkeyOptions = builder.Services.AddOptions<PasskeySettings>()
    .Bind(builder.Configuration.GetSection(PasskeySettings.SectionName));
if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
{
    // Deployed hosts must validate configuration before database initialization; local and integration hosts keep their localhost/test defaults usable.
    builder.Services.AddSingleton<IValidateOptions<PasskeySettings>, ProductionConfigurationValidator>();
    passkeyOptions.ValidateOnStart();
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
        // Version 3 enables Identity's built-in Passkey entity while retaining the approved context base type.
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddSignInManager()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.Configure<IdentityPasskeyOptions>(options =>
{
    options.ServerDomain = passkeySettings.ServerDomain;
    options.UserVerificationRequirement = "required";
    options.ResidentKeyRequirement = "preferred";
    options.AuthenticatorTimeout = TimeSpan.FromSeconds(passkeySettings.AuthenticatorTimeoutSeconds);
    options.ValidateOrigin = context =>
        ValueTask.FromResult(
            string.Equals(context.Origin, passkeySettings.ExpectedOrigin, StringComparison.OrdinalIgnoreCase));
});

builder.Services.ConfigureApplicationCookie(options =>
{
    // Protected Razor Pages must return users to the implemented Passkey login page.
    options.LoginPath = "/";
    // The POC login page has no return-url flow; avoid leaking the protected path into the query string.
    options.Events.OnRedirectToLogin = AuthenticationCookieEvents.RedirectToLoginAsync;
    options.Events.OnRedirectToAccessDenied = AuthenticationCookieEvents.RedirectToAccessDeniedAsync;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>();
builder.Services.AddSingleton<RegistrationStateProtector>();
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddRateLimiter(options =>
{
    // Public JSON callers need the standard throttling status rather than the middleware's service-unavailable default.
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter(PasskeyEndpointExtensions.RateLimitPolicy, limiter =>
    {
        limiter.PermitLimit = 20;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});

var app = builder.Build();

if (!EF.IsDesignTime)
{
    // Resolve validated options before migrations because WebApplication starts hosted startup validators only at Run().
    _ = app.Services.GetRequiredService<IOptions<PasskeySettings>>().Value;

    // EF tooling builds the host to discover the model; it must not contact PostgreSQL while generating migrations.
    await using var scope = app.Services.CreateAsyncScope();
    await DatabaseInitializer.InitializeAsync(scope.ServiceProvider, app.Lifetime.ApplicationStopping);
}

// The proxy must restore the original HTTPS scheme before middleware enforces HTTPS or writes transport-security headers.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapHealthChecks("/health");
app.MapPasskeyEndpoints();
app.MapAuthEndpoints();
app.MapRazorPages();

app.Run();

/// <summary>
/// Exposes the top-level web application entry point to ASP.NET Core integration tests.
/// </summary>
public partial class Program { }
