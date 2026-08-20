namespace RecruitOps.Application.DTOs;

// What goes in OutboundMessage.PayloadJson — one record per OutboundMessageKind that needs one.
//
// The rule they all follow (ADR-0026 §2): a payload carries ONLY what cannot be re-read at send
// time. Everything on a live row — the candidate's name, the interview slot, the job title — is
// read from the database when the message actually goes, so an interview moved between enqueue
// and send is sent with the new time rather than the old one. A payload field is therefore an
// admission that something is not stored anywhere else, and each one below says where it went
// missing.
//
// ⚠️ Anything put here is retained for the life of the row and is in scope for the Module 7.4
// retention policy. That is the second reason to keep these thin.

/// <summary>An invitation to an interview round (Module 3.2).</summary>
/// <param name="TimeZoneId">The IANA zone the time is written in — "Asia/Yangon". <b>Frozen here
/// because it survives nowhere else:</b> Npgsql stores <c>DateTimeOffset</c> as <c>timestamptz</c>,
/// which normalises to UTC and discards the offset the recruiter's browser sent. Reading it back
/// gives a correct instant with no idea what o'clock the recruiter meant — and "09:00" is the one
/// thing in this email the candidate has to act on.</param>
/// <param name="IsReschedule">Whether this replaces a time the candidate was already given. Only
/// the wording changes — the details are read live either way — but a candidate who has already
/// blocked out Monday morning needs to be told it moved, not invited again.</param>
public sealed record InterviewInvitationPayload(
    string TimeZoneId,
    bool IsReschedule = false);
