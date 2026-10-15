namespace RecruitOps.Domain.Enums;

/// <summary>State of a whole bulk CV upload batch.
///
/// <para><b>Derived, never stored</b> (ADR-0026). A batch is whatever its files add up to:
/// nothing terminal yet is <see cref="Queued"/>, a mixture is <see cref="Processing"/>, all
/// terminal with at least one success is <see cref="Completed"/>, and all terminal with none is
/// <see cref="Failed"/>. Storing it alongside the rows would be a second source of truth that can
/// only drift from the first — which is exactly what the old in-memory implementation did.</para>
/// </summary>
public enum BulkBatchStatus
{
    Queued = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}

/// <summary>State of one file inside a bulk CV upload batch.
///
/// <para>⚠️ <see cref="Processing"/> is <b>never written</b>, and that is deliberate — the same
/// decision as <c>OutboundMessageStatus</c>, which has no "Sending". A row claimed for work is not
/// marked in flight; its <c>NextAttemptAt</c> is pushed into the future instead. So a process that
/// dies mid-extraction leaves a row that is still <see cref="Queued"/> and becomes due again on its
/// own, rather than one stuck in a state only a human can clear. The member is kept because it is
/// part of the published API contract and a client may still receive it from an older
/// deployment.</para>
///
/// <para><see cref="Skipped"/> is reserved for a file correctly not processed. Nothing produces it
/// today; it is not a failure and must not be rendered as one.</para>
/// </summary>
public enum BulkFileStatus
{
    Queued = 0,
    Processing = 1,
    Success = 2,
    Skipped = 3,
    Failed = 4
}
