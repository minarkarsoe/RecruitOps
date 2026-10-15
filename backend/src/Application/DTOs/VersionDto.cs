namespace RecruitOps.Application.DTOs;

/// <summary>Which build an install is running — the input to the customer/version registry
/// ADR-0004 requires ("we always know who runs what").</summary>
/// <param name="Commit">Null when the build was not given one (e.g. a local Docker build without
/// <c>GIT_SHA</c>). Shown as missing rather than guessed.</param>
public record VersionDto(string Version, string? Commit, string Environment);
