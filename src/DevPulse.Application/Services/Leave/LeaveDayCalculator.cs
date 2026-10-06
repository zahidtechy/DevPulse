namespace DevPulse.Application.Services.Leave;

public sealed class LeaveDayCalculator
{
    private readonly IHolidayRepository _holidayRepository;

    public LeaveDayCalculator(IHolidayRepository holidayRepository)
    {
        _holidayRepository = holidayRepository;
    }

    public async Task<decimal> CalculateDaysAsync(
        LeaveCountingModeDto countingMode,
        DateOnly fromDate,
        DateOnly toDate,
        int weekendDaysBitmask,
        CancellationToken cancellationToken = default)
    {
        if (toDate < fromDate)
        {
            return 0;
        }

        if (countingMode == LeaveCountingModeDto.CalendarDays)
        {
            return toDate.DayNumber - fromDate.DayNumber + 1;
        }

        var holidays = await _holidayRepository.GetByYearAsync(fromDate.Year, cancellationToken);
        if (toDate.Year != fromDate.Year)
        {
            var nextYearHolidays = await _holidayRepository.GetByYearAsync(toDate.Year, cancellationToken);
            holidays = holidays.Concat(nextYearHolidays).ToList();
        }

        var holidayDates = ExpandHolidayDates(holidays);
        var count = 0;
        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            if (IsWeekend(date, weekendDaysBitmask))
            {
                continue;
            }

            if (holidayDates.Contains(date))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private static HashSet<DateOnly> ExpandHolidayDates(IReadOnlyList<Domain.Entities.CompanyHoliday> holidays)
    {
        var dates = new HashSet<DateOnly>();
        foreach (var holiday in holidays)
        {
            for (var date = holiday.FromDate; date <= holiday.ToDate; date = date.AddDays(1))
            {
                dates.Add(date);
            }
        }

        return dates;
    }

    private static bool IsWeekend(DateOnly date, int weekendDaysBitmask)
    {
        var dayBit = 1 << (int)date.DayOfWeek;
        return (weekendDaysBitmask & dayBit) != 0;
    }
}
