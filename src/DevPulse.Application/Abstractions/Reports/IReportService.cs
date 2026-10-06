namespace DevPulse.Application.Abstractions.Reports;

public interface IReportService
{
    Task<Result<DeveloperReportResponse>> GenerateDeveloperReportAsync(
        DeveloperReportRequest request,
        CancellationToken cancellationToken = default);
}
