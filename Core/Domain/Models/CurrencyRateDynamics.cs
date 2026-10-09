namespace Core.Domain.Models;

public record CurrencyRateDynamics
{
    public string CharCode { get; init; }
    public string Name { get; init; }
    public decimal CurrentUnitRate { get; init; }
    public decimal? PreviousUnitRate { get; init; }
    public decimal? AbsoluteChange { get; init; }
    public decimal? PercentChange { get; init; }
}