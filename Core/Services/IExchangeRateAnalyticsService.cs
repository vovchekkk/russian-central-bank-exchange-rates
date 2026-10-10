using Core.Domain.Models;
using ErrorOr;

namespace Core.Services;

public interface IExchangeRateAnalyticsService
{
    public Task<ErrorOr<DailyExchangeRatesReport>> GetDailyAnalyticsAsync(
        DateOnly date,
        CancellationToken cancellationToken = default
    );
}