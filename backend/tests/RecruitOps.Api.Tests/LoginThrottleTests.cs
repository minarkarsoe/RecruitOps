using System.Net;
using System.Net.Http.Json;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>Brute-force protection on the one anonymous endpoint that verifies a secret.
///
/// <para>Its own factory instance, because the throttle is a singleton keyed by email: sharing
/// a factory with <see cref="AuthLoginTests"/> would let one class's deliberate failures lock
/// out the other class's happy path, and the failure would depend on execution order.</para>
/// </summary>
public class LoginThrottleTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public LoginThrottleTests(CustomWebAppFactory factory) => _factory = factory;

    private Task<HttpResponseMessage> AttemptAsync(string email, string password) =>
        _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = password });

    [Fact]
    public async Task Repeated_Failures_Lock_The_Account_Out()
    {
        const string email = "throttle.target@alpha.test";

        // The threshold is 5, so the first five are ordinary 401s.
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await AttemptAsync(email, "wrong")).StatusCode);

        var blocked = await AttemptAsync(email, "wrong");
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        // A client that can't tell how long to wait will simply keep hammering.
        Assert.True(blocked.Headers.TryGetValues("Retry-After", out _));
    }

    [Fact]
    public async Task Lockout_Does_Not_Reveal_Whether_The_Account_Exists()
    {
        // An unknown address must lock out exactly like a real one. If only real accounts
        // could reach 429, the status code would become an existence oracle and undo the
        // enumeration protection that the identical 401s exist to provide.
        const string unknown = "definitely.not.a.user@nowhere.test";

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await AttemptAsync(unknown, "wrong")).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await AttemptAsync(unknown, "wrong")).StatusCode);
    }

    [Fact]
    public async Task One_Locked_Account_Does_Not_Affect_Another()
    {
        const string victim = "throttle.noisy@alpha.test";

        for (var i = 0; i < 6; i++) await AttemptAsync(victim, "wrong");
        Assert.Equal(HttpStatusCode.TooManyRequests, (await AttemptAsync(victim, "wrong")).StatusCode);

        // The admin has not failed, so they must still be able to sign in — otherwise the
        // control is a self-inflicted outage rather than a defence.
        var admin = await AttemptAsync(CustomWebAppFactory.AdminEmail, CustomWebAppFactory.AdminPassword);
        admin.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_Successful_Login_Clears_Earlier_Failures()
    {
        // Two typos then success must not leave the user one slip from a lockout.
        for (var i = 0; i < 4; i++)
            await AttemptAsync(CustomWebAppFactory.AdminEmail, "wrong");

        var success = await AttemptAsync(CustomWebAppFactory.AdminEmail, CustomWebAppFactory.AdminPassword);
        success.EnsureSuccessStatusCode();

        // Counter reset: four more failures must still be 401, not 429.
        for (var i = 0; i < 4; i++)
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await AttemptAsync(CustomWebAppFactory.AdminEmail, "wrong")).StatusCode);
    }
}
