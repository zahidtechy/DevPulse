namespace DevPulse.Infrastructure.Persistence.Repositories;

public sealed class HolidayRepository : IHolidayRepository
{
    private readonly DevPulseDbContext _dbContext;

    public HolidayRepository(DevPulseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CompanyHoliday>> GetByYearAsync(int year, CancellationToken cancellationToken = default)
    {
        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);

        return await _dbContext.CompanyHolidays
            .Where(x => x.FromDate <= yearEnd && x.ToDate >= yearStart)
            .OrderBy(x => x.FromDate)
            .ThenBy(x => x.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<CompanyHoliday?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.CompanyHolidays.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<bool> HasOverlapAsync(
        DateOnly fromDate,
        DateOnly toDate,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.CompanyHolidays
            .Where(x => x.FromDate <= toDate && x.ToDate >= fromDate);

        if (excludeId.HasValue)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(CompanyHoliday holiday, CancellationToken cancellationToken = default)
    {
        _dbContext.CompanyHolidays.Add(holiday);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CompanyHoliday holiday, CancellationToken cancellationToken = default)
    {
        _dbContext.CompanyHolidays.Update(holiday);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var holiday = await _dbContext.CompanyHolidays.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (holiday is null)
        {
            return;
        }

        _dbContext.CompanyHolidays.Remove(holiday);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
