using ErrorOr;

namespace Core.Domain.Models;

internal sealed record CurrencyRate
{
    public Currency Currency { get; }
    public decimal UnitRate { get; }

    private CurrencyRate(Currency currency, decimal unitRate)
    {
        Currency = currency;
        UnitRate = unitRate;
    }

    public static ErrorOr<CurrencyRate> Create(
        Currency? currency,
        decimal? unitRate,
        decimal? rawValue = null,
        uint nominal = 1
    )
    {
        if (currency is null)
            return Error.Validation(description: "Отсутствуют данные о валюте.");

        decimal resolvedUnitRate;
        if (unitRate is > 0)
        {
            resolvedUnitRate = unitRate.Value;
        }
        else
        {
            if (rawValue is null or <= 0)
                return Error.Validation(
                    description: $"Некорректный курс для валюты {currency.CharCode}.");

            if (nominal == 0)
                return Error.Validation(
                    description: $"Некорректный номинал ({nominal}) для валюты {currency.CharCode}.");

            resolvedUnitRate = rawValue.Value / nominal;
        }

        return new CurrencyRate(currency, resolvedUnitRate);
    }
}