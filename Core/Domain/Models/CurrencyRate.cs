namespace Core.Domain.Models;

public record CurrencyRate
{
    public ushort NumCode { get; init; }
    public string CharCode { get; init; }
    public string Name { get; init; }
    public decimal UnitRate { get; init; }
}