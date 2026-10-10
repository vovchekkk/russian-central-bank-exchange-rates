using Core.Domain.Models;

namespace Core.Services;

public interface IPdfReportService
{
    Task<byte[]> GenerateDailyReport(
        DailyExchangeRatesReport report,
        CancellationToken cancellationToken = default
    );
}