using System.Text;
using System.Text.RegularExpressions;

namespace RecruitOps.Domain;

/// <summary>Finds `@handle` tokens in note bodies and renders a note safely for display (3.4).
///
/// <para><b>Handles are parsed here, never accepted from the client.</b> If the request
/// supplied the mention list, anyone could post a note that appears addressed to a colleague
/// — and once Module 7 adds notification delivery, could make the system notify on their
/// behalf. The body is the single source of truth for who was mentioned; the caller resolves
/// the handles this returns against users they are actually allowed to see, and unmatched
/// handles stay plain text.</para>
///
/// <para><b>Escaping happens on output, not on input.</b> Stripping markup as it is typed
/// mangles what the user wrote (a Myanmar salary note containing `&lt;` is not an attack) and
/// still fails the day a second renderer appears. <see cref="ToSafeHtml"/> escapes every
/// character of user text and only then inserts the mention markup it generated itself, so
/// no path exists by which body text becomes an element.</para>
/// </summary>
public static class MentionParser
{
    /// <summary>`@` followed by a handle that starts and ends alphanumeric. The interior
    /// allows dot, underscore and hyphen so email local-parts work as handles.
    /// <para>Bounded by <c>{0,62}</c> rather than <c>*</c>: this runs over user-supplied text,
    /// and an unbounded inner class with a required trailing character is the classic shape
    /// for catastrophic backtracking.</para></summary>
    private static readonly Regex HandlePattern = new(
        @"(?<![A-Za-z0-9._-])@([A-Za-z0-9][A-Za-z0-9._-]{0,62}[A-Za-z0-9]|[A-Za-z0-9])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(200));

    /// <summary>One `@handle` occurrence and where it sits in the body.</summary>
    public readonly record struct MentionToken(int Index, int Length, string Handle);

    /// <summary>Every `@handle` in the body, in order, duplicates included.</summary>
    public static IReadOnlyList<MentionToken> Find(string? body)
    {
        if (string.IsNullOrEmpty(body)) return Array.Empty<MentionToken>();

        var tokens = new List<MentionToken>();
        foreach (Match m in HandlePattern.Matches(body))
            tokens.Add(new MentionToken(m.Index, m.Length, m.Groups[1].Value));

        return tokens;
    }

    /// <summary>The distinct handles in the body, lower-cased for matching.</summary>
    public static IReadOnlyCollection<string> DistinctHandles(string? body) =>
        Find(body)
            .Select(t => t.Handle.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>Renders the body as HTML that is safe to inject: all user text is escaped,
    /// and resolved mentions become a span carrying the user id.
    /// <para><paramref name="resolved"/> maps a lower-cased handle to the user it belongs to.
    /// A handle with no entry is left as escaped plain text — an unmatched `@` is far more
    /// often an email address or a Twitter handle than a mistake worth flagging.</para></summary>
    public static string ToSafeHtml(
        string? body,
        IReadOnlyDictionary<string, MentionTarget> resolved)
    {
        if (string.IsNullOrEmpty(body)) return string.Empty;

        var sb = new StringBuilder(body.Length + 32);
        var cursor = 0;

        foreach (var token in Find(body))
        {
            if (!resolved.TryGetValue(token.Handle.ToLowerInvariant(), out var target))
                continue;

            AppendEscaped(sb, body.AsSpan(cursor, token.Index - cursor));

            sb.Append("<span class=\"mention\" data-user-id=\"")
              .Append(target.UserId.ToString())
              .Append("\">@");
            AppendEscaped(sb, target.DisplayName.AsSpan());
            sb.Append("</span>");

            cursor = token.Index + token.Length;
        }

        AppendEscaped(sb, body.AsSpan(cursor));
        return sb.ToString();
    }

    /// <summary>Who a handle resolved to.</summary>
    public readonly record struct MentionTarget(Guid UserId, string DisplayName);

    private static void AppendEscaped(StringBuilder sb, ReadOnlySpan<char> text)
    {
        foreach (var c in text)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                // Escaped too: the markup above uses double quotes, but a future template
                // using single quotes would otherwise turn this into an injection point.
                case '\'': sb.Append("&#39;"); break;
                default: sb.Append(c); break;
            }
        }
    }
}
