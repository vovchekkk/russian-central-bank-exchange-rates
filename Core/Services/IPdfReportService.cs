using Core.Domain.Models;

namespace Core.Services;

public interface IPdfReportService
{
    Task<byte[]> GenerateDailyReportAsync(
        DailyExchangeRatesReport report,
        CancellationToken cancellationToken = default
    );
}