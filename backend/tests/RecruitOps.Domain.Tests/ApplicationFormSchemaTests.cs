using System.Text.Json;
using RecruitOps.Domain;
using Xunit;

namespace RecruitOps.Domain.Tests;

/// <summary>Module 2.2 — the customer-defined application form.
///
/// <para>This validator is the boundary between a recruiter's schema and an anonymous
/// stranger's JSON. Unit-tested rather than only exercised through the API because the
/// interesting cases are the malformed ones, and enumerating those over HTTP is slow and
/// obscures what is actually being asserted.</para>
/// </summary>
public class ApplicationFormSchemaTests
{
    private const string TwoFields = """
        [
          { "key": "expected_salary", "label": "Expected salary", "type": "number", "required": true },
          { "key": "start_date", "label": "Earliest start date", "type": "date", "required": false }
        ]
        """;

    // ── Schema validation (recruiter side) ──────────────────────────────────

    [Fact]
    public void No_Custom_Fields_Is_Valid()
    {
        // Most jobs have none, so null and blank must both be ordinary success, not an error.
        Assert.True(ApplicationFormSchema.TryParse(null, out var a, out _));
        Assert.Empty(a);
        Assert.True(ApplicationFormSchema.TryParse("   ", out var b, out _));
        Assert.Empty(b);
        Assert.True(ApplicationFormSchema.TryParse("[]", out var c, out _));
        Assert.Empty(c);
    }

    [Fact]
    public void A_Valid_Schema_Parses()
    {
        Assert.True(ApplicationFormSchema.TryParse(TwoFields, out var fields, out var error));
        Assert.Null(error);
        Assert.Equal(2, fields.Length);
        Assert.Equal("expected_salary", fields[0].Key);
        Assert.True(fields[0].Required);
    }

