using DevPulse.Infrastructure.ClickUp;
using DevPulse.Infrastructure.Jobs;
using DevPulse.Infrastructure.Persistence;
using DevPulse.Infrastructure.Persistence.Repositories;
using DevPulse.Infrastructure.Security;
using DevPulse.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace DevPulse.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DevPulse");
        services.AddDbContext<DevPulseDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<SeedAdminSettings>(configuration.GetSection(SeedAdminSettings.SectionName));
        services.Configure<KpiSyncOptions>(configuration.GetSection(KpiSyncOptions.SectionName));
        services.Configure<ClickUpApiOptions>(configuration.GetSection(ClickUpApiOptions.SectionName));
        services.Configure<LeaveTelegramOptions>(configuration.GetSection(LeaveTelegramOptions.SectionName));
        services.Configure<HubstaffSyncOptions>(configuration.GetSection(HubstaffSyncOptions.SectionName));
        services.Configure<HubstaffApiOptions>(configuration.GetSection(HubstaffApiOptions.SectionName));

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<DevPulseDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "DevPulse.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);

            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };

            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services.AddDataProtection();
        services.AddScoped<ITokenProtector, DataProtectionTokenProtector>();
        services.AddScoped<IClickUpAccountRepository, ClickUpAccountRepository>();
        services.AddScoped<IDeveloperRepository, DeveloperRepository>();
        services.AddScoped<ISyncedTaskRepository, SyncedTaskRepository>();
        services.AddScoped<ITaskAssignmentPeriodRepository, TaskAssignmentPeriodRepository>();
        services.AddScoped<IKpiSyncRunRepository, KpiSyncRunRepository>();
        services.AddScoped<IHolidayRepository, HolidayRepository>();
        services.AddScoped<ILeaveRepository, LeaveRepository>();
        services.AddScoped<IAttendanceRepository, AttendanceRepository>();
        services.AddScoped<IHubstaffOrganizationRepository, HubstaffOrganizationRepository>();
        services.AddScoped<IHubstaffDailyActivityRepository, HubstaffDailyActivityRepository>();
        services.AddScoped<IHubstaffSyncRunRepository, HubstaffSyncRunRepository>();
        services.AddScoped<IUserEmailLookup, UserEmailLookup>();
        services.AddScoped<IUserDeveloperResolver, UserDeveloperResolver>();
        services.AddScoped<ILeaveTelegramService, LeaveTelegramService>();
        services.AddSingleton<LeaveTelegramNotificationQueue>();
        services.AddSingleton<ILeaveTelegramNotificationQueue>(sp =>
            sp.GetRequiredService<LeaveTelegramNotificationQueue>());
        services.AddSingleton<JwtTokenGenerator>();
        services.AddSingleton<ClickUpApiRateLimiter>();
        services.AddSingleton<HubstaffApiRateLimiter>();
        services.AddScoped<IHubstaffTokenProtector, HubstaffTokenProtector>();
        services.AddScoped<IHubstaffTokenProvider, HubstaffTokenProvider>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddHostedService<KpiSyncBackgroundService>();
        services.AddHostedService<HubstaffSyncBackgroundService>();
        services.AddHostedService<LeaveTelegramNotificationBackgroundService>();

        services.AddHttpClient<IClickUpApiClient, ClickUpApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.clickup.com/api/v2/");
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.Timeout = TimeSpan.FromDays(2);
        });

        services.AddHttpClient<IHubstaffAuthClient, HubstaffAuthClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HubstaffApiOptions>>().Value;
            client.BaseAddress = new Uri(options.AuthBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient<IHubstaffApiClient, HubstaffApiClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HubstaffApiOptions>>().Value;
            client.BaseAddress = new Uri(options.ApiBaseUrl);
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        services.AddHttpClient<ITelegramApiClient, Telegram.TelegramApiClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DevPulseDbContext>();
        await dbContext.Database.MigrateAsync();
        await DatabaseSeeder.SeedAsync(serviceProvider);
    }
}
