using RecruitOps.Domain;
using Xunit;

namespace RecruitOps.Domain.Tests;

/// <summary>Module 3.4 — the parsing and escaping behind notes.
/// <para>Unit-tested here rather than only through the API because these are the two places
/// user-authored text can turn into something else: a forged mention, or markup that
/// executes in the SPA.</para></summary>
public class MentionParserTests
{
    private static IReadOnlyDictionary<string, MentionParser.MentionTarget> Resolved(
        params (string Handle, string Name)[] entries)
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        return entries.ToDictionary(
            e => e.Handle,
            e => new MentionParser.MentionTarget(id, e.Name));
    }

    [Theory]
    [InlineData("@sales.manager please review", "sales.manager")]
    [InlineData("cc @finance.manager", "finance.manager")]
    [InlineData("(@ko_ko) can you join", "ko_ko")]
    [InlineData("@a is a valid single-character handle", "a")]
    public void Finds_A_Handle(string body, string expected)
    {
        var handles = MentionParser.DistinctHandles(body);
        Assert.Contains(expected, handles);
    }

    [Theory]
    [InlineData("reach them at someone@example.com")]   // an email, not a mention
    [InlineData("priced at 100@ per unit")]              // @ after a digit
    [InlineData("no handle here")]
    [InlineData("")]
    public void Does_Not_Invent_A_Handle(string body)
    {
        Assert.Empty(MentionParser.DistinctHandles(body));
    }

    [Fact]
    public void Duplicate_Mentions_Collapse_To_One_Handle()
    {
        var handles = MentionParser.DistinctHandles("@sales.manager and again @Sales.Manager");

        // Case-insensitive matching, so tagging someone twice doesn't notify them twice.
        Assert.Single(handles);
    }

    [Fact]
    public void Escapes_Every_Character_That_Could_Become_Markup()
    {
        var html = MentionParser.ToSafeHtml(
            "<img src=x onerror=\"alert('1')\"> & done",
            Resolved());

        Assert.DoesNotContain("<img", html);
        Assert.Contains("&lt;img", html);
        Assert.Contains("&quot;", html);
        Assert.Contains("&#39;", html);
        Assert.Contains("&amp;", html);
    }

    [Fact]
    public void A_Resolved_Mention_Becomes_Markup_And_An_Unresolved_One_Stays_Text()
    {
        var html = MentionParser.ToSafeHtml(
            "@sales.manager and @nobody",
            Resolved(("sales.manager", "Sales Manager")));

        Assert.Contains("<span class=\"mention\"", html);
        Assert.Contains("@Sales Manager", html);

        // An unmatched handle is far more often an address than an error worth surfacing.
        Assert.Contains("@nobody", html);
        Assert.Equal(1, html.Split("<span").Length - 1);
    }

    [Fact]
    public void A_Display_Name_Containing_Markup_Is_Escaped_Too()
    {
        var html = MentionParser.ToSafeHtml(
            "@x hello",
            Resolved(("x", "<b>Injected</b>")));

        // The display name comes from the users table, which an admin controls — so it is
        // not attacker-supplied today. Escaping it anyway costs nothing and removes the
        // question entirely.
        Assert.DoesNotContain("<b>", html);
        Assert.Contains("&lt;b&gt;", html);
    }

    [Fact]
    public void Text_Around_A_Mention_Survives_Intact()
    {
        var html = MentionParser.ToSafeHtml(
            "before @x after",
            Resolved(("x", "Ko Ko")));

        Assert.StartsWith("before ", html);
        Assert.EndsWith(" after", html);
    }

    [Fact]
    public void An_Empty_Body_Renders_As_Empty()
    {
        Assert.Equal(string.Empty, MentionParser.ToSafeHtml(null, Resolved()));
        Assert.Equal(string.Empty, MentionParser.ToSafeHtml(string.Empty, Resolved()));
    }

    [Fact]
    public void Burmese_Text_Is_Left_Alone()
    {
        const string body = "ကိုယ်ရေးမှတ်တမ်း ကောင်းပါတယ် @x";
        var html = MentionParser.ToSafeHtml(body, Resolved(("x", "Ko Ko")));

        // Non-Latin text must pass through untouched — an escaper that mangles Burmese would
        // make the product unusable for its first customers (ADR-0009 territory).
        Assert.Contains("ကိုယ်ရေးမှတ်တမ်း ကောင်းပါတယ်", html);
    }
}
