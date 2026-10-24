using RecruitOps.Infrastructure.Options;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>The advert link the SPA shows a recruiter to share. It used to be built in the browser as
/// <c>{host}:3000/jobs/{token}</c>, right only in the dev compose stack: production serves <c>/jobs/</c> on the
/// SPA's own origin and publishes no port 3000.</summary>
public class PublicSiteOptionsTests
{
    [Theory]
    [InlineData("https://careers.example.com", "https://careers.example.com/jobs/abc123")]
    [InlineData("https://careers.example.com/", "https://careers.example.com/jobs/abc123")]
    [InlineData("  http://localhost:3000  ", "http://localhost:3000/jobs/abc123")]
    [InlineData("https://example.com/careers", "https://example.com/careers/jobs/abc123")]
    public void A_Configured_Site_Gives_The_Advert_Its_Address(string baseUrl, string expected) =>
        Assert.Equal(expected, new PublicSiteOptions { BaseUrl = baseUrl }.JobUrl("abc123"));

    [Theory]
    [InlineData("")]
    [InlineData("careers.example.com")]          // not absolute
    [InlineData("ftp://careers.example.com")]    // not http(s)
    public void No_Usable_Site_Means_No_Address(string baseUrl) =>
        Assert.Null(new PublicSiteOptions { BaseUrl = baseUrl }.JobUrl("abc123"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void An_Unpublished_Posting_Has_No_Address(string? token) =>
        Assert.Null(new PublicSiteOptions { BaseUrl = "https://careers.example.com" }.JobUrl(token));
}
