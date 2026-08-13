namespace RecruitOps.Api.Tests;

/// <summary>
/// The factory with the AI configuration a customer install actually gets: no API key, and
/// <c>EnableFallback: false</c>. Exists so the shipped default is covered by tests rather than
/// only the development-stub path, which is all <see cref="CustomWebAppFactory"/> exercises.
/// </summary>
public class NoAiFallbackWebAppFactory : CustomWebAppFactory
{
    protected override bool EnableAiFallback => false;
}
