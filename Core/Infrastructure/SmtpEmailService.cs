using System.Globalization;
using System.Text;
using Core.Domain.Models;
using Core.Infrastructure.Helpers;
using Core.Services;
using Microsoft.Extensions.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Core.Infrastructure;

public class SmtpEmailService(IOptions<SmtpOptions> options) : IEmailService
{
    private static readonly CultureInfo RuCulture = new("ru-RU");
    private readonly SmtpOptions _options = options.Value;

    public async Task SendDailyReportAsync(
        string recipientEmail,
        DailyExchangeRatesReport report,
        byte[] pdfAttachment,
        CancellationToken cancellationToken = default
    )
    {
        var message = CreateMimeMessage(recipientEmail, report, pdfAttachment);
        
        using var client = new SmtpClient();
        
        await client.ConnectAsync(_options.Host, _options.Port, SecureSocketOptions.Auto, cancellationToken);
        await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }

    private MimeMessage CreateMimeMessage(
        string recipientEmail,
        DailyExchangeRatesReport report,
        byte[] pdfAttachment
    )
    {
        var message = new MimeMessage();

        message.From.Add(MailboxAddress.Parse(_options.SenderEmail));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = $"Курсы валют ЦБ РФ на {report.CurrentDate:dd.MM.yyyy}";

        var bodyBuilder = new BodyBuilder
        {
            TextBody = BuildTextSummary(report)
        };
        
        var fileName = $"cbr-rates-{report.CurrentDate:yyyy-MM-dd}.pdf";
        bodyBuilder.Attachments.Add(fileName, pdfAttachment, new ContentType("application", "pdf"));
        
        message.Body = bodyBuilder.ToMessageBody();
        
        return message;
    }

    private static string BuildTextSummary(DailyExchangeRatesReport report)
    {
        var stringBuilder = new StringBuilder();
        
        stringBuilder.AppendLine("Топ-3 роста:");
        
        foreach (var item in report.TopGrown)
            stringBuilder.AppendLine(item.FormatSummaryLine());
        stringBuilder.AppendLine();
        
        stringBuilder.AppendLine("Топ-3 падения:");
        
        foreach (var item in report.TopFallen)
            stringBuilder.AppendLine(item.FormatSummaryLine());

        return stringBuilder.ToString();
    }
}