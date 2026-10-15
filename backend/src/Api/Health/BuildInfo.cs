using System.Reflection;

namespace RecruitOps.Api.Health;

/// <summary>What this build is, read once from the assembly. The SDK writes
/// <c>&lt;Version&gt;+&lt;SourceRevisionId&gt;</c> into the informational version; locally the
/// revision comes from git automatically, and the Docker build passes it as <c>GIT_SHA</c>
/// because <c>.git</c> sits outside the build context.</summary>
public static class BuildInfo
{
    public static readonly string Version;
    public static readonly string? Commit;

    static BuildInfo()
    {
        var informational = typeof(BuildInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

        var plus = informational.IndexOf('+');
        Version = plus < 0 ? informational : informational[..plus];
        Commit = plus < 0 || plus == informational.Length - 1 ? null : informational[(plus + 1)..];
    }
}
