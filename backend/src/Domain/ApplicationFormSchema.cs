using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace RecruitOps.Domain;

/// <summary>One customer-defined question on a job's application form (Module 2.2).</summary>
public sealed class ApplicationFormField
{
    /// <summary>Stable identifier used as the answer's JSON key. Constrained to a safe
    /// character set because it is written into a JSONB document and later read back by
    /// reporting — a key containing quotes or dots is a problem waiting for the query that
    /// tries to select it.</summary>
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>One of <see cref="ApplicationFormSchema.FieldTypes"/>.</summary>
    public string Type { get; set; } = "text";

    public bool Required { get; set; }

    /// <summary>Choices for <c>select</c>. Ignored for every other type.</summary>
    public string[]? Options { get; set; }
}

/// <summary>Parsing and validation for customer-defined application forms.
///
/// <para><b>Why this is in Domain and not in a service.</b> The schema is written by a
/// recruiter through the internal API and the answers arrive from an anonymous stranger
/// through the public API. Those are two different code paths with two different threat
/// models, and they have to agree exactly on what the schema means. If they disagree, the
/// public endpoint becomes a way to write arbitrary JSON into the customer's database under
/// the cover of a "custom field".</para>
///
/// <para><b>Answers are rebuilt, not passed through.</b> <see cref="TryValidateAnswers"/>
/// returns a freshly constructed document containing only known keys with coerced types.
/// Storing the applicant's original JSON — even after checking it — would keep whatever else
/// they put in it.</para>
/// </summary>
public static class ApplicationFormSchema
{
    public static readonly string[] FieldTypes =
        ["text", "textarea", "number", "date", "select", "checkbox"];

    /// <summary>Bounds the anonymous payload and keeps the form usable. A 200-question
    /// application form is a bug, not a requirement.</summary>
    public const int MaxFields = 20;

    private const int MaxTextLength = 500;
    private const int MaxTextAreaLength = 2000;
    private const int MaxOptions = 50;

