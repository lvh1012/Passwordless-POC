using MagicLinkAuthn.Configuration;
using MagicLinkAuthn.Data;
using MagicLinkAuthn.Email;
using MagicLinkAuthn.Endpoints;
using MagicLinkAuthn.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<MagicLinkTokenService>();
builder.Services.AddSingleton<MagicLinkOutboxProtector>();
builder.Services.AddScoped<MagicLinkDeliveryService>();
builder.Services.AddScoped<MagicLinkService>();
builder.Services.AddScoped<UserProfileService>();
builder.Services.AddScoped<AntiforgeryEndpointFilter>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Render is the trusted ingress: the container port is not publicly reachable. Process only the
    // nearest hop so client-supplied entries earlier in an X-Forwarded-For chain are never trusted.
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var magicLinkOptions = builder.Services.AddOptions<MagicLinkSettings>()
    .Bind(builder.Configuration.GetSection(MagicLinkSettings.SectionName));
var resendOptions = builder.Services.AddOptions<ResendSettings>()
    .Bind(builder.Configuration.GetSection(ResendSettings.SectionName));

if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddSingleton<IValidateOptions<MagicLinkSettings>, ProductionConfigurationValidator>();
    builder.Services.AddSingleton<IValidateOptions<ResendSettings>, ProductionConfigurationValidator>();
    magicLinkOptions.ValidateOnStart();
    resendOptions.ValidateOnStart();
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = true;
    })
    .AddSignInManager()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.Redirect("/");
        return Task.CompletedTask;
    };
});

builder.Services.AddDataProtection().PersistKeysToDbContext<ApplicationDbContext>();
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(MagicLinkEndpointExtensions.RequestRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? IPAddress.None.ToString(),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<DevelopmentOutbox>();
    builder.Services.AddSingleton<IMagicLinkEmailSender, DevelopmentMagicLinkEmailSender>();
}
else
{
    builder.Services.AddHttpClient<IMagicLinkEmailSender, ResendMagicLinkEmailSender>(client =>
    {
        client.BaseAddress = new Uri("https://api.resend.com/");
        client.Timeout = TimeSpan.FromSeconds(10);
    });
}

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<MagicLinkOutboxWorker>();
}

var app = builder.Build();

if (!EF.IsDesignTime)
{
    _ = app.Services.GetRequiredService<IOptions<MagicLinkSettings>>().Value;
    _ = app.Services.GetRequiredService<IOptions<ResendSettings>>().Value;

    await using var scope = app.Services.CreateAsyncScope();
    await DatabaseInitializer.InitializeAsync(scope.ServiceProvider, app.Lifetime.ApplicationStopping);
}

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";
    if (context.Request.Path.StartsWithSegments("/magic-link"))
    {
        context.Response.Headers["Cache-Control"] = "no-store";
    }
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<ProfileCompletionMiddleware>();
app.UseAuthorization();
app.UseAntiforgery();

app.MapHealthChecks("/health");
app.MapMagicLinkEndpoints();
if (app.Environment.IsDevelopment())
{
    app.MapDevelopmentOutbox();
}
app.MapRazorPages();

app.Run();

public partial class Program { }
