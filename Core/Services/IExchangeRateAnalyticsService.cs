using Core.Domain.Models;

namespace Core.Services;

public interface IExchangeRateAnalyticsService
{
    Task<DailyExchangeRatesReport> GetDailyAnalyticsAsync(
        DateOnly date,
        CancellationToken cancellationToken = default
    );
}