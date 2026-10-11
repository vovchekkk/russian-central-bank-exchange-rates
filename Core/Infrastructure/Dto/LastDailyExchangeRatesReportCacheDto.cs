namespace Core.Infrastructure.Dto;

internal sealed class LastDailyExchangeRatesReportCacheDto
{
    public DateOnly RequestedDate { get; init; }
    public ExchangeRateReportCacheDto? CurrentReport { get; init; }
    public ExchangeRateReportCacheDto? PreviousReport { get; init; }
}
