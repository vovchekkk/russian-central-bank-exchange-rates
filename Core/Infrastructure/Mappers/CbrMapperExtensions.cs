using System.Globalization;
using Core.Domain.Models;
using Infrastructure.Dto;

namespace Core.Infrastructure.Mappers;

public static class CbrMapperExtensions
{
    private static readonly CultureInfo RuCulture = new("ru-RU");
    
    public static CurrencyRate ToDomain(this ValCursValute dto)
    {
        return new CurrencyRate{
            Id = Guid.Parse(dto.Id),
            NumCode = dto.NumCode,
            CharCode = dto.CharCode,
            Nominal = (int)dto.Nominal,
            Name = dto.Name,
            Value = decimal.Parse(dto.Value, RuCulture),
            UnitRate = decimal.Parse(dto.VunitRate, RuCulture)
        };
    }
    
    public static ExchangeRateReport ToDomain(this ValCurs domain)
    {
        return new ExchangeRateReport{
            Date = DateOnly.ParseExact(domain.Date, "dd.MM.yyyy", CultureInfo.InvariantCulture),
            Rates = domain.Valute.Select(v => v.ToDomain()).ToList()
        };
    }
}