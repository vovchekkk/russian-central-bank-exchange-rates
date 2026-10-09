namespace Core.Domain.Models;

public record ExchangeRateReport
{
    public DateOnly Date { get; init; }
    public IReadOnlyList<CurrencyRate> Rates { get; init; }
}