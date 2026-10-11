using Core.Domain.Models;
using Core.Services;
using ErrorOr;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Infrastructure;

internal sealed class CachedExchangeRateAnalyticsService(
    ExchangeRateAnalyticsService innerService,
    IMemoryCache memoryCache,
    LastReportFileCache fileCache,
    IOptions<CacheOptions> options,
    ILogger<CachedExchangeRateAnalyticsService> logger
) : IExchangeRateAnalyticsService
{
    public async Task<ErrorOr<DailyExchangeRatesReport>> GetDailyAnalyticsAsync(
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

        var dateValidation = DailyExchangeRatesReport.ValidateRequestedDate(date);
        if (dateValidation.IsError)
            return dateValidation.Errors;

        var entryOptions = cacheOptions.ToMemoryCacheEntryOptions();

        if (TryGetFromMemoryCache(date) is { } memoryReport)
        {
            await fileCache.SaveIfChangedAsync(memoryReport, cancellationToken);
            return memoryReport;
        }

        var diskReport = await fileCache.GetAsync(cancellationToken);
        if (TryRestoreFromDiskCache(date, diskReport, entryOptions) is { } restoredReport)
            return restoredReport;

        var fetchResult = await FetchAndCacheAsync(date, entryOptions, cancellationToken);
        if (!fetchResult.IsError)
            return fetchResult.Value;

        return GetFailSafeOrError(date, diskReport, fetchResult.Errors);
    }

    private DailyExchangeRatesReport? TryGetFromMemoryCache(DateOnly date)
    {
        if (!memoryCache.TryGetValue(GetCacheKey(date), out DailyExchangeRatesReport? report) || report is null)
            return null;

        logger.LogInformation(
            "Отчёт на дату {RequestedDate:dd.MM.yyyy} (курс ЦБ от {CurrentDate:dd.MM.yyyy}) получен из L1-кэша (IMemoryCache)",
            date,
            report.CurrentDate
        );

        return report;
    }

    private DailyExchangeRatesReport? TryRestoreFromDiskCache(
        DateOnly date,
        DailyExchangeRatesReport? diskReport,
        MemoryCacheEntryOptions entryOptions
    )
    {
        if (diskReport is null)
            return null;

        CachedCbrClient.CacheReportInterval(memoryCache, entryOptions, diskReport.RequestedDate, diskReport.CurrentReport);
        CachedCbrClient.CacheReportInterval(memoryCache, entryOptions, diskReport.PreviousDate, diskReport.PreviousReport);

        if (diskReport.RequestedDate != date)
            return null;

        memoryCache.Set(GetCacheKey(date), diskReport, entryOptions);
        logger.LogInformation(
            "Отчёт на дату {RequestedDate:dd.MM.yyyy} (курс ЦБ от {CurrentDate:dd.MM.yyyy}) восстановлен из L2-кэша на диске",
            date,
            diskReport.CurrentDate
        );

        return diskReport;
    }

    private async Task<ErrorOr<DailyExchangeRatesReport>> FetchAndCacheAsync(
        DateOnly date,
        MemoryCacheEntryOptions entryOptions,
        CancellationToken cancellationToken
    )
    {
        var result = await innerService.GetDailyAnalyticsAsync(date, cancellationToken);
        if (result.IsError)
            return result.Errors;

        var freshReport = result.Value;
        memoryCache.Set(GetCacheKey(date), freshReport, entryOptions);
        await fileCache.SaveIfChangedAsync(freshReport, cancellationToken);
        return freshReport;
    }

    private ErrorOr<DailyExchangeRatesReport> GetFailSafeOrError(
        DateOnly date,
        DailyExchangeRatesReport? diskReport,
        List<Error> errors
    )
    {
        if (diskReport is null)
            return errors;

        logger.LogWarning(
            "Не удалось загрузить курсы валют на {RequestedDate:dd.MM.yyyy} ({Error}). Сработал Fail-Safe: возвращён последний сохранённый отчёт (на дату {CachedRequestedDate:dd.MM.yyyy}, курс ЦБ от {CachedCurrentDate:dd.MM.yyyy})",
            date,
            errors[0].Description,
            diskReport.RequestedDate,
            diskReport.CurrentDate
        );

        return diskReport;
    }

    private static string GetCacheKey(DateOnly date) => $"cbr:daily-analytics:{date:yyyy-MM-dd}";
}
