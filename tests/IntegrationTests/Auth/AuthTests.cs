using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.BearerToken;
using VideoProcessor.IntegrationTests.Harness;

namespace VideoProcessor.IntegrationTests.Auth;

[Collection(AppCollection.Name)]
public sealed class AuthTests(AppFixture app)
{
    private const string Password = "Correct-horse-1";

    [Fact]
    public async Task Registered_user_can_log_in_and_call_a_protected_endpoint()
    {
        var client = app.CreateClient();
        var email = UniqueEmail();

        var register = await client.PostAsJsonAsync("/auth/register", new { email, password = Password });
        register.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokens = await LogIn(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var info = await client.GetFromJsonAsync<UserInfo>("/auth/manage/info");
        info!.Email.Should().Be(email);
    }

    [Fact]
    public async Task Refreshed_token_can_call_a_protected_endpoint()
    {
        var client = app.CreateClient();
        var email = UniqueEmail();
        await client.PostAsJsonAsync("/auth/register", new { email, password = Password });
        var tokens = await LogIn(client, email);

        var refresh = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = tokens.RefreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshed = await refresh.Content.ReadFromJsonAsync<AccessTokenResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshed!.AccessToken);
        var info = await client.GetAsync("/auth/manage/info");
        info.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Log_in_with_a_wrong_password_is_refused()
    {
        var client = app.CreateClient();
        var email = UniqueEmail();
        await client.PostAsJsonAsync("/auth/register", new { email, password = Password });

        var login = await client.PostAsJsonAsync("/auth/login", new { email, password = "Wrong-password-1" });

        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Protected_endpoint_without_a_token_returns_401()
    {
        var client = app.CreateClient();

        var response = await client.GetAsync("/auth/manage/info");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static async Task<AccessTokenResponse> LogIn(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync("/auth/login", new { email, password = Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await login.Content.ReadFromJsonAsync<AccessTokenResponse>())!;
    }

    private sealed record UserInfo(string Email);
}
