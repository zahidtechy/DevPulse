namespace DevPulse.Application.Services.Holidays;

public sealed class HolidayService : IHolidayService
{
    private const int MaxNameLength = 200;
    private const int MaxReasonLength = 500;

    private readonly IHolidayRepository _holidayRepository;
    private readonly IGoogleHolidaySource _googleHolidaySource;
    private readonly ILogger<HolidayService> _logger;

    public HolidayService(
        IHolidayRepository holidayRepository,
        IGoogleHolidaySource googleHolidaySource,
        ILogger<HolidayService> logger)
    {
        _holidayRepository = holidayRepository;
        _googleHolidaySource = googleHolidaySource;
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

    public async Task<Result<GoogleHolidayPreviewDto>> PreviewGoogleHolidaysAsync(
        int year,
        CancellationToken cancellationToken = default)
    {
        if (year < 1900 || year > 2100)
        {
            return Result<GoogleHolidayPreviewDto>.Failure("Year must be between 1900 and 2100.");
        }

        var sourceResult = await _googleHolidaySource.GetByYearAsync(year, cancellationToken);
        if (sourceResult.IsFailure)
        {
            return Result<GoogleHolidayPreviewDto>.Failure(sourceResult.Error!);
        }

        var existing = await _holidayRepository.GetByYearAsync(year, cancellationToken);
        return Result<GoogleHolidayPreviewDto>.Success(BuildGooglePreview(year, sourceResult.Value!, existing));
    }

    public async Task<Result<GoogleHolidayImportResultDto>> ImportGoogleHolidaysAsync(
        ImportGoogleHolidaysRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Year < 1900 || request.Year > 2100)
        {
            return Result<GoogleHolidayImportResultDto>.Failure("Year must be between 1900 and 2100.");
        }

        if (request.HolidayKeys is null || request.HolidayKeys.Count == 0 || request.HolidayKeys.Count > 366
            || request.HolidayKeys.Any(key => string.IsNullOrWhiteSpace(key) || key.Length != 64))
        {
            return Result<GoogleHolidayImportResultDto>.Failure("Select between 1 and 366 holidays from the Google preview.");
        }

        var keys = request.HolidayKeys.ToHashSet(StringComparer.Ordinal);
        if (keys.Count != request.HolidayKeys.Count)
        {
            return Result<GoogleHolidayImportResultDto>.Failure("Select each holiday only once.");
        }

        // Fetch again rather than trusting names/dates posted by the browser. Content-based
        // keys also prevent importing a holiday whose Google data changed after the preview.
        var sourceResult = await _googleHolidaySource.GetByYearAsync(request.Year, cancellationToken);
        if (sourceResult.IsFailure)
        {
            return Result<GoogleHolidayImportResultDto>.Failure(sourceResult.Error!);
        }

        var selected = sourceResult.Value!.Where(holiday => keys.Contains(holiday.Key)).ToList();
        if (selected.Count != keys.Count)
        {
            return Result<GoogleHolidayImportResultDto>.Failure("The selected Google holidays have changed or are no longer available. Fetch again before importing.");
        }

        foreach (var holiday in selected)
        {
            var validation = ValidateFields(holiday.FromDate, holiday.ToDate, holiday.Name, holiday.Reason);
            if (validation.IsFailure)
            {
                return Result<GoogleHolidayImportResultDto>.Failure(validation.Error!);
            }
        }

        var candidates = selected.Select(holiday => new CompanyHoliday
        {
            FromDate = holiday.FromDate,
            ToDate = holiday.ToDate,
            Name = holiday.Name.Trim(),
            Reason = NormalizeOptionalText(holiday.Reason)
        }).ToList();

        // The repository rechecks current date ranges inside the import transaction.
        // This is insert-only: existing holidays (including manual edits) are never changed.
        var imported = await _holidayRepository.AddNonOverlappingAsync(candidates, cancellationToken);
        var existing = await _holidayRepository.GetByYearAsync(request.Year, cancellationToken);
        var skippedCount = selected.Count - imported.Count;

        _logger.LogInformation("Imported {ImportedCount} Google holidays for {Year}; skipped {SkippedCount} overlapping entries",
            imported.Count, request.Year, skippedCount);

        return Result<GoogleHolidayImportResultDto>.Success(new GoogleHolidayImportResultDto(
            imported.Count,
            skippedCount,
            BuildGooglePreview(request.Year, sourceResult.Value!, existing)));
    }

    private static GoogleHolidayPreviewDto BuildGooglePreview(
        int year,
        IReadOnlyList<GoogleHolidayDto> holidays,
        IReadOnlyList<CompanyHoliday> existing)
    {
        var items = holidays.OrderBy(holiday => holiday.FromDate).ThenBy(holiday => holiday.Name)
            .Select(holiday =>
            {
                var overlaps = existing
                    .Where(item => item.FromDate <= holiday.ToDate && item.ToDate >= holiday.FromDate)
                    .OrderBy(item => item.FromDate)
                    .Select(MapToDto)
                    .ToList();

                var alreadyExists = overlaps.Any(item => item.FromDate == holiday.FromDate
                    && item.ToDate == holiday.ToDate
                    && string.Equals(item.Name.Trim(), holiday.Name.Trim(), StringComparison.OrdinalIgnoreCase));

                var status = overlaps.Count == 0
                    ? GoogleHolidayImportStatusDto.New
                    : alreadyExists
                        ? GoogleHolidayImportStatusDto.AlreadyExists
                        : GoogleHolidayImportStatusDto.Overlap;

                return new GoogleHolidayPreviewItemDto(holiday, status, overlaps);
            }).ToList();

        return new GoogleHolidayPreviewDto(year, "Bangladesh", DateTime.UtcNow, items);
    }

    private async Task<Result> ValidateRequestAsync(
        DateOnly fromDate,
        DateOnly toDate,
        string name,
        string? reason,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var validation = ValidateFields(fromDate, toDate, name, reason);
        if (validation.IsFailure)
        {
            return validation;
        }

        if (await _holidayRepository.HasOverlapAsync(fromDate, toDate, excludeId, cancellationToken))
        {
            return Result.Failure("This date range overlaps with an existing holiday.");
        }

        return Result.Success();
    }

    private static Result ValidateFields(DateOnly fromDate, DateOnly toDate, string name, string? reason)
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
