using System.Net.Mail;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RecruitOps.Application.Interfaces;
using RecruitOps.Infrastructure.Options;
using RecruitOps.Infrastructure.Services.Delivery;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>The SMTP transport from ADR-0026 §1.
///
/// <para>What is actually worth pinning here is not "does SMTP work" — that needs somebody else's
/// mail server — but the two judgements this class makes on its own: <b>what an unconfigured
/// install does</b>, and <b>which failures are permanent</b>. Both are decisions about whether an
/// interview invitation survives, and both are invisible until a customer loses one.</para>
/// </summary>
public class SmtpEmailSenderTests : IDisposable
{
    private readonly string _pickupDirectory =
        Path.Combine(Path.GetTempPath(), "recruitops-mail-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_pickupDirectory)) Directory.Delete(_pickupDirectory, recursive: true);
        GC.SuppressFinalize(this);
    }

    private SmtpEmailSender Sender(Action<SmtpOptions> configure)
    {
        var options = new SmtpOptions();
        configure(options);
        return new SmtpEmailSender(Options.Create(options), NullLogger<SmtpEmailSender>.Instance);
    }

    private SmtpEmailSender PickupSender() => Sender(o =>
    {
        o.PickupDirectory = _pickupDirectory;
        o.FromAddress = "recruitment@company.test";
        o.FromDisplayName = "Company Recruitment";
    });

    private static EmailMessage Invitation(string to = "candidate@example.test") =>
        new(to, "Interview invitation - Collections Officer", "Dear Aye Aye,\r\n\r\nPlease come in.");

    // ------------------------------------------------------------- not configured

    /// <summary>An install with no mail server must fail loudly, and must fail <b>retryably</b>.
    ///
    /// <para>Permanent would burn the queue for a mistake an administrator fixes in two minutes:
    /// they set the host, and every invitation queued in the meantime has already been given up
    /// on. The attempt cap is what stops a genuinely dead install from retrying forever.</para></summary>
    [Fact]
    public async Task An_Unconfigured_Install_Fails_Retryably()
    {
        var sender = Sender(_ => { /* nothing set at all */ });

        var ex = await Assert.ThrowsAsync<EmailDeliveryException>(
            () => sender.SendAsync(Invitation()));

        Assert.False(ex.IsPermanent);
        Assert.Contains("no mail server configured", ex.Message);
    }

    /// <summary>A host with no From address is still unconfigured. A relay rejects a message with
    /// no envelope sender, and "SMTP is not configured" is a far more actionable thing to read in
    /// the delivery log than an ArgumentException from deep in the BCL.</summary>
    [Fact]
    public async Task A_Host_Without_A_From_Address_Counts_As_Unconfigured()
    {
        var sender = Sender(o => o.Host = "mail.company.test");

        var ex = await Assert.ThrowsAsync<EmailDeliveryException>(
            () => sender.SendAsync(Invitation()));

        Assert.False(ex.IsPermanent);
    }

    // ------------------------------------------------------------- addresses

    [Theory]
    [InlineData("not-an-address")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("two@addresses.test, second@addresses.test")]
    public async Task A_Malformed_Address_Is_Permanent(string recipient)
    {
        var ex = await Assert.ThrowsAsync<EmailDeliveryException>(
            () => PickupSender().SendAsync(Invitation(recipient)));

        Assert.True(ex.IsPermanent);
    }

    /// <summary>Header injection through a candidate's own name, which is the one value in an
    /// invitation that an outsider controls. A newline in the subject would let them append
    /// headers — a Bcc, most usefully — to a message this system sends on the company's behalf.</summary>
    [Fact]
    public async Task A_Newline_In_The_Subject_Cannot_Add_A_Header()
    {
        await PickupSender().SendAsync(new EmailMessage(
            "candidate@example.test",
            "Interview invitation\r\nBcc: attacker@evil.test",
            "Body."));

        var written = await File.ReadAllTextAsync(Directory.GetFiles(_pickupDirectory, "*.eml").Single());
        var lines = written.Split("\r\n");

        // A header is a line that starts at column 0; a folded continuation starts with
        // whitespace. So this is the assertion that matters: the injected text never became one.
        Assert.DoesNotContain(lines, l => l.StartsWith("Bcc:", StringComparison.OrdinalIgnoreCase));

        // It is still there — flattened into the subject, where it is inert text.
        Assert.Contains("attacker@evil.test", written);
    }

