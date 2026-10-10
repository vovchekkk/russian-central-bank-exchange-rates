using Core.Domain.Models;

namespace Core.Services;

public interface IEmailService
{
    Task SendDailyReportAsync(
        string recipientEmail,
        DailyExchangeRatesReport report,
        byte[] pdfAttachment,
        CancellationToken cancellationToken = default
    );
}