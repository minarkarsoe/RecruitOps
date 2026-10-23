namespace RecruitOps.Domain.Enums;

/// <summary>What a candidate is asked for after accepting (Module 4.3, ADR-0030 §5b) — the four items a
/// company marks required or optional. Flags: a company's choice, and the copy fixed on an offer when it
/// is accepted, are one value each. Stored as a number; a value is added, never renumbered.</summary>
[Flags]
public enum PreboardingItems
{
    None = 0,

    /// <summary>Both sides of the NRC — <see cref="PreboardingDocumentKind.NrcFront"/> and
    /// <see cref="PreboardingDocumentKind.NrcBack"/>.</summary>
    Nrc = 1,

    /// <summary>Typed details plus a photo of the bank book.</summary>
    BankAccount = 2,

    Education = 4,

    /// <summary>For the staff card; the one file the Admin handoff carries.</summary>
    Photo = 8,
}

/// <summary>One document the recruiter approves or sends back. NRC is one item to the candidate and two
/// documents to the recruiter, who checks each side. Stored by name.</summary>
public enum PreboardingDocumentKind
{
    NrcFront,
    NrcBack,
    BankAccount,
    Education,
    Photo,
}

/// <summary>Where a document is. Stored by name.</summary>
public enum PreboardingDocumentStatus
{
    /// <summary>Nothing in it — never uploaded, or every file removed.</summary>
    Missing,

    /// <summary>Changed by the candidate since anyone reviewed it: waiting for the recruiter.</summary>
    Submitted,

    /// <summary>Final: the candidate can no longer change it, and there is no un-approve.</summary>
    Approved,

    /// <summary>Sent back with a message, and not changed since.</summary>
    Returned,
}
