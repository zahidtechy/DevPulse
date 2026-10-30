using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DevPulse.Application.Abstractions.Holidays;
using DevPulse.Shared.Contracts.Holidays;
using Ical.Net;
using Ical.Net.DataTypes;

namespace DevPulse.Infrastructure.GoogleCalendar;

public sealed class GoogleHolidayClient : IGoogleHolidaySource
{
    private const string CalendarId = "en.bd.official#holiday@group.v.calendar.google.com";
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleHolidayClient> _logger;

    public GoogleHolidayClient(HttpClient httpClient, ILogger<GoogleHolidayClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<GoogleHolidayDto>>> GetByYearAsync(
        int year,
        CancellationToken cancellationToken = default)
    {
        if (year < 1900 || year > 2100)
        {
            return Result<IReadOnlyList<GoogleHolidayDto>>.Failure("Year must be between 1900 and 2100.");
        }

        string content;
        try
        {
            var path = $"ical/{Uri.EscapeDataString(CalendarId)}/public/basic.ics";
            content = await _httpClient.GetStringAsync(path, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<IReadOnlyList<GoogleHolidayDto>>.Failure("Google Calendar took too long to respond. Please try again.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not fetch the Bangladesh Google holiday calendar");
            return Result<IReadOnlyList<GoogleHolidayDto>>.Failure("Could not fetch Bangladesh holidays from Google Calendar. Please try again later.");
        }

        try
        {
            return Result<IReadOnlyList<GoogleHolidayDto>>.Success(ParseHolidays(content, year, cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google returned invalid Bangladesh holiday data for {Year}", year);
            return Result<IReadOnlyList<GoogleHolidayDto>>.Failure("Google Calendar returned holiday data that could not be read safely. Please try again later.");
        }
    }

    private static IReadOnlyList<GoogleHolidayDto> ParseHolidays(string content, int year, CancellationToken cancellationToken)
    {
        var trimmed = content.Trim().TrimStart('\uFEFF').TrimStart();
        if (!trimmed.StartsWith("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase)
            || !trimmed.EndsWith("END:VCALENDAR", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The response is not an iCalendar document.");
        }

        var calendar = Calendar.Load(trimmed) ?? throw new InvalidDataException("The calendar could not be read.");
        var yearStart = new DateOnly(year, 1, 1);
        var yearEndExclusive = new DateOnly(year + 1, 1, 1);
        var holidays = new List<SourceHoliday>();

        // Evaluate events separately: calendar-wide occurrence de-duplication would
        // discard distinct holidays sharing the same date (e.g. May Day and Vesak).
        foreach (var calendarEvent in calendar.Events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!calendarEvent.IsActive)
            {
                continue;
            }

            if (calendarEvent.Start is null || string.IsNullOrWhiteSpace(calendarEvent.Summary)
                || string.IsNullOrWhiteSpace(calendarEvent.Uid))
            {
                throw new InvalidDataException("A Google holiday is missing its date, name, or identifier.");
            }

            if (!calendarEvent.IsAllDay)
            {
                continue;
            }

            var end = calendarEvent.Start.Add(calendarEvent.EffectiveDuration);
            var durationDays = end.Date.DayNumber - calendarEvent.Start.Date.DayNumber;
            if (end.HasTime || durationDays < 1 || durationDays > 366)
            {
                throw new InvalidDataException("A Google holiday has an invalid all-day date range.");
            }

            // Include events spanning the year boundary, and bound recurring events
            // to the selected year. Date-only values are never converted through UTC.
            var occurrences = calendarEvent.GetOccurrences(new CalDateTime(yearStart.AddDays(-durationDays)))
                .TakeWhile(occurrence => occurrence.Period.StartTime.Date < yearEndExclusive);

            foreach (var occurrence in occurrences)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fromDate = occurrence.Period.StartTime.Date;
                var endExclusive = occurrence.Period.EffectiveEndTime?.Date ?? fromDate.AddDays(durationDays);
                if (endExclusive <= fromDate)
                {
                    throw new InvalidDataException("A Google holiday ends before it starts.");
                }

                if (endExclusive <= yearStart)
                {
                    continue;
                }

                holidays.Add(new SourceHoliday(
                    calendarEvent.Uid,
                    fromDate,
                    endExclusive.AddDays(-1),
                    calendarEvent.Summary.Trim(),
                    string.IsNullOrWhiteSpace(calendarEvent.Description) ? null : calendarEvent.Description.Trim()));

                if (holidays.Count > 4096)
                {
                    throw new InvalidDataException("The Google holiday calendar contains too many events.");
                }
            }
        }

        return holidays.GroupBy(holiday => new { holiday.FromDate, holiday.ToDate })
            .OrderBy(group => group.Key.FromDate)
            .ThenBy(group => group.Key.ToDate)
            .Select(group =>
            {
                var name = string.Join(" / ", group.Select(holiday => holiday.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal));
                var descriptions = group.Select(holiday => holiday.Reason).OfType<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList();
                var reason = descriptions.Count == 0 ? null : string.Join(" / ", descriptions);

                if (name.Length > 200 || reason?.Length > 500)
                {
                    throw new InvalidDataException("A Google holiday exceeds the supported name or reason length.");
                }

                var keyContent = JsonSerializer.Serialize(new
                {
                    group.Key.FromDate,
                    group.Key.ToDate,
                    Name = name,
                    Reason = reason,
                    SourceIds = group.Select(holiday => holiday.SourceId).Distinct().Order(StringComparer.Ordinal).ToArray()
                });
                var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(keyContent)));

                return new GoogleHolidayDto(key, group.Key.FromDate, group.Key.ToDate, name, reason);
            }).ToList();
    }

    private sealed record SourceHoliday(string SourceId, DateOnly FromDate, DateOnly ToDate, string Name, string? Reason);
}
