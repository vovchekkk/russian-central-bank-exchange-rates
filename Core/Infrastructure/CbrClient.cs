using System.Text;
using System.Xml.Serialization;
using Core.Services;
using Infrastructure.Dto;

namespace Core.Infrastructure;

public class CbrClient(HttpClient client, XmlSerializer serializer) : ICbrClient
{
    static CbrClient()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public async Task<ValCurs> GetValCursAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var dateFormatted = date.ToString("dd/MM/yyyy");
        var url = $"https://www.cbr.ru/scripts/XML_daily.asp?date_req={dateFormatted}";
        
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var encoding = Encoding.GetEncoding("windows-1251");
        var reader = new StreamReader(stream, encoding);
        
        var result = (ValCurs?)serializer.Deserialize(reader);

        return result ?? throw new InvalidOperationException("Failed to deserialize response from CBR");
    }
}