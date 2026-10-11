using Core.Domain.Models;
using Core.Infrastructure.Dto;
using ErrorOr;

namespace Core.Infrastructure.Mappers;

internal static class CacheMapperExtensions
{
    public static LastDailyExchangeRatesReportCacheDto ToCacheDto(this DailyExchangeRatesReport report) => new()
    {
        RequestedDate = report.RequestedDate,
        CurrentReport = report.CurrentReport.ToCacheDto(),
        PreviousReport = report.PreviousReport.ToCacheDto()
    };

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
        ExchangeRateReport? currentReport = null;
        if (dto.CurrentReport is not null)
        {
            var currentReportResult = dto.CurrentReport.ToDomain();
            if (currentReportResult.IsError)
                return currentReportResult.Errors;

            currentReport = currentReportResult.Value;
        }

        ExchangeRateReport? previousReport = null;
        if (dto.PreviousReport is not null)
        {
            var previousReportResult = dto.PreviousReport.ToDomain();
            if (previousReportResult.IsError)
                return previousReportResult.Errors;

            previousReport = previousReportResult.Value;
        }

        return DailyExchangeRatesReport.Create(
            dto.RequestedDate,
            currentReport,
            previousReport
        );
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
