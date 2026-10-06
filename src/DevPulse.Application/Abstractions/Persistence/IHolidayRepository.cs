namespace DevPulse.Application.Abstractions.Persistence;

public interface IHolidayRepository
{
    Task<IReadOnlyList<CompanyHoliday>> GetByYearAsync(int year, CancellationToken cancellationToken = default);

    Task<CompanyHoliday?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> HasOverlapAsync(DateOnly fromDate, DateOnly toDate, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task AddAsync(CompanyHoliday holiday, CancellationToken cancellationToken = default);

    Task UpdateAsync(CompanyHoliday holiday, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
