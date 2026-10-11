using Core.Services.Helpers;
using ErrorOr;

namespace Core.Domain.Models;

public sealed record DailyExchangeRatesReport
{
    public DateOnly RequestedDate { get; }
    public DateOnly CurrentDate { get; }
    public DateOnly PreviousDate { get; }
    public IReadOnlyList<CurrencyRateDynamics> AllRates { get; }
    public IReadOnlyList<CurrencyRateDynamics> TopGrown { get; }
    public IReadOnlyList<CurrencyRateDynamics> TopFallen { get; }
    public decimal? AveragePercentChange { get; }

    private DailyExchangeRatesReport(
        DateOnly requestedDate,
        DateOnly currentDate,
        DateOnly previousDate,
        IReadOnlyList<CurrencyRateDynamics> allRates,
        IReadOnlyList<CurrencyRateDynamics> topGrown,
        IReadOnlyList<CurrencyRateDynamics> topFallen,
        decimal? averagePercentChange)
    {
        RequestedDate = requestedDate;
        CurrentDate = currentDate;
        PreviousDate = previousDate;
        AllRates = allRates;
        TopGrown = topGrown;
        TopFallen = topFallen;
        AveragePercentChange = averagePercentChange;
    }

    internal static ErrorOr<Success> ValidateRequestedDate(DateOnly requestedDate)
    {
        if (requestedDate < ExchangeRateReport.MinCbrDate)
            return Error.Validation(
                description: $"Курсы валют ЦБ РФ доступны начиная с {ExchangeRateReport.MinCbrDate:dd.MM.yyyy}.");

        if (requestedDate > DateOnly.FromDateTime(DateTime.Today))
            return Error.Validation(
                description: "Нельзя запросить курсы валют на дату позже текущей.");

        return Result.Success;
    }

    internal static ErrorOr<DailyExchangeRatesReport> Create(
        DateOnly requestedDate,
        ExchangeRateReport? currentReport,
        ExchangeRateReport? previousReport,
        int topCount = 3)
    {
        var dateValidation = ValidateRequestedDate(requestedDate);
        if (dateValidation.IsError)
            return dateValidation.Errors;

        if (currentReport is null)
            return Error.Validation(
                description: "Отсутствует отчёт с текущими курсами валют.");

        if (previousReport is null)
            return Error.Validation(
                description: "Отсутствует отчёт с предыдущими курсами валют для сравнения.");

        if (topCount <= 0)
            return Error.Validation(
                description: "Количество валют в выборке Топ должно быть больше нуля.");

        if (currentReport.Date <= previousReport.Date)
            return Error.Validation(
                description: $"Дата текущего курса ({currentReport.Date:dd.MM.yyyy}) должна быть позже даты предыдущего курса ({previousReport.Date:dd.MM.yyyy}).");

        var dynamicsResults = currentReport.Rates.LeftJoin(
            previousReport.Rates,
            currentRate => currentRate.Currency,
            prevRate => prevRate.Currency,
            CurrencyRateDynamics.Create
        );

        var dynamics = new List<CurrencyRateDynamics>(currentReport.Rates.Count);
        foreach (var result in dynamicsResults)
        {
            if (result.IsError)
                return result.Errors;
            dynamics.Add(result.Value);
        }

        var readOnlyDynamics = dynamics.AsReadOnly();

        var topGrown = dynamics
            .Where(x => x.PercentChange > 0)
            .OrderByDescending(x => x.PercentChange)
            .Take(topCount)
            .ToList()
            .AsReadOnly();

        var topFallen = dynamics
            .Where(x => x.PercentChange < 0)
            .OrderBy(x => x.PercentChange)
            .Take(topCount)
            .ToList()
            .AsReadOnly();

        var rawAverage = dynamics.Average(x => x.PercentChange);
        decimal? averagePercentChange = rawAverage.HasValue
            ? Math.Round(rawAverage.Value, 4)
            : null;

        return new DailyExchangeRatesReport(
            requestedDate,
            currentReport.Date,
            previousReport.Date,
            readOnlyDynamics,
            topGrown,
            topFallen,
            averagePercentChange);
    }

    public bool Equals(DailyExchangeRatesReport? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;

        return RequestedDate == other.RequestedDate
            && CurrentDate == other.CurrentDate
            && PreviousDate == other.PreviousDate
            && AveragePercentChange == other.AveragePercentChange
            && AllRates.SequenceEqual(other.AllRates)
            && TopGrown.SequenceEqual(other.TopGrown)
            && TopFallen.SequenceEqual(other.TopFallen);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(RequestedDate);
        hash.Add(CurrentDate);
        hash.Add(PreviousDate);
        hash.Add(AveragePercentChange);
        foreach (var rate in AllRates)
            hash.Add(rate);
        return hash.ToHashCode();
    }
}