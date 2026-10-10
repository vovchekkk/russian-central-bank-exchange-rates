using Core.Domain.Models;
using Core.Services.Helpers;
using ErrorOr;
using Microsoft.Extensions.Logging;

namespace Core.Services;

public class ExchangeRateAnalyticsService(
    ICbrClient cbrClient,
    ILogger<ExchangeRateAnalyticsService> logger
) : IExchangeRateAnalyticsService
{
    private static readonly DateOnly MinCbrDate = new(1992, 7, 2);
    
    public async Task<ErrorOr<DailyExchangeRatesReport>> GetDailyAnalyticsAsync(
        DateOnly date,
        CancellationToken cancellationToken = default
    )
    {
        if (date < MinCbrDate)
            return Error.Validation(description: $"Курсы валют ЦБ РФ доступны начиная с {MinCbrDate:dd.MM.yyyy}.");
        
        if (date > DateOnly.FromDateTime(DateTime.Today))
            return Error.Validation(description: "Нельзя запросить курсы валют на дату позже текущей.");
        
        logger.LogInformation("Запрос курсов валют ЦБ РФ на дату {Date:dd.MM.yyyy}", date);

        var currentReportResult = await cbrClient.GetExchangeRateReportAsync(date, cancellationToken);
        if (currentReportResult.IsError)
            return currentReportResult.Errors;
        var currentReport = currentReportResult.Value;

        var prevTargetDate = currentReport.Date.AddDays(-1);

        var prevReportResult = await cbrClient.GetExchangeRateReportAsync(prevTargetDate, cancellationToken);
        if (prevReportResult.IsError)
            return prevReportResult.Errors;
        var prevReport = prevReportResult.Value;

        var dailyReport = BuildDailyReport(date, currentReport, prevReport);

        logger.LogInformation(
            "Отчёт на дату {RequestedDate:dd.MM.yyyy} (курс ЦБ от {CurrentDate:dd.MM.yyyy} в сравнении с {PreviousDate:dd.MM.yyyy}) успешно сформирован (валют: {Count})",
            dailyReport.RequestedDate,
            dailyReport.CurrentDate,
            dailyReport.PreviousDate,
            dailyReport.AllRates.Count
        );

        return dailyReport;
    }

    private static DailyExchangeRatesReport BuildDailyReport(
        DateOnly requestedDate,
        ExchangeRateReport currentReport,
        ExchangeRateReport prevReport
    )
    {
        var dynamics = currentReport.Rates.LeftJoin(
            prevReport.Rates,
            currentCurrencyRate => currentCurrencyRate.CharCode,
            prevCurrencyRate => prevCurrencyRate.CharCode,
            BuildDynamics
        ).ToList();

        var topGrown = dynamics
            .Where(x => x.PercentChange > 0)
            .OrderByDescending(x => x.PercentChange)
            .Take(3)
            .ToList();

        var topFallen = dynamics
            .Where(x => x.PercentChange < 0)
            .OrderBy(x => x.PercentChange)
            .Take(3)
            .ToList();

        var rawAverage = dynamics.Average(x => x.PercentChange);
        decimal? averagePercentChange = rawAverage.HasValue
            ? Math.Round(rawAverage.Value, 4)
            : null;

        return new DailyExchangeRatesReport
        {
            RequestedDate = requestedDate,
            CurrentDate = currentReport.Date,
            PreviousDate = prevReport.Date,
            AllRates = dynamics,
            TopGrown = topGrown,
            TopFallen = topFallen,
            AveragePercentChange = averagePercentChange
        };
    }

    private static CurrencyRateDynamics BuildDynamics(CurrencyRate current, CurrencyRate? prev)
    {
        decimal? absoluteChange = prev is not null
            ? current.UnitRate - prev.UnitRate
            : null;

        decimal? percentChange = prev is { UnitRate: > 0 } && absoluteChange.HasValue
            ? Math.Round(absoluteChange.Value / prev.UnitRate * 100m, 4)
            : null;

        return new CurrencyRateDynamics
        {
            CharCode = current.CharCode,
            Name = current.Name,
            CurrentUnitRate = current.UnitRate,
            PreviousUnitRate = prev?.UnitRate,
            AbsoluteChange = absoluteChange,
            PercentChange = percentChange
        };
    }
}