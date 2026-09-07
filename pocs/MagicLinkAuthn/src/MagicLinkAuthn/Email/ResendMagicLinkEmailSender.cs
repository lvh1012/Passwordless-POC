using MagicLinkAuthn.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MagicLinkAuthn.Email;

public sealed class ResendMagicLinkEmailSender : IMagicLinkEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly ResendSettings _settings;
    private readonly ILogger<ResendMagicLinkEmailSender> _logger;

    public ResendMagicLinkEmailSender(
        HttpClient httpClient,
        IOptions<ResendSettings> settings,
        ILogger<ResendMagicLinkEmailSender> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> SendAsync(
        string recipient,
        Uri magicLink,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var encodedLink = HtmlEncoder.Default.Encode(magicLink.AbsoluteUri);
        var payload = new ResendEmailRequest(
            _settings.From,
            [recipient],
            "Your secure sign-in link",
            $"<p>Use the link below to sign in. It expires soon and can be used once.</p><p><a href=\"{encodedLink}\">Sign in securely</a></p><p>If you did not request this email, ignore it.</p>",
            $"Use this one-time link to sign in:\n\n{magicLink.AbsoluteUri}\n\nIf you did not request this email, ignore it.");

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        request.Headers.Add("Idempotency-Key", $"magic-link/{requestId:N}");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new EmailDeliveryException("The email provider could not be reached.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EmailDeliveryException("The email provider timed out.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Resend rejected magic-link request {RequestId} with HTTP status {StatusCode}.",
                    requestId,
                    (int)response.StatusCode);
                throw new EmailDeliveryException("The email provider rejected the request.");
            }

            ResendEmailResponse result;
            try
            {
                result = await response.Content.ReadFromJsonAsync<ResendEmailResponse>(cancellationToken)
                    ?? throw new EmailDeliveryException("The email provider returned an invalid response.");
            }
            catch (JsonException)
            {
                throw new EmailDeliveryException("The email provider returned an invalid response.");
            }
            if (string.IsNullOrWhiteSpace(result.Id) || result.Id.Length > 128)
            {
                throw new EmailDeliveryException("The email provider returned an invalid message identifier.");
            }

            return result.Id;
        }
    }

    private sealed record ResendEmailRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html,
        [property: JsonPropertyName("text")] string Text);

    private sealed record ResendEmailResponse([property: JsonPropertyName("id")] string Id);
}
