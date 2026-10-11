using System.Text.Json;
using Core.Domain.Models;
using Core.Infrastructure.Dto;
using Core.Infrastructure.Mappers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Infrastructure;

internal sealed class LastReportFileCache(
    IOptions<CacheOptions> options,
    ILogger<LastReportFileCache> logger
) : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private DailyExchangeRatesReport? _lastReport;
    private bool _isInitialized;

    public async Task<DailyExchangeRatesReport?> GetAsync(
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
            return null;
        }

        var cacheFilePath = cacheOptions.GetFullFilePath();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await EnsureInitializedAsync(cacheFilePath, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveIfChangedAsync(
        DailyExchangeRatesReport report,
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
            return;
        }

        var cacheFilePath = cacheOptions.GetFullFilePath();
        var tempPath = $"{cacheFilePath}.tmp";

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var existing = await EnsureInitializedAsync(cacheFilePath, cancellationToken);
            if (existing is not null && existing.RequestedDate == report.RequestedDate)
                return;

            var dto = report.ToCacheDto();
            var cacheDir = Path.GetDirectoryName(cacheFilePath);
            if (!string.IsNullOrWhiteSpace(cacheDir))
            {
                Directory.CreateDirectory(cacheDir);
            }

            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    dto,
                    LastDailyExchangeRatesReportCacheJsonContext.Default.LastDailyExchangeRatesReportCacheDto,
                    cancellationToken
                );
            }

            File.Move(tempPath, cacheFilePath, overwrite: true);
            _lastReport = report;

            logger.LogInformation(
                "Последний загруженный отчёт (на {RequestedDate:dd.MM.yyyy}, курс ЦБ от {CurrentDate:dd.MM.yyyy}) сохранён в файловый кэш",
                report.RequestedDate,
                report.CurrentDate
            );
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(ex, "Не удалось сохранить последний отчёт в файловый кэш {Path}", cacheFilePath);
        }
        finally
        {
            TryDeleteTempFile(tempPath);
            _lock.Release();
        }
    }

    public void Dispose() => _lock.Dispose();

    private async Task<DailyExchangeRatesReport?> EnsureInitializedAsync(
        string cacheFilePath,
        CancellationToken cancellationToken
    )
    {
        if (!_isInitialized)
        {
            _lastReport = await TryLoadFromDiskAsync(cacheFilePath, cancellationToken);
            _isInitialized = true;
        }

        return _lastReport;
    }

    private async Task<DailyExchangeRatesReport?> TryLoadFromDiskAsync(
        string cacheFilePath,
        CancellationToken cancellationToken
    )
    {
        if (!File.Exists(cacheFilePath))
            return null;

        try
        {
            await using var stream = File.OpenRead(cacheFilePath);
            var dto = await JsonSerializer.DeserializeAsync(
                stream,
                LastDailyExchangeRatesReportCacheJsonContext.Default.LastDailyExchangeRatesReportCacheDto,
                cancellationToken
            );

            if (dto is null)
                return null;

            var domainResult = dto.ToDomain();
            if (domainResult.IsError)
            {
                logger.LogWarning(
                    "Файл кэша {Path} содержит некорректные данные ({Error}) и будет проигнорирован",
                    cacheFilePath,
                    domainResult.FirstError.Description
                );
                return null;
            }

            var report = domainResult.Value;
            logger.LogInformation(
                "Загружен сохранённый отчёт из файлового кэша на дату {RequestedDate:dd.MM.yyyy} (курс ЦБ от {CurrentDate:dd.MM.yyyy})",
                report.RequestedDate,
                report.CurrentDate
            );

            return report;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(ex, "Не удалось прочитать файл кэша {Path}", cacheFilePath);
            return null;
        }
    }

    private void TryDeleteTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Не удалось удалить временный файл кэша {TempPath}", tempPath);
        }
    }
}
