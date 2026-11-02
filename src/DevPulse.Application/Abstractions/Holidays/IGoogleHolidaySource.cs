namespace DevPulse.Application.Abstractions.Holidays;

public interface IGoogleHolidaySource
{
    Task<Result<IReadOnlyList<GoogleHolidayDto>>> GetByYearAsync(
        int year,
        CancellationToken cancellationToken = default);
}
