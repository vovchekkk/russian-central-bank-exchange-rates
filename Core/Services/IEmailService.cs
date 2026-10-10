using Core.Domain.Models;
using ErrorOr;

namespace Core.Services;

public interface IEmailService
{
    public Task<ErrorOr<Success>> SendDailyReportAsync(
        string recipientEmail,
        DailyExchangeRatesReport report,
        byte[] pdfAttachment,
        CancellationToken cancellationToken = default
    );
}