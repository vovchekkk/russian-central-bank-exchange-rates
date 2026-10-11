namespace Core.Infrastructure.Dto;

internal sealed class CurrencyRateCacheDto
{
    public ushort NumCode { get; init; }
    public string CharCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal UnitRate { get; init; }
}
