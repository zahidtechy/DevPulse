namespace DevPulse.Application.Services.Holidays;

public sealed class HolidayService : IHolidayService
{
    private const int MaxNameLength = 200;
    private const int MaxReasonLength = 500;

    private readonly IHolidayRepository _holidayRepository;
    private readonly ILogger<HolidayService> _logger;

    public HolidayService(IHolidayRepository holidayRepository, ILogger<HolidayService> logger)
    {
        _holidayRepository = holidayRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<HolidayDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default)
    {
        var holidays = await _holidayRepository.GetByYearAsync(year, cancellationToken);
        return holidays.Select(MapToDto).ToList();
    }

    public async Task<Result<HolidayDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var holiday = await _holidayRepository.GetByIdAsync(id, cancellationToken);
        return holiday is null
            ? Result<HolidayDto>.Failure("Holiday was not found.")
            : Result<HolidayDto>.Success(MapToDto(holiday));
    }

    public async Task<Result<HolidayDto>> CreateAsync(CreateHolidayRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateRequestAsync(request.FromDate, request.ToDate, request.Name, request.Reason, null, cancellationToken);
        if (validation.IsFailure)
        {
            return Result<HolidayDto>.Failure(validation.Error!);
        }

        var holiday = new CompanyHoliday
        {
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            Name = request.Name.Trim(),
            Reason = NormalizeOptionalText(request.Reason)
        };

        await _holidayRepository.AddAsync(holiday, cancellationToken);
        _logger.LogInformation("Created holiday {HolidayName} ({FromDate} to {ToDate})", holiday.Name, holiday.FromDate, holiday.ToDate);

        return Result<HolidayDto>.Success(MapToDto(holiday));
    }

    public async Task<Result<HolidayDto>> UpdateAsync(Guid id, UpdateHolidayRequest request, CancellationToken cancellationToken = default)
    {
        var holiday = await _holidayRepository.GetByIdAsync(id, cancellationToken);
        if (holiday is null)
        {
            return Result<HolidayDto>.Failure("Holiday was not found.");
        }

        var validation = await ValidateRequestAsync(request.FromDate, request.ToDate, request.Name, request.Reason, id, cancellationToken);
        if (validation.IsFailure)
        {
            return Result<HolidayDto>.Failure(validation.Error!);
        }

        holiday.FromDate = request.FromDate;
        holiday.ToDate = request.ToDate;
        holiday.Name = request.Name.Trim();
        holiday.Reason = NormalizeOptionalText(request.Reason);

        await _holidayRepository.UpdateAsync(holiday, cancellationToken);
        _logger.LogInformation("Updated holiday {HolidayId}", id);

        return Result<HolidayDto>.Success(MapToDto(holiday));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var holiday = await _holidayRepository.GetByIdAsync(id, cancellationToken);
        if (holiday is null)
        {
            return Result.Failure("Holiday was not found.");
        }

        await _holidayRepository.DeleteAsync(id, cancellationToken);
        _logger.LogInformation("Deleted holiday {HolidayId}", id);

        return Result.Success();
    }

    private async Task<Result> ValidateRequestAsync(
        DateOnly fromDate,
        DateOnly toDate,
        string name,
        string? reason,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure("Holiday name is required.");
        }

        if (name.Trim().Length > MaxNameLength)
        {
            return Result.Failure($"Holiday name cannot exceed {MaxNameLength} characters.");
        }

        if (toDate < fromDate)
        {
            return Result.Failure("To date must be on or after from date.");
        }

        var normalizedReason = NormalizeOptionalText(reason);
        if (normalizedReason is not null && normalizedReason.Length > MaxReasonLength)
        {
            return Result.Failure($"Reason cannot exceed {MaxReasonLength} characters.");
        }

        if (await _holidayRepository.HasOverlapAsync(fromDate, toDate, excludeId, cancellationToken))
        {
            return Result.Failure("This date range overlaps with an existing holiday.");
        }

        return Result.Success();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static HolidayDto MapToDto(CompanyHoliday holiday) =>
        new(
            holiday.Id,
            holiday.FromDate,
            holiday.ToDate,
            holiday.Name,
            holiday.Reason,
            holiday.CreatedAtUtc);
}
