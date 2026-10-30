namespace DevPulse.Shared.Contracts.Holidays;

public record GoogleHolidayDto(
    string Key,
    DateOnly FromDate,
    DateOnly ToDate,
    string Name,
    string? Reason);

public enum GoogleHolidayImportStatusDto
{
    New,
    AlreadyExists,
    Overlap
}

public record GoogleHolidayPreviewItemDto(
    GoogleHolidayDto Holiday,
    GoogleHolidayImportStatusDto Status,
    IReadOnlyList<HolidayDto> OverlappingHolidays);

public record GoogleHolidayPreviewDto(
    int Year,
    string Country,
    DateTime FetchedAtUtc,
    IReadOnlyList<GoogleHolidayPreviewItemDto> Holidays);

public record ImportGoogleHolidaysRequest(
    int Year,
    IReadOnlyList<string> HolidayKeys);

public record GoogleHolidayImportResultDto(
    int ImportedCount,
    int SkippedCount,
    GoogleHolidayPreviewDto Preview);
