using Core.Domain.Models;
using ErrorOr;

namespace Core.Services;

internal interface ICbrClient
{
    public Task<ErrorOr<ExchangeRateReport>> GetExchangeRateReportAsync(
        DateOnly date,
        CancellationToken cancellationToken = default
    );
}