    // ------------------------------------------------------------- pickup directory

    /// <summary>The development path writes a real message rather than pretending to send one.
    ///
    /// <para>That distinction is the whole reason this mode is allowed outside a fenced-off
    /// development flag, unlike the AI fallback: it fabricates nothing. What lands on disk is what
    /// the relay would have carried.</para></summary>
    [Fact]
    public async Task The_Pickup_Directory_Writes_A_Real_Message()
    {
        await PickupSender().SendAsync(Invitation());

        var file = Assert.Single(Directory.GetFiles(_pickupDirectory, "*.eml"));
        var written = await File.ReadAllTextAsync(file);

        Assert.Contains("candidate@example.test", written);
        Assert.Contains("recruitment@company.test", written);
        Assert.Contains("Interview invitation", written);
    }

    [Fact]
    public async Task The_Pickup_Directory_Is_Created_If_It_Is_Missing()
    {
        Assert.False(Directory.Exists(_pickupDirectory));
        await PickupSender().SendAsync(Invitation());
        Assert.True(Directory.Exists(_pickupDirectory));
    }

    // ------------------------------------------------------------- failure classification

    /// <summary>The table this class exists to get right.
    ///
    /// <para>Permanent means the <i>address</i> is unusable, and nothing else. A rejected password,
    /// a required STARTTLS, a busy server, a connection that never opened — every one of those is
    /// somebody's to fix, and marking them permanent throws away an invitation because a relay was
    /// restarting.</para></summary>
    [Theory]
    [InlineData(SmtpStatusCode.MailboxUnavailable)]            // 550
    [InlineData(SmtpStatusCode.UserNotLocalTryAlternatePath)]  // 551
    [InlineData(SmtpStatusCode.MailboxNameNotAllowed)]         // 553
    [InlineData(SmtpStatusCode.TransactionFailed)]             // 554
    public void An_Unusable_Address_Is_Permanent(SmtpStatusCode status)
        => Assert.True(SmtpEmailSender.IsPermanentFailure(status));

    [Theory]
    [InlineData(SmtpStatusCode.GeneralFailure)]                // -1, could not connect
    [InlineData(SmtpStatusCode.ServiceNotAvailable)]           // 421
    [InlineData(SmtpStatusCode.MailboxBusy)]                   // 450
    [InlineData(SmtpStatusCode.LocalErrorInProcessing)]        // 451
    [InlineData(SmtpStatusCode.InsufficientStorage)]           // 452
    [InlineData(SmtpStatusCode.ClientNotPermitted)]            // 454, TLS/auth unavailable
    [InlineData(SmtpStatusCode.MustIssueStartTlsFirst)]        // 530
    public void Everything_Else_Is_Worth_Retrying(SmtpStatusCode status)
        => Assert.False(SmtpEmailSender.IsPermanentFailure(status));

    /// <summary>A server that is simply not there. Nothing about that says the address is wrong,
    /// so the message must survive to be tried again.</summary>
    [Fact]
    public async Task A_Server_That_Cannot_Be_Reached_Is_Retryable()
    {
        var sender = Sender(o =>
        {
            // Port 1 on the loopback: refused immediately, so this stays a fast unit test.
            o.Host = "127.0.0.1";
            o.Port = 1;
            o.UseStartTls = false;
            o.FromAddress = "recruitment@company.test";
            o.TimeoutSeconds = 5;
        });

        var ex = await Assert.ThrowsAsync<EmailDeliveryException>(
            () => sender.SendAsync(Invitation()));

        Assert.False(ex.IsPermanent);
    }
}
