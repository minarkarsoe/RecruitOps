using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RecruitOps.Api.Auth;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>
/// Marks a test that reads repository-level files (<c>docker-compose.yml</c>, <c>.env</c>) rather
/// than files inside the backend project.
///
/// <para>Those files sit outside the backend image's build context by design (ADR-0015), so a
/// test that needs them cannot run inside the packaged image — which is where CI runs this suite.
/// Setting <c>Skip</c> at discovery time makes that a <b>reported skip with a reason</b> rather
/// than a failure, and rather than the worse option of a test that passes while asserting
/// nothing.</para>
///
/// <para>xUnit 2.9 has no <c>SkipUnless</c>/<c>Assert.Skip</c> — both arrived in v3 — so
/// subclassing <see cref="FactAttribute"/> is the supported way to skip conditionally here.</para>
/// </summary>
public sealed class RepoScopeFactAttribute : FactAttribute
{
    public RepoScopeFactAttribute()
    {
        if (!ChallengerM12ConfigPrecedenceTests.RunningFromCheckout)
        {
            Skip = "Repo-scope check: docker-compose.yml and .env sit outside the backend build "
                 + "context (ADR-0015) and cannot be read from inside the packaged image. "
                 + "Runs on a developer checkout.";
        }
    }
}

/// <summary>
/// challenger_m1_2 — milestone 1, remit: CONFIGURATION PRECEDENCE. Which value wins, where.
///
/// <para>challenger_m1_1 proves each shipped JSON file binds to 60/120 <em>in isolation</em>
/// (<c>AddJsonFile(path)</c>, one file at a time). That cannot catch a precedence bug: the
/// original defect was exactly that <c>appsettings.Development.json</c> — the file
/// <c>docker-compose.yml</c> actually uses, because the backend service runs with
/// <c>ASPNETCORE_ENVIRONMENT: Development</c> — silently overrode a corrected base file.</para>
///
/// <para>These tests layer the files the way the real host layers them
/// (<c>appsettings.json</c> then <c>appsettings.{Environment}.json</c> then env vars) and
/// assert the EFFECTIVE value per environment, plus the compiled-in default that applies when
/// no section ships at all.</para>
/// </summary>
public class ChallengerM12ConfigPrecedenceTests
{
    // ⚠️ These paths were rewritten on 2026-08-28 because the whole class had been failing in
    // CI — all ten tests, on every run since at least 2026-08-25.
    //
    // The old lookup walked up from `AppContext.BaseDirectory` searching for
    // `backend/RecruitOps.sln`, which only ever exists in a git checkout. CI runs this suite
    // inside the backend image (`docker build --target test ./backend`), where the build context
    // IS `backend/`, so the source lands at `/src` and there is no `backend/` directory anywhere
    // above it. The walk ran off the top of the filesystem, `Assert.NotNull(dir)` failed, and
    // every test in the class died before reaching its own assertions.
    //
    // Locally they passed, so the failure looked like CI being broken rather than the tests
    // being wrong about where they were. Same shape as the `@types/node` build divergence fixed
    // the same day: a green local run proving nothing about the packaged artefact.

    /// <summary>
    /// The directory containing <c>RecruitOps.sln</c>. That is <c>&lt;repo&gt;/backend</c> in a
    /// checkout and <c>/src</c> inside the image — the two layouts this suite must run in.
    /// </summary>
    private static readonly string SolutionRoot = FindAncestorContaining("RecruitOps.sln")
        ?? throw new InvalidOperationException(
            $"RecruitOps.sln not found above '{AppContext.BaseDirectory}'. The test project has " +
            "moved relative to the solution, or the image layout changed.");

    private static string ApiDir => Path.Combine(SolutionRoot, "src", "Api");

    /// <summary>
    /// True when the suite is running from a git checkout rather than from inside the packaged
    /// backend image. The discriminator is the solution root's own name: a checkout puts the
    /// solution in <c>backend/</c>, the image copies it to <c>/src</c>.
    /// </summary>
    /// <remarks>Public because <see cref="RepoScopeFactAttribute"/> reads it at discovery.</remarks>
    public static bool RunningFromCheckout =>
        string.Equals(Path.GetFileName(SolutionRoot), "backend", StringComparison.Ordinal);

