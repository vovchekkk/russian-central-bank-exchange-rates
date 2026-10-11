namespace Core.Infrastructure.Dto;

internal sealed class ExchangeRateReportCacheDto
{
    public DateOnly Date { get; init; }
    public List<CurrencyRateCacheDto>? Rates { get; init; } = [];
}
