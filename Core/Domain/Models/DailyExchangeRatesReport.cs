using Core.Domain.Helpers;
using ErrorOr;

namespace Core.Domain.Models;

public sealed record DailyExchangeRatesReport
{
    public const int DefaultTopCount = 3;
    public const int DefaultTrendDaysCount = 7;

    public static readonly IReadOnlyList<Currency> DefaultTrendCurrencies = [Currency.Usd, Currency.Eur];

    internal IReadOnlyList<ExchangeRateReport> HistoricalReports { get; }
    internal ExchangeRateReport CurrentReport => HistoricalReports[0];
    internal ExchangeRateReport PreviousReport => HistoricalReports[1];

    public DateOnly RequestedDate { get; }
    public DateOnly CurrentDate => CurrentReport.Date;
    public DateOnly PreviousDate => PreviousReport.Date;
    public IReadOnlyList<CurrencyRateDynamics> AllRates { get; }
    public IReadOnlyList<CurrencyRateDynamics> TopGrown { get; }
    public IReadOnlyList<CurrencyRateDynamics> TopFallen { get; }
    public decimal? AveragePercentChange { get; }
    public IReadOnlyList<CurrencyRateTrend> WeeklyTrends { get; }

    private DailyExchangeRatesReport(
        DateOnly requestedDate,
        IReadOnlyList<ExchangeRateReport> historicalReports,
        IReadOnlyList<CurrencyRateDynamics> allRates,
        IReadOnlyList<CurrencyRateDynamics> topGrown,
        IReadOnlyList<CurrencyRateDynamics> topFallen,
        decimal? averagePercentChange,
        IReadOnlyList<CurrencyRateTrend> weeklyTrends
    )
    {
        RequestedDate = requestedDate;
        HistoricalReports = historicalReports;
        AllRates = allRates;
        TopGrown = topGrown;
        TopFallen = topFallen;
        AveragePercentChange = averagePercentChange;
        WeeklyTrends = weeklyTrends;
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
        IEnumerable<ExchangeRateReport>? historicalReports,
        int topCount = DefaultTopCount,
        int trendDaysCount = DefaultTrendDaysCount
    )
    {
        var dateValidation = ValidateRequestedDate(requestedDate);
        if (dateValidation.IsError)
            return dateValidation.Errors;

        if (topCount <= 0)
            return Error.Validation(
                description: "Количество валют в выборке Топ должно быть больше нуля.");

        if (trendDaysCount <= 0)
            return Error.Validation(
                description: "Количество дней для построения динамики курса должно быть больше нуля.");

        var reportsValidation = ValidateHistoricalReports(requestedDate, historicalReports);
        if (reportsValidation.IsError)
            return reportsValidation.Errors;

        var readOnlyReports = reportsValidation.Value;
        var currentReport = readOnlyReports[0];
        var previousReport = readOnlyReports[1];

        var dynamicsResult = BuildCurrencyDynamics(currentReport, previousReport);
        if (dynamicsResult.IsError)
            return dynamicsResult.Errors;

        var trendsResult = BuildWeeklyTrends(requestedDate, readOnlyReports, trendDaysCount);
        if (trendsResult.IsError)
            return trendsResult.Errors;

        var readOnlyDynamics = dynamicsResult.Value;

        var topGrown = readOnlyDynamics
            .Where(x => x.PercentChange > 0)
            .OrderByDescending(x => x.PercentChange)
            .Take(topCount)
            .ToList()
            .AsReadOnly();

        var topFallen = readOnlyDynamics
            .Where(x => x.PercentChange < 0)
            .OrderBy(x => x.PercentChange)
            .Take(topCount)
            .ToList()
            .AsReadOnly();

        var rawAverage = readOnlyDynamics.Average(x => x.PercentChange);
        decimal? averagePercentChange = rawAverage.HasValue
            ? Math.Round(rawAverage.Value, 4)
            : null;

        return new DailyExchangeRatesReport(
            requestedDate,
            readOnlyReports,
            readOnlyDynamics,
            topGrown,
            topFallen,
            averagePercentChange,
            trendsResult.Value);
    }

    private static ErrorOr<IReadOnlyList<ExchangeRateReport>> ValidateHistoricalReports(
        DateOnly requestedDate,
        IEnumerable<ExchangeRateReport>? historicalReports
    )
    {
        if (historicalReports is null)
            return Error.Validation(
                description: "Отсутствуют срезы курсов валют для формирования отчёта.");

        var reportsList = historicalReports.ToList().AsReadOnly();
        if (reportsList.Count < 2)
            return Error.Validation(
                description: "Для расчёта динамики необходимы курсы минимум за два торговых дня.");

        if (reportsList[0].Date > requestedDate)
            return Error.Validation(
                description: $"Дата актуального курса ({reportsList[0].Date:dd.MM.yyyy}) не может быть позже запрошенной даты ({requestedDate:dd.MM.yyyy}).");

        for (var i = 1; i < reportsList.Count; i++)
        {
            if (reportsList[i].Date >= reportsList[i - 1].Date)
                return Error.Validation(
                    description:
                    $"Нарушена хронология срезов курсов валют: {reportsList[i - 1].Date:dd.MM.yyyy} и {reportsList[i].Date:dd.MM.yyyy}.");
        }

        return reportsList;
    }

    private static ErrorOr<IReadOnlyList<CurrencyRateDynamics>> BuildCurrencyDynamics(
        ExchangeRateReport currentReport,
        ExchangeRateReport previousReport
    )
    {
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

        return dynamics.AsReadOnly();
    }

    private static ErrorOr<IReadOnlyList<CurrencyRateTrend>> BuildWeeklyTrends(
        DateOnly requestedDate,
        IReadOnlyList<ExchangeRateReport> historicalReports,
        int trendDaysCount
    )
    {
        var trends = new List<CurrencyRateTrend>(DefaultTrendCurrencies.Count);

        foreach (var currency in DefaultTrendCurrencies)
        {
            var trendResult = CurrencyRateTrend.Create(currency, requestedDate, historicalReports, trendDaysCount);
            if (trendResult.IsError)
            {
                if (trendResult.FirstError.Type == ErrorType.NotFound)
                    continue;

                return trendResult.Errors;
            }

            trends.Add(trendResult.Value);
        }

        return trends.AsReadOnly();
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
               && TopFallen.SequenceEqual(other.TopFallen)
               && WeeklyTrends.SequenceEqual(other.WeeklyTrends);
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
        foreach (var trend in WeeklyTrends)
            hash.Add(trend);
        return hash.ToHashCode();
    }
}