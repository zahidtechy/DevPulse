namespace DevPulse.Client.Components;

public static class WorkDurationFormatter
{
    public static string Format(TimeSpan duration, bool includeSeconds = false)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        var hours = (int)duration.TotalHours;
        var minutes = duration.Minutes;
        var seconds = duration.Seconds;

        if (hours > 0)
        {
            return includeSeconds
                ? $"{hours}h {minutes:00}m {seconds:00}s"
                : $"{hours}h {minutes:00}m";
        }

        if (minutes > 0)
        {
            return includeSeconds
                ? $"{minutes}m {seconds:00}s"
                : $"{minutes}m";
        }

        return includeSeconds ? $"{seconds}s" : "<1m";
    }
}
