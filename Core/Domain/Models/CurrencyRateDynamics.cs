namespace Core.Domain.Models;

public record CurrencyRateDynamics
{
    public required string CharCode { get; init; }
    public required string Name { get; init; }
    public required decimal CurrentUnitRate { get; init; }
    public required decimal? PreviousUnitRate { get; init; }
    public required decimal? AbsoluteChange { get; init; }
    public required decimal? PercentChange { get; init; }
}