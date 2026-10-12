using Core.Domain.Models;
using ErrorOr;
using Microsoft.Extensions.Logging;

namespace Core.Services;

internal sealed class ExchangeRateAnalyticsService(
    ICbrClient cbrClient,
    ILogger<ExchangeRateAnalyticsService> logger
) : IExchangeRateAnalyticsService
{
    public async Task<ErrorOr<DailyExchangeRatesReport>> GetDailyAnalyticsAsync(
        DateOnly date,
        CancellationToken cancellationToken = default
    )
    {
        var dateValidation = DailyExchangeRatesReport.ValidateRequestedDate(date);
        if (dateValidation.IsError)
            return dateValidation.Errors;

        logger.LogInformation("Запрос курсов валют ЦБ РФ на дату {Date:dd.MM.yyyy}", date);

        var reportsResult = await FetchHistoricalReportsAsync(date, cancellationToken);
        if (reportsResult.IsError)
            return reportsResult.Errors;

        var dailyReportResult = DailyExchangeRatesReport.Create(date, reportsResult.Value);
        if (dailyReportResult.IsError)
            return dailyReportResult.Errors;

        var dailyReport = dailyReportResult.Value;

        logger.LogInformation(
            "Отчёт на дату {RequestedDate:dd.MM.yyyy} (курс ЦБ от {CurrentDate:dd.MM.yyyy} в сравнении с {PreviousDate:dd.MM.yyyy}) успешно сформирован (валют: {Count})",
            dailyReport.RequestedDate,
            dailyReport.CurrentDate,
            dailyReport.PreviousDate,
            dailyReport.AllRates.Count
        );

        return dailyReport;
    }

    private async Task<ErrorOr<IReadOnlyList<ExchangeRateReport>>> FetchHistoricalReportsAsync(
        DateOnly date,
        CancellationToken cancellationToken
    )
    {
        var currentReportResult = await cbrClient.GetExchangeRateReportAsync(date, cancellationToken);
        if (currentReportResult.IsError)
            return currentReportResult.Errors;

        var currentReport = currentReportResult.Value;
        var reports = new List<ExchangeRateReport> { currentReport };

        var windowStartDate = date.AddDays(-(DailyExchangeRatesReport.DefaultTrendDaysCount - 1));
        if (windowStartDate < ExchangeRateReport.MinCbrDate)
            windowStartDate = ExchangeRateReport.MinCbrDate;

        while (reports.Count < 2 || reports[^1].Date > windowStartDate)
        {
            var nextTargetDate = reports[^1].Date.AddDays(-1);
            if (reports.Count >= 2 && nextTargetDate < ExchangeRateReport.MinCbrDate)
                break;

            var nextReportResult = await cbrClient.GetExchangeRateReportAsync(nextTargetDate, cancellationToken);
            if (nextReportResult.IsError)
                return nextReportResult.Errors;

            var nextReport = nextReportResult.Value;
            if (nextReport.Date >= reports[^1].Date)
                break;

            reports.Add(nextReport);
        }

        return reports.AsReadOnly();
    }
}