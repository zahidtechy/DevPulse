namespace DevPulse.Application.Abstractions.Leave;

public interface ILeaveTelegramService
{
    Task NotifyAsync(string htmlMessage, CancellationToken cancellationToken = default);

    Task<Result> SendTestAsync(CancellationToken cancellationToken = default);

    Task<LeaveSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<Result<LeaveSettingsDto>> UpdateSettingsAsync(UpdateLeaveSettingsRequest request, CancellationToken cancellationToken = default);

    Task<int> GetWeekendDaysBitmaskAsync(CancellationToken cancellationToken = default);
}
