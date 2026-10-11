using ErrorOr;

namespace Core.Domain.Models;

public sealed record ExchangeRateReport
{
    public static readonly DateOnly MinCbrDate = new(1992, 7, 2);

    public DateOnly Date { get; }
    public IReadOnlyList<CurrencyRate> Rates { get; }

    private ExchangeRateReport(DateOnly date, IReadOnlyList<CurrencyRate> rates)
    {
        Date = date;
        Rates = rates;
    }

    public static ErrorOr<ExchangeRateReport> Create(DateOnly date, IEnumerable<CurrencyRate>? rates)
    {
        if (date < MinCbrDate)
            return Error.Validation(
                description: $"Курсы валют ЦБ РФ доступны начиная с {MinCbrDate:dd.MM.yyyy}.");

        if (rates is null)
            return Error.NotFound(
                description: $"На дату {date:dd.MM.yyyy} курсы валют ЦБ РФ отсутствуют.");

        var ratesList = rates.ToList().AsReadOnly();
        if (ratesList.Count == 0)
            return Error.NotFound(
                description: $"На дату {date:dd.MM.yyyy} курсы валют ЦБ РФ отсутствуют.");

        var seenCurrencies = new HashSet<Currency>(ratesList.Count);
        foreach (var rate in ratesList)
        {
            if (!seenCurrencies.Add(rate.Currency))
                return Error.Validation(
                    description: $"В отчёте за {date:dd.MM.yyyy} обнаружен дубликат валюты '{rate.Currency.CharCode}'.");
        }

        return new ExchangeRateReport(date, ratesList);
    }

    public bool Equals(ExchangeRateReport? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        return Date == other.Date && Rates.SequenceEqual(other.Rates);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Date);
        foreach (var rate in Rates)
            hash.Add(rate);
        return hash.ToHashCode();
    }
}