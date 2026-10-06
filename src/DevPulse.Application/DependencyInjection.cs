namespace DevPulse.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IClickUpAccountService, ClickUpAccountService>();
        services.AddScoped<IDeveloperService, DeveloperService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IReportExportService, ReportExcelExportService>();
        services.AddScoped<IKpiSyncService, KpiSyncService>();
        services.AddScoped<ICachedAnalyticsService, CachedAnalyticsService>();
        services.AddScoped<IHolidayService, HolidayService>();
        services.AddScoped<LeaveDayCalculator>();
        services.AddScoped<ILeaveService, LeaveService>();
        services.AddScoped<AttendanceStatusCalculator>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<IHubstaffOrganizationService, HubstaffOrganizationService>();
        services.AddScoped<IHubstaffSyncService, HubstaffSyncService>();
        services.AddScoped<IHubstaffAnalyticsService, HubstaffAnalyticsService>();
        return services;
    }
}
