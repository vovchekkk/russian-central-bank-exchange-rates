namespace Core.Domain.Models;

public record CurrencyRate
{
    public Guid Id { get; init; }
    public ushort NumCode { get; init; }
    public string CharCode { get; init; }
    public int Nominal { get; init; }
    public string Name { get; init; }
    public decimal Value { get; init; }
    public decimal UnitRate { get; init; }
}