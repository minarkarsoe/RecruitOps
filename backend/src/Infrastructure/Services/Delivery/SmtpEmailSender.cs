using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitOps.Application.Interfaces;
using RecruitOps.Infrastructure.Options;

namespace RecruitOps.Infrastructure.Services.Delivery;

/// <summary>The required <see cref="IEmailSender"/>: plain SMTP over
/// <c>System.Net.Mail.SmtpClient</c> (ADR-0026 §1).
///
/// <para><b>No new dependency.</b> MailKit is the better-regarded client and would bring implicit
/// TLS on port 465 and XOAUTH2 — which Microsoft 365 and Google Workspace need, and which the
/// integrations design draws. It is not here because ADR-0026 chose to add no package for this
/// capability and the BCL covers the floor the ADR actually specifies. Recorded in
/// <c>FEATURE-STATUS.md</c> as the known limit of the SMTP adapter rather than left to be
/// rediscovered by the first customer on Microsoft 365.</para>
///
/// <para>The one thing this class really decides is <b>permanent versus retryable</b>, and it errs
/// hard towards retryable — see <see cref="IsPermanentFailure"/>.</para>
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (!_options.IsConfigured)
        {
            // Loud, because an install in this state cannot send an offer and nobody finds out
            // until a candidate does not turn up.
            _logger.LogError(
                "SMTP is not configured, so nothing can be delivered. Set Smtp:Host and "
                + "Smtp:FromAddress (or Smtp:PickupDirectory for local development).");

            throw new EmailDeliveryException(
                "This installation has no mail server configured, so the message was not sent. "
                + "An administrator needs to set up SMTP.",
                isPermanent: false);
        }

        using var mail = BuildMessage(message);
        using var client = BuildClient();

        try
        {
            await client.SendMailAsync(mail, ct);
        }
        catch (OperationCanceledException)
        {
            // Shutdown, not a delivery failure. The row stays claimed and becomes due again.
            throw;
        }
        catch (SmtpFailedRecipientsException ex)
        {
            throw Rejected(message.To, ex.InnerExceptions.FirstOrDefault()?.StatusCode ?? ex.StatusCode, ex);
        }
        catch (SmtpFailedRecipientException ex)
        {
            throw Rejected(message.To, ex.StatusCode, ex);
        }
        catch (SmtpException ex)
        {
            throw Rejected(message.To, ex.StatusCode, ex);
        }
        catch (Exception ex)
        {
            // A socket reset, a DNS failure, a TLS handshake that fell over. None of these say
            // anything about the address, so none of them are permanent.
            throw new EmailDeliveryException(
                $"The mail server could not be reached ({ex.Message}).", isPermanent: false, ex);
        }
    }

    private MailMessage BuildMessage(EmailMessage message)
    {
        // One row in the delivery log means one recipient (see EmailMessage.To). MailAddress
        // does NOT reject "a@x.test, b@y.test" — it parses the first and quietly keeps going,
        // so without this guard a two-address field would deliver an offer to one of them and
        // the log would claim both. Refused instead: a comma outside a quoted local part is
        // vanishingly rare, and being wrong about where candidate mail went is not.
        if (message.To.Contains(',') || message.To.Contains(';'))
        {
            throw new EmailDeliveryException(
                $"'{message.To}' looks like more than one address. Each message goes to exactly "
                + "one recipient so that the delivery log can answer for it.",
                isPermanent: true);
        }

        MailAddress to;
        try
        {
            to = new MailAddress(message.To);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            // The one genuinely permanent class of failure, and it is worth being certain about:
            // no number of retries turns a malformed address into a deliverable one.
            throw new EmailDeliveryException(
                $"'{message.To}' is not a usable email address, so nothing could be sent to it.",
                isPermanent: true, ex);
        }

        var mail = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromDisplayName, Encoding.UTF8),
            // Header injection: a subject is one line, and this is rendered from data a candidate
            // supplied (their own name). Stripping here rather than trusting each caller.
            Subject = SingleLine(message.Subject),
            SubjectEncoding = Encoding.UTF8,
            Body = message.PlainTextBody,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false,
        };
        mail.To.Add(to);

        if (!string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            // Plain text stays the body so a text-only client still reads something sensible;
            // HTML rides alongside as the richer alternative.
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                message.PlainTextBody, Encoding.UTF8, "text/plain"));
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                message.HtmlBody, Encoding.UTF8, "text/html"));
        }

        return mail;
    }

    private SmtpClient BuildClient()
    {
        if (!string.IsNullOrWhiteSpace(_options.PickupDirectory))
        {
            var directory = Path.GetFullPath(_options.PickupDirectory);
            Directory.CreateDirectory(directory);

            _logger.LogInformation(
                "SMTP is in pickup-directory mode: mail is written to {Directory} and not sent.",
                directory);

            return new SmtpClient
            {
                DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                PickupDirectoryLocation = directory,
            };
        }

        var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseStartTls,
            Timeout = _options.TimeoutSeconds * 1000,
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }
        else
        {
            // Explicit: an internal relay that authorises by network address must not be sent the
            // process's Windows credentials, which is what UseDefaultCredentials would do.
            client.UseDefaultCredentials = false;
        }

        return client;
    }

    private static EmailDeliveryException Rejected(string recipient, SmtpStatusCode status, Exception inner)
    {
        var permanent = IsPermanentFailure(status);

        var detail = permanent
            ? $"The mail server rejected '{recipient}' as undeliverable ({(int)status})."
            : $"The mail server did not accept the message ({(int)status}: {inner.Message}).";

        return new EmailDeliveryException(detail, permanent, inner);
    }

    /// <summary>Permanent means <b>the address is wrong</b>, and nothing else.
    ///
    /// <para>Every other SMTP refusal — a rejected password (535), STARTTLS required (530), the
    /// server too busy (421, 450, 452), a connection that never opened (<c>GeneralFailure</c>) —
    /// is somebody's to fix, usually in minutes. Marking those permanent would throw away an
    /// interview invitation because a relay was restarting. The attempt cap is what stops a
    /// genuinely broken install from retrying forever, and it does that job without this method
    /// having to guess.</para>
    ///
    /// <para>The known imprecision, stated rather than hidden: 550 and 554 are also what some
    /// relays return for "relaying denied", which is a configuration fault and not a bad address.
    /// Such a message lands on Failed with the server's own text in the delivery log. That is the
    /// wrong bucket, and it is visible — which is the trade being taken.</para>
    ///
    /// <para>Public only so a test can pin the table directly. This one decision is what stands
    /// between a relay hiccup and a permanently lost interview invitation, and reaching it through
    /// a real SMTP conversation would mean either a fake server or no test at all.</para></summary>
    public static bool IsPermanentFailure(SmtpStatusCode status) => status switch
    {
        SmtpStatusCode.MailboxUnavailable            // 550
            or SmtpStatusCode.UserNotLocalTryAlternatePath // 551
            or SmtpStatusCode.MailboxNameNotAllowed  // 553
            or SmtpStatusCode.TransactionFailed      // 554
            => true,
        _ => false,
    };

    private static string SingleLine(string value) =>
        value.Replace("\r", " ").Replace("\n", " ").Trim();
}
