using MagicLinkAuthn.Configuration;
using MagicLinkAuthn.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using Xunit;

namespace MagicLinkAuthn.Tests.Email;

public sealed class ResendMagicLinkEmailSenderTests
{
    [Fact]
    public async Task SendAsync_UsesBearerAuthenticationAndStableIdempotencyKey()
    {
        var requestId = Guid.Parse("2c4ad80d-f627-402f-a573-5ba9a5ee6197");
        var handler = new CapturingHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };
        var sender = new ResendMagicLinkEmailSender(
            client,
            Options.Create(new ResendSettings
            {
                ApiKey = "re_test_value",
                From = "Magic Link <login@example.test>"
            }),
            NullLogger<ResendMagicLinkEmailSender>.Instance);

        var result = await sender.SendAsync(
            "person@example.test",
            new Uri("https://example.test/magic-link/callback#token=opaque"),
            requestId,
            CancellationToken.None);

        Assert.Equal("email-id", result);
        Assert.Equal("Bearer", handler.Request!.Headers.Authorization!.Scheme);
        Assert.Equal("re_test_value", handler.Request.Headers.Authorization.Parameter);
        Assert.Equal("magic-link/2c4ad80df627402fa5735ba9a5ee6197", handler.Request.Headers.GetValues("Idempotency-Key").Single());
        Assert.Contains("person@example.test", handler.Body, StringComparison.Ordinal);
        Assert.Contains("#token=opaque", handler.Body, StringComparison.Ordinal);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"email-id\"}", Encoding.UTF8, "application/json")
            };
        }
    }
}
