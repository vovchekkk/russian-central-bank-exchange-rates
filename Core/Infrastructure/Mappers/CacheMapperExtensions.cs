using Core.Domain.Models;
using Core.Infrastructure.Dto;
using ErrorOr;

namespace Core.Infrastructure.Mappers;

internal static class CacheMapperExtensions
{
    public static LastDailyExchangeRatesReportCacheDto ToCacheDto(this DailyExchangeRatesReport report)
    {
        var historicalReports = new List<ExchangeRateReportCacheDto>(report.HistoricalReports.Count);
        foreach (var slice in report.HistoricalReports)
        {
            historicalReports.Add(slice.ToCacheDto());
        }

        return new LastDailyExchangeRatesReportCacheDto
        {
            RequestedDate = report.RequestedDate,
            HistoricalReports = historicalReports
        };
    }

    public static ExchangeRateReportCacheDto ToCacheDto(this ExchangeRateReport report)
    {
        var items = new List<CurrencyRateCacheDto>(report.Rates.Count);
        foreach (var rate in report.Rates)
        {
            items.Add(rate.ToCacheDto());
        }

        return new ExchangeRateReportCacheDto
        {
            Date = report.Date,
            Rates = items
        };
    }

    public static CurrencyRateCacheDto ToCacheDto(this CurrencyRate rate) => new()
    {
        NumCode = rate.Currency.NumCode,
        CharCode = rate.Currency.CharCode,
        Name = rate.Currency.Name,
        UnitRate = rate.UnitRate
    };

    public static ErrorOr<DailyExchangeRatesReport> ToDomain(this LastDailyExchangeRatesReportCacheDto dto)
    {
        var historicalReports = new List<ExchangeRateReport>(dto.HistoricalReports?.Count ?? 0);
        if (dto.HistoricalReports is not null)
        {
            foreach (var reportDto in dto.HistoricalReports)
            {
                var reportResult = reportDto.ToDomain();
                if (reportResult.IsError)
                    return reportResult.Errors;

                historicalReports.Add(reportResult.Value);
            }
        }

        return DailyExchangeRatesReport.Create(dto.RequestedDate, historicalReports);
    }

    public static ErrorOr<ExchangeRateReport> ToDomain(this ExchangeRateReportCacheDto dto)
    {
        var rates = new List<CurrencyRate>(dto.Rates?.Count ?? 0);
        if (dto.Rates is not null)
        {
            foreach (var item in dto.Rates)
            {
                var rateResult = item.ToDomain();
                if (rateResult.IsError)
                    return rateResult.Errors;

                rates.Add(rateResult.Value);
            }
        }

        return ExchangeRateReport.Create(dto.Date, rates);
    }

    public static ErrorOr<CurrencyRate> ToDomain(this CurrencyRateCacheDto dto)
    {
        var currencyResult = Currency.Create(dto.NumCode, dto.CharCode, dto.Name);
        if (currencyResult.IsError)
            return currencyResult.Errors;

        return CurrencyRate.Create(currencyResult.Value, dto.UnitRate, rawValue: null, nominal: 1);
    }
}
