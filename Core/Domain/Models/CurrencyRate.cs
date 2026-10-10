namespace Core.Domain.Models;

public record CurrencyRate
{
    public required ushort NumCode { get; init; }
    public required string CharCode { get; init; }
    public required string Name { get; init; }
    public required decimal UnitRate { get; init; }
}