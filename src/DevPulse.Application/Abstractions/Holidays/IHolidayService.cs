namespace DevPulse.Application.Abstractions.Holidays;

public interface IHolidayService
{
    Task<IReadOnlyList<HolidayDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default);

    Task<Result<HolidayDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<HolidayDto>> CreateAsync(CreateHolidayRequest request, CancellationToken cancellationToken = default);

    Task<Result<HolidayDto>> UpdateAsync(Guid id, UpdateHolidayRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
