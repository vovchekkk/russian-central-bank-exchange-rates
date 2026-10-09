using System.Globalization;
using Core.Domain.Models;
using Infrastructure.Dto;

namespace Core.Infrastructure.Mappers;

public static class CbrMapperExtensions
{
    private static readonly CultureInfo RuCulture = new("ru-RU");

    public static CurrencyRate ToDomain(this ValCursValute dto)
    {
        decimal unitRate;

        if (!string.IsNullOrEmpty(dto.VunitRate))
            unitRate = decimal.Parse(dto.VunitRate, RuCulture);
        else
        {
            var rawValue = decimal.Parse(dto.Value, RuCulture);
            unitRate = dto.Nominal > 0
                ? rawValue / dto.Nominal
                : throw new InvalidOperationException(
                    $"Invalid Nominal in {dto.CharCode}': {dto.Nominal}. Nominal must be greater than 0"
                );
        }

        return new CurrencyRate
        {
            NumCode = dto.NumCode,
            CharCode = dto.CharCode,
            Name = dto.Name,
            UnitRate = unitRate
        };
    }

    public static ExchangeRateReport ToDomain(this ValCurs domain)
    {
        return new ExchangeRateReport
        {
            Date = DateOnly.ParseExact(domain.Date, "dd.MM.yyyy", CultureInfo.InvariantCulture),
            Rates = domain.Valute.Select(v => v.ToDomain()).ToList()
        };
    }
}