    /// <summary>
    /// The repository root — the directory holding <c>docker-compose.yml</c>. Only meaningful in
    /// a checkout: the backend image's build context is <c>./backend</c> by design (ADR-0015),
    /// so repo-level files are structurally outside it and copying them in would put repo
    /// configuration, and potentially a <c>.env</c>, into a runtime image.
    /// </summary>
    private static string RepoRoot
    {
        get
        {
            Assert.True(RunningFromCheckout,
                "RepoRoot is not reachable from inside the packaged image — mark the test " +
                "[RepoScopeFact] instead of [Fact].");

            var root = Directory.GetParent(SolutionRoot)!.FullName;

            // Fail loudly rather than skip if a checkout has somehow lost the file: a silent
            // skip on a developer machine is how a repo-scope guard stops guarding.
            Assert.True(File.Exists(Path.Combine(root, "docker-compose.yml")),
                $"'{root}' looks like a checkout but has no docker-compose.yml.");

            return root;
        }
    }

    private static string? FindAncestorContaining(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, fileName)))
        {
            dir = dir.Parent;
        }

        return dir?.FullName;
    }

    /// <summary>Reproduces the default host layering for a given environment name.</summary>
    private static (LoginRateLimitOptions Login, PublicApplyRateLimitOptions Apply) Resolve(
        string environmentName,
        IDictionary<string, string?>? environmentVariables = null)
    {
        var cfgBuilder = new ConfigurationBuilder()
            .SetBasePath(ApiDir)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true);

        if (environmentVariables is not null)
        {
            // Environment variables sit ABOVE both JSON files in the real host.
            cfgBuilder.AddInMemoryCollection(environmentVariables);
        }

        var cfg = cfgBuilder.Build();

        var services = new ServiceCollection();
        services.AddOptions();
        // Verbatim from backend/src/Api/Program.cs lines 85-86.
        services.Configure<LoginRateLimitOptions>(cfg.GetSection("RateLimit:Login"));
        services.Configure<PublicApplyRateLimitOptions>(cfg.GetSection("RateLimit:PublicApply"));
        using var sp = services.BuildServiceProvider();

        return (sp.GetRequiredService<IOptions<LoginRateLimitOptions>>().Value,
                sp.GetRequiredService<IOptions<PublicApplyRateLimitOptions>>().Value);
    }

    /// <summary>
    /// Production (what you get with ASPNETCORE_ENVIRONMENT unset) and Development (what
    /// docker compose runs) must BOTH land on 60/120. Development is the one that shipped wrong.
    /// </summary>
    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    [InlineData("Staging")]
    public void EffectiveLimits_AreRestoredValues_InEveryEnvironment(string environmentName)
    {
        var (login, apply) = Resolve(environmentName);

        Assert.Equal(60, login.PermitLimit);
        Assert.Equal(60, login.WindowSeconds);
        Assert.Equal(120, apply.PermitLimit);
        Assert.Equal(60, apply.WindowSeconds);
    }

    /// <summary>
    /// The compiled-in defaults — the value in force if a deployment ever ships without the
    /// RateLimit section. Asserted through the real DI <c>Configure</c> path against an EMPTY
    /// configuration, not just via <c>new()</c>, because that is what Program.cs does.
    /// </summary>
    [Fact]
    public void WithNoRateLimitSectionAtAll_CompiledInDefaultsApply()
    {
        var empty = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<LoginRateLimitOptions>(empty.GetSection("RateLimit:Login"));
        services.Configure<PublicApplyRateLimitOptions>(empty.GetSection("RateLimit:PublicApply"));
        using var sp = services.BuildServiceProvider();

        Assert.Equal(60, sp.GetRequiredService<IOptions<LoginRateLimitOptions>>().Value.PermitLimit);
        Assert.Equal(120, sp.GetRequiredService<IOptions<PublicApplyRateLimitOptions>>().Value.PermitLimit);
    }

    /// <summary>
    /// Documents the precedence rule itself: an environment variable beats BOTH JSON files.
    /// This is the layer that would silently defeat the whole milestone if docker-compose.yml
    /// (or a customer's install) ever set RateLimit__Login__PermitLimit.
    /// </summary>
    [Fact]
    public void EnvironmentVariable_Overrides_BothJsonFiles()
    {
        var (login, apply) = Resolve("Development", new Dictionary<string, string?>
        {
            ["RateLimit:Login:PermitLimit"] = "10",
            ["RateLimit:PublicApply:PermitLimit"] = "10",
        });

        Assert.Equal(10, login.PermitLimit);
        Assert.Equal(10, apply.PermitLimit);
    }

    /// <summary>
    /// The guard for the rule above: no compose service, and no .env alongside it, may set a
    /// RateLimit override. If one is ever added this test names it.
    /// </summary>
    [RepoScopeFact]
    public void DockerCompose_And_DotEnv_SetNoRateLimitOverride()
    {
        foreach (var file in new[] { "docker-compose.yml", ".env", ".env.example" })
        {
            var path = Path.Combine(RepoRoot, file);
            if (!File.Exists(path))
            {
                continue;
            }

            var text = File.ReadAllText(path);
            Assert.False(text.Contains("RateLimit__", StringComparison.OrdinalIgnoreCase),
                $"{file} sets a RateLimit__* environment variable. Environment variables beat " +
                "both appsettings files, so this silently overrides the restored 60/120 limits.");
        }
    }

    /// <summary>
    /// docker-compose.yml runs the backend as Development. That is WHY
    /// appsettings.Development.json had to be fixed too. If this assumption ever stops holding
    /// the reasoning behind the four-file fix stops holding with it.
    /// </summary>
    [RepoScopeFact]
    public void DockerCompose_RunsBackendAsDevelopment()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot, "docker-compose.yml"));
        Assert.Contains("ASPNETCORE_ENVIRONMENT: Development", text);
    }

    /// <summary>
    /// There is no appsettings.Production.json today. If one is ever added it becomes the
    /// highest-precedence JSON layer for a production install and must carry the restored
    /// values (or no RateLimit section at all, which falls back to the compiled-in defaults).
    /// </summary>
    [Fact]
    public void IfAProductionAppSettingsFileExists_ItDoesNotLowerTheLimits()
    {
        var path = Path.Combine(ApiDir, "appsettings.Production.json");
        if (!File.Exists(path))
        {
            return; // nothing to enforce yet — the Production layer is simply absent
        }

        var (login, apply) = Resolve("Production");
        Assert.Equal(60, login.PermitLimit);
        Assert.Equal(120, apply.PermitLimit);
    }

    /// <summary>
    /// launchSettings.json is the F5 path a developer uses. It selects Development, so a
    /// developer sees the same values docker compose does.
    /// </summary>
    [Fact]
    public void LaunchSettings_SelectsDevelopment_AndSetsNoRateLimitOverride()
    {
        var path = Path.Combine(ApiDir, "Properties", "launchSettings.json");
        Assert.True(File.Exists(path), $"Missing {path}");

        var text = File.ReadAllText(path);
        Assert.Contains("\"ASPNETCORE_ENVIRONMENT\": \"Development\"", text);
        Assert.DoesNotContain("RateLimit", text);
    }

    /// <summary>
    /// Both RateLimit sections carry a "// note" key. The binder must ignore it rather than
    /// throw — otherwise the section fails to bind and the limits silently revert to defaults
    /// (which happen to be correct today, and would therefore hide the failure).
    /// </summary>
    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Development.json")]
    public void CommentKeyInSection_IsPresent_AndDoesNotBreakBinding(string fileName)
    {
        var cfg = new ConfigurationBuilder()
            .SetBasePath(ApiDir)
            .AddJsonFile(fileName)
            .Build();

        Assert.False(string.IsNullOrWhiteSpace(cfg["RateLimit:Login:// note"]),
            $"{fileName} lost the explanatory note on RateLimit:Login.");

        var login = new LoginRateLimitOptions { PermitLimit = -1, WindowSeconds = -1 };
        cfg.GetSection("RateLimit:Login").Bind(login);
        Assert.Equal(60, login.PermitLimit);
        Assert.Equal(60, login.WindowSeconds);
    }
}
