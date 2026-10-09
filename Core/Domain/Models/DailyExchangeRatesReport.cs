namespace Core.Domain.Models;

public record DailyExchangeRatesReport
{
    public DateOnly CurrentDate { get; init; }
    public DateOnly PreviousDate { get; init; }
    public IReadOnlyList<CurrencyRateDynamics> AllRates { get; init; }
    public IReadOnlyList<CurrencyRateDynamics> TopGrown { get; init; }
    public IReadOnlyList<CurrencyRateDynamics> TopFallen { get; init; }
}