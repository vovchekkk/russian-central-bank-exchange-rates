using System.ComponentModel.DataAnnotations;

namespace Core.Infrastructure;

public class SmtpOptions
{
    public const string SectionName = "Smtp";
    
    [Required]
    public string Host { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; init; } = 587;
    
    [Required]
    public string Username { get; init; } = string.Empty;
    
    [Required]
    public string Password { get; init; } = string.Empty;
    
    [Required, EmailAddress]
    public string SenderEmail { get; init; } = string.Empty;
    
    [Range(typeof(TimeSpan), "00:00:01", "00:02:00")]
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
}