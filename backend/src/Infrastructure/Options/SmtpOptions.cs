namespace RecruitOps.Infrastructure.Options;

/// <summary>How this deployment puts mail on the wire (ADR-0026 §1).
///
/// <para>Plain SMTP, because it is the only transport that works in <b>every</b> deployment we
/// sell — including the air-gapped on-premise bank whose only mail path is an internal relay.
/// See <c>design/internal/settings-integrations.html</c>, which already renders "Plain SMTP" as a
/// first-class choice next to Microsoft 365 and Google Workspace rather than as an advanced
/// setting.</para>
///
/// <para>⚠️ <see cref="Password"/> is a secret. Where secrets live is still open under Module 7
/// (key vault versus encrypted column, ADR-0026 "Open questions"), so today it comes from
/// configuration like every other credential in this repo — meaning environment variables or a
/// file that is not committed, never <c>appsettings.json</c>.</para>
/// </summary>
public class SmtpOptions
{
    public const string SectionName = "Smtp";

    /// <summary>Mail server host name. <b>Empty means this install cannot send mail</b> — see
    /// <see cref="IsConfigured"/> for what happens then.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>587 for submission with STARTTLS, which is what almost every relay wants. 25 is
    /// the right answer for an internal relay that does not authenticate.</summary>
    public int Port { get; set; } = 587;

    /// <summary>Upgrade the connection with STARTTLS. On by default: a password sent in the clear
    /// across a customer's network is a finding, and every server worth connecting to supports it.
    /// Turn it off only for a relay on the same host that does not.
    /// <para>⚠️ <b>Implicit TLS (port 465) is not supported</b> — <c>System.Net.Mail.SmtpClient</c>
    /// only speaks STARTTLS. Use 587. A relay that offers nothing but 465 needs a different client
    /// library, which is a decision, not a config change.</para></summary>
    public bool UseStartTls { get; set; } = true;

    /// <summary>Left empty for a relay that authorises by network address, which is the common
    /// on-premise arrangement.</summary>
    public string? Username { get; set; }

    public string? Password { get; set; }

    /// <summary>The envelope sender. Some relays reject a From they do not own, so this is not
    /// optional decoration.
    /// <para>Whether this should instead be the acting recruiter's own address — better for
    /// replies, and needing delegated Microsoft 365 permission — is an open question on ADR-0026.
    /// A candidate replying to a <c>noreply@</c> invitation is a real cost, recorded rather than
    /// solved.</para></summary>
    public string FromAddress { get; set; } = string.Empty;

    public string FromDisplayName { get; set; } = "Recruitment";

    /// <summary>Per send. Long enough for a slow relay, and comfortably inside the worker's
    /// visibility timeout — if a send outlives that, the message is claimed a second time and the
    /// candidate gets two emails.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Set to a directory path to write <c>.eml</c> files there instead of connecting to
    /// a server. <b>For local development.</b>
    ///
    /// <para>This is not a stub and does not pretend to have sent anything: the message is really
    /// rendered by the real code path and really written where configured, so what you open is
    /// exactly what a candidate would have received. Compare the AI fallback in
    /// <c>appsettings.json</c>, which fabricates content and is therefore fenced to Development —
    /// this one invents nothing, so its only risk is an operator who meant to configure a
    /// server.</para></summary>
    public string? PickupDirectory { get; set; }

    /// <summary>True when a send could actually go somewhere.
    ///
    /// <para>When false, <c>SmtpEmailSender</c> reports a <b>retryable</b> failure rather than a
    /// permanent one. That is deliberate: an unconfigured relay is an operator's mistake and is
    /// fixable in minutes, so the queue should still be holding the invitation when they fix it.
    /// The attempt cap still stops it circulating forever, and the delivery log says plainly that
    /// SMTP was never configured.</para></summary>
    /// <para><see cref="FromAddress"/> counts, not just a destination: a message with no From is
    /// rejected before it leaves the process, and "SMTP is not configured" is a far more useful
    /// thing to read in the delivery log than an <c>ArgumentException</c>.</para>
    public bool IsConfigured =>
        (!string.IsNullOrWhiteSpace(PickupDirectory) || !string.IsNullOrWhiteSpace(Host))
        && !string.IsNullOrWhiteSpace(FromAddress);
}
