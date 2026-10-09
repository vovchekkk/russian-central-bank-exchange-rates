using System.Globalization;
using Core.Domain.Models;
using Core.Infrastructure.Mappers;
using Core.Services.Helpers;

namespace Core.Services;

public class ExchangeRateAnalyticsService(ICbrClient cbrClient) : IExchangeRateAnalyticsService
{
    public async Task<DailyExchangeRatesReport> GetDailyAnalyticsAsync(
        DateOnly date,
        CancellationToken cancellationToken = default
    )
    {
        var currentRaw = await cbrClient.GetValCursAsync(date, cancellationToken);
        var currentReport = currentRaw.ToDomain();

        var prevTargetDate = date.AddDays(-1);
        var prevRaw = await cbrClient.GetValCursAsync(prevTargetDate, cancellationToken);
        var prevReport = prevRaw.ToDomain();

        var dynamics = currentReport.Rates.LeftJoin(
            prevReport.Rates,
            currentCurrencyRate => currentCurrencyRate.CharCode,
            prevCurrencyRate => prevCurrencyRate.CharCode,
            (currentCurrencyRate, prevCurrencyRate) => new CurrencyRateDynamics
            {
                CharCode = currentCurrencyRate.CharCode,
                Name = currentCurrencyRate.Name,
                CurrentUnitRate = currentCurrencyRate.UnitRate,
                PreviousUnitRate = prevCurrencyRate?.UnitRate,
                AbsoluteChange = prevCurrencyRate != null
                    ? currentCurrencyRate.UnitRate - prevCurrencyRate.UnitRate
                    : null,
                PercentChange = prevCurrencyRate != null && prevCurrencyRate.UnitRate != 0
                    ? Math.Round((currentCurrencyRate.UnitRate - prevCurrencyRate.UnitRate) / prevCurrencyRate.UnitRate * 100m, 4)
                    : null
            }
        ).ToList();

        var topGrown = dynamics
            .OrderByDescending(x => x.PercentChange)
            .Take(3)
            .ToList();

        var topFallen = dynamics
            .OrderBy(x => x.PercentChange)
            .Take(3)
            .ToList();

        return new DailyExchangeRatesReport
        {
            CurrentDate = currentReport.Date,
            PreviousDate = prevReport.Date,
            AllRates = dynamics,
            TopGrown = topGrown,
            TopFallen = topFallen
        };
    }
}