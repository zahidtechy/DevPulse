namespace DevPulse.Client.Services.Notifications;

/// <summary>
/// Correlation keys for the toast bus.
///
/// Toasts created with the same key replace each other, so retrying an action that
/// previously failed clears the stale error instead of leaving it on screen until the
/// user closes it. Keys are scoped to a page, or to a workflow that several pages share.
/// </summary>
public static class ToastKeys
{
    public const string Login = "login";
    public const string ChangePassword = "change-password";

    public const string Attendance = "attendance";
    public const string AttendanceCorrectionSubmit = "attendance-correction-submit";
    public const string AttendanceSettings = "attendance-settings";

    public const string Developers = "developers";
    public const string Holidays = "holidays";
    public const string HubstaffOrganizations = "hubstaff-organizations";
    public const string ClickUpAccounts = "clickup-accounts";
    public const string Users = "users";

    public const string Leave = "leave";
    public const string LeaveTypes = "leave-types";
    public const string LeaveSettings = "leave-settings";
    public const string LeaveAnalytics = "leave-analytics";

    public const string Reports = "reports";
    public const string Analytics = "analytics";
    public const string TimeTracking = "time-tracking";

    // Shared workflows: the same operation can be triggered from more than one page,
    // and whichever attempt succeeds should clear the error raised by the other.
    public const string KpiSync = "kpi-sync";
    public const string HubstaffSync = "hubstaff-sync";
}
