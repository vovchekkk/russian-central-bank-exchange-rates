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

        var currentReportResult = await cbrClient.GetExchangeRateReportAsync(date, cancellationToken);
        if (currentReportResult.IsError)
            return currentReportResult.Errors;
        var currentReport = currentReportResult.Value;

        var prevTargetDate = currentReport.Date.AddDays(-1);

        var prevReportResult = await cbrClient.GetExchangeRateReportAsync(prevTargetDate, cancellationToken);
        if (prevReportResult.IsError)
            return prevReportResult.Errors;
        var prevReport = prevReportResult.Value;

        var dailyReportResult = DailyExchangeRatesReport.Create(date, currentReport, prevReport);
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
}