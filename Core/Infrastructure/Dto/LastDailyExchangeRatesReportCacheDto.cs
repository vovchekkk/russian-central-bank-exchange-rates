namespace Core.Infrastructure.Dto;

internal sealed class LastDailyExchangeRatesReportCacheDto
{
    public DateOnly RequestedDate { get; init; }
    public List<ExchangeRateReportCacheDto>? HistoricalReports { get; init; } = [];
}