    [Theory]
    // A key becomes a JSONB key that reporting will later select on — quotes, dots and
    // spaces in it are a problem waiting for the query that tries.
    [InlineData("""[{ "key": "bad key", "label": "L", "type": "text" }]""")]
    [InlineData("""[{ "key": "bad.key", "label": "L", "type": "text" }]""")]
    [InlineData("""[{ "key": "", "label": "L", "type": "text" }]""")]
    // Unknown type: the public renderer has no case for it, so it would render as nothing.
    [InlineData("""[{ "key": "k", "label": "L", "type": "file" }]""")]
    // No label means the applicant sees an unlabelled box.
    [InlineData("""[{ "key": "k", "label": "", "type": "text" }]""")]
    // A dropdown with nothing to drop down is unanswerable, and blocks a required field.
    [InlineData("""[{ "key": "k", "label": "L", "type": "select" }]""")]
    [InlineData("""[{ "key": "k", "label": "L", "type": "select", "options": [] }]""")]
    [InlineData("not json at all")]
    public void An_Invalid_Schema_Is_Rejected(string json)
    {
        Assert.False(ApplicationFormSchema.TryParse(json, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Duplicate_Keys_Are_Rejected_Case_Insensitively()
    {
        // Two keys differing only by case would be indistinguishable to anyone reading the
        // answers later, and only one of them would survive the answer dictionary.
        var json = """
            [
              { "key": "Salary", "label": "A", "type": "text" },
              { "key": "salary", "label": "B", "type": "text" }
            ]
            """;

        Assert.False(ApplicationFormSchema.TryParse(json, out _, out var error));
        Assert.Contains("more than once", error);
    }

    [Fact]
    public void Too_Many_Fields_Are_Rejected()
    {
        var fields = Enumerable.Range(0, ApplicationFormSchema.MaxFields + 1)
            .Select(i => $$"""{ "key": "f{{i}}", "label": "Q{{i}}", "type": "text" }""");
        var json = $"[{string.Join(',', fields)}]";

        Assert.False(ApplicationFormSchema.TryParse(json, out _, out var error));
        Assert.Contains("at most", error);
    }

    // ── Answer validation (anonymous applicant side) ────────────────────────

    [Fact]
    public void Answers_Are_Coerced_To_Canonical_Types()
    {
        var answers = """{ "expected_salary": "850000", "start_date": "2026-09-01" }""";

        Assert.True(ApplicationFormSchema.TryValidateAnswers(TwoFields, answers, out var json, out var error));
        Assert.Null(error);

        using var doc = JsonDocument.Parse(json!);
        // Stored as a number, not the string the browser sent, so reporting can aggregate it.
        Assert.Equal(JsonValueKind.Number, doc.RootElement.GetProperty("expected_salary").ValueKind);
        // Dates are round-tripped as yyyy-MM-dd so nobody has to guess whether 03/04 was
        // March or April.
        Assert.Equal("2026-09-01", doc.RootElement.GetProperty("start_date").GetString());
    }

    [Fact]
    public void Unknown_Keys_Are_Dropped_Not_Rejected()
    {
        // A stale browser tab submitting a question the recruiter deleted five minutes ago
        // should not cost the applicant their whole submission — but the extra key must not
        // reach the database either.
        var answers = """{ "expected_salary": "500000", "injected": "anything at all" }""";

        Assert.True(ApplicationFormSchema.TryValidateAnswers(TwoFields, answers, out var json, out _));

        using var doc = JsonDocument.Parse(json!);
        Assert.True(doc.RootElement.TryGetProperty("expected_salary", out _));
        Assert.False(doc.RootElement.TryGetProperty("injected", out _));
    }

    [Fact]
    public void A_Missing_Required_Answer_Is_Refused()
    {
        var answers = """{ "start_date": "2026-09-01" }""";

        Assert.False(ApplicationFormSchema.TryValidateAnswers(TwoFields, answers, out _, out var error));
        // The applicant reads this, so it has to name the question rather than the key.
        Assert.Contains("Expected salary", error);
    }

    [Fact]
    public void A_Missing_Optional_Answer_Is_Omitted_Not_Blanked()
    {
        var answers = """{ "expected_salary": "500000" }""";

        Assert.True(ApplicationFormSchema.TryValidateAnswers(TwoFields, answers, out var json, out _));

        using var doc = JsonDocument.Parse(json!);
        // Absent, not "" — an empty string would later read as "they answered nothing",
        // which is a different fact from "they were never asked to".
        Assert.False(doc.RootElement.TryGetProperty("start_date", out _));
    }

    [Fact]
    public void A_Select_Answer_Outside_The_Offered_Choices_Is_Refused()
    {
        var schema = """
            [{ "key": "shift", "label": "Preferred shift", "type": "select",
               "required": true, "options": ["Day", "Night"] }]
            """;

        Assert.True(ApplicationFormSchema.TryValidateAnswers(schema, """{ "shift": "Day" }""", out _, out _));

        // The list is fixed; anything else came from a tampered form, and accepting it would
        // put values into the data that no report or filter expects.
        Assert.False(ApplicationFormSchema.TryValidateAnswers(
            schema, """{ "shift": "Whenever" }""", out _, out var error));
        Assert.Contains("offered choices", error);
    }

    [Fact]
    public void A_Number_Field_Refuses_Text()
    {
        Assert.False(ApplicationFormSchema.TryValidateAnswers(
            TwoFields, """{ "expected_salary": "as much as possible" }""", out _, out var error));
        Assert.Contains("must be a number", error);
    }

    [Fact]
    public void Booleans_And_Numbers_Are_Accepted_As_Either_JSON_Type()
    {
        // Browsers and hand-written clients disagree about whether a checkbox is `true` or
        // `"true"`. Neither should be a submission failure.
        var schema = """
            [
              { "key": "agree", "label": "I agree", "type": "checkbox", "required": true },
              { "key": "years", "label": "Years of experience", "type": "number", "required": true }
            ]
            """;

        Assert.True(ApplicationFormSchema.TryValidateAnswers(
            schema, """{ "agree": true, "years": 7 }""", out _, out _));
        Assert.True(ApplicationFormSchema.TryValidateAnswers(
            schema, """{ "agree": "true", "years": "7" }""", out _, out _));
    }

    [Fact]
    public void A_Required_Checkbox_Must_Be_Ticked()
    {
        var schema = """
            [{ "key": "agree", "label": "I accept the privacy notice", "type": "checkbox", "required": true }]
            """;

        // "Required" on a checkbox means consent, not merely "send me the field".
        Assert.False(ApplicationFormSchema.TryValidateAnswers(
            schema, """{ "agree": false }""", out _, out var error));
        Assert.Contains("privacy notice", error);
    }

    [Fact]
    public void An_Overlong_Text_Answer_Is_Refused()
    {
        var schema = """[{ "key": "note", "label": "Note", "type": "text" }]""";
        var answers = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["note"] = new string('x', 5_000),
        });

        // This arrives from an anonymous request; without a cap it is a free write of
        // arbitrary size into the customer's database.
        Assert.False(ApplicationFormSchema.TryValidateAnswers(schema, answers, out _, out var error));
        Assert.Contains("characters or fewer", error);
    }

    [Fact]
    public void Answers_To_A_Form_With_No_Fields_Are_Discarded()
    {
        // Nothing was asked, so nothing is stored — even if the client sent something.
        Assert.True(ApplicationFormSchema.TryValidateAnswers(
            null, """{ "smuggled": "value" }""", out var json, out _));
        Assert.Null(json);
    }
}
