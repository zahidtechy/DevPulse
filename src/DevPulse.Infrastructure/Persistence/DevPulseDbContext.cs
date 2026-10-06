using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace DevPulse.Infrastructure.Persistence;

public sealed class DevPulseDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public DevPulseDbContext(DbContextOptions<DevPulseDbContext> options) : base(options)
    {
    }

    public DbSet<ClickUpAccount> ClickUpAccounts => Set<ClickUpAccount>();

    public DbSet<Developer> Developers => Set<Developer>();

    public DbSet<DeveloperClickUpMapping> DeveloperClickUpMappings => Set<DeveloperClickUpMapping>();

    public DbSet<SyncedTask> SyncedTasks => Set<SyncedTask>();

    public DbSet<TaskAssignmentPeriod> TaskAssignmentPeriods => Set<TaskAssignmentPeriod>();

    public DbSet<KpiSyncRun> KpiSyncRuns => Set<KpiSyncRun>();

    public DbSet<DeveloperKpiSnapshot> DeveloperKpiSnapshots => Set<DeveloperKpiSnapshot>();

    public DbSet<CompanyHoliday> CompanyHolidays => Set<CompanyHoliday>();

    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();

    public DbSet<LeaveApplication> LeaveApplications => Set<LeaveApplication>();

    public DbSet<LeaveSettings> LeaveSettings => Set<LeaveSettings>();

    public DbSet<AttendanceSettings> AttendanceSettings => Set<AttendanceSettings>();

    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    public DbSet<AttendanceCorrectionRequest> AttendanceCorrectionRequests => Set<AttendanceCorrectionRequest>();

    public DbSet<HubstaffOrganization> HubstaffOrganizations => Set<HubstaffOrganization>();

    public DbSet<DeveloperHubstaffMapping> DeveloperHubstaffMappings => Set<DeveloperHubstaffMapping>();

    public DbSet<HubstaffDailyActivity> HubstaffDailyActivities => Set<HubstaffDailyActivity>();

    public DbSet<HubstaffSyncRun> HubstaffSyncRuns => Set<HubstaffSyncRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => x.DeveloperId)
                .IsUnique()
                .HasFilter("[DeveloperId] IS NOT NULL");

            entity.HasOne(x => x.Developer)
                .WithMany()
                .HasForeignKey(x => x.DeveloperId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ClickUpAccount>(entity =>
        {
            entity.ToTable("ClickUpAccounts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.WorkspaceId).HasMaxLength(50).IsRequired();
            entity.Property(x => x.EncryptedAccessToken).IsRequired();
            entity.Property(x => x.LastValidationMessage).HasMaxLength(500);
            entity.HasIndex(x => x.WorkspaceId).IsUnique();
        });

        modelBuilder.Entity<Developer>(entity =>
        {
            entity.ToTable("Developers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.WorkRole).HasConversion<int>();
            entity.HasIndex(x => x.Email);
            entity.HasIndex(x => x.ReportingManagerDeveloperId);

            entity.HasOne(x => x.ReportingManager)
                .WithMany()
                .HasForeignKey(x => x.ReportingManagerDeveloperId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<DeveloperClickUpMapping>(entity =>
        {
            entity.ToTable("DeveloperClickUpMappings");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.DeveloperId, x.ClickUpAccountId }).IsUnique();
            entity.HasIndex(x => new { x.ClickUpAccountId, x.ClickUpUserId }).IsUnique();

            entity.HasOne(x => x.Developer)
                .WithMany(x => x.ClickUpMappings)
                .HasForeignKey(x => x.DeveloperId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.ClickUpAccount)
                .WithMany()
                .HasForeignKey(x => x.ClickUpAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SyncedTask>(entity =>
        {
            entity.ToTable("SyncedTasks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AccountName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ProjectName).HasMaxLength(200);
            entity.Property(x => x.FolderName).HasMaxLength(200);
            entity.Property(x => x.TaskId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.TaskName).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(100);
            entity.Property(x => x.Priority).HasMaxLength(50);
            entity.Property(x => x.ListName).HasMaxLength(200);
            entity.Property(x => x.Url).HasMaxLength(1000);
            entity.Property(x => x.ParentTaskId).HasMaxLength(64);
            entity.Property(x => x.ParentTaskName).HasMaxLength(500);
            entity.Property(x => x.TaskType).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => new { x.AccountId, x.TaskId }).IsUnique();
            entity.HasIndex(x => new { x.AccountId, x.IsCompleted, x.DateDone });
            entity.HasIndex(x => new { x.AccountId, x.IsCompleted, x.DateCreated });

            entity.HasOne(x => x.Account)
                .WithMany()
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskAssignmentPeriod>(entity =>
        {
            entity.ToTable("TaskAssignmentPeriods");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TaskId).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => new { x.AccountId, x.TaskId, x.DeveloperId, x.UnassignedAtUtc });
            entity.HasIndex(x => new { x.DeveloperId, x.AssignedAtUtc, x.UnassignedAtUtc });
            entity.HasIndex(x => new { x.AccountId, x.TaskId, x.DeveloperId })
                .IsUnique()
                .HasFilter("[UnassignedAtUtc] IS NULL")
                .HasDatabaseName("IX_TaskAssignmentPeriods_Open");

            entity.HasOne(x => x.Account)
                .WithMany()
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Developer)
                .WithMany()
                .HasForeignKey(x => x.DeveloperId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KpiSyncRun>(entity =>
        {
            entity.ToTable("KpiSyncRuns");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ErrorMessage).HasMaxLength(2000);
            entity.HasIndex(x => x.StartedAtUtc);
        });

        modelBuilder.Entity<DeveloperKpiSnapshot>(entity =>
        {
            entity.ToTable("DeveloperKpiSnapshots");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DeveloperName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.HasIndex(x => new { x.FromDate, x.ToDate, x.DeveloperId });

            entity.HasOne(x => x.SyncRun)
                .WithMany()
                .HasForeignKey(x => x.SyncRunId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Developer)
                .WithMany()
                .HasForeignKey(x => x.DeveloperId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CompanyHoliday>(entity =>
        {
            entity.ToTable("CompanyHolidays");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(500);
            entity.HasIndex(x => new { x.FromDate, x.ToDate });
        });

        modelBuilder.Entity<LeaveType>(entity =>
        {
            entity.ToTable("LeaveTypes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PolicyNotes).HasMaxLength(1000);
            entity.Property(x => x.CountingMode).HasConversion<int>();
            entity.HasIndex(x => x.Name);
        });

        modelBuilder.Entity<LeaveApplication>(entity =>
        {
            entity.ToTable("LeaveApplications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.ReviewerComment).HasMaxLength(1000);
            entity.Property(x => x.RequestedDays).HasPrecision(5, 1);
            entity.Property(x => x.Status).HasConversion<int>();
            entity.HasIndex(x => new { x.ApplicantDeveloperId, x.Status });
            entity.HasIndex(x => new { x.ApproverDeveloperId, x.Status });
            entity.HasIndex(x => x.FromDate);

            entity.HasOne(x => x.ApplicantDeveloper)
                .WithMany()
                .HasForeignKey(x => x.ApplicantDeveloperId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ApproverDeveloper)
                .WithMany()
                .HasForeignKey(x => x.ApproverDeveloperId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.LeaveType)
                .WithMany()
                .HasForeignKey(x => x.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LeaveSettings>(entity =>
        {
            entity.ToTable("LeaveSettings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EncryptedTelegramBotToken).HasMaxLength(2000);
            entity.Property(x => x.TelegramChatId).HasMaxLength(50);
            entity.Property(x => x.LastTelegramError).HasMaxLength(1000);
        });

        modelBuilder.Entity<AttendanceSettings>(entity =>
        {
            entity.ToTable("AttendanceSettings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OfficeTimeZoneId).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.ToTable("AttendanceRecords");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.DeveloperId, x.WorkDate }).IsUnique();

            entity.HasOne(x => x.Developer)
                .WithMany()
                .HasForeignKey(x => x.DeveloperId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AttendanceCorrectionRequest>(entity =>
        {
            entity.ToTable("AttendanceCorrectionRequests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.ReviewerComment).HasMaxLength(1000);
            entity.Property(x => x.Status).HasConversion<int>();
            entity.HasIndex(x => new { x.DeveloperId, x.WorkDate, x.Status });

            entity.HasOne(x => x.Developer)
                .WithMany()
                .HasForeignKey(x => x.DeveloperId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<HubstaffOrganization>(entity =>
        {
            entity.ToTable("HubstaffOrganizations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.HubstaffOrganizationName).HasMaxLength(200);
            entity.Property(x => x.EncryptedPersonalAccessToken).IsRequired();
            entity.Property(x => x.LastValidationMessage).HasMaxLength(500);
            entity.HasIndex(x => x.OrganizationId).IsUnique();
        });

        modelBuilder.Entity<DeveloperHubstaffMapping>(entity =>
        {
            entity.ToTable("DeveloperHubstaffMappings");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.HubstaffOrganizationId, x.DeveloperId }).IsUnique();
            entity.HasIndex(x => new { x.HubstaffOrganizationId, x.HubstaffUserId }).IsUnique();

            entity.HasOne(x => x.Developer)
                .WithMany(x => x.HubstaffMappings)
                .HasForeignKey(x => x.DeveloperId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.HubstaffOrganization)
                .WithMany(x => x.Mappings)
                .HasForeignKey(x => x.HubstaffOrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<HubstaffDailyActivity>(entity =>
        {
            entity.ToTable("HubstaffDailyActivities");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProjectName).HasMaxLength(200);
            entity.Property(x => x.HubstaffUserEmail).HasMaxLength(320);
            entity.HasIndex(x => new { x.HubstaffOrganizationId, x.HubstaffDailyActivityId }).IsUnique();
            entity.HasIndex(x => new { x.HubstaffOrganizationId, x.WorkDate });
            entity.HasIndex(x => new { x.DeveloperId, x.WorkDate });
            entity.HasIndex(x => new { x.HubstaffOrganizationId, x.HubstaffUserId, x.WorkDate });

            entity.HasOne(x => x.HubstaffOrganization)
                .WithMany(x => x.DailyActivities)
                .HasForeignKey(x => x.HubstaffOrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Developer)
                .WithMany()
                .HasForeignKey(x => x.DeveloperId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<HubstaffSyncRun>(entity =>
        {
            entity.ToTable("HubstaffSyncRuns");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ErrorMessage).HasMaxLength(2000);
            entity.Property(x => x.Status).HasConversion<int>();
            entity.HasIndex(x => x.StartedAtUtc);
        });
    }
}
