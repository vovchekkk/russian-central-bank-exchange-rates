using Core.Domain.Models;
using Core.Services;
using ErrorOr;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Infrastructure;

internal sealed class CachedCbrClient(
    CbrClient innerClient,
    IMemoryCache memoryCache,
    IOptions<CacheOptions> options,
    ILogger<CachedCbrClient> logger
) : ICbrClient
{
    public async Task<ErrorOr<ExchangeRateReport>> GetExchangeRateReportAsync(
        DateOnly date,
        CancellationToken cancellationToken = default
    )
    {
        CacheOptions cacheOptions;
        try
        {
            cacheOptions = options.Value;
        }
        catch (Exception ex) when (ex is OptionsValidationException or InvalidOperationException)
        {
            logger.LogError(ex, "Некорректные настройки Cache в конфигурационном файле");
            return Error.Failure(
                description: "Некорректные настройки кэширования в конфигурационном файле (config.json)."
            );
        }

        if (memoryCache.TryGetValue(GetCacheKey(date), out ExchangeRateReport? cachedReport) &&
            cachedReport is not null)
        {
            logger.LogInformation(
                "Курсы валют ЦБ РФ на {RequestedDate:dd.MM.yyyy} (курс от {ReportDate:dd.MM.yyyy}) получены из MemoryCache",
                date,
                cachedReport.Date
            );
            return cachedReport;
        }

        var result = await innerClient.GetExchangeRateReportAsync(date, cancellationToken);
        if (result.IsError)
            return result.Errors;

        var report = result.Value;
        var entryOptions = cacheOptions.ToMemoryCacheEntryOptions();
        CacheReportInterval(memoryCache, entryOptions, date, report);

        return report;
    }

    internal static void CacheReportInterval(
        IMemoryCache memoryCache,
        MemoryCacheEntryOptions entryOptions,
        DateOnly requestedDate,
        ExchangeRateReport report
    )
    {
        memoryCache.Set(GetCacheKey(report.Date), report, entryOptions);
        memoryCache.Set(GetCacheKey(requestedDate), report, entryOptions);

        CacheIntermediateNonTradingDays(memoryCache, entryOptions, requestedDate, report);
    }

    private static void CacheIntermediateNonTradingDays(
        IMemoryCache memoryCache,
        MemoryCacheEntryOptions entryOptions,
        DateOnly requestedDate,
        ExchangeRateReport report
    )
    {
        for (var cursor = report.Date.AddDays(1); cursor < requestedDate; cursor = cursor.AddDays(1))
            memoryCache.Set(GetCacheKey(cursor), report, entryOptions);
    }

    private static string GetCacheKey(DateOnly date) => $"cbr:exchange-rates:{date:yyyy-MM-dd}";
}
