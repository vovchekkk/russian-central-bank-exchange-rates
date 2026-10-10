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

        var prevTargetDate = currentReport.Date.AddDays(-1);
        var prevRaw = await cbrClient.GetValCursAsync(prevTargetDate, cancellationToken);
        var prevReport = prevRaw.ToDomain();

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
            CurrentDate = currentReport.Date,
            PreviousDate = prevReport.Date,
            AllRates = dynamics,
            TopGrown = topGrown,
            TopFallen = topFallen,
            AveragePercentChange = averagePercentChange
        };
    }

    private CurrencyRateDynamics BuildDynamics(CurrencyRate current, CurrencyRate? prev)
    {
        decimal? absoluteChange = prev is not null
            ? current.UnitRate - prev.UnitRate
            : null;
        
        decimal? percentChange = absoluteChange.HasValue && prev is not null && prev.UnitRate != 0
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