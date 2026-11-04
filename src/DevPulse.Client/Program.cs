using DevPulse.Client;
using DevPulse.Client.Services.Notifications;
using DevPulse.Client.Services.Ui;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
var applicationBaseUri = new Uri(builder.HostEnvironment.BaseAddress);

builder.Services.AddSingleton(AppJsonOptions.Default);
builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(DevPulse.Shared.Constants.AppRoles.Admin));
    options.AddPolicy("CanViewReports", policy =>
        policy.RequireRole(
            DevPulse.Shared.Constants.AppRoles.Admin,
            DevPulse.Shared.Constants.AppRoles.User));
});

builder.Services.AddSingleton<SessionState>();
builder.Services.AddScoped<AccessTokenStore>();
builder.Services.AddScoped(sp => new AuthApiClient(
    applicationBaseUri,
    sp.GetRequiredService<JsonSerializerOptions>()));
builder.Services.AddScoped<TokenRefreshCoordinator>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddScoped(sp =>
{
    var handler = new JwtAuthorizationHandler(
        sp.GetRequiredService<AccessTokenStore>(),
        sp.GetRequiredService<TokenRefreshCoordinator>(),
        sp.GetRequiredService<SessionState>(),
        applicationBaseUri)
    {
        InnerHandler = new HttpClientHandler()
    };

    return new HttpClient(handler)
    {
        BaseAddress = applicationBaseUri
    };
});

builder.Services.AddSingleton<ToastService>();
builder.Services.AddScoped<ShellProgressService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IViewportService, ViewportService>();
builder.Services.AddScoped<IClickUpAccountApiClient, ClickUpAccountApiClient>();
builder.Services.AddScoped<IDeveloperApiClient, DeveloperApiClient>();
builder.Services.AddScoped<IReportApiClient, ReportApiClient>();
builder.Services.AddScoped<IAnalyticsApiClient, AnalyticsApiClient>();
builder.Services.AddScoped<IUserApiClient, UserApiClient>();
builder.Services.AddScoped<IHolidayApiClient, HolidayApiClient>();
builder.Services.AddScoped<ILeaveApiClient, LeaveApiClient>();
builder.Services.AddScoped<IAttendanceApiClient, AttendanceApiClient>();
builder.Services.AddScoped<IHubstaffOrganizationApiClient, HubstaffOrganizationApiClient>();
builder.Services.AddScoped<IHubstaffSyncApiClient, HubstaffSyncApiClient>();
builder.Services.AddScoped<IHubstaffAnalyticsApiClient, HubstaffAnalyticsApiClient>();

await builder.Build().RunAsync();
