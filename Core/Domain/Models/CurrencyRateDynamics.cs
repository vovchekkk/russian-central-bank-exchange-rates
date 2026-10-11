using ErrorOr;

namespace Core.Domain.Models;

public sealed record CurrencyRateDynamics
{
    public Currency Currency { get; }
    public decimal CurrentUnitRate { get; }
    public decimal? PreviousUnitRate { get; }
    public decimal? AbsoluteChange { get; }
    public decimal? PercentChange { get; }

    private CurrencyRateDynamics(
        Currency currency,
        decimal currentUnitRate,
        decimal? previousUnitRate,
        decimal? absoluteChange,
        decimal? percentChange)
    {
        Currency = currency;
        CurrentUnitRate = currentUnitRate;
        PreviousUnitRate = previousUnitRate;
        AbsoluteChange = absoluteChange;
        PercentChange = percentChange;
    }

    internal static ErrorOr<CurrencyRateDynamics> Create(CurrencyRate? current, CurrencyRate? previous)
    {
        if (current is null)
            return Error.Validation(
                description: "Отсутствуют данные о текущем курсе валюты для расчёта динамики.");

        if (previous is not null && !current.Currency.Equals(previous.Currency))
            return Error.Validation(
                description: $"Невозможно рассчитать динамику для разных валют: '{current.Currency.CharCode}' и '{previous.Currency.CharCode}'.");

        decimal? absoluteChange = previous is not null
            ? current.UnitRate - previous.UnitRate
            : null;

        decimal? percentChange = previous is { UnitRate: > 0 } && absoluteChange.HasValue
            ? Math.Round(absoluteChange.Value / previous.UnitRate * 100m, 4)
            : null;

        return new CurrencyRateDynamics(
            current.Currency,
            current.UnitRate,
            previous?.UnitRate,
            absoluteChange,
            percentChange);
    }
}