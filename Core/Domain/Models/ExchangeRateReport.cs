namespace Core.Domain.Models;

public record ExchangeRateReport
{
    public required DateOnly Date { get; init; }
    public required IReadOnlyList<CurrencyRate> Rates { get; init; }
}