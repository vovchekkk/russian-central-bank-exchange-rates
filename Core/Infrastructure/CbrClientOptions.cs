using System.ComponentModel.DataAnnotations;

namespace Core.Infrastructure;

public class CbrClientOptions
{
    public const string SectionName = "CbrClient";
    
    [Required, Url]
    public string BaseUrl { get; init; } = string.Empty;
    
    [Range(typeof(TimeSpan), "00:00:01", "00:02:00")]
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
}