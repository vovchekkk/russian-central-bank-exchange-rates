using System.ComponentModel.DataAnnotations;
using ErrorOr;
using System.Net.Sockets;
using System.Text;
using Core.Domain.Models;
using Core.Infrastructure.Helpers;
using Core.Services;
using Microsoft.Extensions.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Core.Infrastructure;

public class SmtpEmailService(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailService> logger
) : IEmailService
{
    private static readonly EmailAddressAttribute EmailValidator = new();

    public async Task<ErrorOr<Success>> SendDailyReportAsync(
        string recipientEmail,
        DailyExchangeRatesReport report,
        byte[] pdfAttachment,
        CancellationToken cancellationToken = default
    )
    {
        SmtpOptions smtpOptions;
        try
        {
            smtpOptions = options.Value;
        }
        catch (Exception ex) when (ex is OptionsValidationException or InvalidOperationException)
        {
            logger.LogError(ex, "Некорректные настройки SMTP в конфигурационном файле");
            return Error.Failure(
                description: "Некорректные настройки почтового сервера в конфигурационном файле (config.json)."
            );
        }

        if (string.IsNullOrWhiteSpace(recipientEmail) ||
            !EmailValidator.IsValid(recipientEmail) ||
            !MailboxAddress.TryParse(recipientEmail, out var recipientAddress))
            return Error.Validation(description: "Указан некорректный формат email-адреса получателя.");

        if (report.AllRates.Count == 0)
            return Error.Validation(description: "Нет данных в отчёте для отправки письма.");

        if (pdfAttachment.Length == 0)
            return Error.Validation(description: "Не удалось прикрепить PDF-отчёт: файл пуст.");

        try
        {
            logger.LogInformation(
                "Отправка отчёта за {Date:dd.MM.yyyy} на адрес {Recipient}",
                report.CurrentDate,
                recipientAddress.Address);

            var message = CreateMimeMessage(recipientAddress, smtpOptions.SenderEmail, report, pdfAttachment);

            using var client = new SmtpClient();
            client.Timeout = (int)smtpOptions.Timeout.TotalMilliseconds;

            await client.ConnectAsync(smtpOptions.Host, smtpOptions.Port, SecureSocketOptions.Auto, cancellationToken);
            await client.AuthenticateAsync(smtpOptions.Username, smtpOptions.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);

            logger.LogInformation("Письмо успешно отправлено на адрес {Recipient}", recipientAddress.Address);
            return Result.Success;
        }
        catch (Exception ex) when (ex is AuthenticationException or SaslException)
        {
            logger.LogError(ex, "Ошибка авторизации SMTP для пользователя {Username}", smtpOptions.Username);
            return Error.Failure(
                description: "Ошибка авторизации на SMTP-сервере. Проверьте логин и пароль в конфигурации.");
        }
        catch (SmtpCommandException ex)
        {
            logger.LogError(
                ex,
                "SMTP-сервер отклонил отправку письма на {Recipient} (код: {StatusCode})",
                recipientAddress.Address,
                ex.StatusCode
            );
            return Error.Failure(
                description: "Почтовый сервер отклонил отправку письма. Проверьте адрес получателя и отправителя.");
        }
        catch (SslHandshakeException ex)
        {
            logger.LogError(ex, "Ошибка SSL/TLS при подключении к {Host}:{Port}", smtpOptions.Host, smtpOptions.Port);
            return Error.Failure(
                description: "Не удалось установить защищённое SSL/TLS-соединение с почтовым сервером.");
        }
        catch (TimeoutException ex)
        {
            logger.LogError(ex, "Таймаут при обращении к SMTP-серверу {Host}:{Port}", smtpOptions.Host,
                smtpOptions.Port);
            return Error.Failure(description: "Превышено время ожидания ответа от почтового сервера.");
        }
        catch (Exception ex) when (ex is SocketException or IOException or SmtpProtocolException)
        {
            logger.LogError(ex, "Ошибка сети при обращении к SMTP-серверу {Host}:{Port}", smtpOptions.Host,
                smtpOptions.Port);
            return Error.Failure(
                description: "Не удалось подключиться к почтовому серверу. Проверьте настройки Host/Port и интернет.");
        }
    }

    private static MimeMessage CreateMimeMessage(
        MailboxAddress recipientEmail,
        string senderEmail,
        DailyExchangeRatesReport report,
        byte[] pdfAttachment
    )
    {
        var message = new MimeMessage();

        message.From.Add(MailboxAddress.Parse(senderEmail));
        message.To.Add(recipientEmail);
        message.Subject = $"Курсы валют ЦБ РФ на {report.CurrentDate:dd.MM.yyyy}";

        var bodyBuilder = new BodyBuilder { TextBody = BuildTextSummary(report) };

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