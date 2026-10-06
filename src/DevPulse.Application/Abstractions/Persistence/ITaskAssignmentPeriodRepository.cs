namespace DevPulse.Application.Abstractions.Persistence;

public sealed record TaskCurrentAssignee(
    Guid AccountId,
    string TaskId,
    Guid DeveloperId,
    long? DateCreatedMs);

public interface ITaskAssignmentPeriodRepository
{
    /// <summary>
    /// Opens periods for newly assigned people and closes periods for people no longer assigned.
    /// Never deletes existing period rows.
    /// </summary>
    Task ApplyCurrentAssigneesAsync(
        IReadOnlyList<TaskCurrentAssignee> currentAssignees,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken = default);

    Task InsertIfMissingAsync(
        IReadOnlyList<TaskAssignmentPeriod> periods,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Corrects the start of an open period when demo/seed data knows a later handoff time.
    /// Never deletes the row.
    /// </summary>
    Task AdjustOpenPeriodStartAsync(
        Guid accountId,
        string taskId,
        Guid developerId,
        DateTime assignedAtUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskAssignmentPeriod>> GetOverlappingAsync(
        IReadOnlyList<Guid> developerIds,
        DateTime rangeStartUtc,
        DateTime rangeEndExclusiveUtc,
        IReadOnlyList<Guid>? accountIds = null,
        CancellationToken cancellationToken = default);
}
