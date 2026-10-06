namespace DevPulse.Domain.Entities;

/// <summary>
/// Company holiday spanning one or more calendar days.
/// </summary>
public class CompanyHoliday
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
