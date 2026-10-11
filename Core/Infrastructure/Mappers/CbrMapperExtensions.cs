using System.Globalization;
using Core.Domain.Models;
using Infrastructure.Dto;
using ErrorOr;

namespace Core.Infrastructure.Mappers;

internal static class CbrMapperExtensions
{
    private static readonly CultureInfo RuCulture = new("ru-RU");

    public static ErrorOr<CurrencyRate> ToDomain(this ValCursValute dto)
    {
        var currencyResult = Currency.Create(dto.NumCode, dto.CharCode, dto.Name);
        if (currencyResult.IsError)
            return currencyResult.Errors;

        decimal? unitRate = decimal.TryParse(dto.VunitRate, RuCulture, out var parsedUnitRate)
            ? parsedUnitRate
            : null;

        decimal? rawValue = decimal.TryParse(dto.Value, RuCulture, out var parsedValue)
            ? parsedValue
            : null;

        return CurrencyRate.Create(currencyResult.Value, unitRate, rawValue, dto.Nominal);
    }

    public static ErrorOr<ExchangeRateReport> ToDomain(this ValCurs dto)
    {
        if (!DateOnly.TryParseExact(dto.Date, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return Error.Validation(
                description: $"Некорректный формат даты в ответе ЦБ РФ: '{dto.Date}'.");
        }
        
        var rates = new List<CurrencyRate>(dto.Valute?.Count ?? 0);
        if (dto.Valute is not null)
        {
            foreach (var valuteDto in dto.Valute)
            {
                var rateResult = valuteDto.ToDomain();
                if (rateResult.IsError)
                    return rateResult.Errors;
                rates.Add(rateResult.Value);
            }
        }
        
        return ExchangeRateReport.Create(date, rates);
    }
}