namespace DevPulse.Shared.Contracts.Holidays;

public record HolidayDto(
    Guid Id,
    DateOnly FromDate,
    DateOnly ToDate,
    string Name,
    string? Reason,
    DateTime CreatedAtUtc);

public record CreateHolidayRequest(
    DateOnly FromDate,
    DateOnly ToDate,
    string Name,
    string? Reason);

public record UpdateHolidayRequest(
    DateOnly FromDate,
    DateOnly ToDate,
    string Name,
    string? Reason);
