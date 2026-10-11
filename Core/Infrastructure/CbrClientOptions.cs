using System.ComponentModel.DataAnnotations;

namespace Core.Infrastructure;

internal sealed class CbrClientOptions
{
    public const string SectionName = "CbrClient";
    
    [Required, Url]
    public string BaseUrl { get; set; } = string.Empty;
    
    [Range(typeof(TimeSpan), "00:00:01", "00:02:00")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}