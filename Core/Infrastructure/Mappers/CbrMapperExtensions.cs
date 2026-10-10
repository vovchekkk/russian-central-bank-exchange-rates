using System.Globalization;
using Core.Domain.Models;
using Infrastructure.Dto;
using ErrorOr;

namespace Core.Infrastructure.Mappers;

public static class CbrMapperExtensions
{
    private static readonly CultureInfo RuCulture = new("ru-RU");

    public static ErrorOr<CurrencyRate> ToDomain(this ValCursValute dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CharCode))
            return Error.Validation(description: $"Отсутствует символьный код (CharCode) у валюты с NumCode '{dto.NumCode}'.");
        
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Error.Validation(description: $"Отсутствует название (Name) для валюты {dto.CharCode}.");
        
        if (!decimal.TryParse(dto.VunitRate, RuCulture, out var unitRate) || unitRate <= 0)
        {
            if (!decimal.TryParse(dto.Value, RuCulture, out var rawValue) || rawValue <= 0)
                return Error.Validation(
                    description: $"Некорректный курс Value ('{dto.Value}') для валюты {dto.CharCode}.");
            
            if (dto.Nominal == 0)
                return Error.Validation(
                    description: $"Некорректный номинал ({dto.Nominal}) для валюты {dto.CharCode}.");
            
            unitRate = rawValue / dto.Nominal;
        }
        
        return new CurrencyRate
        {
            NumCode = dto.NumCode,
            CharCode = dto.CharCode.Trim().ToUpperInvariant(),
            Name = dto.Name.Trim(),
            UnitRate = unitRate
        };
    }

    public static ErrorOr<ExchangeRateReport> ToDomain(this ValCurs dto)
    {
        if (!DateOnly.TryParseExact(dto.Date, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return Error.Validation(
                description: $"Некорректный формат даты в ответе ЦБ РФ: '{dto.Date}'.");
        }
        
        if (dto.Valute is null || dto.Valute.Count == 0)
        {
            return Error.NotFound(
                description: $"В ответе ЦБ РФ за {dto.Date} отсутствует список валют.");
        }
        
        var rates = new List<CurrencyRate>(dto.Valute.Count);
        foreach (var valuteDto in dto.Valute)
        {
            var rateResult = valuteDto.ToDomain();
            if (rateResult.IsError)
                return rateResult.Errors;
            rates.Add(rateResult.Value);
        }
        
        return new ExchangeRateReport
        {
            Date = date,
            Rates = rates
        };
    }
}