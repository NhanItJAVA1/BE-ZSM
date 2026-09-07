using System.Net;
using System.Text.Json;
using BE_ZSM.Exceptions;
using BE_ZSM.Services.Provider;

namespace BE_ZSM.Tests;

public sealed class GoogleAuthProviderTests
{
    private const string ClientId = "test-google-client-id.apps.googleusercontent.com";

    [Theory]
    [InlineData("aud")]
    [InlineData("audience")]
    [InlineData("azp")]
    [InlineData("issued_to")]
    public async Task ValidateAsync_AcceptsGoogleAccessTokenAudienceAliases(string audienceField)
    {
        Environment.SetEnvironmentVariable("GOOGLE_CLIENT_ID", ClientId);

        using var httpClient = new HttpClient(new GoogleTokenHandler(audienceField, ClientId));
        var provider = new GoogleAuthProvider(httpClient);

        var user = await provider.ValidateAsync("google-access-token");

        Assert.Equal("google-subject", user.ProviderUserId);
        Assert.Equal("alice@example.test", user.Email);
        Assert.True(user.EmailVerified);
    }

    [Fact]
    public async Task ValidateAsync_RejectsGoogleAccessTokenWithDifferentAudience()
    {
        Environment.SetEnvironmentVariable("GOOGLE_CLIENT_ID", ClientId);

        using var httpClient = new HttpClient(new GoogleTokenHandler("audience", "other-client-id"));
        var provider = new GoogleAuthProvider(httpClient);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => provider.ValidateAsync("google-access-token"));

        Assert.Equal("INVALID_GOOGLE_TOKEN", exception.ErrorCode);
    }

    private sealed class GoogleTokenHandler : HttpMessageHandler
    {
        private readonly string _audienceField;
        private readonly string _audienceValue;

        public GoogleTokenHandler(string audienceField, string audienceValue)
        {
            _audienceField = audienceField;
            _audienceValue = audienceValue;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var host = request.RequestUri?.Host;
            var body = host switch
            {
                "oauth2.googleapis.com" => JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    [_audienceField] = _audienceValue
                }),
                "www.googleapis.com" => JsonSerializer.Serialize(new
                {
                    sub = "google-subject",
                    email = "alice@example.test",
                    email_verified = true,
                    name = "Alice",
                    picture = "https://example.test/alice.png"
                }),
                _ => throw new InvalidOperationException($"Unexpected host: {host}")
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            };

            return Task.FromResult(response);
        }
    }
}
