using Core.Domain.Models;
using ErrorOr;
using Infrastructure.Dto;

namespace Core.Services;

public interface ICbrClient
{
    public Task<ErrorOr<ExchangeRateReport>> GetExchangeRateReportAsync(
        DateOnly date,
        CancellationToken cancellationToken = default
    );
}