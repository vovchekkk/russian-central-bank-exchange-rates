using System.Globalization;
using System.Text;
using System.Xml.Serialization;
using Core.Domain.Models;
using Core.Infrastructure.Mappers;
using ErrorOr;
using Core.Services;
using Infrastructure.Dto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Infrastructure;

public class CbrClient(
    HttpClient client,
    IOptions<CbrClientOptions> options,
    XmlSerializer serializer,
    ILogger<CbrClient> logger
) : ICbrClient
{
    private static readonly Encoding Windows1251;
    
    static CbrClient()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Windows1251 = Encoding.GetEncoding("windows-1251");
    }

    public async Task<ErrorOr<ExchangeRateReport>> GetExchangeRateReportAsync(
        DateOnly date,
        CancellationToken cancellationToken = default
    )
    {
        CbrClientOptions cbrOptions;
        try
        {
            cbrOptions = options.Value;
        }
        catch (Exception ex) when (ex is OptionsValidationException or InvalidOperationException)
        {
            logger.LogError(ex, "Некорректные настройки CbrClient в конфигурационном файле");
            return Error.Failure(
                description: "Некорректные настройки подключения к ЦБ РФ в конфигурационном файле (config.json)."
            );
        }

        var dateFormatted = date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        var relativeUrl = $"scripts/XML_daily.asp?date_req={dateFormatted}";

        if (!Uri.TryCreate(cbrOptions.BaseUrl, UriKind.Absolute, out var baseUri) ||
            !Uri.TryCreate(baseUri, relativeUrl, out var requestUri))
        {
            logger.LogError("Некорректный формат BaseUrl в конфигурации: {BaseUrl}", cbrOptions.BaseUrl);
            return Error.Failure(
                description: "Некорректный адрес сервера ЦБ РФ (BaseUrl) в конфигурационном файле (config.json)."
            );
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(cbrOptions.Timeout);

        try
        {
            logger.LogInformation("HTTP GET {Url}", requestUri);

            using var response = await client.GetAsync(
                requestUri,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token
            );
            
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Сервер ЦБ РФ вернул неуспешный статус {StatusCode} ({ReasonPhrase}) при запросе за {Date}",
                    (int)response.StatusCode,
                    response.ReasonPhrase,
                    dateFormatted
                );
                return Error.Failure(
                    description: $"Сервер ЦБ РФ вернул ошибку (HTTP {(int)response.StatusCode}). Попробуйте позже."
                );
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);

            using var reader = new StreamReader(stream, Windows1251, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

            var valCurs = (ValCurs?)serializer.Deserialize(reader);
            
            if (valCurs is null)
            {
                logger.LogError("Сервер ЦБ РФ вернул пустой объект ValCurs за {Date}", dateFormatted);
                return Error.Failure(description: "Не удалось десериализовать ответ от сервера ЦБ РФ.");
            }
            
            if (string.IsNullOrWhiteSpace(valCurs.Date) || valCurs.Valute is null || valCurs.Valute.Count == 0)
            {
                logger.LogWarning("В ответе ЦБ РФ за {Date} отсутствуют курсы валют", dateFormatted);
                return Error.NotFound(description: $"На дату {dateFormatted} курсы валют ЦБ РФ отсутствуют.");
            }
            
            var reportResult = valCurs.ToDomain();
            if (reportResult.IsError)
            {
                logger.LogWarning(
                    "Ошибка валидации данных в ответе ЦБ РФ за {Date}: {Error}",
                    dateFormatted,
                    reportResult.FirstError.Description
                );
            }

            return reportResult;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Таймаут при запросе к ЦБ РФ за {Date}", dateFormatted);
            return Error.Failure(description: "Превышено время ожидания ответа от сервера ЦБ РФ.");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            logger.LogError(ex, "Ошибка сети или чтения потока при запросе к ЦБ РФ за {Date}", dateFormatted);
            return Error.Failure(description: "Не удалось получить данные от сервера ЦБ РФ. Проверьте интернет.");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Ошибка десериализации XML от ЦБ РФ за {Date}", dateFormatted);
            return Error.Failure(description: "Сервер ЦБ РФ вернул некорректный формат данных.");
        }
    }
}