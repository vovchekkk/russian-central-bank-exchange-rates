namespace Core.Domain.Models;

public record DailyExchangeRatesReport
{
    public required DateOnly CurrentDate { get; init; }
    public required DateOnly PreviousDate { get; init; }
    public required IReadOnlyList<CurrencyRateDynamics> AllRates { get; init; }
    public required IReadOnlyList<CurrencyRateDynamics> TopGrown { get; init; }
    public required IReadOnlyList<CurrencyRateDynamics> TopFallen { get; init; }
    public required decimal? AveragePercentChange { get; init; }
}