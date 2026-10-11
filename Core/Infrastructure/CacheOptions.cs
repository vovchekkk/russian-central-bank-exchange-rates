using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Caching.Memory;

namespace Core.Infrastructure;

internal sealed class CacheOptions
{
    public const string SectionName = "Cache";

    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan SlidingExpiration { get; set; } = TimeSpan.FromHours(6);

    [Range(typeof(TimeSpan), "00:01:00", "7.00:00:00")]
    public TimeSpan AbsoluteExpiration { get; set; } = TimeSpan.FromHours(24);

    [Required]
    public string FilePath { get; set; } = "cache/last-report.json";

    public MemoryCacheEntryOptions ToMemoryCacheEntryOptions() => new()
    {
        SlidingExpiration = SlidingExpiration,
        AbsoluteExpirationRelativeToNow = AbsoluteExpiration
    };

    public string GetFullFilePath() =>
        Path.IsPathRooted(FilePath)
            ? FilePath
            : Path.Combine(AppContext.BaseDirectory, FilePath);
}
