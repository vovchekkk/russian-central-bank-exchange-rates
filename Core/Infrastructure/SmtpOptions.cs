using System.ComponentModel.DataAnnotations;

namespace Core.Infrastructure;

internal sealed class SmtpOptions
{
    public const string SectionName = "Smtp";
    
    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 587;
    
    [Required]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    public string Password { get; set; } = string.Empty;
    
    [Required, EmailAddress]
    public string SenderEmail { get; set; } = string.Empty;
    
    [Range(typeof(TimeSpan), "00:00:01", "00:02:00")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);
}