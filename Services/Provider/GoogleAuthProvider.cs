using BE_ZSM.Enums;
using BE_ZSM.Exceptions;
using BE_ZSM.Services.Auth.Models;
using Google.Apis.Auth;
using System.Text.Json.Serialization;

namespace BE_ZSM.Services.Provider;

public class GoogleAuthProvider : IExternalAuthProvider
{
    private readonly HttpClient _httpClient;

    public GoogleAuthProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public AuthProvider Provider => AuthProvider.Google;

    public async Task<ExternalUserInfo> ValidateAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new UnauthorizedException("Invalid Google token", "INVALID_GOOGLE_TOKEN");

        var externalUser = IsJwt(token)
            ? await ValidateGoogleIdTokenAsync(token)
            : await ValidateGoogleAccessTokenAsync(token);

        if (!externalUser.EmailVerified)
            throw new UnauthorizedException("Google email is not verified", "GOOGLE_EMAIL_NOT_VERIFIED");

        return externalUser;
    }

    private static async Task<ExternalUserInfo> ValidateGoogleIdTokenAsync(string token)
    {
        var clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");

        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("GOOGLE_CLIENT_ID is not configured.");

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                token,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [clientId]
                });

            return new ExternalUserInfo
            {
                ProviderUserId = payload.Subject,
                Email = payload.Email,
                DisplayName = payload.Name,
                AvatarUrl = payload.Picture,
                EmailVerified = payload.EmailVerified
            };
        }
        catch (InvalidJwtException)
        {
            throw new UnauthorizedException("Invalid Google token", "INVALID_GOOGLE_TOKEN");
        }
    }

    private async Task<ExternalUserInfo> ValidateGoogleAccessTokenAsync(string token)
    {
        var clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");

        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("GOOGLE_CLIENT_ID is not configured.");

        try
        {
            var escapedToken = Uri.EscapeDataString(token);
            var tokenInfo = await _httpClient.GetFromJsonAsync<GoogleTokenInfoResponse>(
                $"https://oauth2.googleapis.com/tokeninfo?access_token={escapedToken}");

            if (tokenInfo == null || !tokenInfo.IsIssuedFor(clientId))
                throw new UnauthorizedException("Invalid Google token", "INVALID_GOOGLE_TOKEN");

            var userInfo = await _httpClient.GetFromJsonAsync<GoogleUserInfoResponse>(
                $"https://www.googleapis.com/oauth2/v3/userinfo?access_token={escapedToken}");

            if (userInfo == null
                || string.IsNullOrWhiteSpace(userInfo.Subject)
                || string.IsNullOrWhiteSpace(userInfo.Email))
            {
                throw new UnauthorizedException("Invalid Google token", "INVALID_GOOGLE_TOKEN");
            }

            return new ExternalUserInfo
            {
                ProviderUserId = userInfo.Subject,
                Email = userInfo.Email,
                DisplayName = userInfo.Name,
                AvatarUrl = userInfo.Picture,
                EmailVerified = userInfo.EmailVerified
            };
        }
        catch (UnauthorizedException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new UnauthorizedException("Invalid Google token", "INVALID_GOOGLE_TOKEN");
        }
    }

    private static bool IsJwt(string token)
    {
        return token.Count(x => x == '.') == 2;
    }

    private sealed class GoogleTokenInfoResponse
    {
        [JsonPropertyName("aud")]
        public string? Audience { get; set; }

        [JsonPropertyName("audience")]
        public string? LegacyAudience { get; set; }

        [JsonPropertyName("azp")]
        public string? AuthorizedParty { get; set; }

        [JsonPropertyName("issued_to")]
        public string? IssuedTo { get; set; }

        public bool IsIssuedFor(string clientId)
        {
            return string.Equals(Audience, clientId, StringComparison.Ordinal)
                || string.Equals(LegacyAudience, clientId, StringComparison.Ordinal)
                || string.Equals(AuthorizedParty, clientId, StringComparison.Ordinal)
                || string.Equals(IssuedTo, clientId, StringComparison.Ordinal);
        }
    }

    private sealed class GoogleUserInfoResponse
    {
        [JsonPropertyName("sub")]
        public string? Subject { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("email_verified")]
        public bool EmailVerified { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("picture")]
        public string? Picture { get; set; }
    }
}
