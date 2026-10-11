using System.Text.Json.Serialization;

namespace Core.Infrastructure.Dto;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(LastDailyExchangeRatesReportCacheDto))]
internal sealed partial class LastDailyExchangeRatesReportCacheJsonContext : JsonSerializerContext;
