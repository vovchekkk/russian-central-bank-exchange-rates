using ErrorOr;

namespace Core.Domain.Models;

public sealed record CurrencyRatePoint
{
    public DateOnly Date { get; }
    public decimal UnitRate { get; }

    private CurrencyRatePoint(DateOnly date, decimal unitRate)
    {
        Date = date;
        UnitRate = unitRate;
    }

    internal static ErrorOr<CurrencyRatePoint> Create(DateOnly date, decimal unitRate)
    {
        if (date < ExchangeRateReport.MinCbrDate)
            return Error.Validation(
                description: $"Дата курса ({date:dd.MM.yyyy}) не может быть раньше {ExchangeRateReport.MinCbrDate:dd.MM.yyyy}.");

        if (unitRate <= 0)
            return Error.Validation(
                description: $"Курс валюты на {date:dd.MM.yyyy} должен быть больше нуля.");

        return new CurrencyRatePoint(date, unitRate);
    }
}
