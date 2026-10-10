using System.Xml.Serialization;
using Core.Infrastructure;
using Core.Services;
using Infrastructure.Dto;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Core;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddOptions<CbrClientOptions>()
            .Bind(configuration.GetSection(CbrClientOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        services.AddSingleton(new XmlSerializer(typeof(ValCurs)));

        services.AddHttpClient<ICbrClient, CbrClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<CbrClientOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = options.Timeout;
            client.DefaultRequestHeaders.Add("Accept", "application/xml");
            client.DefaultRequestHeaders.Add("User-Agent", "RussianCentralBankExchangeRates/1.0");
        });

        services.AddScoped<IExchangeRateAnalyticsService, ExchangeRateAnalyticsService>();

        return services;
    }
}