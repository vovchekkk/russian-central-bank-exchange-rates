using Core.Domain.Models;
using ErrorOr;

namespace Core.Services;

public interface IPdfReportService
{
    public Task<ErrorOr<byte[]>> GenerateDailyReportAsync(
        DailyExchangeRatesReport report,
        CancellationToken cancellationToken = default
    );
}