    private static readonly Regex KeyPattern = new("^[a-zA-Z0-9_]{1,50}$", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Validates a schema document. Null or blank means "no custom fields", which
    /// is valid — most jobs won't have any.</summary>
    public static bool TryParse(string? json, out ApplicationFormField[] fields, out string? error)
    {
        fields = [];
        error = null;

        if (string.IsNullOrWhiteSpace(json)) return true;

        ApplicationFormField[]? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ApplicationFormField[]>(json, Json);
        }
        catch (JsonException ex)
        {
            error = $"The application form is not valid JSON: {ex.Message}";
            return false;
        }

        if (parsed is null || parsed.Length == 0) return true;

        if (parsed.Length > MaxFields)
        {
            error = $"An application form can have at most {MaxFields} custom fields.";
            return false;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in parsed)
        {
            // `Key` is declared non-nullable, but it arrives from `JsonSerializer.Deserialize`,
            // where a document simply omitting it leaves the property null. Coalesced once, into
            // a local, so the null case is handled in one place rather than at each use — the
            // previous code did it only at the regex check and passed the raw value to
            // `HashSet.Add` four lines later, which is what CS8604 was pointing at.
            var key = field.Key ?? string.Empty;

            if (!KeyPattern.IsMatch(key))
            {
                error = $"Field key '{key}' must be 1–50 letters, digits or underscores.";
                return false;
            }

            // Case-insensitive, because two keys differing only by case would be
            // indistinguishable to anyone reading the answers later.
            if (!seen.Add(key))
            {
                error = $"Field key '{key}' is used more than once.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(field.Label) || field.Label.Length > 100)
            {
                error = $"Field '{key}' needs a label of 1–100 characters.";
                return false;
            }

            if (!FieldTypes.Contains(field.Type))
            {
                error = $"Field '{key}' has unknown type '{field.Type}'.";
                return false;
            }

            if (field.Type == "select")
            {
                var options = field.Options ?? [];
                if (options.Length == 0)
                {
                    error = $"Field '{key}' is a dropdown, so it needs at least one option.";
                    return false;
                }
                if (options.Length > MaxOptions)
                {
                    error = $"Field '{key}' has more than {MaxOptions} options.";
                    return false;
                }
                if (options.Any(string.IsNullOrWhiteSpace))
                {
                    error = $"Field '{key}' has a blank option.";
                    return false;
                }
            }
        }

        fields = parsed;
        return true;
    }

    /// <summary>Validates an applicant's answers against a schema and returns a rebuilt
    /// document. Unknown keys are dropped rather than rejected: a stale browser tab
    /// submitting a field the recruiter deleted five minutes ago should not lose the
    /// applicant their whole submission.</summary>
    public static bool TryValidateAnswers(
        string? schemaJson, string? answersJson, out string? normalizedJson, out string? error)
    {
        normalizedJson = null;
        error = null;

        if (!TryParse(schemaJson, out var fields, out error)) return false;
        if (fields.Length == 0) return true; // nothing to collect, so nothing is stored

        Dictionary<string, JsonElement> answers;
        try
        {
            answers = string.IsNullOrWhiteSpace(answersJson)
                ? []
                : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(answersJson, Json) ?? [];
        }
        catch (JsonException)
        {
            error = "Your answers could not be read. Please try again.";
            return false;
        }

        var output = new Dictionary<string, object?>();

        foreach (var field in fields)
        {
            var present = answers.TryGetValue(field.Key, out var raw);
            var text = present ? AsText(raw) : null;

            if (string.IsNullOrWhiteSpace(text))
            {
                if (field.Required)
                {
                    error = $"Please answer: {field.Label}";
                    return false;
                }
                continue; // optional and unanswered — omit rather than store an empty string
            }

            text = text.Trim();

            switch (field.Type)
            {
                case "text":
                    if (text.Length > MaxTextLength)
                    {
                        error = $"{field.Label} must be {MaxTextLength} characters or fewer.";
                        return false;
                    }
                    output[field.Key] = text;
                    break;

                case "textarea":
                    if (text.Length > MaxTextAreaLength)
                    {
                        error = $"{field.Label} must be {MaxTextAreaLength} characters or fewer.";
                        return false;
                    }
                    output[field.Key] = text;
                    break;

                case "number":
                    if (!decimal.TryParse(text, out var number))
                    {
                        error = $"{field.Label} must be a number.";
                        return false;
                    }
                    output[field.Key] = number;
                    break;

                case "date":
                    // Round-tripped as yyyy-MM-dd so reporting never has to guess whether
                    // 03/04 meant March or April.
                    if (!DateOnly.TryParse(text, out var date))
                    {
                        error = $"{field.Label} must be a date.";
                        return false;
                    }
                    output[field.Key] = date.ToString("yyyy-MM-dd");
                    break;

                case "select":
                    if (!(field.Options ?? []).Contains(text!, StringComparer.Ordinal))
                    {
                        // The list is fixed; anything else came from a tampered form.
                        error = $"{field.Label} must be one of the offered choices.";
                        return false;
                    }
                    output[field.Key] = text;
                    break;

                case "checkbox":
                    if (!bool.TryParse(text, out var flag))
                    {
                        error = $"{field.Label} must be yes or no.";
                        return false;
                    }
                    if (field.Required && !flag)
                    {
                        error = $"Please confirm: {field.Label}";
                        return false;
                    }
                    output[field.Key] = flag;
                    break;
            }
        }

        normalizedJson = output.Count == 0 ? null : JsonSerializer.Serialize(output);
        return true;
    }

    /// <summary>Flattens a JSON value to text so the type checks above have one thing to
    /// work on. A client may send <c>true</c> or <c>"true"</c>, <c>7</c> or <c>"7"</c>, and
    /// neither should be a submission failure.</summary>
    private static string? AsText(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => null, // null, arrays and objects are not answers to any field type we offer
    };
}
