using System.Xml.Serialization;
using Core.Infrastructure;
using Core.Services;
using Infrastructure.Dto;
using Microsoft.Extensions.DependencyInjection;

namespace Core;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddSingleton(new XmlSerializer(typeof(ValCurs)));
        
        services.AddHttpClient<ICbrClient, CbrClient>(client =>
        {
            client.BaseAddress = new Uri("https://www.cbr.ru/");
            client.DefaultRequestHeaders.Add("Accept", "application/xml");
            client.DefaultRequestHeaders.Add("User-Agent", "RussianCentralBankExchangeRates/1.0");
        });

        return services;
    }
}