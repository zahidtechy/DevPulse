using Microsoft.AspNetCore.DataProtection;

namespace DevPulse.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var seedSettings = scope.ServiceProvider.GetRequiredService<IOptions<SeedAdminSettings>>().Value;
        var dbContext = scope.ServiceProvider.GetRequiredService<DevPulseDbContext>();
        var tokenProtector = scope.ServiceProvider.GetRequiredService<ITokenProtector>();
        var telegramOptions = scope.ServiceProvider.GetRequiredService<IOptions<LeaveTelegramOptions>>().Value;
        var dataProtectionProvider = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.NewGuid() });
            }
        }

       // await SeedAdminUserAsync(userManager, seedSettings, logger);
       // await SeedDemoClickUpAccountsAsync(dbContext, tokenProtector, logger);
        await SeedLeaveTelegramSettingsAsync(dbContext, telegramOptions, dataProtectionProvider, logger);
        await SeedAttendanceSettingsAsync(dbContext, logger);
    }

    private static async Task SeedAdminUserAsync(
        UserManager<ApplicationUser> userManager,
        SeedAdminSettings seedSettings,
        ILogger logger)
    {
        var admin = await userManager.FindByEmailAsync(seedSettings.Email);
        if (admin is not null)
        {
            return;
        }

        admin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = seedSettings.Email,
            Email = seedSettings.Email,
            DisplayName = seedSettings.DisplayName,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(admin, seedSettings.Password);
        if (!result.Succeeded)
        {
            logger.LogWarning("Failed to seed admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(admin, AppRoles.Admin);
        logger.LogInformation("Seeded default admin user {Email}", seedSettings.Email);
    }

    private static async Task SeedDemoClickUpAccountsAsync(
        DevPulseDbContext dbContext,
        ITokenProtector tokenProtector,
        ILogger logger)
    {
        if (await dbContext.ClickUpAccounts.AnyAsync(a => a.WorkspaceId.StartsWith(DemoSeedData.WorkspaceIdPrefix)))
        {
            return;
        }

        var encryptedToken = tokenProtector.Protect(DemoSeedData.DemoAccessToken);
        var seededAt = DateTime.UtcNow;

        var internalAccount = new ClickUpAccount
        {
            Id = DemoSeedData.InternalAccountId,
            Name = $"{AppBranding.CompanyName} Internal",
            WorkspaceId = $"{DemoSeedData.WorkspaceIdPrefix}DV-INTERNAL",
            EncryptedAccessToken = encryptedToken,
            IsActive = true,
            CreatedAtUtc = seededAt,
            LastValidatedAtUtc = seededAt,
            LastValidationMessage = "Demo workspace seeded for developer work reporting."
        };

        var acmeAccount = new ClickUpAccount
        {
            Id = DemoSeedData.AcmeAccountId,
            Name = "Client — Acme Corp",
            WorkspaceId = $"{DemoSeedData.WorkspaceIdPrefix}DV-ACME",
            EncryptedAccessToken = encryptedToken,
            IsActive = true,
            CreatedAtUtc = seededAt,
            LastValidatedAtUtc = seededAt,
            LastValidationMessage = "Demo workspace seeded for developer work reporting."
        };

        dbContext.ClickUpAccounts.AddRange(internalAccount, acmeAccount);
        await dbContext.SaveChangesAsync();
        logger.LogInformation(
            "Seeded demo ClickUp workspaces for {Company}: {AccountCount} accounts",
            AppBranding.CompanyName,
            2);
    }

    private static async Task SeedLeaveTelegramSettingsAsync(
        DevPulseDbContext dbContext,
        LeaveTelegramOptions telegramOptions,
        IDataProtectionProvider dataProtectionProvider,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(telegramOptions.BotToken) || string.IsNullOrWhiteSpace(telegramOptions.ChatId))
        {
            return;
        }

        var settings = await dbContext.LeaveSettings.FirstOrDefaultAsync();
        if (settings is null)
        {
            settings = new LeaveSettings();
            dbContext.LeaveSettings.Add(settings);
        }

        if (!string.IsNullOrWhiteSpace(settings.EncryptedTelegramBotToken)
            && !string.IsNullOrWhiteSpace(settings.TelegramChatId))
        {
            return;
        }

        var protector = dataProtectionProvider.CreateProtector("DevPulse.Telegram.BotTokens.v1");
        settings.EncryptedTelegramBotToken ??= protector.Protect(telegramOptions.BotToken.Trim());
        settings.TelegramChatId ??= telegramOptions.ChatId.Trim();
        settings.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();
        logger.LogInformation("Seeded leave Telegram group chat ID from appsettings.");
    }

    private static async Task SeedAttendanceSettingsAsync(DevPulseDbContext dbContext, ILogger logger)
    {
        if (!await dbContext.AttendanceSettings.AnyAsync())
        {
            dbContext.AttendanceSettings.Add(new AttendanceSettings());
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Seeded default attendance settings.");
            return;
        }

        var settings = await dbContext.AttendanceSettings.FirstAsync();
        var updated = false;

        if (settings.PunchInAllowMinutesBeforeWorkStart == 0)
        {
            settings.PunchInAllowMinutesBeforeWorkStart = 60;
            updated = true;
        }

        if (settings.PunchOutAllowMinutesAfterWorkEnd == 0)
        {
            settings.PunchOutAllowMinutesAfterWorkEnd = 120;
            updated = true;
        }

        if (updated)
        {
            settings.UpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Backfilled attendance punch window defaults.");
        }
    }
}
