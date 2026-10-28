namespace DevPulse.Client.Services.Notifications;

/// <summary>
/// Workflow keys for the toast bus.
///
/// The toast bus displays one global notification at a time, regardless of key. Keys remain
/// available so a workflow can explicitly dismiss or query its currently visible toast.
/// </summary>
public static class ToastKeys
{
    public const string Login = "login";
    public const string ChangePassword = "change-password";
    public const string Session = "session";

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
